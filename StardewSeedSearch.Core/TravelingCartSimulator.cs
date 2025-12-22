using System;
using System.Collections.Generic;

namespace StardewSeedSearch.Core;

public static class TravelingCartSimulator
{
    // Precomputed daysPlayed (0..111) where location is Forest cart in Y1.
    static readonly int[] forestDaysYear1 = [5,7,19,21,26,28,33,35,40,42,47,49,54,56,61,63,68,70,75,77,82,84,89,91,96,98,103,105,110,112];
    static readonly IReadOnlyList<RandomObjectCandidate> Candidates = TravelingCartPredictor.GetObjectCandidatesForAnalysis();

    // Accumulate total purchasable units (qty 1 or 5) for watched object IDs
    // by a given cutoff day (inclusive).
    public static void AccumulateUnitsUpTo(ulong gameId, int cutoffDaysPlayedInclusive,ReadOnlySpan<int> watchedObjectIds, Span<int> totalUnitsOut)
    {
        if (watchedObjectIds.Length != totalUnitsOut.Length)
            throw new ArgumentException("watchedObjectIds and totalUnitsOut must have the same length.");

        // caller can pass a stackalloc'd span; we assume it's already zeroed OR we clear it here:
        totalUnitsOut.Clear();

        for (int i = 0; i < forestDaysYear1.Length; i++)
        {
            int day = forestDaysYear1[i];
            if (day > cutoffDaysPlayedInclusive)
                break;

            ProcessOneCartDay(gameId, day, watchedObjectIds, totalUnitsOut);
        }
}

    private static void ProcessOneCartDay(ulong gameId, int daysPlayed, ReadOnlySpan<int> watchedIds, Span<int> totals)
    {
        var rng = StardewRng.CreateDaySaveRandom(daysPlayed, gameId);

        // Buffer of composite keys for stage-1 passing candidates:
        // high 32 bits = rng key, low 32 bits = candidate index (iteration order).
        // Sorting by composite => groups by key, and within a key the later index wins (overwrite semantics).
        var pool = System.Buffers.ArrayPool<ulong>.Shared;
        ulong[] composites = pool.Rent(Candidates.Count); // worst-case upper bound
        int count = 0;

        // Stage 1: iterate ALL objects, always consume rng.Next() once per entry.
        for (int i = 0; i < Candidates.Count; i++)
        {
            var c = Candidates[i];
            int key = rng.Next();

            if (!TravelingCartPredictor.ItemIdCheck(c))
                continue;

            composites[count++] = ((ulong)(uint)key << 32) | (uint)i;
        }

        Array.Sort(composites, 0, count);

        int selected = 0;

        // Walk groups by key; pick the last candidate index per key (overwrite-on-collision).
        uint currentKey = 0;
        int chosenIndexForKey = -1;
        bool haveKey = false;

        for (int j = 0; j < count; j++)
        {
            uint key = (uint)(composites[j] >> 32);
            int idx = (int)(composites[j] & 0xFFFFFFFF);

            if (!haveKey)
            {
                haveKey = true;
                currentKey = key;
                chosenIndexForKey = idx;
                continue;
            }

            if (key == currentKey)
            {
                // same key => overwrite; keep later index
                chosenIndexForKey = idx;
                continue;
            }

            // key changed => process the chosen candidate for the previous key
            if (TryConsumePickedCandidate(Candidates[chosenIndexForKey], rng, watchedIds, totals))
            {
                selected++;
                if (selected >= 10)
                    break;
            }

            currentKey = key;
            chosenIndexForKey = idx;
        }

        // process last key group if we didn't hit 10 yet
        if (selected < 10 && haveKey)
        {
            TryConsumePickedCandidate(Candidates[chosenIndexForKey], rng, watchedIds, totals);
        }

        pool.Return(composites, clearArray: false);
    }

    private static bool TryConsumePickedCandidate(
        RandomObjectCandidate c,
        Random rng,
        ReadOnlySpan<int> watchedIds,
        Span<int> totals)
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