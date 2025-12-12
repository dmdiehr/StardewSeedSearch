using System;
using System.Collections.Generic;

namespace StardewSeedSearch.Core.Helpers;

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
}
