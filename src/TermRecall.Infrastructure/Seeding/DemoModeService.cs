using Microsoft.Data.Sqlite;
using TermRecall.Application.Services;

namespace TermRecall.Infrastructure.Seeding;

public sealed class DemoModeService : IDemoModeService
{
    public bool IsDemo => DataProfiles.IsDemoActive();

    public async Task<DemoSnapshot> InfoAsync(CancellationToken cancellationToken)
    {
        var path = DataProfiles.DemoDatabasePath;
        if (!File.Exists(path))
        {
            return new DemoSnapshot(IsDemo, false, 0, 0, "Демонстрационный файл ещё не создан.");
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);
            var terms = await ScalarAsync(connection, "SELECT COUNT(*) FROM terms;", cancellationToken);
            var reviews = await ScalarAsync(connection, "SELECT COUNT(*) FROM review_history;", cancellationToken);
            return new DemoSnapshot(IsDemo, true, terms, reviews, $"Терминов {terms}, повторений {reviews}. Файл отдельный от пользовательской базы.");
        }
        catch (SqliteException exception)
        {
            return new DemoSnapshot(IsDemo, true, 0, 0, "Файл демобазы есть, но прочитать его не удалось: " + exception.Message);
        }
    }

    public Task CreateAsync(CancellationToken cancellationToken)
        => DemoDatabaseBuilder.EnsureAsync(DataProfiles.DemoDatabasePath, DateTime.UtcNow, cancellationToken);

    public Task RecreateAsync(CancellationToken cancellationToken)
        => DemoDatabaseBuilder.CreateAsync(DataProfiles.DemoDatabasePath, DateTime.UtcNow, cancellationToken);

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        DemoDatabaseBuilder.DeleteFiles(DataProfiles.DemoDatabasePath);
        return Task.CompletedTask;
    }

    private static async Task<int> ScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
}
