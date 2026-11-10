namespace TermRecall.Application.Services;

public sealed record DemoSnapshot(bool Active, bool Exists, int Terms, int Reviews, string Summary);

public interface IDemoModeService
{
    bool IsDemo { get; }
    Task<DemoSnapshot> InfoAsync(CancellationToken cancellationToken);
    Task CreateAsync(CancellationToken cancellationToken);
    Task RecreateAsync(CancellationToken cancellationToken);
    Task DeleteAsync(CancellationToken cancellationToken);
}
