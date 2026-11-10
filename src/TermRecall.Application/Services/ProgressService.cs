using System.Globalization;
using TermRecall.Application.Abstractions;
using TermRecall.Application.DTOs;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Enums;

namespace TermRecall.Application.Services;

public interface IProgressService
{
    Task<ProgressReport> GetAsync(CancellationToken cancellationToken);
}

public sealed class ProgressService(
    ITermRepository terms,
    IReviewHistoryRepository history,
    ISettingsRepository settings,
    IAchievementRepository achievements,
    IClock clock) : IProgressService
{
    public async Task<ProgressReport> GetAsync(CancellationToken cancellationToken)
    {
        var records = await history.ListAsync(cancellationToken);
        var allTerms = (await terms.ListAsync(cancellationToken)).Where(term => !term.Archived).ToList();
        var user = await settings.GetAsync(cancellationToken);
        var today = ReviewCalendar.LocalDate(clock.UtcNow, clock.TimeZone);
        var byDay = records
            .GroupBy(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone))
            .ToDictionary(group => group.Key, group => group.Count());
        var weekStart = StudyCalendar.WeekStart(today);
        var weekMinutes = StudyTime.MinutesFromMilliseconds(records
            .Where(record =>
            {
                var date = ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone);
                return date >= weekStart && date <= today;
            })
            .Select(record => record.ResponseDurationMs));
        var facts = new AchievementFacts(
            records.Count,
            StreakCalculator.LongestStreak(byDay.Keys),
            allTerms.Count(term => term.Status == TermStatus.Mastered),
            weekMinutes);
        var earned = AchievementCatalog.EarnedCodes(facts);
        var granted = (await achievements.ListAsync(cancellationToken)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var code in earned)
        {
            if (granted.ContainsKey(code))
            {
                continue;
            }

            var awardedAt = clock.UtcNow;
            await achievements.AddAsync(code, awardedAt, cancellationToken);
            granted[code] = awardedAt;
        }

        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var month = today.ToString("MMMM yyyy", culture);
        if (month.Length > 0)
        {
            month = char.ToUpper(month[0], culture) + month[1..];
        }

        var cards = AchievementCatalog.All
            .Select(definition => new AchievementCard(
                definition.Code,
                definition.Title,
                definition.Requirement,
                granted.TryGetValue(definition.Code, out var awardedAt) ? awardedAt : null))
            .ToList();
        return new ProgressReport(month, StudyCalendar.Month(today, byDay), weekMinutes, user.WeeklyMinutesGoal, cards);
    }
}
