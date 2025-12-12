using System;
using System.Collections.Generic;

namespace StardewSeedSearch.Core;

public static class GreenRainPredictor
{
    // Returns the summer day-of-month (1–28) that will be Green Rain, for a given year & game ID.
    public static int PredictGreenRainDay(int year, ulong gameId)
    {
        // Mirror: CreateRandom(Game1.year * 777, Game1.uniqueIDForThisGame);
        var rng = StardewRng.CreateRandom(year * 777, gameId);

        int[] possibleDays = { 5, 6, 7, 14, 15, 16, 18, 23 };

        return ChooseFrom(rng, possibleDays);
    }

    // Small helper mirroring r.ChooseFrom(possible_days)
    private static T ChooseFrom<T>(Random rng, IReadOnlyList<T> list)
    {
        int index = rng.Next(list.Count); // 0..Count-1
        return list[index];
    }
}
