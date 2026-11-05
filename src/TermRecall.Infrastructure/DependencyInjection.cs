using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TermRecall.Application.Abstractions;
using TermRecall.Application.Services;
using TermRecall.Infrastructure.Database;
using TermRecall.Infrastructure.Exchange;
using TermRecall.Infrastructure.Repositories;

namespace TermRecall.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTermRecall(this IServiceCollection services, string databasePath)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton(new DatabaseOptions(databasePath));
        services.AddSingleton<SqliteDatabase>();
        services.AddSingleton<ITransactionRunner>(provider => provider.GetRequiredService<SqliteDatabase>());
        services.AddSingleton<IDatabaseInitializer>(provider => provider.GetRequiredService<SqliteDatabase>());
        services.AddSingleton<IDeckRepository, DeckRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<ITermRepository, TermRepository>();
        services.AddSingleton<IReviewHistoryRepository, ReviewHistoryRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<ILibraryExchange, LibraryExchange>();
        services.AddSingleton<IBackupService, BackupService>();
        services.TryAddSingleton<IReminderScheduler, NoOpReminderScheduler>();
        services.AddSingleton<IDeckService, DeckService>();
        services.AddSingleton<ITermService, TermService>();
        services.AddSingleton<IReviewService, ReviewService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<ISettingsService, SettingsService>();
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
