using StardewSeedSearch.Core;
using System.Text;

namespace StardewSeedSearch.Core.Search;

public static class WeatherScoring
{
    /// <summary>
    /// Nice-to-have weather score for Year 1.
    ///
    /// Bits in weatherMask:
    /// 0 = early second rain (Spring 7-11 has >=1 Rain)
    /// 1 = late spring rain  (Spring 21-28 has >=1 Rain)
    /// 2 = early green rain  (Summer 5-7 has GreenRain)
    /// 3 = lots of summer rain (Summer 1-28 has >=5 Rain days; Storm not counted)
    /// </summary>
    public static int ScoreWeatherY1(ulong gameId, out byte weatherMask)
    {
        weatherMask = 0;
        int score = 0;

        if (AnyWeather(1, Season.Spring, 7, 11, gameId, Weather.Rain))
        {
            weatherMask |= 1 << 0;
            score++;
        }

        if (AnyWeather(1, Season.Spring, 21, 28, gameId, Weather.Rain))
        {
            weatherMask |= 1 << 1;
            score++;
        }

        if (AnyWeather(1, Season.Summer, 5, 7, gameId, Weather.GreenRain))
        {
            weatherMask |= 1 << 2;
            score++;
        }

        int summerRainDays = CountWeather(1, Season.Summer, 1, 28, gameId, Weather.Rain);
        if (summerRainDays >= 5)
        {
            weatherMask |= 1 << 3;
            score++;
        }

        return score;
    }

    private static bool AnyWeather(int year, Season season, int startDay, int endDay, ulong gameId, Weather target)
    {
        for (int day = startDay; day <= endDay; day++)
        {
            if (WeatherPredictor.GetWeatherForDate(year, season, day, gameId) == target)
                return true;
        }
        return false;
    }

    private static int CountWeather(int year, Season season, int startDay, int endDay, ulong gameId, Weather target)
    {
        int count = 0;
        for (int day = startDay; day <= endDay; day++)
        {
            if (WeatherPredictor.GetWeatherForDate(year, season, day, gameId) == target)
                count++;
        }
        return count;
    }

        public static string FormatWeatherMask(byte weatherMask)
    {
        if (weatherMask == 0) return "-";

        var sb = new StringBuilder(64);
        bool first = true;

        void Add(string s)
        {
            if (!first) sb.Append(", ");
            sb.Append(s);
            first = false;
        }

        // Bits:
        // 0 = early second rain (Spring 7-11 has >=1 Rain)
        // 1 = late spring rain  (Spring 21-28 has >=1 Rain)
        // 2 = early green rain  (Summer 5-7 has GreenRain)
        // 3 = lots of summer rain (Summer 1-28 has >=5 Rain days; Storm not counted)
        if ((weatherMask & (1 << 0)) != 0) Add("early2ndRain");
        if ((weatherMask & (1 << 1)) != 0) Add("lateSpringRain");
        if ((weatherMask & (1 << 2)) != 0) Add("earlyGreenRain");
        if ((weatherMask & (1 << 3)) != 0) Add("summerRain>=5");

        return sb.ToString();
    }
}
