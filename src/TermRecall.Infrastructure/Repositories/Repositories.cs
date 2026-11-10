using Microsoft.Data.Sqlite;
using TermRecall.Application.Abstractions;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Repositories;

public sealed class DeckRepository(SqliteDatabase database) : IDeckRepository
{
    public Task<IReadOnlyList<Deck>> ListAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            var items = new List<Deck>();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id, name, description, created_at, updated_at FROM decks ORDER BY name COLLATE NOCASE;";
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                items.Add(new Deck
                {
                    Id = Sqlio.ReadGuid(reader, 0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CreatedAtUtc = Sqlio.ReadTime(reader, 3),
                    UpdatedAtUtc = Sqlio.ReadTime(reader, 4)
                });
            }

            return (IReadOnlyList<Deck>)items;
        }, cancellationToken);

    public Task<Deck?> GetAsync(Guid id, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id, name, description, created_at, updated_at FROM decks WHERE id = $id;";
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(id));
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return null;
            }

            return new Deck
            {
                Id = Sqlio.ReadGuid(reader, 0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                CreatedAtUtc = Sqlio.ReadTime(reader, 3),
                UpdatedAtUtc = Sqlio.ReadTime(reader, 4)
            };
        }, cancellationToken);

    public Task AddAsync(Deck deck, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO decks(id, name, description, created_at, updated_at)
                VALUES ($id, $name, $description, $created, $updated);
                """;
            Bind(command, deck);
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task UpdateAsync(Deck deck, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE decks
                SET name = $name, description = $description, created_at = $created, updated_at = $updated
                WHERE id = $id;
                """;
            Bind(command, deck);
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM decks WHERE id = $id;";
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(id));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    private static void Bind(SqliteCommand command, Deck deck)
    {
        command.Parameters.AddWithValue("$id", Sqlio.GuidText(deck.Id));
        command.Parameters.AddWithValue("$name", deck.Name);
        command.Parameters.AddWithValue("$description", deck.Description);
        command.Parameters.AddWithValue("$created", Sqlio.Time(deck.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", Sqlio.Time(deck.UpdatedAtUtc));
    }
}

public sealed class CategoryRepository(SqliteDatabase database) : ICategoryRepository
{
    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            var items = new List<Category>();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id, name, color FROM categories ORDER BY name COLLATE NOCASE;";
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                items.Add(Read(reader));
            }

            return (IReadOnlyList<Category>)items;
        }, cancellationToken);

    public Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id, name, color FROM categories WHERE id = $id;";
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(id));
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? Read(reader) : null;
        }, cancellationToken);

    public Task<Category?> FindByNameAsync(string name, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id, name, color FROM categories WHERE name = $name COLLATE NOCASE;";
            command.Parameters.AddWithValue("$name", name.Trim());
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? Read(reader) : null;
        }, cancellationToken);

    public Task AddAsync(Category category, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO categories(id, name, color) VALUES ($id, $name, $color);";
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(category.Id));
            command.Parameters.AddWithValue("$name", category.Name);
            command.Parameters.AddWithValue("$color", category.Color);
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    private static Category Read(SqliteDataReader reader) => new()
    {
        Id = Sqlio.ReadGuid(reader, 0),
        Name = reader.GetString(1),
        Color = reader.GetString(2)
    };
}

public sealed class TermRepository(SqliteDatabase database) : ITermRepository
{
    private const string Columns = """
        id, title, definition, example, explanation, category_id, deck_id, created_at,
        last_reviewed_at, next_review_at, interval_days, ease_factor, repetition_count, status, lapse_count,
        code, reference_url, archived
        """;

    public Task<IReadOnlyList<Term>> ListAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            var items = new List<Term>();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {Columns} FROM terms ORDER BY title COLLATE NOCASE;";
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                items.Add(Read(reader));
            }

            await AttachTagsAsync(connection, transaction, items, token);
            return (IReadOnlyList<Term>)items;
        }, cancellationToken);

    public Task<Term?> GetAsync(Guid id, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {Columns} FROM terms WHERE id = $id;";
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(id));
            Term? term = null;
            await using (var reader = await command.ExecuteReaderAsync(token))
            {
                if (await reader.ReadAsync(token))
                {
                    term = Read(reader);
                }
            }

            if (term is not null)
            {
                await AttachTagsAsync(connection, transaction, [term], token);
            }

            return term;
        }, cancellationToken);

    public Task AddAsync(Term term, CancellationToken cancellationToken)
        => database.QueryAsync((connection, transaction, token) => WriteAsync(connection, transaction, term, insert: true, token), cancellationToken);

    public Task UpdateAsync(Term term, CancellationToken cancellationToken)
        => database.QueryAsync((connection, transaction, token) => WriteAsync(connection, transaction, term, insert: false, token), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM terms WHERE id = $id;";
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(id));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task ResetSchedulingAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE terms
                SET last_reviewed_at = NULL,
                    next_review_at = NULL,
                    interval_days = 0,
                    ease_factor = $ef,
                    repetition_count = 0,
                    status = $status,
                    lapse_count = 0;
                """;
            command.Parameters.AddWithValue("$ef", Sm2Algorithm.DefaultEaseFactor);
            command.Parameters.AddWithValue("$status", (int)TermStatus.New);
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    private static async Task<int> WriteAsync(SqliteConnection connection, SqliteTransaction? transaction, Term term, bool insert, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = insert
            ? $"""
               INSERT INTO terms({Columns})
               VALUES ($id, $title, $definition, $example, $explanation, $category, $deck, $created,
                       $last, $next, $interval, $ease, $reps, $status, $lapses, $code, $url, $archived);
               """
            : """
              UPDATE terms SET
                  title = $title,
                  definition = $definition,
                  example = $example,
                  explanation = $explanation,
                  category_id = $category,
                  deck_id = $deck,
                  created_at = $created,
                  last_reviewed_at = $last,
                  next_review_at = $next,
                  interval_days = $interval,
                  ease_factor = $ease,
                  repetition_count = $reps,
                  status = $status,
                  lapse_count = $lapses,
                  code = $code,
                  reference_url = $url,
                  archived = $archived
              WHERE id = $id;
              """;
        command.Parameters.AddWithValue("$id", Sqlio.GuidText(term.Id));
        command.Parameters.AddWithValue("$title", term.Title);
        command.Parameters.AddWithValue("$definition", term.Definition);
        command.Parameters.AddWithValue("$example", term.Example);
        command.Parameters.AddWithValue("$explanation", term.Explanation);
        command.Parameters.AddWithValue("$category", Sqlio.Db(term.CategoryId));
        command.Parameters.AddWithValue("$deck", Sqlio.GuidText(term.DeckId));
        command.Parameters.AddWithValue("$created", Sqlio.Time(term.CreatedAtUtc));
        command.Parameters.AddWithValue("$last", Sqlio.Db(term.LastReviewedAtUtc));
        command.Parameters.AddWithValue("$next", Sqlio.Db(term.NextReviewAtUtc));
        command.Parameters.AddWithValue("$interval", term.IntervalDays);
        command.Parameters.AddWithValue("$ease", term.EaseFactor);
        command.Parameters.AddWithValue("$reps", term.RepetitionCount);
        command.Parameters.AddWithValue("$status", (int)term.Status);
        command.Parameters.AddWithValue("$lapses", term.LapseCount);
        command.Parameters.AddWithValue("$code", term.Code);
        command.Parameters.AddWithValue("$url", term.ReferenceUrl);
        command.Parameters.AddWithValue("$archived", term.Archived ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await ReplaceTagsAsync(connection, transaction, term, cancellationToken);
        return 0;
    }

    private static async Task AttachTagsAsync(SqliteConnection connection, SqliteTransaction? transaction, IReadOnlyList<Term> terms, CancellationToken cancellationToken)
    {
        if (terms.Count == 0)
        {
            return;
        }

        var byId = terms.ToDictionary(term => term.Id);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT term_id, tag FROM term_tags ORDER BY tag COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Sqlio.ReadGuid(reader, 0);
            if (byId.TryGetValue(id, out var term))
            {
                term.Tags.Add(reader.GetString(1));
            }
        }
    }

    private static async Task ReplaceTagsAsync(SqliteConnection connection, SqliteTransaction? transaction, Term term, CancellationToken cancellationToken)
    {
        await using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM term_tags WHERE term_id = $id;";
        delete.Parameters.AddWithValue("$id", Sqlio.GuidText(term.Id));
        await delete.ExecuteNonQueryAsync(cancellationToken);
        foreach (var tag in term.Tags)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO term_tags(term_id, tag) VALUES ($id, $tag);";
            insert.Parameters.AddWithValue("$id", Sqlio.GuidText(term.Id));
            insert.Parameters.AddWithValue("$tag", tag);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static Term Read(SqliteDataReader reader) => new()
    {
        Id = Sqlio.ReadGuid(reader, 0),
        Title = reader.GetString(1),
        Definition = reader.GetString(2),
        Example = reader.GetString(3),
        Explanation = reader.GetString(4),
        CategoryId = Sqlio.ReadGuidOrNull(reader, 5),
        DeckId = Sqlio.ReadGuid(reader, 6),
        CreatedAtUtc = Sqlio.ReadTime(reader, 7),
        LastReviewedAtUtc = Sqlio.ReadTimeOrNull(reader, 8),
        NextReviewAtUtc = Sqlio.ReadTimeOrNull(reader, 9),
        IntervalDays = reader.GetInt32(10),
        EaseFactor = reader.GetDouble(11),
        RepetitionCount = reader.GetInt32(12),
        Status = (TermStatus)reader.GetInt32(13),
        LapseCount = reader.GetInt32(14),
        Code = reader.GetString(15),
        ReferenceUrl = reader.GetString(16),
        Archived = reader.GetInt32(17) == 1
    };
}

public sealed class ReviewHistoryRepository(SqliteDatabase database) : IReviewHistoryRepository
{
    private const string Columns = """
        id, term_id, reviewed_at, quality, previous_interval, new_interval,
        previous_ease_factor, new_ease_factor, response_duration_ms,
        repetition_count_before, repetition_count_after, calculation_note
        """;

    public Task AddAsync(ReviewRecord record, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO review_history({Columns})
                VALUES ($id, $term, $at, $quality, $prevI, $newI, $prevEf, $newEf, $duration, $before, $after, $note);
                """;
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(record.Id));
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(record.TermId));
            command.Parameters.AddWithValue("$at", Sqlio.Time(record.ReviewedAtUtc));
            command.Parameters.AddWithValue("$quality", record.Quality);
            command.Parameters.AddWithValue("$prevI", record.PreviousInterval);
            command.Parameters.AddWithValue("$newI", record.NewInterval);
            command.Parameters.AddWithValue("$prevEf", record.PreviousEaseFactor);
            command.Parameters.AddWithValue("$newEf", record.NewEaseFactor);
            command.Parameters.AddWithValue("$duration", Sqlio.Db(record.ResponseDurationMs));
            command.Parameters.AddWithValue("$before", record.RepetitionCountBefore);
            command.Parameters.AddWithValue("$after", record.RepetitionCountAfter);
            command.Parameters.AddWithValue("$note", record.CalculationNote);
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    public Task<IReadOnlyList<ReviewRecord>> ListAsync(CancellationToken cancellationToken)
        => ListCoreAsync(null, cancellationToken);

    public Task<IReadOnlyList<ReviewRecord>> ListByTermAsync(Guid termId, CancellationToken cancellationToken)
        => ListCoreAsync(termId, cancellationToken);

    public Task DeleteAllAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM review_history;";
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);

    private Task<IReadOnlyList<ReviewRecord>> ListCoreAsync(Guid? termId, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            var items = new List<ReviewRecord>();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = termId is null
                ? $"SELECT {Columns} FROM review_history ORDER BY reviewed_at;"
                : $"SELECT {Columns} FROM review_history WHERE term_id = $term ORDER BY reviewed_at;";
            if (termId is Guid id)
            {
                command.Parameters.AddWithValue("$term", Sqlio.GuidText(id));
            }

            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                items.Add(new ReviewRecord
                {
                    Id = Sqlio.ReadGuid(reader, 0),
                    TermId = Sqlio.ReadGuid(reader, 1),
                    ReviewedAtUtc = Sqlio.ReadTime(reader, 2),
                    Quality = reader.GetInt32(3),
                    PreviousInterval = reader.GetInt32(4),
                    NewInterval = reader.GetInt32(5),
                    PreviousEaseFactor = reader.GetDouble(6),
                    NewEaseFactor = reader.GetDouble(7),
                    ResponseDurationMs = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    RepetitionCountBefore = reader.GetInt32(9),
                    RepetitionCountAfter = reader.GetInt32(10),
                    CalculationNote = reader.GetString(11)
                });
            }

            return (IReadOnlyList<ReviewRecord>)items;
        }, cancellationToken);
}

public sealed class SettingsRepository(SqliteDatabase database) : ISettingsRepository
{
    public Task<UserSettings> GetAsync(CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT daily_new_terms_limit, daily_review_limit, reminder_enabled, reminder_time, theme,
                       include_new_terms, mastery_target_days, weight_success, weight_regularity, weight_mastery,
                       display_name, difficult_ease_threshold, weekly_minutes_goal
                FROM user_settings WHERE id = 1;
                """;
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return new UserSettings();
            }

            return new UserSettings
            {
                DailyNewTermsLimit = reader.GetInt32(0),
                DailyReviewLimit = reader.GetInt32(1),
                ReminderEnabled = reader.GetInt32(2) == 1,
                ReminderTime = TimeSpan.Parse(reader.GetString(3)),
                Theme = (ThemePreference)reader.GetInt32(4),
                IncludeNewTerms = reader.GetInt32(5) == 1,
                MasteryTargetDays = reader.GetInt32(6),
                WeightSuccess = reader.GetDouble(7),
                WeightRegularity = reader.GetDouble(8),
                WeightMastery = reader.GetDouble(9),
                DisplayName = reader.GetString(10),
                DifficultEaseThreshold = reader.GetDouble(11),
                WeeklyMinutesGoal = reader.GetInt32(12)
            };
        }, cancellationToken);

    public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO user_settings(
                    id, daily_new_terms_limit, daily_review_limit, reminder_enabled, reminder_time, theme,
                    include_new_terms, mastery_target_days, weight_success, weight_regularity, weight_mastery,
                    display_name, difficult_ease_threshold, weekly_minutes_goal)
                VALUES (1, $new, $review, $enabled, $time, $theme, $include, $target, $ws, $wr, $wm, $name, $threshold, $minutes)
                ON CONFLICT(id) DO UPDATE SET
                    daily_new_terms_limit = excluded.daily_new_terms_limit,
                    daily_review_limit = excluded.daily_review_limit,
                    reminder_enabled = excluded.reminder_enabled,
                    reminder_time = excluded.reminder_time,
                    theme = excluded.theme,
                    include_new_terms = excluded.include_new_terms,
                    mastery_target_days = excluded.mastery_target_days,
                    weight_success = excluded.weight_success,
                    weight_regularity = excluded.weight_regularity,
                    weight_mastery = excluded.weight_mastery,
                    display_name = excluded.display_name,
                    difficult_ease_threshold = excluded.difficult_ease_threshold,
                    weekly_minutes_goal = excluded.weekly_minutes_goal;
                """;
            command.Parameters.AddWithValue("$new", settings.DailyNewTermsLimit);
            command.Parameters.AddWithValue("$review", settings.DailyReviewLimit);
            command.Parameters.AddWithValue("$enabled", settings.ReminderEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$time", settings.ReminderTime.ToString(@"hh\:mm"));
            command.Parameters.AddWithValue("$theme", (int)settings.Theme);
            command.Parameters.AddWithValue("$include", settings.IncludeNewTerms ? 1 : 0);
            command.Parameters.AddWithValue("$target", settings.MasteryTargetDays);
            command.Parameters.AddWithValue("$ws", settings.WeightSuccess);
            command.Parameters.AddWithValue("$wr", settings.WeightRegularity);
            command.Parameters.AddWithValue("$wm", settings.WeightMastery);
            command.Parameters.AddWithValue("$name", settings.DisplayName);
            command.Parameters.AddWithValue("$threshold", settings.DifficultEaseThreshold);
            command.Parameters.AddWithValue("$minutes", settings.WeeklyMinutesGoal);
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);
}
