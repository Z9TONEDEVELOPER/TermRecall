namespace TermRecall.Domain.Enums;

/// <summary>
/// Статус освоения термина. Меняется только после повторения, а не по календарю.
/// </summary>
public enum TermStatus
{
    New = 0,
    Learning = 1,
    Review = 2,
    Mastered = 3,
    Difficult = 4
}
