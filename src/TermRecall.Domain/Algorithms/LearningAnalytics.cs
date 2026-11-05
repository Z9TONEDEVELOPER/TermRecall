namespace TermRecall.Domain.Algorithms;

/// <summary>
/// Веса агрегированного показателя. Сумма должна быть равна 1, чтобы шкала оставалась 0–100.
/// Это настраиваемый индекс продукта, а не измеренная вероятность запоминания.
/// </summary>
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

    public static double MasteredSharePercent(int mastered, int totalTerms)
    {
        if (totalTerms <= 0)
        {
            return 0;
        }

        return mastered / (double)totalTerms * 100d;
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
