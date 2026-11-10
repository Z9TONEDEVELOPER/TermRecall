using TermRecall.Domain.Enums;

namespace TermRecall.Domain.Entities;

public sealed class TermMedia
{
    public Guid Id { get; set; }
    public Guid TermId { get; set; }
    public MediaKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string AbsolutePath { get; set; } = string.Empty;
}
