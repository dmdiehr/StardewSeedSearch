namespace StardewSeedSearch.Core;

public static class WeatherPredictor
{
    public static Weather GetWeatherForDate(int year, Season season, int dayOfMonth, ulong gameId)
    {

        // 1. Guaranteed Weather
        
        //First of season always sunny
        if (dayOfMonth == 1) return Weather.Sun;

        //First Week Sun
        if ((year == 1) && (season == Season.Spring) && (dayOfMonth == 2 || (dayOfMonth == 4) ||(dayOfMonth == 5))) return Weather.Sun;

        //First Week Rain
        if ((year == 1) && (season == Season.Spring) && (dayOfMonth == 3)) return Weather.Rain;

        //Summer storms
        if ((season == Season.Summer) && ((dayOfMonth == 13) || (dayOfMonth == 26))) return Weather.Storm;

        //Festivals
        if (IsFestivalDay(season, dayOfMonth)) return Weather.Festival;

        //Passive Festivals (for now only Night market, don't know if fishing competitions count or not)
        if ((season == Season.Winter) && (dayOfMonth is >= 15 and <= 17)) return Weather.Sun;

        // 2. Green Rain (summer only; we already know its logic)
        if (season == Season.Summer)
        {
            int greenDay = GreenRainPredictor.PredictGreenRainDay(year, gameId);
            if (dayOfMonth == greenDay)
                return Weather.GreenRain;
        }

        // 3) normal generated weather
        return GetGeneratedWeather(year, season, dayOfMonth, gameId);
    }

    public static IReadOnlyList<Weather> GetWeatherForSeason(int year, Season season, ulong gameId)
{
    var result = new Weather[28];
    for (int day = 1; day <= 28; day++)
    {
        result[day - 1] = GetWeatherForDate(year, season, day, gameId);
    }
    return result;
}

public static IReadOnlyList<Weather> GetWeatherForYear(int year, ulong gameId)
{
    var result = new Weather[112];
    int i = 0;

    foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
    {
        for (int day = 1; day <= 28; day++)
        {
            result[i++] = GetWeatherForDate(year, season, day, gameId);
        }
    }

    return result;
}


private static Weather GetGeneratedWeather(int year, Season season, int dayOfMonth, ulong gameId)
{
    // We intentionally DO NOT implement any GameStateQuery RANDOM-based outcomes here:
    // - Wind (Spring/Fall) uses RANDOM -> depends on Game1.random state -> not reproducible externally.
    // - Storm upgrades (Spring/Fall/Summer) use RANDOM -> same issue.
    //
    // Requirement: distinguish rain/storm vs sun/wind.
    // So:
    // - any “would be Storm” collapses to Rain
    // - any “would be Wind” collapses to Sun

    // Winter: deterministic snow vs sun
    if (season == Season.Winter)
    {
        bool snow = RollSyncedRandomDay(year, season, dayOfMonth, gameId, key: "location_weather", chance: 0.63);
        return snow ? Weather.Snow : Weather.Sun;
    }

    // Summer: deterministic rain-vs-sun (storms collapse to rain)
    if (season == Season.Summer)
    {
        // day 1 already forced sun earlier, but keep the guard to mirror data
        if (dayOfMonth != 1 && RollSyncedSummerRainRandom(year, dayOfMonth, gameId))
            return Weather.Rain;

        return Weather.Sun;
    }

    // Spring/Fall: deterministic base rain-vs-sun (storms collapse to rain, wind collapses to sun)
    if (season == Season.Spring || season == Season.Fall)
    {
        bool rain = RollSyncedRandomDay(year, season, dayOfMonth, gameId, key: "location_weather", chance: 0.183);
        return rain ? Weather.Rain : Weather.Sun;
    }

    return Weather.Sun;
}
    


    private static bool RollSyncedRandomDay(int year, Season season, int dayOfMonth, ulong gameId, string key, double chance)
    {
        // Mirrors: SYNCED_RANDOM day <key> <chance>  => random.NextDouble() < chance
        if (!StardewRng.TryCreateIntervalRandomForDate(
                interval: "day",
                key: key,
                gameId: gameId,
                year: year,
                season: season,
                dayOfMonth: dayOfMonth,
                out Random rng,
                out string? error))
        {
            throw new InvalidOperationException($"TryCreateIntervalRandomForDate failed: {error}");
        }

        return rng.NextDouble() < chance;
    }

    private static bool RollSyncedSummerRainRandom(int year, int dayOfMonth, ulong gameId)
    {
        // Decompiled 1.6.1+ SYNCED_SUMMER_RAIN_RANDOM:
        // random = Utility.CreateDaySaveRandom(hash("summer_rain_chance"));
        // chance = 0.12 + dayOfMonth * 0.003;
        // return random.NextBool(chance);
        // I don't know why this uses zero-based days played, it might just be that it's actually about the forecast not the actual

        long daysPlayed = Helper.GetDaysPlayedZeroBased(year, Season.Summer, dayOfMonth);
        int seedA = HashUtility.GetDeterministicHashCode("summer_rain_chance");

        Random rng = StardewRng.CreateDaySaveRandom(daysPlayed, gameId, seedA);

        double chance = 0.12 + (dayOfMonth * 0.003);
        return rng.NextDouble() < chance;
    }

    private static bool IsFestivalDay(Season season, int dayOfMonth)
    {
        switch (season)
        {
            case Season.Spring:
                if (dayOfMonth == 13 || dayOfMonth == 24)
                    return true;
                break;

            case Season.Summer:
                if (dayOfMonth == 11 || dayOfMonth == 28)
                    return true;
                break;

            case Season.Fall:
                if (dayOfMonth == 16 || dayOfMonth == 27)
                    return true;
                break;

            case Season.Winter:
                if (dayOfMonth == 8 || dayOfMonth == 25)
                    return true;
                break;

        }

        return false;
    }

}

