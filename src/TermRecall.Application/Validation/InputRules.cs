using TermRecall.Application.Exceptions;
using TermRecall.Domain.Entities;

namespace TermRecall.Application.Validation;

public static class InputRules
{
    public const int MaxTitle = 200;
    public const int MaxDefinition = 4000;
    public const int MaxLongText = 4000;
    public const int MaxName = 80;

    public static void WrittenResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Сначала напишите определение своими словами.");
        }

        if (text.Trim().Length > MaxDefinition)
        {
            throw new ValidationException($"Ответ не длиннее {MaxDefinition} символов.");
        }
    }

    public static void Quality(int quality)
    {
        if (quality is < 0 or > 5)
        {
            throw new ValidationException("Оценка должна быть целым числом от 0 до 5.");
        }
    }

    public static void Deck(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("У набора должно быть название.");
        }

        if (name.Trim().Length > MaxName)
        {
            throw new ValidationException($"Название набора не длиннее {MaxName} символов.");
        }

        if (description.Length > MaxLongText)
        {
            throw new ValidationException("Описание набора слишком длинное.");
        }
    }

    public static void Term(string title, string definition, string example, string explanation)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ValidationException("Введите название термина.");
        }

        if (title.Trim().Length > MaxTitle)
        {
            throw new ValidationException($"Название термина не длиннее {MaxTitle} символов.");
        }

        if (string.IsNullOrWhiteSpace(definition))
        {
            throw new ValidationException("Введите определение. Без него карточку нельзя учить.");
        }

        if (definition.Trim().Length > MaxDefinition)
        {
            throw new ValidationException($"Определение не длиннее {MaxDefinition} символов.");
        }

        if (example.Length > MaxLongText || explanation.Length > MaxLongText)
        {
            throw new ValidationException("Пример или пояснение слишком длинные.");
        }
    }

    public static void CodeAndLink(string code, string link)
    {
        if (code.Trim().Length > 80)
        {
            throw new ValidationException("Код не длиннее 80 символов.");
        }

        var url = link.Trim();
        if (url.Length > 300)
        {
            throw new ValidationException("Ссылка не длиннее 300 символов.");
        }

        if (url.Length > 0 && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Ссылка должна начинаться с http:// или https://.");
        }
    }

    public static IReadOnlyList<string> Tags(string text)
    {
        var tags = new List<string>();
        foreach (var part in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length > 40)
            {
                throw new ValidationException("Один тег не длиннее 40 символов.");
            }

            if (tags.Any(tag => string.Equals(tag, part, StringComparison.CurrentCultureIgnoreCase)))
            {
                continue;
            }

            tags.Add(part);
            if (tags.Count > 12)
            {
                throw new ValidationException("У термина не больше 12 тегов.");
            }
        }

        return tags;
    }

    public static void Settings(UserSettings settings)
    {
        if (settings.DailyNewTermsLimit is < 0 or > 200)
        {
            throw new ValidationException("Новых терминов в день может быть от 0 до 200.");
        }

        if (settings.DailyReviewLimit is < 1 or > 500)
        {
            throw new ValidationException("Дневной лимит повторений может быть от 1 до 500.");
        }

        if (settings.MasteryTargetDays is < 1 or > 3650)
        {
            throw new ValidationException("Целевой интервал освоения может быть от 1 до 3650 дней.");
        }

        if (string.IsNullOrWhiteSpace(settings.DisplayName) || settings.DisplayName.Trim().Length > 40)
        {
            throw new ValidationException("Имя должно содержать от 1 до 40 символов.");
        }

        if (settings.ReminderTime < TimeSpan.Zero || settings.ReminderTime >= TimeSpan.FromDays(1))
        {
            throw new ValidationException("Время напоминания задаётся внутри суток.");
        }

        var sum = settings.WeightSuccess + settings.WeightRegularity + settings.WeightMastery;
        if (settings.WeightSuccess < 0 || settings.WeightRegularity < 0 || settings.WeightMastery < 0
            || Math.Abs(sum - 1d) > 0.001d)
        {
            throw new ValidationException("Веса успешности, регулярности и освоения должны быть неотрицательными и в сумме давать 1.");
        }

        if (settings.DifficultEaseThreshold is < 1.3 or > 3.5)
        {
            throw new ValidationException("Порог сложных терминов должен быть от 1.3 до 3.5.");
        }

        if (settings.WeeklyMinutesGoal is < 0 or > 10080)
        {
            throw new ValidationException("Недельная цель может быть от 0 до 10080 минут. Ноль значит, что цель не задана.");
        }
    }
}
