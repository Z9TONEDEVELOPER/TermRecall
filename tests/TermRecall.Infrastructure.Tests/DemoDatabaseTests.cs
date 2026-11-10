using Microsoft.Extensions.DependencyInjection;
using TermRecall.Application.Abstractions;
using TermRecall.Domain.Algorithms;
using TermRecall.Infrastructure;
using TermRecall.Infrastructure.Seeding;

namespace TermRecall.Infrastructure.Tests;

public class DemoDatabaseTests
{
    [Fact]
    public void Schedule_StaysInsideNinetyDaysAndRepeats()
    {
        var first = DemoSchedule.Build();
        var second = DemoSchedule.Build();
        Assert.Equal(first, second);
        Assert.InRange(first.Count, 500, 800);
        Assert.All(first, step => Assert.InRange(step.DayOffset, -89, 0));
        Assert.Equal(DemoSchedule.TermCount, DemoCatalog.Terms.Count);
        Assert.Equal(DemoCatalog.Terms.Count, DemoCatalog.Terms.Select(term => term.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task DemoDatabase_FollowsSm2AndLeavesTheUserFileAlone()
    {
        var root = Path.Combine(Path.GetTempPath(), "termrecall-demo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var demo = Path.Combine(root, "termrecall-demo.db");
        var user = Path.Combine(root, "termrecall.db");
        await File.WriteAllTextAsync(user, "user-marker");
        var now = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        await DemoDatabaseBuilder.CreateAsync(demo, now, CancellationToken.None);
        Assert.Equal("user-marker", await File.ReadAllTextAsync(user));

        await using var provider = Open(demo);
        var terms = await provider.GetRequiredService<ITermRepository>().ListAsync(CancellationToken.None);
        var history = await provider.GetRequiredService<IReviewHistoryRepository>().ListAsync(CancellationToken.None);
        Assert.InRange(terms.Count, 120, 150);
        Assert.InRange(history.Count, 500, 800);
        Assert.Contains(terms, term => term.Status == Domain.Enums.TermStatus.New && !term.Archived);
        Assert.True(terms.Count(term => term.Status == Domain.Enums.TermStatus.Mastered) >= 5);
        Assert.Contains(terms, term => term.Status == Domain.Enums.TermStatus.Difficult);
        Assert.Contains(terms, term => term.Archived);
        Assert.NotEmpty(await provider.GetRequiredService<IFavoriteRepository>().ListIdsAsync(CancellationToken.None));
        var grants = await provider.GetRequiredService<IAchievementRepository>().ListAsync(CancellationToken.None);
        Assert.Contains("first-review", grants.Keys);
        Assert.Contains("reviews-50", grants.Keys);
        Assert.Contains("streak-7", grants.Keys);
        Assert.Contains("mastered-5", grants.Keys);
        Assert.Contains("hour-week", grants.Keys);

        foreach (var term in terms.Where(term => term.LastReviewedAtUtc is not null))
        {
            var last = history.Where(record => record.TermId == term.Id).OrderBy(record => record.ReviewedAtUtc).Last();
            Assert.Equal(last.NewEaseFactor, term.EaseFactor, 10);
            Assert.Equal(last.NewInterval, term.IntervalDays);
            Assert.Equal(last.RepetitionCountAfter, term.RepetitionCount);
            Assert.Contains("EF'", last.CalculationNote);
        }

        var sample = terms.Single(term => term.Title == "JIT");
        await provider.DisposeAsync();

        var again = Path.Combine(root, "again.db");
        await DemoDatabaseBuilder.CreateAsync(again, now, CancellationToken.None);
        await using var second = Open(again);
        var jit = (await second.GetRequiredService<ITermRepository>().ListAsync(CancellationToken.None)).Single(term => term.Title == "JIT");
        Assert.Equal(sample.EaseFactor, jit.EaseFactor, 10);
        Assert.Equal(sample.IntervalDays, jit.IntervalDays);
        Assert.Equal(history.Count, (await second.GetRequiredService<IReviewHistoryRepository>().ListAsync(CancellationToken.None)).Count);
    }

    private static ServiceProvider Open(string path)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new AdjustableClock { UtcNow = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc) });
        services.AddTermRecall(path, seedStarterCatalog: false);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return provider;
    }
}
