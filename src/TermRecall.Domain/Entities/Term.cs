using TermRecall.Domain.Enums;

namespace TermRecall.Domain.Entities;

public sealed class Term
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public Guid DeckId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastReviewedAtUtc { get; set; }
    public DateTime? NextReviewAtUtc { get; set; }
    public int IntervalDays { get; set; }
    public double EaseFactor { get; set; } = 2.5;
    public int RepetitionCount { get; set; }
    public TermStatus Status { get; set; } = TermStatus.New;
    public int LapseCount { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public List<string> Tags { get; set; } = [];
}
