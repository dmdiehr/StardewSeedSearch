using System;
using System.Collections.Generic;

namespace StardewSeedSearch.Core;

public static class StardewRng
{
    private const double Mod = 2147483647.0;

    /// <summary>
    /// Clone of Utility.CreateRandomSeed for non-legacy mode (1.6+ hashing).
    /// We ignore Game1.UseLegacyRandom and always use the hash path.
    /// </summary>
    public static int CreateRandomSeed(
        double seedA,
        double seedB = 0.0,
        double seedC = 0.0,
        double seedD = 0.0,
        double seedE = 0.0)
    {
        int a = (int)(seedA % Mod);
        int b = (int)(seedB % Mod);
        int c = (int)(seedC % Mod);
        int d = (int)(seedD % Mod);
        int e = (int)(seedE % Mod);

        return HashUtility.GetDeterministicHashCode(a, b, c, d, e);
    }

    /// <summary>
    /// Clone of Utility.CreateRandom.
    /// </summary>
    public static Random CreateRandom(
        double seedA,
        double seedB = 0.0,
        double seedC = 0.0,
        double seedD = 0.0,
        double seedE = 0.0)
    {
        int seed = CreateRandomSeed(seedA, seedB, seedC, seedD, seedE);
        return new Random(seed);
    }

    /// <summary>
    /// Clone of Utility.CreateDaySaveRandom, but with explicit inputs instead of Game1 globals.
    /// </summary>
    public static Random CreateDaySaveRandom(
        long daysPlayed,
        ulong uniqueIdForThisGame,
        double seedA = 0.0,
        double seedB = 0.0,
        double seedC = 0.0)
    {
        // In the real code: CreateRandom(Game1.stats.DaysPlayed, Game1.uniqueIDForThisGame / 2uL, seedA, seedB, seedC);
        return CreateRandom(daysPlayed, uniqueIdForThisGame / 2.0, seedA, seedB, seedC);
    }

    /// <summary>
    /// Clone of Utility.Shuffle(Random, List&lt;T&gt;).
    /// </summary>
    public static void Shuffle<T>(Random rng, IList<T> list)
    {
        int j = list.Count;
        while (j > 1)
        {
            int i = rng.Next(j--);
            T temp = list[j];
            list[j] = list[i];
            list[i] = temp;
        }
    }

    /// <summary>
    /// Clone of Utility.Shuffle(Random, T[]).
    /// </summary>
    public static void Shuffle<T>(Random rng, T[] array)
    {
        int j = array.Length;
        while (j > 1)
        {
            int i = rng.Next(j--);
            T temp = array[j];
            array[j] = array[i];
            array[i] = temp;
        }
    }
    public static T? ChooseFrom<T>(this Random rng, IList<T> list)
    {
        if (list is null) throw new ArgumentNullException(nameof(list));
        if (list.Count == 0) return default;
        return list[rng.Next(list.Count)];
    }

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
                intervalSeed = Helper.GetDaysPlayed(year, season, dayOfMonth);
                break;

            case "season":
                // Game1.currentSeason is the lowercase season string: "spring"/"summer"/"fall"/"winter"
                intervalSeed = HashUtility.GetDeterministicHashCode(Helper.GetSeasonName(season) + year);
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

}
