namespace TermRecall.Domain.Algorithms;

public readonly record struct CalendarCell(DateOnly Date, int Reviews, bool InFocusMonth, bool IsToday);

public readonly record struct AchievementDefinition(string Code, string Title, string Requirement);

public readonly record struct AchievementFacts(int Reviews, int LongestStreak, int MasteredTerms, int WeekMinutes);

public static class StudyCalendar
{
    public static DateOnly WeekStart(DateOnly day)
    {
        var mondayOffset = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-mondayOffset);
    }

    public static IReadOnlyList<CalendarCell> Month(DateOnly today, IReadOnlyDictionary<DateOnly, int> reviewsByDay)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        var start = WeekStart(first);
        var last = first.AddMonths(1).AddDays(-1);
        var end = WeekStart(last).AddDays(6);
        var cells = new List<CalendarCell>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            reviewsByDay.TryGetValue(date, out var count);
            cells.Add(new CalendarCell(date, count, date.Year == today.Year && date.Month == today.Month, date == today));
        }

        return cells;
    }
}

public static class StudyTime
{
    public static int MinutesFromMilliseconds(IEnumerable<int?> durationsMs)
    {
        long total = 0;
        foreach (var duration in durationsMs)
        {
            if (duration is int milliseconds && milliseconds > 0)
            {
                total += milliseconds;
            }
        }

        return (int)Math.Min(int.MaxValue, total / 60_000);
    }
}

public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; } =
    [
        new("first-review", "Первое повторение", "В журнале повторений есть хотя бы одна запись."),
        new("reviews-50", "Пятьдесят повторений", "В журнале накопилось 50 повторений."),
        new("streak-7", "Неделя подряд", "Серия занятий хотя бы раз дошла до 7 дней."),
        new("mastered-5", "Пять освоенных", "Сейчас не меньше пяти терминов со статусом «Освоен»."),
        new("hour-week", "Час за неделю", "За текущую неделю в журнале не меньше 60 минут.")
    ];

    public static IReadOnlyList<string> EarnedCodes(AchievementFacts facts)
    {
        var codes = new List<string>();
        if (facts.Reviews >= 1)
        {
            codes.Add("first-review");
        }

        if (facts.Reviews >= 50)
        {
            codes.Add("reviews-50");
        }

        if (facts.LongestStreak >= 7)
        {
            codes.Add("streak-7");
        }

        if (facts.MasteredTerms >= 5)
        {
            codes.Add("mastered-5");
        }

        if (facts.WeekMinutes >= 60)
        {
            codes.Add("hour-week");
        }

        return codes;
    }
}
