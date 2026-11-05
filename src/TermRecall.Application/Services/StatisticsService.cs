using TermRecall.Application.Abstractions;
using TermRecall.Application.DTOs;
using TermRecall.Application.Validation;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;

namespace TermRecall.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummary> GetAsync(CancellationToken cancellationToken);
}

public interface IStatisticsService
{
    Task<StatisticsReport> GetAsync(StatsRange range, CancellationToken cancellationToken);
}

public interface ISettingsService
{
    Task<UserSettings> GetAsync(CancellationToken cancellationToken);
    Task<UserSettings> SaveAsync(UserSettings settings, CancellationToken cancellationToken);
    Task ResetStatisticsAsync(CancellationToken cancellationToken);
}

public sealed class DashboardService(
    ITermRepository terms,
    IReviewHistoryRepository history,
    ISettingsRepository settings,
    IClock clock) : IDashboardService
{
    public async Task<DashboardSummary> GetAsync(CancellationToken cancellationToken)
    {
        var allTerms = await terms.ListAsync(cancellationToken);
        var records = await history.ListAsync(cancellationToken);
        var user = await settings.GetAsync(cancellationToken);
        var today = ReviewCalendar.LocalDate(clock.UtcNow, clock.TimeZone);
        var due = allTerms.Count(term => ReviewCalendar.IsDue(term.NextReviewAtUtc, clock.UtcNow, clock.TimeZone));
        var overdue = allTerms.Count(term => ReviewCalendar.IsOverdue(term.NextReviewAtUtc, clock.UtcNow, clock.TimeZone));
        var fresh = allTerms.Count(term => term.Status == TermStatus.New && term.LastReviewedAtUtc is null);
        var mastered = allTerms.Count(term => term.Status == TermStatus.Mastered);
        var difficult = allTerms.Count(term => ReviewQueuePlanner.IsDifficult(term, user.DifficultEaseThreshold));
        var success = LearningAnalytics.SuccessPercent(LearningAnalytics.CountSuccessful(records.Select(record => record.Quality)), records.Count);
        var activeDays = records
            .Select(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone))
            .ToHashSet();
        var reviewsToday = records.Count(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) == today);
        var week = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(-6 + offset))
            .Select(date => new DayPoint(
                date,
                records.Count(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) == date),
                records.Count(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) == date && record.Quality >= 3)))
            .ToList();

        var showReminder = user.ReminderEnabled
                           && TimeZoneInfo.ConvertTimeFromUtc(ReviewCalendar.EnsureUtc(clock.UtcNow), clock.TimeZone).TimeOfDay >= user.ReminderTime
                           && reviewsToday == 0
                           && (due > 0 || fresh > 0);

        return new DashboardSummary(
            allTerms.Count,
            due,
            overdue,
            fresh,
            mastered,
            difficult,
            StreakCalculator.CurrentStreak(activeDays, today),
            success,
            reviewsToday,
            user.DailyReviewLimit,
            reviewsToday >= user.DailyReviewLimit,
            showReminder,
            week);
    }
}

public sealed class StatisticsService(
    ITermRepository terms,
    IReviewHistoryRepository history,
    ISettingsRepository settings,
    IClock clock) : IStatisticsService
{
    public async Task<StatisticsReport> GetAsync(StatsRange range, CancellationToken cancellationToken)
    {
        var allTerms = await terms.ListAsync(cancellationToken);
        var records = await history.ListAsync(cancellationToken);
        var user = await settings.GetAsync(cancellationToken);
        var today = ReviewCalendar.LocalDate(clock.UtcNow, clock.TimeZone);
        var from = range switch
        {
            StatsRange.Week => today.AddDays(-6),
            StatsRange.Month => today.AddDays(-29),
            _ => records.Count == 0
                ? today
                : records.Min(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone))
        };

        bool InRange(ReviewRecord record)
        {
            var date = ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone);
            return date >= from && date <= today;
        }

        var window = records.Where(InRange).ToList();
        var qualities = window.Select(record => record.Quality).ToList();
        var successful = LearningAnalytics.CountSuccessful(qualities);
        var success = LearningAnalytics.SuccessPercent(successful, qualities.Count);
        var mastered = allTerms.Count(term => term.Status == TermStatus.Mastered);
        var learning = allTerms.Count(term => term.Status is TermStatus.Learning or TermStatus.Review);
        var difficult = allTerms.Count(term => term.Status == TermStatus.Difficult);
        var fresh = allTerms.Count(term => term.Status == TermStatus.New);
        var studied = allTerms.Where(term => term.LastReviewedAtUtc is not null).ToList();
        var averageInterval = studied.Count == 0 ? 0 : studied.Average(term => term.IntervalDays);
        var windowDays = today.DayNumber - from.DayNumber + 1;
        var active = window.Select(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone)).Distinct().Count();
        var regularity = LearningAnalytics.RegularityPercent(active, windowDays);
        var share = LearningAnalytics.MasteredSharePercent(mastered, allTerms.Count);
        var weights = new AnalyticsWeights(user.WeightSuccess, user.WeightRegularity, user.WeightMastery);
        var effectiveness = LearningAnalytics.Effectiveness(success, regularity, share, weights);

        var activity = BuildActivity(window, from, today);
        var trend = BuildMasteryTrend(allTerms, records, from, today, user.MasteryTargetDays);
        var slices = Enum.GetValues<TermStatus>()
            .Select(status => new StatusSlice(status, allTerms.Count(term => term.Status == status)))
            .ToList();

        return new StatisticsReport(
            range,
            from,
            today,
            window.Count,
            successful,
            window.Count - successful,
            LearningAnalytics.AverageQuality(qualities),
            success,
            mastered,
            learning,
            difficult,
            fresh,
            averageInterval,
            effectiveness,
            regularity,
            share,
            user.MasteryTargetDays,
            activity,
            slices,
            trend);
    }

    private IReadOnlyList<DayPoint> BuildActivity(IReadOnlyList<ReviewRecord> window, DateOnly from, DateOnly today)
    {
        var span = today.DayNumber - from.DayNumber + 1;
        if (span <= 42)
        {
            return Enumerable.Range(0, span)
                .Select(offset => from.AddDays(offset))
                .Select(date => Point(window, date))
                .ToList();
        }

        var buckets = new List<DayPoint>();
        for (var start = from; start <= today; start = start.AddDays(7))
        {
            var end = start.AddDays(6);
            if (end > today)
            {
                end = today;
            }

            var slice = window.Where(record =>
            {
                var date = ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone);
                return date >= start && date <= end;
            }).ToList();
            buckets.Add(new DayPoint(start, slice.Count, slice.Count(record => record.Quality >= 3)));
        }

        return buckets;
    }

    private DayPoint Point(IReadOnlyList<ReviewRecord> window, DateOnly date)
    {
        var slice = window.Where(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) == date).ToList();
        return new DayPoint(date, slice.Count, slice.Count(record => record.Quality >= 3));
    }

    private IReadOnlyList<DayPoint> BuildMasteryTrend(
        IReadOnlyList<Term> allTerms,
        IReadOnlyList<ReviewRecord> records,
        DateOnly from,
        DateOnly today,
        int targetDays)
    {
        var span = today.DayNumber - from.DayNumber + 1;
        var step = span > 42 ? 7 : 1;
        var points = new List<DayPoint>();
        for (var date = from; date <= today; date = date.AddDays(step))
        {
            var mastered = 0;
            foreach (var term in allTerms)
            {
                var last = records
                    .Where(record => record.TermId == term.Id && ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) <= date)
                    .OrderBy(record => record.ReviewedAtUtc)
                    .LastOrDefault();
                if (last is not null && last.Quality >= 3 && last.NewInterval >= targetDays && last.RepetitionCountAfter >= 2)
                {
                    mastered++;
                }
            }

            points.Add(new DayPoint(date, mastered, mastered));
        }

        return points;
    }
}

public sealed class SettingsService(
    ISettingsRepository settings,
    IReviewHistoryRepository history,
    ITermRepository terms,
    ITermService termService,
    IReminderScheduler reminders) : ISettingsService
{
    public Task<UserSettings> GetAsync(CancellationToken cancellationToken)
        => settings.GetAsync(cancellationToken);

    public async Task<UserSettings> SaveAsync(UserSettings incoming, CancellationToken cancellationToken)
    {
        incoming.DisplayName = incoming.DisplayName.Trim();
        incoming.Id = UserSettings.SingletonId;
        InputRules.Settings(incoming);
        await settings.SaveAsync(incoming, cancellationToken);
        await termService.ReevaluateStatusesAsync(incoming.MasteryTargetDays, cancellationToken);
        await reminders.ApplyAsync(incoming, cancellationToken);
        return incoming;
    }

    public async Task ResetStatisticsAsync(CancellationToken cancellationToken)
    {
        await history.DeleteAllAsync(cancellationToken);
        await terms.ResetSchedulingAsync(cancellationToken);
    }
}
