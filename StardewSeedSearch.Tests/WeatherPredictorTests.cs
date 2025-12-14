using StardewSeedSearch.Core;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public class WeatherPredictorTests
{
    private readonly ITestOutputHelper output;

    public WeatherPredictorTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ForcedSpring3Year1_IsRain()
    {
        int year = 1;
        ulong gameId = 123456789UL; // gameId doesn't matter for forced days

        var weather = WeatherPredictor.GetWeatherForDate(year, Season.Spring, 3, gameId);

        Assert.Equal(Weather.Rain, weather);
    }

    [Fact]
    public void GreenRainDay_UsesGreenRainPredictor()
    {
        int year = 1;
        ulong gameId = 123456789UL;

        int greenDay = GreenRainPredictor.PredictGreenRainDay(year, gameId);
        var weather = WeatherPredictor.GetWeatherForDate(year, Season.Summer, greenDay, gameId);

        Assert.Equal(Weather.GreenRain, weather);

        output.WriteLine($"Green Rain (Y{year}) for game {gameId} is on summer {greenDay}");
    }

    [Fact]
    public void FirstDayofSeasonIsAlwaysSunny()
    {
        //No matter what the random numbers, the result should be the same. So if this test is flaky it means the method is broken.
        Random rnd = new Random();
        ulong gameId = (ulong)rnd.Next(1, 999999);
        int year = rnd.Next(1,20);

        var springFirstWeather = WeatherPredictor.GetWeatherForDate(year, Season.Spring, 1, gameId);
        var summerFirstWeather = WeatherPredictor.GetWeatherForDate(year, Season.Summer, 1, gameId);
        var fallFirstWeather = WeatherPredictor.GetWeatherForDate(year, Season.Fall, 1, gameId);
        var winterFirstWeather = WeatherPredictor.GetWeatherForDate(year, Season.Winter, 1, gameId);

        Assert.Equal(Weather.Sun, springFirstWeather);
        Assert.Equal(Weather.Sun, summerFirstWeather);
        Assert.Equal(Weather.Sun, fallFirstWeather);
        Assert.Equal(Weather.Sun, winterFirstWeather);

    }

    [Fact]
    public void ResultsMatchKnownYear()
    {
        ulong gameId = 1234567;
        var knownYear = new List<Weather>(
            [Weather.Sun, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Festival, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Festival, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.GreenRain, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Festival, Weather.Sun, Weather.Storm, Weather.Sun, Weather.Rain, Weather.Rain, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Sun, Weather.Rain, Weather.Rain, Weather.Rain, Weather.Sun, Weather.Sun, Weather.Storm, Weather.Sun, Weather.Festival, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Festival, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Rain, Weather.Sun, Weather.Festival, Weather.Sun, Weather.Sun, Weather.Snow, Weather.Snow, Weather.Snow, Weather.Sun, Weather.Snow, Weather.Snow, Weather.Festival, Weather.Snow, Weather.Snow, Weather.Sun, Weather.Sun, Weather.Snow, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Sun, Weather.Snow, Weather.Snow, Weather.Snow, Weather.Snow, Weather.Snow, Weather.Sun, Weather.Snow, Weather.Festival, Weather.Snow, Weather.Sun, Weather.Snow]);

    
        var testYear = WeatherPredictor.GetWeatherForYear(1, gameId);

        Assert.Equivalent(knownYear, testYear);
    }


}
