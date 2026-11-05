using Microsoft.Extensions.DependencyInjection;
using TermRecall.Application.Abstractions;
using TermRecall.Application.DTOs;
using TermRecall.Application.Services;
using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;
using TermRecall.Infrastructure;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Tests;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
}

public sealed class TestApp : IAsyncDisposable
{
    public TestApp()
    {
        var directory = Path.Combine(Path.GetTempPath(), "termrecall-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        DatabasePath = Path.Combine(directory, "termrecall.db");
        Clock = new FakeClock();
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(Clock);
        services.AddTermRecall(DatabasePath);
        Services = services.BuildServiceProvider();
        Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public string DatabasePath { get; }
    public FakeClock Clock { get; }
    public ServiceProvider Services { get; }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public async ValueTask DisposeAsync() => await Services.DisposeAsync();
}

public class LearningStoreTests
{
    [Fact]
    public async Task Seed_CreatesCsharpTerminology()
    {
        await using var app = new TestApp();
        var terms = await app.Get<ITermService>().ListAsync(CancellationToken.None);
        var decks = await app.Get<IDeckService>().ListAsync(CancellationToken.None);
        Assert.True(terms.Count >= 20);
        Assert.Contains(decks, deck => deck.Name == "Основы C#");
        Assert.Contains(terms, term => term.Title == "CLR");
        Assert.All(terms, term => Assert.Equal(TermStatus.New, term.Status));
    }

    [Fact]
    public async Task Crud_AndHistorySurvivesTermEditAndRestart()
    {
        var app = new TestApp();
        var decks = await app.Get<IDeckService>().ListAsync(CancellationToken.None);
        var created = await app.Get<ITermService>().SaveAsync(new TermDraft(null, "Интерфейс", "Контракт типа.", "interface IClock", "Можно реализовать несколько.", decks[0].Id, null, null), CancellationToken.None);
        created = await app.Get<ITermService>().SaveAsync(new TermDraft(created.Id, "Интерфейс", "Контракт без состояния.", "interface IClock {}", "Уточнено.", decks[0].Id, null, "Контракты"), CancellationToken.None);

        var outcome = await app.Get<IReviewService>().SubmitAsync(created.Id, 4, 1500, CancellationToken.None);
        Assert.Equal(1, outcome.NewInterval);
        Assert.Equal(2.5, outcome.NewEaseFactor, 10);

        var edited = await app.Get<ITermService>().SaveAsync(new TermDraft(created.Id, "Интерфейс C#", created.Definition, created.Example, created.Explanation, created.DeckId, created.CategoryId, null), CancellationToken.None);
        Assert.Equal(1, edited.IntervalDays);
        Assert.Equal(2.5, edited.EaseFactor, 10);

        var path = app.DatabasePath;
        await app.Services.DisposeAsync();

        var again = new ServiceCollection();
        again.AddTermRecall(path);
        await using var restarted = again.BuildServiceProvider();
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(CancellationToken.None);
        var loaded = await restarted.GetRequiredService<ITermRepository>().GetAsync(created.Id, CancellationToken.None);
        var history = await restarted.GetRequiredService<IReviewHistoryRepository>().ListByTermAsync(created.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("Интерфейс C#", loaded!.Title);
        Assert.Equal(1, loaded.IntervalDays);
        Assert.Single(history);
        Assert.Contains("EF'", history[0].CalculationNote);
    }

    [Fact]
    public async Task CourseworkReviews_ArePersistedWithDates()
    {
        await using var app = new TestApp();
        var term = (await app.Get<ITermService>().ListAsync(CancellationToken.None)).First(item => item.Title == "CLR");
        var review = app.Get<IReviewService>();

        var first = await review.SubmitAsync(term.Id, 4, 800, CancellationToken.None);
        Assert.Equal(new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc), first.NextReviewUtc);

        app.Clock.UtcNow = first.NextReviewUtc.AddHours(8);
        var second = await review.SubmitAsync(term.Id, 5, 600, CancellationToken.None);
        Assert.Equal(6, second.NewInterval);
        Assert.Equal(2.6, second.NewEaseFactor, 10);
        Assert.Equal(new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc), second.NextReviewUtc);

        app.Clock.UtcNow = second.NextReviewUtc.AddHours(8);
        var third = await review.SubmitAsync(term.Id, 4, 700, CancellationToken.None);
        Assert.Equal(16, third.NewInterval);
        Assert.Equal(2.6, third.NewEaseFactor, 10);

        var queueBefore = await review.BuildQueueAsync(StudyMode.DueMix, null, false, CancellationToken.None);
        Assert.DoesNotContain(queueBefore, item => item.Id == term.Id);

        app.Clock.UtcNow = third.NextReviewUtc.AddDays(3);
        var queueAfterGap = await review.BuildQueueAsync(StudyMode.DueMix, null, false, CancellationToken.None);
        Assert.Contains(queueAfterGap, item => item.Id == term.Id && item.IntervalDays == 16 && item.Status == TermStatus.Review);
    }

    [Fact]
    public async Task Statistics_UseStoredReviews()
    {
        await using var app = new TestApp();
        var term = (await app.Get<ITermService>().ListAsync(CancellationToken.None)).First();
        await app.Get<IReviewService>().SubmitAsync(term.Id, 5, null, CancellationToken.None);
        await app.Get<IReviewService>().SubmitAsync(term.Id, 2, null, CancellationToken.None);

        var report = await app.Get<IStatisticsService>().GetAsync(StatsRange.Week, CancellationToken.None);
        Assert.Equal(2, report.TotalReviews);
        Assert.Equal(1, report.SuccessfulReviews);
        Assert.Equal(1, report.FailedReviews);
        Assert.Equal(3.5, report.AverageQuality, 10);
        Assert.Equal(50, report.SuccessPercent, 10);

        var dashboard = await app.Get<IDashboardService>().GetAsync(CancellationToken.None);
        Assert.Equal(7, dashboard.LastSevenDays.Count);
        Assert.True(dashboard.Streak >= 1);
    }

    [Fact]
    public async Task DeleteDeck_RemovesTermsButBackupRestoresThem()
    {
        await using var app = new TestApp();
        var before = await app.Get<ITermService>().ListAsync(CancellationToken.None);
        var bytes = await app.Get<IBackupService>().CreateBackupAsync(CancellationToken.None);
        var deck = (await app.Get<IDeckService>().ListAsync(CancellationToken.None)).First();
        await app.Get<IDeckService>().DeleteAsync(deck.Id, CancellationToken.None);
        var afterDelete = await app.Get<ITermService>().ListAsync(CancellationToken.None);
        Assert.DoesNotContain(afterDelete, term => term.DeckId == deck.Id);

        await using var stream = new MemoryStream(bytes);
        await app.Get<IBackupService>().RestoreAsync(stream, CancellationToken.None);
        var restored = await app.Get<ITermService>().ListAsync(CancellationToken.None);
        Assert.Equal(before.Count, restored.Count);
    }

    [Fact]
    public async Task CsvRoundTrip_SkipsDuplicatesAndImportsNewDeck()
    {
        await using var app = new TestApp();
        var exchange = app.Get<ILibraryExchange>();
        var csv = await exchange.ExportCsvAsync(CancellationToken.None);
        var again = await exchange.ImportCsvAsync(csv, CancellationToken.None);
        Assert.Equal(0, again.Added);
        Assert.True(again.Skipped > 0);

        var imported = await exchange.ImportCsvAsync("""
            Title,Definition,Example,Explanation,Category,Deck
            "JIT","Компиляция IL в машинный код во время выполнения.","", "", "Платформа","Среда выполнения"
            """, CancellationToken.None);
        Assert.Equal(1, imported.Added);
        Assert.Equal(1, imported.UpdatedDecks);
        var terms = await app.Get<ITermService>().ListAsync(CancellationToken.None);
        Assert.Contains(terms, term => term.Title == "JIT");
    }

    [Fact]
    public async Task ResetStatistics_ClearsHistoryAndKeepsTerms()
    {
        await using var app = new TestApp();
        var term = (await app.Get<ITermService>().ListAsync(CancellationToken.None)).First();
        await app.Get<IReviewService>().SubmitAsync(term.Id, 4, null, CancellationToken.None);
        await app.Get<ISettingsService>().ResetStatisticsAsync(CancellationToken.None);
        var reloaded = await app.Get<ITermRepository>().GetAsync(term.Id, CancellationToken.None);
        var history = await app.Get<IReviewHistoryRepository>().ListAsync(CancellationToken.None);
        Assert.Empty(history);
        Assert.Equal(TermStatus.New, reloaded!.Status);
        Assert.Equal(0, reloaded.IntervalDays);
        Assert.Equal(Sm2Algorithm.DefaultEaseFactor, reloaded.EaseFactor, 10);
        Assert.Equal("CLR", (await app.Get<ITermService>().ListAsync(CancellationToken.None)).Single(item => item.Title == "CLR").Title);
    }
}
