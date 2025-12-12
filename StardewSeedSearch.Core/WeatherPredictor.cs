namespace StardewSeedSearch.Core;

public static class WeatherPredictor
{
    public static Weather GetWeatherForDate(int year, Season season, int dayOfMonth, ulong gameId)
    {
        // 1. Forced weather from vanilla rules
        var forced = GetForcedWeather(year, season, dayOfMonth);
        if (forced != Weather.Unknown)
            return forced;

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

    private static Weather GetForcedWeather(int year, Season season, int dayOfMonth)
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
                return Weather.Sun;

            if (year == 1)
            {
                if (dayOfMonth == 2 || dayOfMonth == 4)
                    return Weather.Sun;
                if (dayOfMonth == 3)
                    return Weather.Rain;
            }
        }
        else if (season == Season.Summer)
        {
            if (dayOfMonth == 1)
                return Weather.Sun;
            if (dayOfMonth == 13 || dayOfMonth == 26)
                return Weather.Storm;
        }
        else if (season == Season.Fall)
        {
            if (dayOfMonth == 1)
                return Weather.Sun;
        }
        else if (season == Season.Winter)
        {
            if (dayOfMonth == 1)
                return Weather.Sun;
            
            // Night Market (passive festival) on Winter 15–17:
            // weather in the Default context (valley) is always sunny on those days.
            if (dayOfMonth is >= 15 and <= 17)
                return Weather.Sun;
        }

         // ---- Festival overrides ----
        switch (season)
        {
            case Season.Spring:
                if (dayOfMonth == 13 || dayOfMonth == 24)
                    return Weather.Festival;
                break;

            case Season.Summer:
                if (dayOfMonth == 11 || dayOfMonth == 28)
                    return Weather.Festival;
                break;

            case Season.Fall:
                if (dayOfMonth == 16 || dayOfMonth == 27)
                    return Weather.Festival;
                break;

            case Season.Winter:
                if (dayOfMonth == 8 || dayOfMonth == 25)
                    return Weather.Festival;
                break;
        }

        //Return unknown when its not a force weather date
        return Weather.Unknown;
    }

    private static Weather GetGeneratedWeather(int year, Season season, int dayOfMonth, ulong gameId)
    {
        long daysPlayed = Helper.GetDaysPlayed(year, season, dayOfMonth);
        Random ctxRandom = CreateWeatherContextRandom(daysPlayed, gameId);

        // --- SummerStorm (SEASON summer, SYNCED_SUMMER_RAIN_RANDOM, RANDOM .85) ---
        if (season == Season.Summer && RollSyncedSummerRainRandom(year, dayOfMonth, gameId))
        {
            if (RollRandom(ctxRandom, 0.85))
                return Weather.Storm;

            // --- SummerStorm2 (SEASON summer, SYNCED_SUMMER_RAIN_RANDOM, RANDOM .25, DAYS_PLAYED 28, !DAY_OF_MONTH 1, !DAY_OF_MONTH 2) ---
            if (daysPlayed >= 28 && dayOfMonth != 1 && dayOfMonth != 2 && RollRandom(ctxRandom, 0.25))
                return Weather.Storm;
        }

        // --- FallStorm (SEASON spring fall, SYNCED_RANDOM day location_weather .183, RANDOM .25, DAYS_PLAYED 28, !DAY_OF_MONTH 1, !DAY_OF_MONTH 2) ---
        if ((season == Season.Spring || season == Season.Fall) &&
            RollSyncedRandomDay(year, season, dayOfMonth, gameId, key: "location_weather", chance: 0.183))
        {
            if (daysPlayed >= 28 && dayOfMonth != 1 && dayOfMonth != 2 && RollRandom(ctxRandom, 0.25))
                return Weather.Storm;
        }

        // --- WinterSnow (SEASON winter, SYNCED_RANDOM day location_weather 0.63) ---
        if (season == Season.Winter &&
            RollSyncedRandomDay(year, season, dayOfMonth, gameId, key: "location_weather", chance: 0.63))
        {
            return Weather.Snow;
        }

        // --- SummerRain (SEASON summer, SYNCED_SUMMER_RAIN_RANDOM, !DAY_OF_MONTH 1) ---
        if (season == Season.Summer && dayOfMonth != 1 && RollSyncedSummerRainRandom(year, dayOfMonth, gameId))
            return Weather.Rain;

        // --- FallRain (SEASON spring fall, SYNCED_RANDOM day location_weather 0.183) ---
        if ((season == Season.Spring || season == Season.Fall) &&
            RollSyncedRandomDay(year, season, dayOfMonth, gameId, key: "location_weather", chance: 0.183))
        {
            return Weather.Rain;
        }

        // --- SpringWind (DAYS_PLAYED 3, SEASON spring, RANDOM .20) ---
        if (season == Season.Spring && daysPlayed >= 3 && RollRandom(ctxRandom, 0.20))
            return Weather.Wind;

        // --- FallWind (DAYS_PLAYED 3, SEASON fall, RANDOM .6) ---
        if (season == Season.Fall && daysPlayed >= 3 && RollRandom(ctxRandom, 0.60))
            return Weather.Wind;

        // --- Default (Sun) ---
        return Weather.Sun;
    }   
    


    private static bool RollSyncedRandomDay(int year, Season season, int dayOfMonth, ulong gameId, string key, double chance)
    {
        // Mirrors: SYNCED_RANDOM day <key> <chance>  => random.NextDouble() < chance
        if (!Helper.TryCreateIntervalRandomForDate(
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

        long daysPlayed = Helper.GetDaysPlayed(year, Season.Summer, dayOfMonth);
        int seedA = HashUtility.GetDeterministicHashCode("summer_rain_chance");

        Random rng = StardewRng.CreateDaySaveRandom(daysPlayed, gameId, seedA);

        double chance = 0.12 + (dayOfMonth * 0.003);
        return rng.NextDouble() < chance;
    }

    private static bool RollRandom(Random rng, double chance)
    {
        // Mirrors Helpers.RandomImpl(random, ..., chance) with no @addDailyLuck
        return rng.NextDouble() < chance;
    }

    private static Random CreateWeatherContextRandom(long daysPlayed, ulong gameId)
    {
        // ASSUMPTION TO VERIFY:
        // GameStateQuery RANDOM uses the context random which (for weather) matches the day RNG.
        // If this is wrong, this is the only method you need to change.
        return StardewRng.CreateDaySaveRandom(daysPlayed, gameId);
    }


}

