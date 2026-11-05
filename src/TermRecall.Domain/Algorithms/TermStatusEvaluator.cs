using TermRecall.Domain.Entities;
using TermRecall.Domain.Enums;

namespace TermRecall.Domain.Algorithms;

/// <summary>
/// Статус после ответа. Показатель освоения по интервалу здесь не используется как вероятность:
/// «Освоен» означает, что текущий интервал достиг заданного порога после успешного ответа.
/// </summary>
public static class TermStatusEvaluator
{
    public static TermStatus Evaluate(Term term, int quality, int masteryTargetDays)
    {
        if (masteryTargetDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(masteryTargetDays));
        }

        if (term.LastReviewedAtUtc is null && term.RepetitionCount == 0)
        {
            return TermStatus.New;
        }

        if (quality < Sm2Algorithm.MinimumPassingQuality)
        {
            return TermStatus.Difficult;
        }

        if (term.IntervalDays >= masteryTargetDays && term.RepetitionCount >= 2)
        {
            return TermStatus.Mastered;
        }

        if (term.RepetitionCount >= 2)
        {
            return TermStatus.Review;
        }

        return TermStatus.Learning;
    }
}
