using System.Globalization;

namespace TermRecall.Domain.Algorithms;

/// <summary>
/// Состояние карточки, которое подаётся на вход SM-2.
/// </summary>
public readonly record struct Sm2State(double EaseFactor, int IntervalDays, int RepetitionCount)
{
    public static Sm2State Initial { get; } = new(Sm2Algorithm.DefaultEaseFactor, 0, 0);
}

/// <summary>
/// Результат одного шага SM-2. Содержит и предыдущее, и новое состояние.
/// </summary>
public readonly record struct Sm2Result(
    Sm2State Previous,
    double EaseFactor,
    int IntervalDays,
    int RepetitionCount,
    int Quality,
    double EaseDelta,
    bool Successful,
    string Explanation)
{
    public Sm2State Next => new(EaseFactor, IntervalDays, RepetitionCount);
}

/// <summary>
/// Алгоритм SuperMemo 2.
/// Формула коэффициента лёгкости совпадает с оригинальной публикацией SM-2:
/// EF' = EF + (0.1 − (5 − q) × (0.08 + (5 − q) × 0.02)), EF' ≥ 1.3.
/// Интервал успешного повторения: 1 день, затем 6 дней, далее round(I × EF').
/// Округление — до ближайшего целого дня, половинки от нуля (AwayFromZero).
/// </summary>
public static class Sm2Algorithm
{
    public const double DefaultEaseFactor = 2.5;
    public const double MinimumEaseFactor = 1.3;
    public const int FirstSuccessIntervalDays = 1;
    public const int SecondSuccessIntervalDays = 6;
    public const int MinimumPassingQuality = 3;

    public static Sm2Result Review(Sm2State state, int quality)
    {
        if (quality is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Оценка SM-2 должна быть от 0 до 5.");
        }

        if (state.EaseFactor < MinimumEaseFactor)
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Коэффициент лёгкости не может быть меньше 1.3.");
        }

        var delta = EaseDelta(quality);
        var ease = state.EaseFactor + delta;
        if (ease < MinimumEaseFactor)
        {
            ease = MinimumEaseFactor;
        }

        var successful = quality >= MinimumPassingQuality;
        int repetitions;
        int interval;
        if (successful)
        {
            repetitions = state.RepetitionCount + 1;
            interval = repetitions switch
            {
                1 => FirstSuccessIntervalDays,
                2 => SecondSuccessIntervalDays,
                _ => RoundDays(state.IntervalDays * ease)
            };
        }
        else
        {
            repetitions = 0;
            interval = FirstSuccessIntervalDays;
        }

        var explanation = Explain(state, quality, delta, ease, interval, repetitions, successful);
        return new Sm2Result(state, ease, interval, repetitions, quality, delta, successful, explanation);
    }

    /// <summary>
    /// EF' − EF = 0.1 − (5 − q) × (0.08 + (5 − q) × 0.02).
    /// </summary>
    public static double EaseDelta(int quality)
    {
        var gap = 5 - quality;
        return 0.1 - gap * (0.08 + gap * 0.02);
    }

    public static int RoundDays(double rawDays)
    {
        var rounded = (int)Math.Round(rawDays, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    private static string Explain(
        Sm2State state,
        int quality,
        double delta,
        double ease,
        int interval,
        int repetitions,
        bool successful)
    {
        var culture = CultureInfo.InvariantCulture;
        var gap = 5 - quality;
        var easeFormula =
            $"EF' = {state.EaseFactor.ToString("0.###", culture)} + (0.1 − ({gap}) × (0.08 + ({gap}) × 0.02)) = {state.EaseFactor.ToString("0.###", culture)} + ({delta.ToString("0.###", culture)}) = {ease.ToString("0.###", culture)}";
        if (ease <= MinimumEaseFactor && state.EaseFactor + delta < MinimumEaseFactor)
        {
            easeFormula += $". Значение ниже {MinimumEaseFactor.ToString("0.0", culture)}, поэтому EF' = {MinimumEaseFactor.ToString("0.0", culture)}";
        }

        string intervalFormula;
        if (!successful)
        {
            intervalFormula =
                $"q = {quality} < {MinimumPassingQuality}: воспроизведение неуспешно. n сбрасывается с {state.RepetitionCount} до 0, интервал устанавливается в {FirstSuccessIntervalDays} день.";
        }
        else if (repetitions == 1)
        {
            intervalFormula = $"q = {quality} ≥ {MinimumPassingQuality}: первое успешное повторение, I = {FirstSuccessIntervalDays} день, n = 1.";
        }
        else if (repetitions == 2)
        {
            intervalFormula = $"q = {quality} ≥ {MinimumPassingQuality}: второе успешное повторение, I = {SecondSuccessIntervalDays} дней, n = 2.";
        }
        else
        {
            var raw = state.IntervalDays * ease;
            intervalFormula =
                $"q = {quality} ≥ {MinimumPassingQuality}: I = round({state.IntervalDays.ToString(culture)} × {ease.ToString("0.###", culture)}) = round({raw.ToString("0.###", culture)}) = {interval} дн., n = {repetitions}.";
        }

        return easeFormula + " " + intervalFormula;
    }
}
