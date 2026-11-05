using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;

namespace TermRecall.Domain.Tests;

public class ReviewCalendarTests
{
    [Fact]
    public void NextReview_UsesLocalMidnightAndSurvivesALongGap()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        var reviewed = new DateTime(2026, 3, 10, 22, 30, 0, DateTimeKind.Utc);
        var due = ReviewCalendar.NextReviewUtc(reviewed, 1, zone);

        Assert.Equal(new DateTime(2026, 3, 11, 21, 0, 0, DateTimeKind.Utc), due);
        var later = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(ReviewCalendar.IsOverdue(due, later, zone));
        Assert.Equal(1, 1);
    }

    [Fact]
    public void SameLocalDay_IsDueButNotOverdue()
    {
        var zone = TimeZoneInfo.Utc;
        var due = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        var noon = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(ReviewCalendar.IsDue(due, noon, zone));
        Assert.False(ReviewCalendar.IsOverdue(due, noon, zone));
    }

    [Fact]
    public void BerlinDst_DoesNotShiftTheCivilDay()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var reviewed = new DateTime(2026, 3, 28, 23, 30, 0, DateTimeKind.Utc);
        var due = ReviewCalendar.NextReviewUtc(reviewed, 1, zone);
        Assert.Equal(new DateOnly(2026, 3, 30), ReviewCalendar.LocalDate(due, zone));
    }
}

public class LearningAnalyticsTests
{
    [Fact]
    public void SuccessAverageAndMastery_FollowTheFormulas()
    {
        Assert.Equal(75, LearningAnalytics.SuccessPercent(3, 4), 10);
        Assert.Equal(0, LearningAnalytics.SuccessPercent(0, 0));
        Assert.Equal(3.5, LearningAnalytics.AverageQuality([5, 4, 3, 2]), 10);
        Assert.Equal(0, LearningAnalytics.IntervalMasteryPercent(0, 21));
        Assert.Equal(100, LearningAnalytics.IntervalMasteryPercent(40, 21), 10);
        var partial = LearningAnalytics.IntervalMasteryPercent(6, 21);
        Assert.Equal(6d / 21d * 100d, partial, 10);
    }

    [Fact]
    public void Effectiveness_UsesConfigurableWeights()
    {
        var value = LearningAnalytics.Effectiveness(80, 50, 20, new AnalyticsWeights(0.5, 0.2, 0.3));
        Assert.Equal(56, value, 10);
        Assert.Throws<ArgumentException>(() => LearningAnalytics.Effectiveness(1, 1, 1, new AnalyticsWeights(0.5, 0.5, 0.5)));
    }

    [Fact]
    public void Regularity_IsCappedAtOneHundred()
    {
        Assert.Equal(100, LearningAnalytics.RegularityPercent(10, 7), 10);
        Assert.Equal(0, LearningAnalytics.RegularityPercent(0, 0));
    }
}

public class StreakCalculatorTests
{
    [Fact]
    public void Streak_ContinuesFromYesterdayIfTodayIsEmpty()
    {
        var today = new DateOnly(2026, 11, 5);
        var days = new[] { today.AddDays(-1), today.AddDays(-2), today.AddDays(-4) };
        Assert.Equal(2, StreakCalculator.CurrentStreak(days, today));
    }

    [Fact]
    public void Streak_IncludesToday()
    {
        var today = new DateOnly(2026, 11, 5);
        Assert.Equal(3, StreakCalculator.CurrentStreak([today, today.AddDays(-1), today.AddDays(-2)], today));
    }
}

public class ReviewQueuePlannerTests
{
    [Fact]
    public void Queue_OrdersOverdueThenTodayThenLimitedNew()
    {
        var zone = TimeZoneInfo.Utc;
        var now = new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
        var deck = Guid.NewGuid();
        var overdue = Term("Просрочен", deck, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), TermStatus.Review, 2.2, 3);
        var due = Term("Сегодня", deck, now.Date, TermStatus.Learning, 2.4, 1);
        var future = Term("Позже", deck, now.AddDays(4), TermStatus.Review, 2.5, 2);
        var freshA = Term("Новый А", deck, null, TermStatus.New, 2.5, 0);
        freshA.CreatedAtUtc = now.AddMinutes(-2);
        var freshB = Term("Новый Б", deck, null, TermStatus.New, 2.5, 0);
        freshB.CreatedAtUtc = now.AddMinutes(-1);

        var queue = ReviewQueuePlanner.Plan(
            [future, freshB, due, freshA, overdue],
            now,
            zone,
            new QueueRequest(StudyMode.DueMix, null, true, 10, 1, 0, 0, 1.6, false));

        Assert.Equal(["Просрочен", "Сегодня", "Новый А"], queue.Select(term => term.Title).ToArray());
    }

    [Fact]
    public void MissedDays_DoNotRemoveOrMasterTheTerm()
    {
        var term = Term("CLR", Guid.NewGuid(), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), TermStatus.Review, 2.5, 3);
        term.IntervalDays = 16;
        var queue = ReviewQueuePlanner.Plan(
            [term],
            new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc),
            TimeZoneInfo.Utc,
            new QueueRequest(StudyMode.DueMix, null, true, 20, 5, 0, 0, 1.6, false));

        Assert.Single(queue);
        Assert.Equal(TermStatus.Review, queue[0].Status);
        Assert.Equal(16, queue[0].IntervalDays);
    }

    [Fact]
    public void DailyLimit_StopsTheQueue()
    {
        var deck = Guid.NewGuid();
        var now = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        var terms = Enumerable.Range(0, 5)
            .Select(index => Term("T" + index, deck, now, TermStatus.Learning, 2.5, 1))
            .ToList();
        var queue = ReviewQueuePlanner.Plan(terms, now, TimeZoneInfo.Utc, new QueueRequest(StudyMode.DueMix, null, false, 4, 10, 3, 0, 1.6, false));
        Assert.Single(queue);
    }

    [Fact]
    public void DifficultMode_IncludesNotYetDueHardCards()
    {
        var deck = Guid.NewGuid();
        var now = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        var hardLater = Term("Трудный", deck, now.AddDays(3), TermStatus.Difficult, 1.4, 0);
        var easyDue = Term("Лёгкий", deck, now, TermStatus.Review, 2.6, 4);
        var queue = ReviewQueuePlanner.Plan([easyDue, hardLater], now, TimeZoneInfo.Utc, new QueueRequest(StudyMode.Difficult, null, true, 10, 10, 0, 0, 1.6, false));
        Assert.Equal(["Трудный"], queue.Select(term => term.Title).ToArray());
    }

    private static Term Term(string title, Guid deck, DateTime? next, TermStatus status, double ease, int reps) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Definition = title,
        DeckId = deck,
        CreatedAtUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        NextReviewAtUtc = next,
        LastReviewedAtUtc = status == TermStatus.New ? null : new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        Status = status,
        EaseFactor = ease,
        RepetitionCount = reps,
        IntervalDays = reps == 0 ? 0 : 6
    };
}

public class TermStatusEvaluatorTests
{
    [Fact]
    public void Status_FollowsFailureLearningAndMastery()
    {
        var term = new Term { RepetitionCount = 0, IntervalDays = 0 };
        Assert.Equal(TermStatus.New, TermStatusEvaluator.Evaluate(term, 5, 21));

        term.LastReviewedAtUtc = DateTime.UtcNow;
        term.RepetitionCount = 1;
        term.IntervalDays = 1;
        Assert.Equal(TermStatus.Learning, TermStatusEvaluator.Evaluate(term, 4, 21));

        term.RepetitionCount = 3;
        term.IntervalDays = 16;
        Assert.Equal(TermStatus.Review, TermStatusEvaluator.Evaluate(term, 4, 21));

        term.IntervalDays = 21;
        Assert.Equal(TermStatus.Mastered, TermStatusEvaluator.Evaluate(term, 5, 21));

        Assert.Equal(TermStatus.Difficult, TermStatusEvaluator.Evaluate(term, 2, 21));
    }
}
