using TermRecall.Application.Abstractions;
using TermRecall.Application.DTOs;
using TermRecall.Application.Exceptions;
using TermRecall.Domain.Algorithms;

namespace TermRecall.Application.Services;

public interface ICardDraftService
{
    Task<ImportResult> CreateAsync(Guid deckId, string text, CancellationToken cancellationToken);
}

/// <summary>
/// Создаёт новые термины из строк. Интервал и журнал SM-2 не меняет.
/// </summary>
public sealed class CardDraftService(ITermService terms, IDeckRepository decks) : ICardDraftService
{
    public async Task<ImportResult> CreateAsync(Guid deckId, string text, CancellationToken cancellationToken)
    {
        if (await decks.GetAsync(deckId, cancellationToken) is null)
        {
            throw new ValidationException("Выберите набор для новых карточек.");
        }

        var drafts = CardDrafts.Parse(text);
        if (drafts.Count == 0)
        {
            throw new ValidationException("Нужны строки вида «термин - определение». Пустые строки и строки без разделителя пропускаются.");
        }

        var existing = await terms.ListAsync(cancellationToken);
        var added = 0;
        var skipped = 0;
        var errors = new List<string>();
        foreach (var draft in drafts)
        {
            if (existing.Any(term => term.DeckId == deckId && string.Equals(term.Title, draft.Title, StringComparison.CurrentCultureIgnoreCase)))
            {
                skipped++;
                continue;
            }

            try
            {
                var created = await terms.SaveAsync(new TermDraft(null, draft.Title, draft.Definition, string.Empty, string.Empty, deckId, null, null), cancellationToken);
                existing = existing.Append(created).ToList();
                added++;
            }
            catch (ValidationException exception)
            {
                errors.Add(draft.Title + ": " + exception.Message);
                skipped++;
            }
        }

        return new ImportResult(added, 0, skipped, errors);
    }
}
