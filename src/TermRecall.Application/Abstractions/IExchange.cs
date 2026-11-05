using TermRecall.Application.DTOs;

namespace TermRecall.Application.Abstractions;

public interface ILibraryExchange
{
    Task<string> ExportCsvAsync(CancellationToken cancellationToken);
    Task<string> ExportJsonAsync(CancellationToken cancellationToken);
    Task<ImportResult> ImportCsvAsync(string csv, CancellationToken cancellationToken);
    Task<ImportResult> ImportJsonAsync(string json, CancellationToken cancellationToken);
}

public interface IBackupService
{
    Task<byte[]> CreateBackupAsync(CancellationToken cancellationToken);
    Task RestoreAsync(Stream backup, CancellationToken cancellationToken);
}
