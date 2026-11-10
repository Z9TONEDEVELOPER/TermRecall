using System.Text;
using TermRecall.Application.DTOs;

namespace TermRecall.Application.Services;

public static class StudyReport
{
    public static string Text(DashboardSummary dashboard, StatisticsReport week, ProgressReport progress)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TermRecall");
        builder.AppendLine("Отчёт по локальному журналу. Будущие оценки в него не входят: они ещё неизвестны.");
        builder.AppendLine($"В библиотеке {dashboard.LibraryCount} терминов вне архива.");
        builder.AppendLine($"К повторению {dashboard.DueCount}, просрочено {dashboard.OverdueCount}, новых {dashboard.NewCount}, освоенных {dashboard.MasteredCount}.");
        builder.AppendLine($"Серия {dashboard.Streak} дн. Сегодня повторений {dashboard.ReviewsToday} из {dashboard.DailyReviewLimit}.");
        builder.AppendLine($"За выбранную неделю повторений {week.TotalReviews}, успешных {week.SuccessfulReviews}, неудачных {week.FailedReviews}.");
        builder.AppendLine(progress.WeekGoalMinutes <= 0
            ? $"За текущую неделю записано {progress.WeekMinutes} мин. Цель не задана."
            : $"За текущую неделю {progress.WeekMinutes} из {progress.WeekGoalMinutes} мин.");
        var earned = progress.Achievements.Count(card => card.AwardedAtUtc is not null);
        builder.AppendLine($"Достижений зафиксировано {earned} из {progress.Achievements.Count}.");
        return builder.ToString().TrimEnd();
    }
}
