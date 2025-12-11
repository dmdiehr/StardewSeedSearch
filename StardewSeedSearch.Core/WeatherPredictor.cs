namespace StardewSeedSearch.Core;

public enum WeatherType
{
    Unknown,
    Sun,
    Rain,
    GreenRain,
    Festival,
    Storm,
    Wind,
    Snow
}

public static class WeatherPredictor
{
    public static WeatherType GetWeatherForDate(int year, Season season, int dayOfMonth, ulong gameId)
    {
        // 1. Forced weather from vanilla rules
        var forced = GetForcedWeather(year, season, dayOfMonth);
        if (forced != WeatherType.Unknown)
            return forced;

        // 2. Green Rain (summer only; we already know its logic)
        if (season == Season.Summer)
        {
            int greenDay = GreenRainPredictor.PredictGreenRainDay(year, gameId);
            if (dayOfMonth == greenDay)
                return WeatherType.GreenRain;
        }

        // 3) normal generated weather
        return GetGeneratedWeather(year, season, dayOfMonth, gameId);
    }

    
    private static int GetDaysPlayed(int year, Season season, int dayOfMonth)
    {
        int seasonIndex = season switch
        {
            Season.Spring => 0,
            Season.Summer => 1,
            Season.Fall   => 2,
            Season.Winter => 3,
            _ => 0
        };

        // Assumption: DaysPlayed is 1 on Spring 1, Year 1.
        // Each year has 112 days (4 * 28).
        return (year - 1) * 112 + seasonIndex * 28 + dayOfMonth;
    }

    private static WeatherType GetForcedWeather(int year, Season season, int dayOfMonth)
    {
        // From the official 1.6 weather docs:
        // spring 1               -> Sun
        // spring 2 (year 1 only) -> Sun
        // spring 3 (year 1 only) -> Rain
        // spring 4 (year 1 only) -> Sun
        // summer 1               -> Sun
        // summer 13              -> Storm
        // summer 26              -> Storm
        // fall 1                 -> Sun
        // winter 1               -> Sun
        //
        // Festivals & weddings are handled elsewhere / later.

        if (season == Season.Spring)
        {
            if (dayOfMonth == 1)
                return WeatherType.Sun;

            if (year == 1)
            {
                if (dayOfMonth == 2 || dayOfMonth == 4)
                    return WeatherType.Sun;
                if (dayOfMonth == 3)
                    return WeatherType.Rain;
            }
        }
        else if (season == Season.Summer)
        {
            if (dayOfMonth == 1)
                return WeatherType.Sun;
            if (dayOfMonth == 13 || dayOfMonth == 26)
                return WeatherType.Storm;
        }
        else if (season == Season.Fall)
        {
            if (dayOfMonth == 1)
                return WeatherType.Sun;
        }
        else if (season == Season.Winter)
        {
            if (dayOfMonth == 1)
                return WeatherType.Sun;
            
            // Night Market (passive festival) on Winter 15–17:
            // weather in the Default context (valley) is always sunny on those days.
            if (dayOfMonth is >= 15 and <= 17)
                return WeatherType.Sun;
        }

         // ---- Festival overrides ----
        switch (season)
        {
            case Season.Spring:
                if (dayOfMonth == 13 || dayOfMonth == 24)
                    return WeatherType.Festival;
                break;

            case Season.Summer:
                if (dayOfMonth == 11 || dayOfMonth == 28)
                    return WeatherType.Festival;
                break;

            case Season.Fall:
                if (dayOfMonth == 16 || dayOfMonth == 27)
                    return WeatherType.Festival;
                break;

            case Season.Winter:
                if (dayOfMonth == 8 || dayOfMonth == 25)
                    return WeatherType.Festival;
                break;
        }

        //Return unknown when its not a force weather date
        return WeatherType.Unknown;
    }

    private static WeatherType GetGeneratedWeather(int year, Season season, int dayOfMonth, ulong gameId)
    {
        int daysPlayed = GetDaysPlayed(year, season, dayOfMonth);
  
        return WeatherType.Unknown;
    }

}

