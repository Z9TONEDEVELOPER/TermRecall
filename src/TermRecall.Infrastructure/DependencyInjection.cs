using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TermRecall.Application.Abstractions;
using TermRecall.Application.Services;
using TermRecall.Infrastructure.Database;
using TermRecall.Infrastructure.Exchange;
using TermRecall.Infrastructure.Repositories;
using TermRecall.Infrastructure.Seeding;

namespace TermRecall.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTermRecall(this IServiceCollection services, string databasePath, bool seedStarterCatalog = true)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton(new DatabaseOptions(databasePath, seedStarterCatalog));
        services.AddSingleton<SqliteDatabase>();
        services.AddSingleton<ITransactionRunner>(provider => provider.GetRequiredService<SqliteDatabase>());
        services.AddSingleton<IDatabaseInitializer>(provider => provider.GetRequiredService<SqliteDatabase>());
        services.AddSingleton<IDeckRepository, DeckRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<ITermRepository, TermRepository>();
        services.AddSingleton<IReviewHistoryRepository, ReviewHistoryRepository>();
        services.AddSingleton<IWrittenAnswerRepository, WrittenAnswerRepository>();
        services.AddSingleton<IAssessmentRepository, AssessmentRepository>();
        services.AddSingleton<IFavoriteRepository, FavoriteRepository>();
        services.AddSingleton<IAchievementRepository, AchievementRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<ITermMediaStore, TermMediaStore>();
        services.AddSingleton<ILibraryExchange, LibraryExchange>();
        services.AddSingleton<IBackupService, BackupService>();
        services.TryAddSingleton<IReminderScheduler, NoOpReminderScheduler>();
        services.AddSingleton<IDeckService, DeckService>();
        services.AddSingleton<ITermService, TermService>();
        services.AddSingleton<IReviewService, ReviewService>();
        services.AddSingleton<IWrittenRecallService, WrittenRecallService>();
        services.AddSingleton<IAssessmentService, AssessmentService>();
        services.AddSingleton<IFavoriteService, FavoriteService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IProgressService, ProgressService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICardDraftService, CardDraftService>();
        services.AddSingleton<IDemoModeService, DemoModeService>();
        return services;
    }

    public static string DefaultDatabasePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        return Path.Combine(root, "TermRecall", "termrecall.db");
    }
}
