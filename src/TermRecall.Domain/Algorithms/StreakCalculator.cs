namespace TermRecall.Domain.Algorithms;

public static class StreakCalculator
{
    /// <summary>
    /// Серия заканчивается сегодня, если сегодня уже было повторение,
    /// иначе вчера. День без повторений разрывает серию.
    /// </summary>
    public static int CurrentStreak(IEnumerable<DateOnly> activeLocalDates, DateOnly today)
    {
        var days = activeLocalDates as ISet<DateOnly> ?? activeLocalDates.ToHashSet();
        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        if (!days.Contains(cursor))
        {
            return 0;
        }

        var streak = 0;
        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }
}
