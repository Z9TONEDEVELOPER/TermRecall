using TermRecall.Application.Abstractions;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Repositories;

public sealed class AssessmentRepository(SqliteDatabase database) : IAssessmentRepository
{
    public Task AddAsync(AssessmentAnswer answer, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO assessment_answers(id, mode, term_id, answered_at, correct, selected_text, duration_ms)
                VALUES ($id, $mode, $term, $at, $correct, $text, $duration);
                """;
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(answer.Id));
            command.Parameters.AddWithValue("$mode", answer.Mode == AssessmentMode.Exam ? "exam" : "test");
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(answer.TermId));
            command.Parameters.AddWithValue("$at", Sqlio.Time(answer.AnsweredAtUtc));
            command.Parameters.AddWithValue("$correct", answer.Correct ? 1 : 0);
            command.Parameters.AddWithValue("$text", answer.SelectedText);
            command.Parameters.AddWithValue("$duration", Sqlio.Db(answer.DurationMs));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task DeleteAllAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM assessment_answers;";
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);
}

public sealed class FavoriteRepository(SqliteDatabase database) : IFavoriteRepository
{
    public Task<IReadOnlySet<Guid>> ListIdsAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT term_id FROM term_favorites;";
            var ids = new HashSet<Guid>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                ids.Add(Sqlio.ReadGuid(reader, 0));
            }

            return (IReadOnlySet<Guid>)ids;
        }, cancellationToken);

    public Task<bool> ContainsAsync(Guid termId, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT 1 FROM term_favorites WHERE term_id = $term;";
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(termId));
            var value = await command.ExecuteScalarAsync(token);
            return value is not null && value is not DBNull;
        }, cancellationToken);

    public Task AddAsync(Guid termId, DateTime createdAtUtc, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO term_favorites(term_id, created_at)
                VALUES ($term, $at)
                ON CONFLICT(term_id) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(termId));
            command.Parameters.AddWithValue("$at", Sqlio.Time(createdAtUtc));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task RemoveAsync(Guid termId, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM term_favorites WHERE term_id = $term;";
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(termId));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);
}
