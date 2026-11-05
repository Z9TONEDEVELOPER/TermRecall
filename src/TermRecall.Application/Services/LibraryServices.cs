using TermRecall.Application.Abstractions;
using TermRecall.Application.DTOs;
using TermRecall.Application.Exceptions;
using TermRecall.Application.Validation;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;

namespace TermRecall.Application.Services;

public interface IDeckService
{
    Task<IReadOnlyList<Deck>> ListAsync(CancellationToken cancellationToken);
    Task<Deck> SaveAsync(Guid? id, string name, string description, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ITermService
{
    Task<IReadOnlyList<Term>> ListAsync(CancellationToken cancellationToken);
    Task<Term> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Term> SaveAsync(TermDraft draft, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task ReevaluateStatusesAsync(int masteryTargetDays, CancellationToken cancellationToken);
}

public sealed class DeckService(IDeckRepository decks, IClock clock) : IDeckService
{
    public Task<IReadOnlyList<Deck>> ListAsync(CancellationToken cancellationToken)
        => decks.ListAsync(cancellationToken);

    public async Task<Deck> SaveAsync(Guid? id, string name, string description, CancellationToken cancellationToken)
    {
        description ??= string.Empty;
        InputRules.Deck(name, description);
        var now = clock.UtcNow;
        if (id is null)
        {
            var deck = new Deck
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Description = description.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await decks.AddAsync(deck, cancellationToken);
            return deck;
        }

        var existing = await decks.GetAsync(id.Value, cancellationToken)
            ?? throw new NotFoundException("Набор не найден.");
        existing.Name = name.Trim();
        existing.Description = description.Trim();
        existing.UpdatedAtUtc = now;
        await decks.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await decks.GetAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("Набор не найден.");
        }

        await decks.DeleteAsync(id, cancellationToken);
    }
}

public sealed class TermService(
    ITermRepository terms,
    IDeckRepository decks,
    ICategoryRepository categories,
    IReviewHistoryRepository history,
    IClock clock) : ITermService
{
    public Task<IReadOnlyList<Term>> ListAsync(CancellationToken cancellationToken)
        => terms.ListAsync(cancellationToken);

    public async Task<Term> GetAsync(Guid id, CancellationToken cancellationToken)
        => await terms.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Термин не найден.");

    public async Task<Term> SaveAsync(TermDraft draft, CancellationToken cancellationToken)
    {
        var example = draft.Example ?? string.Empty;
        var explanation = draft.Explanation ?? string.Empty;
        InputRules.Term(draft.Title, draft.Definition, example, explanation);
        if (await decks.GetAsync(draft.DeckId, cancellationToken) is null)
        {
            throw new ValidationException("Выберите набор, в который попадёт термин.");
        }

        var categoryId = await ResolveCategoryAsync(draft, cancellationToken);
        if (draft.Id is null)
        {
            var term = new Term
            {
                Id = Guid.NewGuid(),
                Title = draft.Title.Trim(),
                Definition = draft.Definition.Trim(),
                Example = example.Trim(),
                Explanation = explanation.Trim(),
                DeckId = draft.DeckId,
                CategoryId = categoryId,
                CreatedAtUtc = clock.UtcNow,
                EaseFactor = Sm2Algorithm.DefaultEaseFactor,
                IntervalDays = 0,
                RepetitionCount = 0,
                Status = TermStatus.New
            };
            await terms.AddAsync(term, cancellationToken);
            return term;
        }

        var existing = await terms.GetAsync(draft.Id.Value, cancellationToken)
            ?? throw new NotFoundException("Термин не найден.");
        existing.Title = draft.Title.Trim();
        existing.Definition = draft.Definition.Trim();
        existing.Example = example.Trim();
        existing.Explanation = explanation.Trim();
        existing.DeckId = draft.DeckId;
        existing.CategoryId = categoryId;
        await terms.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await terms.GetAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("Термин не найден.");
        }

        await terms.DeleteAsync(id, cancellationToken);
    }

    public async Task ReevaluateStatusesAsync(int masteryTargetDays, CancellationToken cancellationToken)
    {
        var records = await history.ListAsync(cancellationToken);
        var lastQuality = records
            .GroupBy(record => record.TermId)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.ReviewedAtUtc).Last().Quality);

        foreach (var term in await terms.ListAsync(cancellationToken))
        {
            var quality = lastQuality.TryGetValue(term.Id, out var value) ? value : Sm2Algorithm.MinimumPassingQuality;
            var status = TermStatusEvaluator.Evaluate(term, quality, masteryTargetDays);
            if (status == term.Status)
            {
                continue;
            }

            term.Status = status;
            await terms.UpdateAsync(term, cancellationToken);
        }
    }

    private async Task<Guid?> ResolveCategoryAsync(TermDraft draft, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(draft.NewCategoryName))
        {
            var name = draft.NewCategoryName.Trim();
            if (name.Length > InputRules.MaxName)
            {
                throw new ValidationException($"Название категории не длиннее {InputRules.MaxName} символов.");
            }

            var existing = await categories.FindByNameAsync(name, cancellationToken);
            if (existing is not null)
            {
                return existing.Id;
            }

            var created = new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                Color = "#6558E8"
            };
            await categories.AddAsync(created, cancellationToken);
            return created.Id;
        }

        if (draft.CategoryId is Guid id)
        {
            if (await categories.GetAsync(id, cancellationToken) is null)
            {
                throw new ValidationException("Выбранная категория не найдена.");
            }

            return id;
        }

        return null;
    }
}
