using Microsoft.Data.Sqlite;
using TermRecall.Application.Abstractions;
using TermRecall.Domain.Entities;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Repositories;

public sealed class WrittenAnswerRepository(SqliteDatabase database) : IWrittenAnswerRepository
{
    public Task AddAsync(WrittenAnswer answer, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO written_answers(id, term_id, answered_at, response_text, quality, duration_ms)
                VALUES ($id, $term, $at, $text, $quality, $duration);
                """;
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(answer.Id));
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(answer.TermId));
            command.Parameters.AddWithValue("$at", Sqlio.Time(answer.AnsweredAtUtc));
            command.Parameters.AddWithValue("$text", answer.ResponseText);
            command.Parameters.AddWithValue("$quality", answer.Quality);
            command.Parameters.AddWithValue("$duration", Sqlio.Db(answer.DurationMs));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task<IReadOnlyList<WrittenAnswer>> ListAsync(Guid? termId, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = termId is null
                ? "SELECT id, term_id, answered_at, response_text, quality, duration_ms FROM written_answers ORDER BY answered_at;"
                : "SELECT id, term_id, answered_at, response_text, quality, duration_ms FROM written_answers WHERE term_id = $term ORDER BY answered_at;";
            if (termId is Guid id)
            {
                command.Parameters.AddWithValue("$term", Sqlio.GuidText(id));
            }

            var rows = new List<WrittenAnswer>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                rows.Add(Read(reader));
            }

            return (IReadOnlyList<WrittenAnswer>)rows;
        }, cancellationToken);

    public Task DeleteAllAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM written_answers;";
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    private static WrittenAnswer Read(SqliteDataReader reader) => new()
    {
        Id = Sqlio.ReadGuid(reader, 0),
        TermId = Sqlio.ReadGuid(reader, 1),
        AnsweredAtUtc = Sqlio.ReadTime(reader, 2),
        ResponseText = reader.GetString(3),
        Quality = reader.GetInt32(4),
        DurationMs = reader.IsDBNull(5) ? null : reader.GetInt32(5)
    };
}
