using TermRecall.Application.Abstractions;
using TermRecall.Application.Exceptions;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Repositories;

public sealed class TermMediaStore(SqliteDatabase database, DatabaseOptions options) : ITermMediaStore
{
    public const int MaxBytes = 1_500_000;

    public async Task<TermMedia> AddAsync(Guid termId, string fileName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var kind = KindOf(fileName);
        if (content.Length == 0)
        {
            throw new ValidationException("Файл пустой.");
        }

        if (content.Length > MaxBytes)
        {
            throw new ValidationException("Файл больше 1,5 МБ. Такой размер не переносится в JSON между устройствами.");
        }

        var media = new TermMedia
        {
            Id = Guid.NewGuid(),
            TermId = termId,
            Kind = kind,
            FileName = Path.GetFileName(fileName),
            MediaType = kind == MediaKind.Image ? "image" : "audio",
            CreatedAtUtc = DateTime.UtcNow
        };
        media.AbsolutePath = PathFor(media);
        Directory.CreateDirectory(Folder());
        await File.WriteAllBytesAsync(media.AbsolutePath, content.ToArray(), cancellationToken);
        try
        {
            await database.QueryAsync(async (connection, transaction, token) =>
            {
                await using var remove = connection.CreateCommand();
                remove.Transaction = transaction;
                remove.CommandText = "DELETE FROM term_media WHERE term_id = $term AND kind = $kind;";
                remove.Parameters.AddWithValue("$term", Sqlio.GuidText(termId));
                remove.Parameters.AddWithValue("$kind", media.MediaType);
                await remove.ExecuteNonQueryAsync(token);

                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO term_media(id, term_id, kind, file_name, media_type, created_at)
                    VALUES ($id, $term, $kind, $name, $type, $at);
                    """;
                insert.Parameters.AddWithValue("$id", Sqlio.GuidText(media.Id));
                insert.Parameters.AddWithValue("$term", Sqlio.GuidText(termId));
                insert.Parameters.AddWithValue("$kind", media.MediaType);
                insert.Parameters.AddWithValue("$name", media.FileName);
                insert.Parameters.AddWithValue("$type", media.MediaType);
                insert.Parameters.AddWithValue("$at", Sqlio.Time(media.CreatedAtUtc));
                await insert.ExecuteNonQueryAsync(token);
                return 0;
            }, cancellationToken);
        }
        catch
        {
            TryDelete(media.AbsolutePath);
            throw;
        }

        await DeleteReplacedFilesAsync(termId, media, cancellationToken);
        return media;
    }

    public Task<IReadOnlyList<TermMedia>> ListAsync(Guid termId, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT id, term_id, kind, file_name, created_at
                FROM term_media WHERE term_id = $term ORDER BY created_at;
                """;
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(termId));
            var items = new List<TermMedia>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var media = Read(reader);
                media.AbsolutePath = PathFor(media);
                items.Add(media);
            }

            return (IReadOnlyList<TermMedia>)items;
        }, cancellationToken);

    public async Task<byte[]?> ReadAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var media = await FindAsync(mediaId, cancellationToken);
        if (media is null || !File.Exists(media.AbsolutePath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(media.AbsolutePath, cancellationToken);
    }

    public async Task DeleteForTermAsync(Guid termId, CancellationToken cancellationToken)
    {
        var items = await ListAsync(termId, cancellationToken);
        await database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM term_media WHERE term_id = $term;";
            command.Parameters.AddWithValue("$term", Sqlio.GuidText(termId));
            await command.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);
        foreach (var item in items)
        {
            TryDelete(item.AbsolutePath);
        }
    }

    private async Task DeleteReplacedFilesAsync(Guid termId, TermMedia kept, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.Exists(Folder()) ? Directory.GetFiles(Folder()) : [])
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!Guid.TryParse(name, out var id) || id == kept.Id)
            {
                continue;
            }

            var row = await FindAsync(id, cancellationToken);
            if (row is null)
            {
                TryDelete(file);
            }
        }
    }

    private Task<TermMedia?> FindAsync(Guid id, CancellationToken cancellationToken)
        => database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id, term_id, kind, file_name, created_at FROM term_media WHERE id = $id;";
            command.Parameters.AddWithValue("$id", Sqlio.GuidText(id));
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return null;
            }

            var media = Read(reader);
            media.AbsolutePath = PathFor(media);
            return media;
        }, cancellationToken);

    private string Folder()
    {
        var directory = Path.GetDirectoryName(options.Path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.GetTempPath();
        }

        var folder = Path.GetFileNameWithoutExtension(options.Path);
        if (!string.Equals(folder, "termrecall", StringComparison.OrdinalIgnoreCase))
        {
            folder += "-media";
        }
        else
        {
            folder = "media";
        }

        return Path.Combine(directory, folder);
    }

    private string PathFor(TermMedia media)
    {
        var extension = Path.GetExtension(media.FileName);
        return Path.Combine(Folder(), media.Id.ToString("N") + extension.ToLowerInvariant());
    }

    private static TermMedia Read(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        var kind = reader.GetString(2) == "audio" ? MediaKind.Audio : MediaKind.Image;
        return new TermMedia
        {
            Id = Sqlio.ReadGuid(reader, 0),
            TermId = Sqlio.ReadGuid(reader, 1),
            Kind = kind,
            FileName = reader.GetString(3),
            MediaType = kind == MediaKind.Audio ? "audio" : "image",
            CreatedAtUtc = Sqlio.ReadTime(reader, 4)
        };
    }

    private static MediaKind KindOf(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is ".png" or ".jpg" or ".jpeg" or ".webp")
        {
            return MediaKind.Image;
        }

        if (extension is ".mp3" or ".m4a" or ".wav" or ".aac")
        {
            return MediaKind.Audio;
        }

        throw new ValidationException("Нужна картинка png, jpg, webp или звук mp3, m4a, wav, aac.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
