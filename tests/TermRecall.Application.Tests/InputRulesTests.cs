using TermRecall.Application.Exceptions;
using TermRecall.Application.Validation;
using TermRecall.Domain.Entities;

namespace TermRecall.Application.Tests;

public class InputRulesTests
{
    [Fact]
    public void Term_RequiresTitleAndDefinition()
    {
        Assert.Throws<ValidationException>(() => InputRules.Term(" ", "определение", "", ""));
        Assert.Throws<ValidationException>(() => InputRules.Term("CLR", "  ", "", ""));
        InputRules.Term("CLR", "Исполняющая среда.", "", "");
    }

    [Fact]
    public void Settings_RejectWeightsThatDoNotSumToOne()
    {
        var settings = new UserSettings { WeightSuccess = 0.9, WeightRegularity = 0.2, WeightMastery = 0.3 };
        Assert.Throws<ValidationException>(() => InputRules.Settings(settings));
    }

    [Fact]
    public void Quality_IsClosedScale()
    {
        Assert.Throws<ValidationException>(() => InputRules.Quality(6));
        InputRules.Quality(0);
        InputRules.Quality(5);
    }
}
