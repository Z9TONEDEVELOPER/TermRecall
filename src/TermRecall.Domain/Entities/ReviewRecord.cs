namespace TermRecall.Domain.Entities;

public sealed class ReviewRecord
{
    public Guid Id { get; set; }
    public Guid TermId { get; set; }
    public DateTime ReviewedAtUtc { get; set; }
    public int Quality { get; set; }
    public int PreviousInterval { get; set; }
    public int NewInterval { get; set; }
    public double PreviousEaseFactor { get; set; }
    public double NewEaseFactor { get; set; }
    public int? ResponseDurationMs { get; set; }
    public int RepetitionCountBefore { get; set; }
    public int RepetitionCountAfter { get; set; }
    public string CalculationNote { get; set; } = string.Empty;
}
