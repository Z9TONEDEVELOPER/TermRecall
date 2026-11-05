using TermRecall.Application.Abstractions;
using TermRecall.Application.DTOs;
using TermRecall.Application.Validation;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;

namespace TermRecall.Application.Services;

public interface IReviewService
{
    Task<ReviewOutcome> SubmitAsync(Guid termId, int quality, int? responseDurationMs, CancellationToken cancellationToken);
    Task<IReadOnlyList<Term>> BuildQueueAsync(StudyMode mode, Guid? deckId, bool ignoreDailyLimit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewRecord>> HistoryAsync(Guid? termId, CancellationToken cancellationToken);
    Task<(int ReviewsToday, int NewToday)> TodayCountsAsync(CancellationToken cancellationToken);
}

public sealed class ReviewService(
    ITermRepository terms,
    IReviewHistoryRepository history,
    ISettingsRepository settings,
    ITransactionRunner transactions,
    IClock clock) : IReviewService
{
    public Task<ReviewOutcome> SubmitAsync(Guid termId, int quality, int? responseDurationMs, CancellationToken cancellationToken)
    {
        InputRules.Quality(quality);
        return transactions.InTransactionAsync(() => SubmitCoreAsync(termId, quality, responseDurationMs, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<Term>> BuildQueueAsync(StudyMode mode, Guid? deckId, bool ignoreDailyLimit, CancellationToken cancellationToken)
    {
        var all = await terms.ListAsync(cancellationToken);
        var user = await settings.GetAsync(cancellationToken);
        var (reviewsToday, newToday) = CountToday(await history.ListAsync(cancellationToken), clock);
        var request = new QueueRequest(
            mode,
            mode == StudyMode.Deck ? deckId : deckId,
            user.IncludeNewTerms || mode == StudyMode.NewOnly,
            user.DailyReviewLimit,
            user.DailyNewTermsLimit,
            reviewsToday,
            newToday,
            user.DifficultEaseThreshold,
            ignoreDailyLimit);
        return ReviewQueuePlanner.Plan(all, clock.UtcNow, clock.TimeZone, request);
    }

    public Task<IReadOnlyList<ReviewRecord>> HistoryAsync(Guid? termId, CancellationToken cancellationToken)
        => termId is Guid id
            ? history.ListByTermAsync(id, cancellationToken)
            : history.ListAsync(cancellationToken);

    public async Task<(int ReviewsToday, int NewToday)> TodayCountsAsync(CancellationToken cancellationToken)
        => CountToday(await history.ListAsync(cancellationToken), clock);

    private async Task<ReviewOutcome> SubmitCoreAsync(Guid termId, int quality, int? responseDurationMs, CancellationToken cancellationToken)
    {
        var term = await terms.GetAsync(termId, cancellationToken)
            ?? throw new Exceptions.NotFoundException("Термин не найден.");
        var user = await settings.GetAsync(cancellationToken);
        var previous = new Sm2State(term.EaseFactor, term.IntervalDays, term.RepetitionCount);
        var result = Sm2Algorithm.Review(previous, quality);
        var now = clock.UtcNow;
        term.EaseFactor = result.EaseFactor;
        term.IntervalDays = result.IntervalDays;
        term.RepetitionCount = result.RepetitionCount;
        term.LastReviewedAtUtc = now;
        term.NextReviewAtUtc = ReviewCalendar.NextReviewUtc(now, result.IntervalDays, clock.TimeZone);
        if (!result.Successful)
        {
            term.LapseCount++;
        }

        term.Status = TermStatusEvaluator.Evaluate(term, quality, user.MasteryTargetDays);
        await terms.UpdateAsync(term, cancellationToken);

        var record = new ReviewRecord
        {
            Id = Guid.NewGuid(),
            TermId = term.Id,
            ReviewedAtUtc = now,
            Quality = quality,
            PreviousInterval = previous.IntervalDays,
            NewInterval = result.IntervalDays,
            PreviousEaseFactor = previous.EaseFactor,
            NewEaseFactor = result.EaseFactor,
            ResponseDurationMs = responseDurationMs is < 0 ? null : responseDurationMs,
            RepetitionCountBefore = previous.RepetitionCount,
            RepetitionCountAfter = result.RepetitionCount,
            CalculationNote = result.Explanation
        };
        await history.AddAsync(record, cancellationToken);

        return new ReviewOutcome(
            term.Id,
            term.Title,
            quality,
            previous.IntervalDays,
            result.IntervalDays,
            previous.EaseFactor,
            result.EaseFactor,
            term.NextReviewAtUtc.Value,
            term.Status,
            result.Explanation);
    }

    internal static (int Reviews, int Introduced) CountToday(IReadOnlyList<ReviewRecord> records, IClock clock)
    {
        var today = ReviewCalendar.LocalDate(clock.UtcNow, clock.TimeZone);
        var reviews = records.Count(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) == today);
        var introduced = records
            .GroupBy(record => record.TermId)
            .Select(group => group.MinBy(record => record.ReviewedAtUtc)!)
            .Count(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) == today);
        return (reviews, introduced);
    }
}
