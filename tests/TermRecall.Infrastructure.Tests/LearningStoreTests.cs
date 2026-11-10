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

        var quarter = await app.Get<IStatisticsService>().GetAsync(StatsRange.Quarter, CancellationToken.None);
        var today = ReviewCalendar.LocalDate(app.Clock.UtcNow, app.Clock.TimeZone);
        Assert.Equal(today.AddDays(-89), quarter.From);
        Assert.True(quarter.Forecast.Days7 >= 1);
        Assert.True(quarter.Forecast.Days30 >= quarter.Forecast.Days14);
        Assert.True(quarter.Forecast.Days14 >= quarter.Forecast.Days7);
        Assert.Contains(quarter.Categories, category => category.Terms > 0);
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

    [Fact]
    public async Task Progress_AwardsOnceFromTheJournalAndResetClearsIt()
    {
        await using var app = new TestApp();
        var term = (await app.Get<ITermService>().ListAsync(CancellationToken.None)).First(item => item.Title == "CLR");
        var before = await app.Get<ITermRepository>().GetAsync(term.Id, CancellationToken.None);
        var empty = await app.Get<IProgressService>().GetAsync(CancellationToken.None);
        Assert.All(empty.Achievements, card => Assert.Null(card.AwardedAtUtc));
        Assert.Equal(60, empty.WeekGoalMinutes);
        Assert.Equal(0, empty.WeekMinutes);
        Assert.Equal(before!.IntervalDays, (await app.Get<ITermRepository>().GetAsync(term.Id, CancellationToken.None))!.IntervalDays);

        await app.Get<IReviewService>().SubmitAsync(term.Id, 5, 120_000, CancellationToken.None);
        var first = await app.Get<IProgressService>().GetAsync(CancellationToken.None);
        var second = await app.Get<IProgressService>().GetAsync(CancellationToken.None);
        var earned = first.Achievements.Single(card => card.Code == "first-review");
        Assert.NotNull(earned.AwardedAtUtc);
        Assert.Equal(earned.AwardedAtUtc, second.Achievements.Single(card => card.Code == "first-review").AwardedAtUtc);
        Assert.Equal(2, first.WeekMinutes);
        Assert.Contains(first.Calendar, cell => cell.IsToday && cell.Reviews == 1);
        Assert.Null(first.Achievements.Single(card => card.Code == "hour-week").AwardedAtUtc);

        await app.Get<ISettingsService>().ResetStatisticsAsync(CancellationToken.None);
        var cleared = await app.Get<IProgressService>().GetAsync(CancellationToken.None);
        Assert.All(cleared.Achievements, card => Assert.Null(card.AwardedAtUtc));
        Assert.Empty(await app.Get<IReviewHistoryRepository>().ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NotesArchiveAndMove_DoNotTouchTheSchedule()
    {
        await using var app = new TestApp();
        var decks = await app.Get<IDeckService>().ListAsync(CancellationToken.None);
        var created = await app.Get<ITermService>().SaveAsync(new TermDraft(
            null,
            "Инвариант",
            "Условие, которое сохраняется.",
            string.Empty,
            string.Empty,
            decks[0].Id,
            null,
            null,
            "x > 0",
            "https://learn.microsoft.com/dotnet",
            "логика, код"), CancellationToken.None);

        var stored = await app.Get<ITermRepository>().GetAsync(created.Id, CancellationToken.None);
        Assert.Equal("x > 0", stored!.Code);
        Assert.Equal(["код", "логика"], stored.Tags.OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase).ToArray());
        Assert.Equal(0, stored.IntervalDays);

        await app.Get<ITermService>().SetArchivedAsync([created.Id], true, CancellationToken.None);
        await app.Get<ITermService>().MoveAsync([created.Id], decks[1].Id, CancellationToken.None);
        var moved = await app.Get<ITermRepository>().GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(decks[1].Id, moved!.DeckId);
        Assert.True(moved.Archived);
        Assert.Equal(0, moved.IntervalDays);
        Assert.Equal(2, moved.Tags.Count);

        var active = await app.Get<IDashboardService>().GetAsync(CancellationToken.None);
        var week = await app.Get<IStatisticsService>().GetAsync(StatsRange.Week, CancellationToken.None);
        var snapshot = await app.Get<IProgressService>().GetAsync(CancellationToken.None);
        var report = StudyReport.Text(active, week, snapshot);
        Assert.Contains("ещё неизвестны", report);
        Assert.DoesNotContain("Инвариант", report);
    }

    [Fact]
    public async Task MediaAndGeneratedCards_StayOutOfTheSchedule()
    {
        await using var app = new TestApp();
        var deck = (await app.Get<IDeckService>().ListAsync(CancellationToken.None))[0];
        var created = await app.Get<ICardDraftService>().CreateAsync(deck.Id, "JIT - Компиляция во время выполнения\nпросто строка\n", CancellationToken.None);
        Assert.Equal(1, created.Added);
        Assert.Equal(0, created.Skipped);
        var term = (await app.Get<ITermService>().ListAsync(CancellationToken.None)).Single(item => item.Title == "JIT");
        Assert.Equal(0, term.IntervalDays);

        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        await app.Get<ITermMediaStore>().AddAsync(term.Id, "dot.png", png, CancellationToken.None);
        var stored = await app.Get<ITermMediaStore>().ListAsync(term.Id, CancellationToken.None);
        Assert.Equal(MediaKind.Image, stored[0].Kind);
        Assert.Equal(png, await app.Get<ITermMediaStore>().ReadAsync(stored[0].Id, CancellationToken.None));
        Assert.Equal(0, (await app.Get<ITermRepository>().GetAsync(term.Id, CancellationToken.None))!.IntervalDays);

        var json = await app.Get<ILibraryExchange>().ExportJsonAsync(CancellationToken.None);
        Assert.Contains("dot.png", json);
        await app.Get<ITermService>().DeleteAsync(term.Id, CancellationToken.None);
        Assert.False(File.Exists(stored[0].AbsolutePath));
        var imported = await app.Get<ILibraryExchange>().ImportJsonAsync(json, CancellationToken.None);
        Assert.Equal(1, imported.Added);
        var again = (await app.Get<ITermService>().ListAsync(CancellationToken.None)).Single(item => item.Title == "JIT");
        var media = await app.Get<ITermMediaStore>().ListAsync(again.Id, CancellationToken.None);
        Assert.Equal(png, await app.Get<ITermMediaStore>().ReadAsync(media[0].Id, CancellationToken.None));
        Assert.Equal(0, again.IntervalDays);
    }

    [Fact]
    public async Task WrittenAnswer_StaysOutOfSm2AndSurvivesRestart()
    {
        var app = new TestApp();
        var term = (await app.Get<ITermService>().ListAsync(CancellationToken.None)).First(item => item.Title == "CLR");
        await app.Get<IWrittenRecallService>().SaveAsync(term.Id, "Среда выполнения .NET", 4, 900, CancellationToken.None);

        var stored = await app.Get<ITermRepository>().GetAsync(term.Id, CancellationToken.None);
        var sm2 = await app.Get<IReviewHistoryRepository>().ListByTermAsync(term.Id, CancellationToken.None);
        var written = await app.Get<IWrittenRecallService>().ListAsync(term.Id, CancellationToken.None);
        Assert.Equal(0, stored!.IntervalDays);
        Assert.Equal(TermStatus.New, stored.Status);
        Assert.Empty(sm2);
        Assert.Single(written);
        Assert.Equal(4, written[0].Quality);
        Assert.Equal("Среда выполнения .NET", written[0].ResponseText);

        var path = app.DatabasePath;
        await app.Services.DisposeAsync();
        var again = new ServiceCollection();
        again.AddTermRecall(path);
        await using var restarted = again.BuildServiceProvider();
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(CancellationToken.None);
        var kept = await restarted.GetRequiredService<IWrittenRecallService>().ListAsync(term.Id, CancellationToken.None);
        Assert.Single(kept);
        await restarted.GetRequiredService<ISettingsService>().ResetStatisticsAsync(CancellationToken.None);
        Assert.Empty(await restarted.GetRequiredService<IWrittenRecallService>().ListAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task AssessmentAndFavorite_StayOutOfSm2()
    {
        await using var app = new TestApp();
        var terms = await app.Get<ITermService>().ListAsync(CancellationToken.None);
        var clr = terms.First(item => item.Title == "CLR");
        var questions = await app.Get<IAssessmentService>().BuildAsync(AssessmentMode.Test, CancellationToken.None);
        Assert.NotEmpty(questions);
        var question = questions[0];
        var wrong = question.Options.First(option => option.TermId != question.TermId);
        var correct = await app.Get<IAssessmentService>().RecordAsync(AssessmentMode.Test, question.TermId, wrong.Text, 400, CancellationToken.None);
        Assert.False(correct);

        var marked = await app.Get<IFavoriteService>().ToggleAsync(clr.Id, CancellationToken.None);
        Assert.True(marked);
        Assert.Contains(clr.Id, await app.Get<IFavoriteService>().ListIdsAsync(CancellationToken.None));

        var stored = await app.Get<ITermRepository>().GetAsync(clr.Id, CancellationToken.None);
        Assert.Equal(0, stored!.IntervalDays);
        Assert.Empty(await app.Get<IReviewHistoryRepository>().ListByTermAsync(clr.Id, CancellationToken.None));

        await app.Get<ISettingsService>().ResetStatisticsAsync(CancellationToken.None);
        Assert.Contains(clr.Id, await app.Get<IFavoriteService>().ListIdsAsync(CancellationToken.None));
        var again = await app.Get<IAssessmentService>().RecordAsync(AssessmentMode.Exam, clr.Id, clr.Definition, null, CancellationToken.None);
        Assert.True(again);
        await app.Get<IFavoriteService>().ToggleAsync(clr.Id, CancellationToken.None);
        Assert.DoesNotContain(clr.Id, await app.Get<IFavoriteService>().ListIdsAsync(CancellationToken.None));
    }
}
