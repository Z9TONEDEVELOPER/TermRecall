using TermRecall.Domain.Entities;

namespace TermRecall.Domain.Algorithms;

public sealed record ChoiceOption(Guid TermId, string Text);

public sealed record ChoiceQuestion(Guid TermId, string Prompt, IReadOnlyList<ChoiceOption> Options);

/// <summary>
/// Собирает вопросы «что означает термин» из определений библиотеки.
/// Не читает и не меняет интервалы SM-2.
/// </summary>
public static class ChoiceQuiz
{
    public const int PracticeCount = 8;
    public const int ExamCount = 10;
    public const double ExamPassRatio = 0.7;

    public static IReadOnlyList<ChoiceQuestion> Build(IReadOnlyList<Term> terms, int questionCount, Random random)
    {
        var usable = terms
            .Where(term => !string.IsNullOrWhiteSpace(term.Title) && !string.IsNullOrWhiteSpace(term.Definition))
            .GroupBy(term => term.Definition.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (usable.Count < 2 || questionCount < 1)
        {
            return [];
        }

        var prompts = Shuffle(usable, random).Take(Math.Min(questionCount, usable.Count));
        var questions = new List<ChoiceQuestion>();
        foreach (var term in prompts)
        {
            var distractors = Shuffle(usable.Where(other => other.Id != term.Id), random).Take(3);
            var options = Shuffle(
                distractors
                    .Select(other => new ChoiceOption(other.Id, other.Definition.Trim()))
                    .Append(new ChoiceOption(term.Id, term.Definition.Trim())),
                random);
            questions.Add(new ChoiceQuestion(term.Id, term.Title.Trim(), options));
        }

        return questions;
    }

    public static bool Passed(int correct, int total)
        => total > 0 && correct / (double)total + 1e-9 >= ExamPassRatio;

    private static List<T> Shuffle<T>(IEnumerable<T> source, Random random)
    {
        var list = source.ToList();
        for (var index = list.Count - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (list[index], list[swap]) = (list[swap], list[index]);
        }

        return list;
    }
}
