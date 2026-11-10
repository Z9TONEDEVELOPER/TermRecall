using TermRecall.Domain.Enums;

namespace TermRecall.Domain.Entities;

public sealed class UserSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public int DailyNewTermsLimit { get; set; } = 10;
    public int DailyReviewLimit { get; set; } = 40;
    public bool ReminderEnabled { get; set; } = true;
    public TimeSpan ReminderTime { get; set; } = new(19, 0, 0);
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public bool IncludeNewTerms { get; set; } = true;
    public int MasteryTargetDays { get; set; } = 21;

    /// <summary>Вес успешности в показателе эффективности. Вместе с двумя другими весами равен 1.</summary>
    public double WeightSuccess { get; set; } = 0.5;

    /// <summary>Вес регулярности занятий.</summary>
    public double WeightRegularity { get; set; } = 0.2;

    /// <summary>Вес доли освоенных терминов.</summary>
    public double WeightMastery { get; set; } = 0.3;

    public string DisplayName { get; set; } = "Максим";

    /// <summary>Порог EF, ниже которого термин попадает в тренировку сложных.</summary>
    public double DifficultEaseThreshold { get; set; } = 1.6;

    /// <summary>Недельная цель в минутах по записанной длительности повторений. 0 — цель не задана.</summary>
    public int WeeklyMinutesGoal { get; set; } = 60;
}
