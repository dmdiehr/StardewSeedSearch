namespace StardewSeedSearch.Core.Helpers;

public static class Helper
{

    public static bool TryCreateIntervalRandomForDate(
        string interval,
        string? key,
        ulong gameId,
        int year,
        Season season,
        int dayOfMonth,
        out Random random,
        out string? error)
    {
        error = null;

        int seed = key != null ? HashUtility.GetDeterministicHashCode(key) : 0;

        double intervalSeed;
        switch (interval.ToLowerInvariant())
        {
            case "day":
                // MUST match Game1.stats.DaysPlayed for that morning.
                // In vanilla saves this is almost always “days since start”, 0-based:
                // Spring 1 Y1 => 0, Spring 2 Y1 => 1, etc.
                intervalSeed = GetDaysPlayed(year, season, dayOfMonth);
                break;

            case "season":
                // Game1.currentSeason is the lowercase season string: "spring"/"summer"/"fall"/"winter"
                intervalSeed = HashUtility.GetDeterministicHashCode(GetSeasonName(season) + year);
                break;

            case "year":
                intervalSeed = HashUtility.GetDeterministicHashCode("year" + year);
                break;

            case "tick":
                error = "interval 'tick' not supported for external prediction";
                random = null!;
                return false;

            default:
                error = $"invalid interval '{interval}'; expected one of 'tick', 'day', 'season', or 'year'";
                random = null!;
                return false;
        }

        // Matches Utility.CreateRandom(seed, uniqueID, intervalSeed)
        random = StardewRng.CreateRandom(seed, (double)gameId, intervalSeed);
        return true;
    }

    public static long GetDaysPlayed(int year, Season season, int dayOfMonth)
    {
        int seasonIndex = season switch
        {
            Season.Spring => 0,
            Season.Summer => 1,
            Season.Fall   => 2,
            Season.Winter => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(season))
        };

    // 0-based: Spring 1 Y1 => 0
    return ((year - 1) * 112L) + (seasonIndex * 28L) + (dayOfMonth - 1);
    }

    public static string GetSeasonName(Season season) => season switch
    {
        Season.Spring => "spring",
        Season.Summer => "summer",
        Season.Fall   => "fall",
        Season.Winter => "winter",
        _ => throw new ArgumentOutOfRangeException(nameof(season))
    };

}

