namespace TermRecall.Domain.Enums;

public enum StudyMode
{
    /// <summary>Просроченные, затем сегодняшние, затем новые в пределах лимита.</summary>
    DueMix = 0,
    Deck = 1,
    Difficult = 2,
    NewOnly = 3
}
