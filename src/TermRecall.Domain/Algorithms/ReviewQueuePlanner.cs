using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;

namespace TermRecall.Domain.Algorithms;

public sealed record QueueRequest(
    StudyMode Mode,
    Guid? DeckId,
    bool IncludeNewTerms,
    int DailyReviewLimit,
    int DailyNewTermsLimit,
    int ReviewsCompletedToday,
    int NewTermsIntroducedToday,
    double DifficultEaseThreshold,
    bool IgnoreDailyLimit);

/// <summary>
/// Очередь строится заново из сроков в базе. Просроченный термин остаётся просроченным,
/// пока пользователь его не повторит.
/// </summary>
public static class ReviewQueuePlanner
{
    public static IReadOnlyList<Term> Plan(
        IEnumerable<Term> terms,
        DateTime nowUtc,
        TimeZoneInfo timeZone,
        QueueRequest request)
    {
        var today = ReviewCalendar.LocalDate(nowUtc, timeZone);
        var pool = terms.ToList();
        if (request.DeckId is Guid deckId)
        {
            pool = pool.Where(term => term.DeckId == deckId).ToList();
        }

        var remaining = request.IgnoreDailyLimit
            ? int.MaxValue
            : Math.Max(0, request.DailyReviewLimit - Math.Max(0, request.ReviewsCompletedToday));
        if (remaining == 0 || pool.Count == 0)
        {
            return [];
        }

        if (request.Mode == StudyMode.NewOnly)
        {
            var budget = Math.Min(remaining, Math.Max(0, request.DailyNewTermsLimit - request.NewTermsIntroducedToday));
            return pool.Where(IsNew).OrderBy(term => term.CreatedAtUtc).Take(budget).ToList();
        }

        if (request.Mode == StudyMode.Difficult)
        {
            pool = pool.Where(term => IsDifficult(term, request.DifficultEaseThreshold)).ToList();
        }

        var overdue = new List<Term>();
        var dueToday = new List<Term>();
        var practiceLater = new List<Term>();
        var fresh = new List<Term>();

        foreach (var term in pool)
        {
            if (IsNew(term))
            {
                fresh.Add(term);
                continue;
            }

            if (term.NextReviewAtUtc is null)
            {
                fresh.Add(term);
                continue;
            }

            var due = ReviewCalendar.LocalDate(term.NextReviewAtUtc.Value, timeZone);
            if (due < today)
            {
                overdue.Add(term);
            }
            else if (due == today)
            {
                dueToday.Add(term);
            }
            else if (request.Mode == StudyMode.Difficult)
            {
                practiceLater.Add(term);
            }
        }

        overdue.Sort(CompareSchedule);
        dueToday.Sort(CompareSchedule);
        practiceLater.Sort(CompareSchedule);
        fresh.Sort(static (left, right) => left.CreatedAtUtc.CompareTo(right.CreatedAtUtc));

        var result = new List<Term>(Math.Min(pool.Count, remaining == int.MaxValue ? pool.Count : remaining));
        Take(result, overdue, remaining);
        Take(result, dueToday, remaining);
        Take(result, practiceLater, remaining);

        var allowNew = request.IncludeNewTerms && request.Mode != StudyMode.Difficult;
        if (allowNew)
        {
            var newBudget = Math.Max(0, request.DailyNewTermsLimit - Math.Max(0, request.NewTermsIntroducedToday));
            foreach (var term in fresh)
            {
                if (result.Count >= remaining || newBudget <= 0)
                {
                    break;
                }

                result.Add(term);
                newBudget--;
            }
        }

        return result;
    }

    private static void Take(List<Term> target, List<Term> source, int limit)
    {
        foreach (var term in source)
        {
            if (target.Count >= limit)
            {
                return;
            }

            target.Add(term);
        }
    }

    private static bool IsNew(Term term)
        => term.Status == TermStatus.New && term.LastReviewedAtUtc is null;

    public static bool IsDifficult(Term term, double easeThreshold)
        => term.Status == TermStatus.Difficult || term.EaseFactor <= easeThreshold;

    private static int CompareSchedule(Term left, Term right)
    {
        var byDate = Nullable.Compare(left.NextReviewAtUtc, right.NextReviewAtUtc);
        if (byDate != 0)
        {
            return byDate;
        }

        var byEase = left.EaseFactor.CompareTo(right.EaseFactor);
        if (byEase != 0)
        {
            return byEase;
        }

        return string.Compare(left.Title, right.Title, StringComparison.CurrentCultureIgnoreCase);
    }
}
