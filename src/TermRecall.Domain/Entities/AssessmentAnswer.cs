using TermRecall.Domain.Enums;

namespace TermRecall.Domain.Entities;

/// <summary>
/// Ответ теста или экзамена. Не участвует в расчёте SM-2.
/// </summary>
public sealed class AssessmentAnswer
{
    public Guid Id { get; set; }
    public AssessmentMode Mode { get; set; }
    public Guid TermId { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
    public bool Correct { get; set; }
    public string SelectedText { get; set; } = string.Empty;
    public int? DurationMs { get; set; }
}
