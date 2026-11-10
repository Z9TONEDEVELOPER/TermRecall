using TermRecall.Application.Abstractions;
using TermRecall.Application.Validation;
using TermRecall.Domain.Entities;

namespace TermRecall.Application.Services;

public interface IWrittenRecallService
{
    Task SaveAsync(Guid termId, string responseText, int quality, int? durationMs, CancellationToken cancellationToken);
    Task<IReadOnlyList<WrittenAnswer>> ListAsync(Guid? termId, CancellationToken cancellationToken);
}

/// <summary>
/// Сохраняет письменный ответ отдельно от журнала SM-2 и не меняет интервал термина.
/// </summary>
public sealed class WrittenRecallService(
    ITermRepository terms,
    IWrittenAnswerRepository answers,
    IClock clock) : IWrittenRecallService
{
    public async Task SaveAsync(Guid termId, string responseText, int quality, int? durationMs, CancellationToken cancellationToken)
    {
        InputRules.WrittenResponse(responseText);
        InputRules.Quality(quality);
        var term = await terms.GetAsync(termId, cancellationToken)
            ?? throw new Exceptions.NotFoundException("Термин не найден.");
        await answers.AddAsync(new WrittenAnswer
        {
            Id = Guid.NewGuid(),
            TermId = term.Id,
            AnsweredAtUtc = clock.UtcNow,
            ResponseText = responseText.Trim(),
            Quality = quality,
            DurationMs = durationMs is < 0 ? null : durationMs
        }, cancellationToken);
    }

    public Task<IReadOnlyList<WrittenAnswer>> ListAsync(Guid? termId, CancellationToken cancellationToken)
        => answers.ListAsync(termId, cancellationToken);
}
