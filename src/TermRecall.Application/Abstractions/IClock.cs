namespace TermRecall.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
    TimeZoneInfo TimeZone { get; }
}
