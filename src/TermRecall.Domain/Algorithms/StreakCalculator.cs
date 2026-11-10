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

    /// <summary>
    /// Самая длинная непрерывная цепочка дней с хотя бы одним повторением.
    /// Дырка в один день обрывает серию. Это факт журнала, не прогноз.
    /// </summary>
    public static int LongestStreak(IEnumerable<DateOnly> activeLocalDates)
    {
        var days = activeLocalDates.Distinct().OrderBy(day => day).ToList();
        var best = 0;
        var run = 0;
        DateOnly? previous = null;
        foreach (var day in days)
        {
            run = previous is DateOnly prior && prior.AddDays(1) == day ? run + 1 : 1;
            previous = day;
            if (run > best)
            {
                best = run;
            }
        }

        return best;
    }
}
