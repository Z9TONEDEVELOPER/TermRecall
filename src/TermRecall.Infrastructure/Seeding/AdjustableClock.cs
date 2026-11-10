using TermRecall.Application.Abstractions;

namespace TermRecall.Infrastructure.Seeding;

public sealed class AdjustableClock : IClock
{
    public DateTime UtcNow { get; set; }
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
}
