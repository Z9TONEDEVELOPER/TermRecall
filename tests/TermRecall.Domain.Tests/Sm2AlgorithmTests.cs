using TermRecall.Domain.Algorithms;

namespace TermRecall.Domain.Tests;

public class Sm2AlgorithmTests
{
    private static readonly Sm2State Initial = new(2.5, 0, 0);

    [Fact]
    public void EaseDelta_MatchesSm2Table()
    {
        Assert.Equal(-0.8, Sm2Algorithm.EaseDelta(0), 10);
        Assert.Equal(-0.54, Sm2Algorithm.EaseDelta(1), 10);
        Assert.Equal(-0.32, Sm2Algorithm.EaseDelta(2), 10);
        Assert.Equal(-0.14, Sm2Algorithm.EaseDelta(3), 10);
        Assert.Equal(0, Sm2Algorithm.EaseDelta(4), 10);
        Assert.Equal(0.1, Sm2Algorithm.EaseDelta(5), 10);
    }

    [Fact]
    public void FirstSuccessfulReview_SetsIntervalToOneDay()
    {
        var result = Sm2Algorithm.Review(Initial, 4);

        Assert.Equal(2.5, result.EaseFactor, 10);
        Assert.Equal(1, result.IntervalDays);
        Assert.Equal(1, result.RepetitionCount);
        Assert.True(result.Successful);
        Assert.Contains("первое успешное", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondSuccessfulReview_SetsIntervalToSixDays()
    {
        var afterFirst = Sm2Algorithm.Review(Initial, 4);
        var result = Sm2Algorithm.Review(afterFirst.Next, 5);

        Assert.Equal(2.6, result.EaseFactor, 10);
        Assert.Equal(6, result.IntervalDays);
        Assert.Equal(2, result.RepetitionCount);
    }

    [Fact]
    public void ThirdSuccessfulReview_MultipliesPreviousIntervalByNewEase()
    {
        var state = Sm2Algorithm.Review(Initial, 4).Next;
        state = Sm2Algorithm.Review(state, 5).Next;
        var result = Sm2Algorithm.Review(state, 4);

        Assert.Equal(2.6, result.EaseFactor, 10);
        Assert.Equal(16, result.IntervalDays);
        Assert.Equal(3, result.RepetitionCount);
        Assert.Contains("round(6 × 2.6)", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void CourseworkSequence_UsesDocumentedNumbers()
    {
        var first = Sm2Algorithm.Review(new Sm2State(2.5, 0, 0), 4);
        var second = Sm2Algorithm.Review(first.Next, 5);
        var third = Sm2Algorithm.Review(second.Next, 4);

        Assert.Equal(2.5, first.EaseFactor, 10);
        Assert.Equal(1, first.IntervalDays);
        Assert.Equal(2.6, second.EaseFactor, 10);
        Assert.Equal(6, second.IntervalDays);
        Assert.Equal(2.6, third.EaseFactor, 10);
        Assert.Equal(16, third.IntervalDays);
    }

    [Fact]
    public void MinimumEaseFactor_IsClampedAtOnePointThree()
    {
        var result = Sm2Algorithm.Review(new Sm2State(1.3, 6, 2), 0);

        Assert.Equal(1.3, result.EaseFactor, 10);
        Assert.Equal(0, result.RepetitionCount);
        Assert.Equal(1, result.IntervalDays);
        Assert.False(result.Successful);
    }

    [Fact]
    public void FailedAnswer_ResetsSuccessfulRepetitionsAndInterval()
    {
        var state = new Sm2State(2.5, 16, 3);
        var result = Sm2Algorithm.Review(state, 2);

        Assert.Equal(2.18, result.EaseFactor, 10);
        Assert.Equal(0, result.RepetitionCount);
        Assert.Equal(1, result.IntervalDays);
    }

    [Fact]
    public void AfterFailure_NextSuccessIsTreatedAsFirstAgain()
    {
        var failed = Sm2Algorithm.Review(new Sm2State(2.5, 16, 3), 1);
        var again = Sm2Algorithm.Review(failed.Next, 5);

        Assert.Equal(1, again.IntervalDays);
        Assert.Equal(1, again.RepetitionCount);
        Assert.Equal(2.06, again.EaseFactor, 10);
    }

    [Fact]
    public void Rounding_UsesAwayFromZero()
    {
        Assert.Equal(11, Sm2Algorithm.RoundDays(10.5));
        Assert.Equal(16, Sm2Algorithm.RoundDays(15.6));
        Assert.Equal(15, Sm2Algorithm.RoundDays(15.4));
        Assert.Equal(1, Sm2Algorithm.RoundDays(0));
    }

    [Fact]
    public void QualityOutsideScale_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sm2Algorithm.Review(Initial, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sm2Algorithm.Review(Initial, -1));
    }

    [Fact]
    public void LongerSuccessfulChain_MatchesHandCalculation()
    {
        var state = Sm2State.Initial;
        state = Sm2Algorithm.Review(state, 5).Next;
        Assert.Equal(2.6, state.EaseFactor, 10);
        Assert.Equal(1, state.IntervalDays);
        Assert.Equal(1, state.RepetitionCount);
        state = Sm2Algorithm.Review(state, 5).Next;
        Assert.Equal(2.7, state.EaseFactor, 10);
        Assert.Equal(6, state.IntervalDays);
        state = Sm2Algorithm.Review(state, 5).Next;
        Assert.Equal(2.8, state.EaseFactor, 10);
        Assert.Equal(17, state.IntervalDays);
        Assert.Equal(3, state.RepetitionCount);
        var fourth = Sm2Algorithm.Review(state, 3);
        Assert.Equal(2.66, fourth.EaseFactor, 10);
        Assert.Equal(45, fourth.IntervalDays);
        Assert.Equal(4, fourth.RepetitionCount);
    }
}
