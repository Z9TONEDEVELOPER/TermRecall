namespace TermRecall.Domain.Enums;

/// <summary>
/// Проверка без записи в журнал SM-2. Тест сразу показывает ответ, экзамен — только итог.
/// </summary>
public enum AssessmentMode
{
    Test = 0,
    Exam = 1
}
