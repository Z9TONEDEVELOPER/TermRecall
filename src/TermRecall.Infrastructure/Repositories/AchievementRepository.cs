using TermRecall.Application.Abstractions;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Repositories;

public sealed class AchievementRepository(SqliteDatabase database) : IAchievementRepository
{
    public Task<IReadOnlyDictionary<string, DateTime>> ListAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT code, awarded_at FROM achievements;";
            var grants = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                grants[reader.GetString(0)] = Sqlio.ReadTime(reader, 1);
            }

            return (IReadOnlyDictionary<string, DateTime>)grants;
        }, cancellationToken);

    public Task AddAsync(string code, DateTime awardedAtUtc, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO achievements(code, awarded_at)
                VALUES ($code, $at)
                ON CONFLICT(code) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$code", code);
            command.Parameters.AddWithValue("$at", Sqlio.Time(awardedAtUtc));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task DeleteAllAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM achievements;";
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);
}
