using Microsoft.Extensions.DependencyInjection;
using TermRecall.Application.Abstractions;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Seeding;

public static class DemoDatabaseBuilder
{
    public static async Task EnsureAsync(string path, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (await CountTermsAsync(path, cancellationToken) >= 120)
        {
            return;
        }

        await CreateAsync(path, utcNow, cancellationToken);
    }

    public static async Task CreateAsync(string path, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (DemoCatalog.Terms.Count != DemoSchedule.TermCount)
        {
            throw new InvalidOperationException($"В каталоге {DemoCatalog.Terms.Count} терминов, план рассчитан на {DemoSchedule.TermCount}.");
        }

        DeleteFiles(path);
        var clock = new AdjustableClock { UtcNow = Specify(utcNow), TimeZone = TimeZoneInfo.Utc };
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);
        services.AddTermRecall(path, seedStarterCatalog: false);
        await using var provider = services.BuildServiceProvider();
        var database = provider.GetRequiredService<SqliteDatabase>();
        try
        {
            await database.InitializeAsync(cancellationToken);
            await database.InTransactionAsync(
                () => FillAsync(provider, clock, cancellationToken),
                cancellationToken);
        }
        catch
        {
            await provider.DisposeAsync();
            DeleteFiles(path);
            throw;
        }
    }

    public static void DeleteFiles(string path)
    {
        TryDelete(path);
        TryDelete(path + "-wal");
        TryDelete(path + "-shm");
        var media = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), Path.GetFileNameWithoutExtension(path) + "-media");
        try
        {
            if (Directory.Exists(media))
            {
                Directory.Delete(media, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static async Task<int> FillAsync(IServiceProvider provider, AdjustableClock clock, CancellationToken cancellationToken)
    {
        var decks = provider.GetRequiredService<IDeckRepository>();
        var categories = provider.GetRequiredService<ICategoryRepository>();
        var terms = provider.GetRequiredService<ITermRepository>();
        var settings = provider.GetRequiredService<ISettingsRepository>();
        var favorites = provider.GetRequiredService<IFavoriteRepository>();
        var achievements = provider.GetRequiredService<IAchievementRepository>();
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var deckIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        for (var i = 0; i < DemoCatalog.Decks.Count; i++)
        {
            var deck = DemoCatalog.Decks[i];
            var id = Id(1_000 + i);
            deckIds[deck.Name] = id;
            await decks.AddAsync(new Deck
            {
                Id = id,
                Name = deck.Name,
                Description = deck.Description,
                CreatedAtUtc = clock.UtcNow,
                UpdatedAtUtc = clock.UtcNow
            }, cancellationToken);
        }

        var categoryIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        for (var i = 0; i < DemoCatalog.Categories.Count; i++)
        {
            var category = DemoCatalog.Categories[i];
            var id = Id(2_000 + i);
            categoryIds[category.Name] = id;
            await categories.AddAsync(new Category { Id = id, Name = category.Name, Color = category.Color }, cancellationToken);
        }

        var catalog = DemoCatalog.Terms;
        for (var i = 0; i < catalog.Count; i++)
        {
            var source = catalog[i];
            await terms.AddAsync(new Term
            {
                Id = Id(i + 1),
                Title = source.Title,
                Definition = source.Definition,
                Example = source.Example,
                Explanation = source.Explanation,
                DeckId = deckIds[source.Deck],
                CategoryId = categoryIds[source.Category],
                CreatedAtUtc = At(today, -89),
                EaseFactor = Sm2Algorithm.DefaultEaseFactor,
                IntervalDays = 0,
                RepetitionCount = 0,
                Status = Domain.Enums.TermStatus.New
            }, cancellationToken);
        }

        await settings.SaveAsync(new UserSettings
        {
            DisplayName = "Максим",
            DailyReviewLimit = 40,
            DailyNewTermsLimit = 10,
            MasteryTargetDays = 21,
            ReminderEnabled = true,
            ReminderTime = new TimeSpan(19, 0, 0),
            IncludeNewTerms = true,
            WeightSuccess = 0.5,
            WeightRegularity = 0.2,
            WeightMastery = 0.3,
            DifficultEaseThreshold = 1.6,
            WeeklyMinutesGoal = 60
        }, cancellationToken);

        var history = provider.GetRequiredService<IReviewHistoryRepository>();
        DateTime? firstReview = null;
        DateTime? fiftieth = null;
        DateTime? streakAward = null;
        DateTime? masteredAward = null;
        DateTime? hourAward = null;
        var activeDays = new HashSet<DateOnly>();
        var mastered = new HashSet<Guid>();
        var reviewNumber = 0;
        var weekStart = StartOfWeek(today);
        foreach (var step in DemoSchedule.Build())
        {
            reviewNumber++;
            clock.UtcNow = At(today, step.DayOffset).AddSeconds(reviewNumber);
            var term = await terms.GetAsync(Id(step.TermIndex + 1), cancellationToken)
                ?? throw new InvalidOperationException("Демотермин не найден.");
            var previous = new Sm2State(term.EaseFactor, term.IntervalDays, term.RepetitionCount);
            var result = Sm2Algorithm.Review(previous, step.Quality);
            term.EaseFactor = result.EaseFactor;
            term.IntervalDays = result.IntervalDays;
            term.RepetitionCount = result.RepetitionCount;
            term.LastReviewedAtUtc = clock.UtcNow;
            term.NextReviewAtUtc = ReviewCalendar.NextReviewUtc(clock.UtcNow, result.IntervalDays, clock.TimeZone);
            if (!result.Successful)
            {
                term.LapseCount++;
            }

            term.Status = TermStatusEvaluator.Evaluate(term, step.Quality, 21);
            await terms.UpdateAsync(term, cancellationToken);
            await history.AddAsync(new ReviewRecord
            {
                Id = Id(100_000 + reviewNumber),
                TermId = term.Id,
                ReviewedAtUtc = clock.UtcNow,
                Quality = step.Quality,
                PreviousInterval = previous.IntervalDays,
                NewInterval = result.IntervalDays,
                PreviousEaseFactor = previous.EaseFactor,
                NewEaseFactor = result.EaseFactor,
                ResponseDurationMs = step.DurationMs,
                RepetitionCountBefore = previous.RepetitionCount,
                RepetitionCountAfter = result.RepetitionCount,
                CalculationNote = result.Explanation
            }, cancellationToken);

            firstReview ??= clock.UtcNow;
            if (reviewNumber == 50)
            {
                fiftieth = clock.UtcNow;
            }

            var localDay = ReviewCalendar.LocalDate(clock.UtcNow, clock.TimeZone);
            activeDays.Add(localDay);
            if (streakAward is null && StreakCalculator.CurrentStreak(activeDays, localDay) >= 7)
            {
                streakAward = clock.UtcNow;
            }

            if (term.Status == Domain.Enums.TermStatus.Mastered)
            {
                mastered.Add(term.Id);
                if (mastered.Count >= 5 && masteredAward is null)
                {
                    masteredAward = clock.UtcNow;
                }
            }

            if (localDay >= weekStart)
            {
                hourAward = clock.UtcNow;
            }
        }

        foreach (var index in FavoriteIndexes())
        {
            await favorites.AddAsync(Id(index + 1), At(today, -2), cancellationToken);
        }

        for (var i = 0; i < catalog.Count; i++)
        {
            if (DemoSchedule.Role(i) != "archive")
            {
                continue;
            }

            var term = await terms.GetAsync(Id(i + 1), cancellationToken)
                ?? throw new InvalidOperationException("Архивный демотермин не найден.");
            term.Archived = true;
            await terms.UpdateAsync(term, cancellationToken);
        }

        if (firstReview is DateTime first)
        {
            await achievements.AddAsync("first-review", first, cancellationToken);
        }

        if (fiftieth is DateTime atFifty)
        {
            await achievements.AddAsync("reviews-50", atFifty, cancellationToken);
        }

        if (streakAward is DateTime streak)
        {
            await achievements.AddAsync("streak-7", streak, cancellationToken);
        }

        if (masteredAward is DateTime masteredAt)
        {
            await achievements.AddAsync("mastered-5", masteredAt, cancellationToken);
        }

        if (hourAward is DateTime hour)
        {
            var records = await history.ListAsync(cancellationToken);
            var minutes = StudyTime.MinutesFromMilliseconds(records
                .Where(record => ReviewCalendar.LocalDate(record.ReviewedAtUtc, clock.TimeZone) >= weekStart)
                .Select(record => record.ResponseDurationMs));
            if (minutes >= 60)
            {
                await achievements.AddAsync("hour-week", hour, cancellationToken);
            }
        }

        return reviewNumber;
    }

    private static IEnumerable<int> FavoriteIndexes() => [1, 21, 40, 60, 90, 110];

    private static DateOnly StartOfWeek(DateOnly today)
    {
        var offset = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-offset);
    }

    private static DateTime At(DateOnly today, int dayOffset)
        => today.AddDays(dayOffset).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);

    private static DateTime Specify(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);

    public static Guid Id(int n) => Guid.Parse($"d1000000-0000-4000-8000-{n:x12}");

    private static async Task<int> CountTermsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Mode=ReadOnly");
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM terms;";
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return 0;
        }
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
