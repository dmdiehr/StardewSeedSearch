using System;
using System.Collections.Generic;

namespace StardewSeedSearch.Core;

public static class TravelingCartSimulator
{
    // Precomputed daysPlayed (0..111) where location is Forest cart in Y1.
    private static readonly int[] forestDaysYear1 = [5,7,19,21,26,28,33,35,40,42,47,49,54,56,61,63,68,70,75,77,82,84,89,91,96,98,103,105,110,112];
    public static ReadOnlySpan<int> ForestDaysYear1 => forestDaysYear1;

    public static readonly IReadOnlyList<RandomObjectCandidate> Candidates = TravelingCartPredictor.GetObjectCandidatesForAnalysis();

    public static void AccumulateDailyUnitsUpTo(
        ulong gameId,
        int cutoffDaysPlayedInclusive,
        ReadOnlySpan<int> watchedObjectIds,
        Span<int> dailyUnitsOut)
    {
        int watchedCount = watchedObjectIds.Length;

        int dayCount = 0;
        while (dayCount < forestDaysYear1.Length && forestDaysYear1[dayCount] <= cutoffDaysPlayedInclusive)
            dayCount++;

        if (dailyUnitsOut.Length != dayCount * watchedCount)
            throw new ArgumentException("dailyUnitsOut must be (numCartDaysUpToCutoff * watchedCount).");

        dailyUnitsOut.Clear();

        for (int d = 0; d < dayCount; d++)
        {
            int day = forestDaysYear1[d];
            var slice = dailyUnitsOut.Slice(d * watchedCount, watchedCount);
            ProcessOneCartDay(gameId, day, watchedObjectIds, slice);
        }
    }

    internal static void ProcessOneCartDay(ulong gameId, int daysPlayed, ReadOnlySpan<int> watchedIds, Span<int> totals)
    {
        var pool = System.Buffers.ArrayPool<ulong>.Shared;
        ulong[] buf = pool.Rent(Candidates.Count);
        try
        {
            ProcessOneCartDay(gameId, daysPlayed, watchedIds, totals, buf);
        }
        finally
        {
            pool.Return(buf, clearArray: false);
        }
    }

    internal static void ProcessOneCartDay(ulong gameId, int daysPlayed, ReadOnlySpan<int> watchedIds, Span<int> totals,ulong[] compositesBuffer)
{
    var rng = StardewRng.CreateDaySaveRandom(daysPlayed, gameId);

    int count = 0;

    for (int i = 0; i < Candidates.Count; i++)
    {
        var c = Candidates[i];
        int key = rng.Next();

        if (!TravelingCartPredictor.ItemIdCheck(c))
            continue;

        compositesBuffer[count++] = ((ulong)(uint)key << 32) | (uint)i;
    }

    Array.Sort(compositesBuffer, 0, count);

    int selected = 0;
    uint currentKey = 0;
    int chosenIndexForKey = -1;
    bool haveKey = false;

    for (int j = 0; j < count; j++)
    {
        uint key = (uint)(compositesBuffer[j] >> 32);
        int idx = (int)(compositesBuffer[j] & 0xFFFFFFFF);

        if (!haveKey)
        {
            haveKey = true;
            currentKey = key;
            chosenIndexForKey = idx;
            continue;
        }

        if (key == currentKey)
        {
            chosenIndexForKey = idx;
            continue;
        }

        if (TryConsumePickedCandidate(Candidates[chosenIndexForKey], rng, watchedIds, totals))
        {
            selected++;
            if (selected >= 10)
                return;
        }

        currentKey = key;
        chosenIndexForKey = idx;
    }

    if (haveKey)
        TryConsumePickedCandidate(Candidates[chosenIndexForKey], rng, watchedIds, totals);
}


    
    private static bool TryConsumePickedCandidate( RandomObjectCandidate c, Random rng, ReadOnlySpan<int> watchedIds, Span<int> totals)
    {
        if (!TravelingCartPredictor.PerItemConditionCheck(c))
            return false;

        // Consume RNG in the same order as the game/JS.
        _ = rng.Next(1, 11);
        _ = rng.Next(3, 6);
        int qty = (rng.NextDouble() < 0.1) ? 5 : 1;

        // Only update if watched. watchedIds length <= 25 so linear scan is fine.
        int id = c.Id;
        for (int i = 0; i < watchedIds.Length; i++)
        {
            if (watchedIds[i] == id)
            {
                totals[i] += qty; // qty is CAPACITY
                break;
            }
        }

        return true;
    }

}