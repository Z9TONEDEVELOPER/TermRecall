namespace TermRecall.Domain.Algorithms;

/// <summary>
/// Веса агрегированного показателя. Сумма должна быть равна 1, чтобы шкала оставалась 0–100.
/// Это настраиваемый индекс продукта, а не измеренная вероятность запоминания.
/// </summary>
public readonly record struct ScheduledLoad(int Days7, int Days14, int Days30);

public readonly record struct CategoryStudy(string Name, int Terms, int Reviews, int Successful);

public readonly record struct AnalyticsWeights(double Success, double Regularity, double Mastery)
{
    public static AnalyticsWeights Default { get; } = new(0.5, 0.2, 0.3);

    public void EnsureValid()
    {
        if (Success < 0 || Regularity < 0 || Mastery < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Success), "Веса не могут быть отрицательными.");
        }

        var sum = Success + Regularity + Mastery;
        if (Math.Abs(sum - 1d) > 0.000_001d)
        {
            throw new ArgumentException($"Сумма весов должна быть равна 1, сейчас {sum:0.####}.");
        }
    }
}

/// <summary>
/// Аналитические показатели TermRecall.
/// P = N_success / N_total × 100.
/// Q_avg = Σq / N.
/// M = min(100, I / I_target × 100) — прогресс по интервалу, не вероятность припоминания.
/// E = w_s·P + w_r·R + w_m·C.
/// </summary>
public static class LearningAnalytics
{
    public static int CountSuccessful(IEnumerable<int> qualities)
        => qualities.Count(static q => q >= Sm2Algorithm.MinimumPassingQuality);

    public static double SuccessPercent(int successful, int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return successful / (double)total * 100d;
    }

    public static double AverageQuality(IReadOnlyCollection<int> qualities)
    {
        if (qualities.Count == 0)
        {
            return 0;
        }

        return qualities.Sum() / (double)qualities.Count;
    }

    /// <summary>
    /// Нормированный прогресс интервала. Не является вероятностью воспроизведения.
    /// </summary>
    public static double IntervalMasteryPercent(int intervalDays, int targetDays)
    {
        if (targetDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetDays));
        }

        if (intervalDays <= 0)
        {
            return 0;
        }

        return Math.Min(100d, intervalDays / (double)targetDays * 100d);
    }

    /// <summary>
    /// Доля дней окна, в которые было хотя бы одно повторение. Не больше 100.
    /// </summary>
    public static double RegularityPercent(int activeDays, int windowDays)
    {
        if (windowDays <= 0)
        {
            return 0;
        }

        return Math.Min(100d, activeDays / (double)windowDays * 100d);
    }

    /// <summary>
    /// Доля дневной цели повторений, не больше 100.
    /// Это выполнение настройки DailyReviewLimit, а не показатель памяти.
    /// </summary>
    public static double DailyGoalPercent(int completedReviews, int dailyGoal)
    {
        if (dailyGoal <= 0)
        {
            return 0;
        }

        return Math.Min(100d, Math.Max(0, completedReviews) / (double)dailyGoal * 100d);
    }

    public static double MasteredSharePercent(int mastered, int totalTerms)
    {
        if (totalTerms <= 0)
        {
            return 0;
        }

        return mastered / (double)totalTerms * 100d;
    }

    /// <summary>
    /// Сколько терминов уже имеют дату следующего повторения внутри окна.
    /// Просроченная дата входит в каждое окно: карточка всё ещё ждёт повторения.
    /// Дата дальше горизонта и термин без даты не считаются.
    /// Повторный визит после этой даты не строится: оценка, которая его сдвинет, ещё неизвестна.
    /// </summary>
    public static ScheduledLoad ForecastScheduledReviews(IEnumerable<DateOnly?> nextLocalDates, DateOnly today)
    {
        var days7 = 0;
        var days14 = 0;
        var days30 = 0;
        foreach (var next in nextLocalDates)
        {
            if (next is not DateOnly due)
            {
                continue;
            }

            if (due <= today.AddDays(6))
            {
                days7++;
            }

            if (due <= today.AddDays(13))
            {
                days14++;
            }

            if (due <= today.AddDays(29))
            {
                days30++;
            }
        }

        return new ScheduledLoad(days7, days14, days30);
    }

    /// <summary>
    /// Считает термины и повторения выбранного периода по текущей категории термина.
    /// </summary>
    public static IReadOnlyList<CategoryStudy> ByCategory(
        IReadOnlyList<(Guid TermId, string CategoryName)> terms,
        IReadOnlyList<(Guid TermId, int Quality)> reviews)
    {
        var byTerm = reviews.ToLookup(review => review.TermId, review => review.Quality);
        return terms
            .GroupBy(
                term => string.IsNullOrWhiteSpace(term.CategoryName) ? "Без категории" : term.CategoryName.Trim(),
                StringComparer.CurrentCultureIgnoreCase)
            .Select(group =>
            {
                var qualities = group.SelectMany(term => byTerm[term.TermId]).ToList();
                return new CategoryStudy(group.Key, group.Count(), qualities.Count, CountSuccessful(qualities));
            })
            .OrderByDescending(study => study.Reviews)
            .ThenBy(study => study.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static double Effectiveness(
        double successPercent,
        double regularityPercent,
        double masteredSharePercent,
        AnalyticsWeights weights)
    {
        weights.EnsureValid();
        return weights.Success * successPercent
               + weights.Regularity * regularityPercent
               + weights.Mastery * masteredSharePercent;
    }
}
