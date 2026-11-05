using System.Globalization;
using System.Text;
using System.Text.Json;
using TermRecall.Application.Abstractions;
using TermRecall.Application.DTOs;
using TermRecall.Application.Exceptions;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Exchange;

public sealed class LibraryExchange(
    IDeckRepository decks,
    ICategoryRepository categories,
    ITermRepository terms,
    ITransactionRunner transactions,
    IClock clock) : ILibraryExchange
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<string> ExportCsvAsync(CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Title,Definition,Example,Explanation,Category,Deck");
        foreach (var term in snapshot.Terms)
        {
            var deck = snapshot.Decks.First(item => item.Id == term.DeckId).Name;
            var category = snapshot.Categories.FirstOrDefault(item => item.Id == term.CategoryId)?.Name ?? string.Empty;
            builder.Append(Csv.Field(term.Title)).Append(',')
                .Append(Csv.Field(term.Definition)).Append(',')
                .Append(Csv.Field(term.Example)).Append(',')
                .Append(Csv.Field(term.Explanation)).Append(',')
                .Append(Csv.Field(category)).Append(',')
                .Append(Csv.Field(deck)).AppendLine();
        }

        return builder.ToString();
    }

    public async Task<string> ExportJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        var document = new LibraryDocument
        {
            ExportedAtUtc = DateTime.UtcNow,
            Decks = snapshot.Decks.Select(deck => new DeckExport(deck.Name, deck.Description)).ToList(),
            Categories = snapshot.Categories.Select(category => new CategoryExport(category.Name, category.Color)).ToList(),
            Terms = snapshot.Terms.Select(term => new TermExport(
                term.Title,
                term.Definition,
                term.Example,
                term.Explanation,
                snapshot.Categories.FirstOrDefault(item => item.Id == term.CategoryId)?.Name,
                snapshot.Decks.First(item => item.Id == term.DeckId).Name,
                term.EaseFactor,
                term.IntervalDays,
                term.RepetitionCount,
                term.Status.ToString(),
                term.LapseCount)).ToList()
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public Task<ImportResult> ImportCsvAsync(string csv, CancellationToken cancellationToken)
    {
        var rows = Csv.Parse(csv);
        if (rows.Count == 0)
        {
            throw new ValidationException("Файл не содержит строк.");
        }

        var header = rows[0].Select(cell => cell.Trim()).ToArray();
        var map = header.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "Title", "Definition", "Deck" })
        {
            if (!map.ContainsKey(required))
            {
                throw new ValidationException($"В CSV нет обязательного столбца {required}.");
            }
        }

        var imports = new List<TermExport>();
        var errors = new List<string>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Cell(string name) => map.TryGetValue(name, out var index) && index < row.Count ? row[index].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(Cell("Title")) || string.IsNullOrWhiteSpace(Cell("Definition")) || string.IsNullOrWhiteSpace(Cell("Deck")))
            {
                errors.Add($"Строка {i + 1}: нужны название, определение и набор.");
                continue;
            }

            imports.Add(new TermExport(Cell("Title"), Cell("Definition"), Cell("Example"), Cell("Explanation"), NullIfEmpty(Cell("Category")), Cell("Deck"), null, null, null, null, null));
        }

        return ImportAsync(imports, cancellationToken, errors);
    }

    public Task<ImportResult> ImportJsonAsync(string json, CancellationToken cancellationToken)
    {
        LibraryDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<LibraryDocument>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ValidationException("JSON библиотеки не удалось прочитать: " + exception.Message);
        }

        if (document?.Terms is null)
        {
            throw new ValidationException("В JSON нет списка терминов.");
        }

        return ImportAsync(document.Terms, cancellationToken, []);
    }

    private async Task<ImportResult> ImportAsync(IReadOnlyList<TermExport> imports, CancellationToken cancellationToken, List<string> errors)
    {
        return await transactions.InTransactionAsync(async () =>
        {
            var deckList = (await decks.ListAsync(cancellationToken)).ToList();
            var categoryList = (await categories.ListAsync(cancellationToken)).ToList();
            var existing = await terms.ListAsync(cancellationToken);
            var added = 0;
            var createdDecks = 0;
            var skipped = 0;
            foreach (var item in imports)
            {
                if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Definition) || string.IsNullOrWhiteSpace(item.Deck))
                {
                    errors.Add($"Термин «{item.Title}» пропущен: нет названия, определения или набора.");
                    skipped++;
                    continue;
                }

                var deck = deckList.FirstOrDefault(candidate => string.Equals(candidate.Name, item.Deck, StringComparison.CurrentCultureIgnoreCase));
                if (deck is null)
                {
                    deck = new Deck
                    {
                        Id = Guid.NewGuid(),
                        Name = item.Deck.Trim(),
                        Description = string.Empty,
                        CreatedAtUtc = clock.UtcNow,
                        UpdatedAtUtc = clock.UtcNow
                    };
                    await decks.AddAsync(deck, cancellationToken);
                    deckList.Add(deck);
                    createdDecks++;
                }

                Guid? categoryId = null;
                if (!string.IsNullOrWhiteSpace(item.Category))
                {
                    var category = categoryList.FirstOrDefault(candidate => string.Equals(candidate.Name, item.Category, StringComparison.CurrentCultureIgnoreCase));
                    if (category is null)
                    {
                        category = new Category { Id = Guid.NewGuid(), Name = item.Category.Trim(), Color = "#6558E8" };
                        await categories.AddAsync(category, cancellationToken);
                        categoryList.Add(category);
                    }

                    categoryId = category.Id;
                }

                if (existing.Any(term => term.DeckId == deck.Id && string.Equals(term.Title, item.Title, StringComparison.CurrentCultureIgnoreCase)))
                {
                    skipped++;
                    continue;
                }

                var term = new Term
                {
                    Id = Guid.NewGuid(),
                    Title = item.Title.Trim(),
                    Definition = item.Definition.Trim(),
                    Example = item.Example?.Trim() ?? string.Empty,
                    Explanation = item.Explanation?.Trim() ?? string.Empty,
                    DeckId = deck.Id,
                    CategoryId = categoryId,
                    CreatedAtUtc = clock.UtcNow,
                    EaseFactor = item.EaseFactor is null ? Sm2Algorithm.DefaultEaseFactor : Math.Max(Sm2Algorithm.MinimumEaseFactor, item.EaseFactor.Value),
                    IntervalDays = Math.Max(0, item.IntervalDays ?? 0),
                    RepetitionCount = Math.Max(0, item.RepetitionCount ?? 0),
                    LapseCount = Math.Max(0, item.LapseCount ?? 0),
                    Status = Enum.TryParse<TermStatus>(item.Status, out var status) ? status : TermStatus.New
                };
                if (term.RepetitionCount == 0 && term.IntervalDays == 0)
                {
                    term.Status = TermStatus.New;
                }
                else if (term.IntervalDays > 0)
                {
                    term.LastReviewedAtUtc = clock.UtcNow;
                    term.NextReviewAtUtc = ReviewCalendar.NextReviewUtc(clock.UtcNow, term.IntervalDays, clock.TimeZone);
                }

                await terms.AddAsync(term, cancellationToken);
                existing = existing.Append(term).ToList();
                added++;
            }

            return new ImportResult(added, createdDecks, skipped, errors);
        }, cancellationToken);
    }

    private async Task<(IReadOnlyList<Deck> Decks, IReadOnlyList<Category> Categories, IReadOnlyList<Term> Terms)> SnapshotAsync(CancellationToken cancellationToken)
        => (await decks.ListAsync(cancellationToken), await categories.ListAsync(cancellationToken), await terms.ListAsync(cancellationToken));

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

internal static class Csv
{
    public static string Field(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }

    public static List<List<string>> Parse(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var current = csv[i];
            if (quoted)
            {
                if (current == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    cell.Append(current);
                }

                continue;
            }

            switch (current)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\n':
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                case '\r':
                    break;
                default:
                    cell.Append(current);
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }
}

public sealed class LibraryDocument
{
    public string Format { get; set; } = "termrecall.library";
    public int Version { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; }
    public List<DeckExport> Decks { get; set; } = [];
    public List<CategoryExport> Categories { get; set; } = [];
    public List<TermExport> Terms { get; set; } = [];
}

public sealed record DeckExport(string Name, string Description);
public sealed record CategoryExport(string Name, string Color);
public sealed record TermExport(
    string Title,
    string Definition,
    string Example,
    string Explanation,
    string? Category,
    string Deck,
    double? EaseFactor,
    int? IntervalDays,
    int? RepetitionCount,
    string? Status,
    int? LapseCount);

public sealed class BackupService(SqliteDatabase database) : IBackupService
{
    public Task<byte[]> CreateBackupAsync(CancellationToken cancellationToken)
        => database.CreateBackupBytesAsync(cancellationToken);

    public Task RestoreAsync(Stream backup, CancellationToken cancellationToken)
        => database.RestoreAsync(backup, cancellationToken);
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Local;
}

public sealed class NoOpReminderScheduler : IReminderScheduler
{
    public Task ApplyAsync(UserSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
}
