namespace TermRecall.Infrastructure.Seeding;

public readonly record struct DemoStep(int TermIndex, int Step, int DayOffset, int Quality, int DurationMs);

/// <summary>
/// План ответов для демобазы. Качества фиксированы, дни считаются от дня генерации.
/// Сам SM-2 здесь не считается: план только задаёт, когда и какую оценку передать в алгоритм.
/// </summary>
public static class DemoSchedule
{
    public const int TermCount = 125;

    public static string Role(int index) => index switch
    {
        < 20 => "new",
        < 35 => "learn",
        < 55 => "review",
        < 85 => "master",
        < 100 => "hard",
        < 112 => "overdue",
        < 122 => "archive",
        _ => "new"
    };

    public static IReadOnlyList<DemoStep> Build()
    {
        var steps = new List<DemoStep>();
        for (var term = 0; term < TermCount; term++)
        {
            var role = Role(term);
            var qualities = Qualities(role);
            if (qualities.Length == 0)
            {
                continue;
            }

            var end = LastDay(role, term);
            var start = Math.Max(-89, end - (qualities.Length - 1) * 3);
            var previous = int.MinValue;
            for (var step = 0; step < qualities.Length; step++)
            {
                var day = qualities.Length == 1
                    ? end
                    : start + (end - start) * step / (qualities.Length - 1);
                if (step == qualities.Length - 1)
                {
                    day = end;
                }

                if (day < previous)
                {
                    day = previous;
                }

                previous = day;
                var duration = day == 0 ? 600_000 : day >= -6 ? 45_000 : 4_000 + (term % 9) * 250;
                steps.Add(new DemoStep(term, step, day, qualities[step], duration));
            }
        }

        return steps
            .OrderBy(step => step.DayOffset)
            .ThenBy(step => step.TermIndex)
            .ThenBy(step => step.Step)
            .ToList();
    }

    private static int[] Qualities(string role) => role switch
    {
        "learn" => [4],
        "review" => [4, 5],
        "master" => [5, 5, 5, 5, 4, 5, 5, 4, 5, 5, 5, 5],
        "hard" => [5, 0, 0, 0],
        "overdue" => [4, 5],
        "archive" => [4, 3],
        _ => []
    };

    private static int LastDay(string role, int index) => role switch
    {
        "learn" => -6 + (index % 7),
        "review" => -1,
        "master" => -3,
        "hard" => 0,
        "overdue" => -10,
        "archive" => -8,
        _ => -3
    };
}
