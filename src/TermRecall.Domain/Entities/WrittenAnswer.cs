namespace TermRecall.Domain.Entities;

/// <summary>
/// Письменный ответ пользователя. Не участвует в расчёте SM-2.
/// </summary>
public sealed class WrittenAnswer
{
    public Guid Id { get; set; }
    public Guid TermId { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
    public string ResponseText { get; set; } = string.Empty;
    public int Quality { get; set; }
    public int? DurationMs { get; set; }
}
