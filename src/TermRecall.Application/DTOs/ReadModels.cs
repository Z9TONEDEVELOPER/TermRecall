using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Enums;

namespace TermRecall.Application.DTOs;

public sealed record DayPoint(DateOnly Date, int Reviews, int Successful);

public sealed record RecentTerm(string Title, TermStatus Status, DateTime ReviewedAtUtc);

public sealed record StatusSlice(TermStatus Status, int Count);

public sealed record DashboardSummary(
    int LibraryCount,
    int DueCount,
    int OverdueCount,
    int NewCount,
    int MasteredCount,
    int DifficultCount,
    int Streak,
    double SuccessPercent,
    int ReviewsToday,
    int DailyReviewLimit,
    bool LimitReached,
    bool ShowReminder,
    IReadOnlyList<DayPoint> LastSevenDays,
    int LearningCount,
    int DueTodayCount,
    int NewInNextSession,
    int NextSessionCount,
    int GoalRemaining,
    double GoalPercent,
    IReadOnlyList<RecentTerm> RecentMastered,
    IReadOnlyList<RecentTerm> RecentReviewed);

public sealed record StatisticsReport(
    StatsRange Range,
    DateOnly From,
    DateOnly To,
    int TotalReviews,
    int SuccessfulReviews,
    int FailedReviews,
    double AverageQuality,
    double SuccessPercent,
    int MasteredTerms,
    int LearningTerms,
    int DifficultTerms,
    int NewTerms,
    double AverageIntervalDays,
    double Effectiveness,
    double RegularityPercent,
    double MasteredSharePercent,
    int MasteryTargetDays,
    IReadOnlyList<DayPoint> Activity,
    IReadOnlyList<StatusSlice> Statuses,
    IReadOnlyList<DayPoint> MasteryTrend,
    double WeightSuccess,
    double WeightRegularity,
    double WeightMastery,
    IReadOnlyList<CategoryStudy> Categories,
    ScheduledLoad Forecast);

public sealed record ReviewOutcome(
    Guid TermId,
    string TermTitle,
    int Quality,
    int PreviousInterval,
    int NewInterval,
    double PreviousEaseFactor,
    double NewEaseFactor,
    DateTime NextReviewUtc,
    TermStatus Status,
    string Explanation);

public sealed record AchievementCard(string Code, string Title, string Requirement, DateTime? AwardedAtUtc);

public sealed record ProgressReport(
    string MonthTitle,
    IReadOnlyList<CalendarCell> Calendar,
    int WeekMinutes,
    int WeekGoalMinutes,
    IReadOnlyList<AchievementCard> Achievements);

public sealed record ImportResult(int Added, int UpdatedDecks, int Skipped, IReadOnlyList<string> Errors);

public sealed record TermDraft(
    Guid? Id,
    string Title,
    string Definition,
    string Example,
    string Explanation,
    Guid DeckId,
    Guid? CategoryId,
    string? NewCategoryName,
    string Code = "",
    string Link = "",
    string Tags = "",
    bool Archived = false);
