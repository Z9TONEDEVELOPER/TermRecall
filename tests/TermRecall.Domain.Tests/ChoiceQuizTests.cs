using TermRecall.Domain.Algorithms;
using TermRecall.Domain.Entities;

namespace TermRecall.Domain.Tests;

public class ChoiceQuizTests
{
    [Fact]
    public void Build_KeepsTheCorrectDefinitionAndLeavesSchedulingUntouched()
    {
        var terms = Enumerable.Range(0, 6).Select(index => new Term
        {
            Id = Guid.NewGuid(),
            Title = "Термин " + index,
            Definition = "Определение " + index,
            IntervalDays = 4,
            EaseFactor = 2.2,
            RepetitionCount = 3
        }).ToList();

        var questions = ChoiceQuiz.Build(terms, 4, new Random(1));

        Assert.Equal(4, questions.Count);
        foreach (var question in questions)
        {
            var source = terms.Single(term => term.Id == question.TermId);
            Assert.Contains(question.Options, option => option.TermId == source.Id && option.Text == source.Definition);
            Assert.Equal(question.Options.Count, question.Options.Select(option => option.Text).Distinct().Count());
        }

        Assert.All(terms, term =>
        {
            Assert.Equal(4, term.IntervalDays);
            Assert.Equal(2.2, term.EaseFactor);
            Assert.Equal(3, term.RepetitionCount);
        });
        Assert.True(ChoiceQuiz.Passed(7, 10));
        Assert.False(ChoiceQuiz.Passed(6, 10));
    }

    [Fact]
    public void Build_RefusesWhenDefinitionsAreNotDistinct()
    {
        var first = new Term { Id = Guid.NewGuid(), Title = "А", Definition = "одно и то же" };
        var second = new Term { Id = Guid.NewGuid(), Title = "Б", Definition = "одно и то же" };
        Assert.Empty(ChoiceQuiz.Build([first, second], 5, new Random(2)));
        Assert.Empty(ChoiceQuiz.Build([first], 5, new Random(2)));
    }
}
