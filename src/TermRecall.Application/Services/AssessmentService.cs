using TermRecall.Application.Abstractions;
using TermRecall.Application.Validation;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;

namespace TermRecall.Application.Services;

public interface IAssessmentService
{
    Task<IReadOnlyList<ChoiceQuestion>> BuildAsync(AssessmentMode mode, CancellationToken cancellationToken);
    Task<bool> RecordAsync(AssessmentMode mode, Guid termId, string selectedText, int? durationMs, CancellationToken cancellationToken);
}

public interface IFavoriteService
{
    Task<IReadOnlySet<Guid>> ListIdsAsync(CancellationToken cancellationToken);
    Task<bool> IsFavoriteAsync(Guid termId, CancellationToken cancellationToken);
    Task<bool> ToggleAsync(Guid termId, CancellationToken cancellationToken);
}

/// <summary>
/// Тест и экзамен сверяют выбранное определение с текстом термина и не вызывают SM-2.
/// </summary>
public sealed class AssessmentService(
    ITermRepository terms,
    IAssessmentRepository answers,
    IClock clock) : IAssessmentService
{
    public async Task<IReadOnlyList<ChoiceQuestion>> BuildAsync(AssessmentMode mode, CancellationToken cancellationToken)
    {
        var all = (await terms.ListAsync(cancellationToken)).Where(term => !term.Archived).ToList();
        var count = mode == AssessmentMode.Exam ? ChoiceQuiz.ExamCount : ChoiceQuiz.PracticeCount;
        return ChoiceQuiz.Build(all, count, Random.Shared);
    }

    public async Task<bool> RecordAsync(AssessmentMode mode, Guid termId, string selectedText, int? durationMs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedText) || selectedText.Trim().Length > InputRules.MaxDefinition)
        {
            throw new Exceptions.ValidationException("Сначала выберите вариант ответа.");
        }

        var term = await terms.GetAsync(termId, cancellationToken)
            ?? throw new Exceptions.NotFoundException("Термин не найден.");
        var correct = string.Equals(term.Definition.Trim(), selectedText.Trim(), StringComparison.CurrentCulture);
        await answers.AddAsync(new AssessmentAnswer
        {
            Id = Guid.NewGuid(),
            Mode = mode,
            TermId = term.Id,
            AnsweredAtUtc = clock.UtcNow,
            Correct = correct,
            SelectedText = selectedText.Trim(),
            DurationMs = durationMs is < 0 ? null : durationMs
        }, cancellationToken);
        return correct;
    }
}

public sealed class FavoriteService(ITermRepository terms, IFavoriteRepository favorites, IClock clock) : IFavoriteService
{
    public Task<IReadOnlySet<Guid>> ListIdsAsync(CancellationToken cancellationToken)
        => favorites.ListIdsAsync(cancellationToken);

    public Task<bool> IsFavoriteAsync(Guid termId, CancellationToken cancellationToken)
        => favorites.ContainsAsync(termId, cancellationToken);

    public async Task<bool> ToggleAsync(Guid termId, CancellationToken cancellationToken)
    {
        _ = await terms.GetAsync(termId, cancellationToken)
            ?? throw new Exceptions.NotFoundException("Термин не найден.");
        if (await favorites.ContainsAsync(termId, cancellationToken))
        {
            await favorites.RemoveAsync(termId, cancellationToken);
            return false;
        }

        await favorites.AddAsync(termId, clock.UtcNow, cancellationToken);
        return true;
    }
}
