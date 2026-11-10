namespace TermRecall.Domain.Algorithms;

public readonly record struct CardDraft(string Title, string Definition);

/// <summary>
/// Разбирает вставленный текст на карточки. Строка без разделителя пропускается.
/// Расписание SM-2 здесь не считается.
/// </summary>
public static class CardDrafts
{
    private static readonly string[] Separators = [" — ", " – ", " - ", "\t", "; "];

    public static IReadOnlyList<CardDraft> Parse(string? text)
    {
        var drafts = new List<CardDraft>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return drafts;
        }

        foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var cut = -1;
            var separator = string.Empty;
            foreach (var candidate in Separators)
            {
                var index = line.IndexOf(candidate, StringComparison.Ordinal);
                if (index > 0 && (cut < 0 || index < cut))
                {
                    cut = index;
                    separator = candidate;
                }
            }

            if (cut < 0)
            {
                continue;
            }

            var title = line[..cut].Trim();
            var definition = line[(cut + separator.Length)..].Trim();
            if (title.Length == 0 || definition.Length == 0)
            {
                continue;
            }

            drafts.Add(new CardDraft(title, definition));
        }

        return drafts;
    }
}
