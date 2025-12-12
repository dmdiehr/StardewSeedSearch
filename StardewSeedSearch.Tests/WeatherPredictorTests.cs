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
    public void WeatherCorrectForKnownGame()
    {

        
        ulong gameId = 999995;
        int year = 1;
        Season season = Season.Summer;   // ← change this to Summer/Fall/Winter
        
        output.WriteLine($"Weather for Year {year}, {season}:");

        for (int day = 1; day <= 28; day++)
        {
            var weather = WeatherPredictor.GetWeatherForDate(year, season, day, gameId);
            output.WriteLine($"{season} {day}: {weather}");
        }

        // var rainDay = WeatherPredictor.GetWeatherForDate(1, Season.Spring, 6, gameId);
        // var sunDay = WeatherPredictor.GetWeatherForDate(1, Season.Spring, 7, gameId);

        // Assert.Equal(Weather.Sun, sunDay);
        // Assert.Equal(Weather.Rain, rainDay);


    }
}
