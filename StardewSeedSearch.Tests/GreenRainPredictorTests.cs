using StardewSeedSearch.Core;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public class GreenRainPredictorTests
{
    [Fact]
    public void PredictGreenRainDay_IsStableForSameInput()
    {
        int year = 1;
        ulong gameId = 123456789UL;

        int d1 = GreenRainPredictor.PredictGreenRainDay(year, gameId);
        int d2 = GreenRainPredictor.PredictGreenRainDay(year, gameId);

        Assert.Equal(d1, d2);
    }

    [Fact]
    public void PredictGreenRainDay_ChangesWithYearOrGameId()
    {
        int year = 1;
        ulong gameId = 123456789UL;

        int d1 = GreenRainPredictor.PredictGreenRainDay(year, gameId);
        int d2 = GreenRainPredictor.PredictGreenRainDay(year + 1, gameId);

        // Not guaranteed, but extremely likely to differ.
        Assert.NotEqual(d1, d2);
    }

    [Fact]
    public void PredictGreenRainDay_InAllowedDaySet()
    {
        int year = 1;
        ulong gameId = 987654321UL;

        int day = GreenRainPredictor.PredictGreenRainDay(year, gameId);

        Assert.Contains(day, new[] { 5, 6, 7, 14, 15, 16, 18, 23 });
    }

    [Fact]
    public void PredictForKnownCase()
    {
        //We know that for game 123456 green rain is on the 16th in year one, and 7 for year two
        ulong gameId = 123456;

        int FirstYearDay = GreenRainPredictor.PredictGreenRainDay(1, gameId);
        int SecondYearDay = GreenRainPredictor.PredictGreenRainDay(2, gameId);

        Assert.Equal(16, FirstYearDay);
        Assert.Equal(7, SecondYearDay);
    }
}
