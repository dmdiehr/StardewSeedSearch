using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using StardewSeedSearch.Core;

namespace StardewSeedSearch.Tests;

public sealed class TravelingCartSimulatorTests
{
    private readonly ITestOutputHelper _output;
    public TravelingCartSimulatorTests(ITestOutputHelper output) => _output = output;


    [Fact]
    public void SeedSatisfiesDemands_Example()
    {
        ulong gameId = 123456789UL;

        // Replace these IDs with your actual object IDs:
        const int Nautilus_Shell = 392;
        const int Holly = 283;
        const int Snow_Yam = 416;
        const int Crocus = 418;
        const int Coffee_Bean = 433;
        const int Autumns_Bounty = 235;
        const int Glazed_Yams = 208;
        const int Green_Tea = 614;
        const int Vegetable_Medley = 200;
        const int Cranberry_Candy = 238;
        const int Grape = 301;
        const int Pink_Cake = 211;
        const int Snail = 721;
        const int Coconut = 88;
        const int Fruit_Salad = 610;
        const int Sunflower = 421;
        const int Fried_Calamari = 202;
        const int Fairy_Rose = 595;
        const int Plum_Pudding = 604;
        const int Red_Cabbage_Seeds = 485;
        const int Red_Cabbage = 266;
        const int Garlic_Seeds = 476;
        const int Garlic = 248;
        const int Rabbits_Foot = 466;
        const int Truffle = 430;
        const int Caviar = 445;
        const int Duck_Feather = 444;
        const int Complete_Breakfast = 201;
        const int Salmon_Dinner = 212;
        const int Hot_Pepper = 260;

        var demands = new[]
        {
            new Demand(DeadlineDaysPlayed: 28, Quantity: 1, OptionsObjectIds: [Coffee_Bean]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Nautilus_Shell]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Red_Cabbage, Red_Cabbage_Seeds]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Truffle]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Rabbits_Foot]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Holly]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Crocus]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Snow_Yam]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Duck_Feather]),
            new Demand(DeadlineDaysPlayed: 68, Quantity: 1, OptionsObjectIds: [Plum_Pudding]),
            new Demand(DeadlineDaysPlayed: 112, Quantity: 2, OptionsObjectIds: [Rabbits_Foot]),
            new Demand(DeadlineDaysPlayed: 112, Quantity: 2, OptionsObjectIds: [Caviar]),
            new Demand(DeadlineDaysPlayed: 10, Quantity: 1, OptionsObjectIds: [Pink_Cake, Snail, Grape, Cranberry_Candy]),
            new Demand(DeadlineDaysPlayed: 32, Quantity: 1, OptionsObjectIds: [Fairy_Rose, Pink_Cake, Plum_Pudding]),
            new Demand(DeadlineDaysPlayed: 26, Quantity: 1, OptionsObjectIds: [Fried_Calamari]),
            new Demand(DeadlineDaysPlayed: 7, Quantity: 1, OptionsObjectIds: [Autumns_Bounty, Glazed_Yams, Green_Tea, Hot_Pepper, Vegetable_Medley]),
            new Demand(DeadlineDaysPlayed: 84, Quantity: 1, OptionsObjectIds: [Garlic, Garlic_Seeds]),
            new Demand(DeadlineDaysPlayed: 14, Quantity: 1, OptionsObjectIds: [Coconut, Fruit_Salad, Pink_Cake, Sunflower]),
            new Demand(DeadlineDaysPlayed: 40, Quantity: 1, OptionsObjectIds: [Complete_Breakfast, Salmon_Dinner])

        };

        // watched IDs = union of everything referenced
        var watched = demands.SelectMany(d => d.OptionsObjectIds).Distinct().ToArray();

        bool ok = CartSeedDemandEvaluator.SeedSatisfiesDemandsY1Forest(gameId, demands, watched);

        _output.WriteLine($"Seed {gameId} => {(ok ? "PASS" : "FAIL")}");
    }


    [Fact]
    public void BulkScanGameGameIds_FindSomeMatches()
    {
        // --- demands (exactly your list) ---
        const int Nautilus_Shell = 392;
        const int Holly = 283;
        const int Snow_Yam = 416;
        const int Crocus = 418;
        const int Coffee_Bean = 433;
        const int Autumns_Bounty = 235;
        const int Glazed_Yams = 208;
        const int Green_Tea = 614;
        const int Vegetable_Medley = 200;
        const int Cranberry_Candy = 238;
        const int Grape = 301;
        const int Pink_Cake = 211;
        const int Snail = 721;
        const int Coconut = 88;
        const int Fruit_Salad = 610;
        const int Sunflower = 421;
        const int Fried_Calamari = 202;
        const int Fairy_Rose = 595;
        const int Plum_Pudding = 604;
        const int Red_Cabbage_Seeds = 485;
        const int Red_Cabbage = 266;
        const int Garlic_Seeds = 476;
        const int Garlic = 248;
        const int Rabbits_Foot = 466;
        const int Truffle = 430;
        const int Caviar = 445;
        const int Duck_Feather = 444;
        const int Complete_Breakfast = 201;
        const int Salmon_Dinner = 212;
        const int Hot_Pepper = 260;

        var demands = new[]
        {
            // new Demand(28,  1, new[] { Coffee_Bean }),
            new Demand(68,  1, new[] { Nautilus_Shell }),
            new Demand(68,  1, new[] { Red_Cabbage, Red_Cabbage_Seeds }),
            // new Demand(68,  1, new[] { Truffle }),
            // new Demand(68,  1, new[] { Rabbits_Foot }),
            new Demand(68,  1, new[] { Holly }),
            new Demand(68,  1, new[] { Crocus }),
            new Demand(68,  1, new[] { Snow_Yam }),
            new Demand(68,  1, new[] { Duck_Feather }),
            new Demand(68,  1, new[] { Plum_Pudding }),
            // new Demand(112, 2, new[] { Rabbits_Foot }),
            // new Demand(112, 2, new[] { Caviar }),
            // new Demand(10,  1, new[] { Pink_Cake, Snail, Grape, Cranberry_Candy }),
            // new Demand(32,  1, new[] { Fairy_Rose, Pink_Cake, Plum_Pudding }),
            // new Demand(26,  1, new[] { Fried_Calamari }),
            // new Demand(7,   1, new[] { Autumns_Bounty, Glazed_Yams, Green_Tea, Hot_Pepper, Vegetable_Medley }),
            // new Demand(84,  1, new[] { Garlic, Garlic_Seeds }),
            // new Demand(14,  1, new[] { Coconut, Fruit_Salad, Pink_Cake, Sunflower }),
            // new Demand(40,  1, new[] { Complete_Breakfast, Salmon_Dinner }),
        };

        var orderedDemands = demands.OrderBy(d => d.DeadlineDaysPlayed).ToArray();

        // watched IDs = union of everything referenced
        int[] watched = demands.SelectMany(d => d.OptionsObjectIds).Distinct().ToArray();

        // --- scan settings ---
        const ulong startGameId = 0;
        const int countToScan = 1_000;

        int matches = 0;
        ulong firstMatch = 0;

        var sw = Stopwatch.StartNew();

        for (ulong gameId = startGameId; gameId < startGameId + (ulong)countToScan; gameId++)
        {
            var plan = CartDemandPlanCompiler.Compile(orderedDemands);

            if (!CartSeedDemandEvaluator.SeedPassesEarlyFeasibilityPruneY1Forest(gameId, plan))
                continue;

            if (CartSeedDemandEvaluator.SeedSatisfiesDemandsY1Forest(gameId, orderedDemands, watched))
            {
                matches++;
                if (firstMatch == 0) firstMatch = gameId;
            }
        }

        sw.Stop();

        double secs = Math.Max(1e-9, sw.Elapsed.TotalSeconds);
        double rate = countToScan / secs;

        _output.WriteLine($"Scanned {countToScan:n0} gameIds starting at {startGameId:n0}");
        _output.WriteLine($"Matches: {matches:n0} (first={firstMatch})");
        _output.WriteLine($"Time: {sw.Elapsed.TotalSeconds:n3}s => {rate:n0} seeds/sec");
    }

    [Fact]
    public void Estimate_PB_given_A_Y1Forest()
    {

            // Put your const ids here or reference them from your existing test file.

        // ----- Demand Set A (earliest 7) -----
        // (uses your const int ids from your other test)
        Demand[] demandsA =
        {
            new Demand(7,   1, new[] { Autumns_Bounty, Glazed_Yams, Green_Tea, Hot_Pepper, Vegetable_Medley }),
            new Demand(10,  1, new[] { Pink_Cake, Snail, Grape, Cranberry_Candy }),
            new Demand(14,  1, new[] { Coconut, Fruit_Salad, Pink_Cake, Sunflower }),
            new Demand(26,  1, new[] { Fried_Calamari }),
            new Demand(28,  1, new[] { Coffee_Bean }),
            new Demand(32,  1, new[] { Fairy_Rose, Pink_Cake, Plum_Pudding }),
            new Demand(40,  1, new[] { Complete_Breakfast, Salmon_Dinner }),
        };

        // ----- Demand Set B (later 12) -----
        Demand[] demandsB =
        {
            new Demand(68,  1, new[] { Nautilus_Shell }),
            new Demand(68,  1, new[] { Red_Cabbage, Red_Cabbage_Seeds }),
            new Demand(68,  1, new[] { Truffle }),
            new Demand(68,  1, new[] { Rabbits_Foot }),
            new Demand(68,  1, new[] { Holly }),
            new Demand(68,  1, new[] { Crocus }),
            new Demand(68,  1, new[] { Snow_Yam }),
            new Demand(68,  1, new[] { Duck_Feather }),
            new Demand(68,  1, new[] { Plum_Pudding }),
            new Demand(84,  1, new[] { Garlic, Garlic_Seeds }),
            new Demand(112, 2, new[] { Rabbits_Foot }),
            new Demand(112, 2, new[] { Caviar }),
        };

        // Ensure deadline order (your lists already are, but keep it robust)
        var demandsASorted = demandsA.OrderBy(d => d.DeadlineDaysPlayed).ToArray();
        var demandsBSorted = demandsB.OrderBy(d => d.DeadlineDaysPlayed).ToArray();

        // watched sets (separate, to keep totals buffers small)
        int[] watchedA = demandsASorted.SelectMany(d => d.OptionsObjectIds).Distinct().ToArray();
        int[] watchedB = demandsBSorted.SelectMany(d => d.OptionsObjectIds).Distinct().ToArray();

        // Scan configuration
        const ulong startId = 1UL;
        const int scanCount = 1_000_000; // bump this up (10M, 100M) when ready
        const int maxTrueHitsToCollect = 25; // just for printing some examples

        long aHits = 0;
        long abHits = 0;

        var exampleAB = new ConcurrentBag<ulong>();

        var sw = Stopwatch.StartNew();

        Parallel.For(0, scanCount,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },

            // Thread-local buffers (NO ArrayPool per seed)
            () =>
            {
                var totalsA = new int[watchedA.Length];
                var totalsB = new int[watchedB.Length];
                var composites = new ulong[TravelingCartSimulator.Candidates.Count];
                return (totalsA, totalsB, composites);
            },

            (i, state, local) =>
            {
                ulong gameId = startId + (ulong)i;

                // ---- A: early prune ----
                if (!CartSeedDemandEvaluator.SeedPassesEarlyFeasibilityPruneY1Forest(
                        gameId, demandsASorted, watchedA, local.totalsA, local.composites))
                {
                    return local;
                }

                // ---- A: full solve (rare) ----
                if (!CartSeedDemandEvaluator.SeedSatisfiesDemandsY1Forest(gameId, demandsASorted, watchedA))
                    return local;

                Interlocked.Increment(ref aHits);

                // ---- B: early prune (only for A hits) ----
                if (!CartSeedDemandEvaluator.SeedPassesEarlyFeasibilityPruneY1Forest(
                        gameId, demandsBSorted, watchedB, local.totalsB, local.composites))
                {
                    return local;
                }

                // ---- B: full solve ----
                if (!CartSeedDemandEvaluator.SeedSatisfiesDemandsY1Forest(gameId, demandsBSorted, watchedB))
                    return local;

                Interlocked.Increment(ref abHits);

                if (exampleAB.Count < maxTrueHitsToCollect)
                    exampleAB.Add(gameId);

                return local;
            },

            _ => { });

        sw.Stop();

        double seconds = Math.Max(1e-9, sw.Elapsed.TotalSeconds);
        _output.WriteLine($"Scanned {scanCount:n0} seeds in {seconds:n3}s  =>  {scanCount / seconds:n0} seeds/sec");
        _output.WriteLine($"A hits:  {aHits:n0}  (P(A) ≈ {((double)aHits / scanCount):G6})");
        _output.WriteLine($"AB hits: {abHits:n0}  (P(A∩B) ≈ {((double)abHits / scanCount):G6})");

        if (aHits > 0)
        {
            double pBgivenA = (double)abHits / aHits;
            _output.WriteLine($"P(B|A) ≈ {pBgivenA:G6}");
            if (abHits > 0)
                _output.WriteLine($"Estimated 1 / P(A∩B) ≈ {1.0 / ((double)abHits / scanCount):n0} seeds");
        }

        foreach (var id in exampleAB.OrderBy(x => x).Take(maxTrueHitsToCollect))
            _output.WriteLine($"AB seed: {id}");
    }



    [Fact]
    public void Scan1M_Parallel_EarlyPrune()
    {
        // --- demands (exactly your list) ---
        const int Nautilus_Shell = 392;
        const int Holly = 283;
        const int Snow_Yam = 416;
        const int Crocus = 418;
        const int Coffee_Bean = 433;
        const int Autumns_Bounty = 235;
        const int Glazed_Yams = 208;
        const int Green_Tea = 614;
        const int Vegetable_Medley = 200;
        const int Cranberry_Candy = 238;
        const int Grape = 301;
        const int Pink_Cake = 211;
        const int Snail = 721;
        const int Coconut = 88;
        const int Fruit_Salad = 610;
        const int Sunflower = 421;
        const int Fried_Calamari = 202;
        const int Fairy_Rose = 595;
        const int Plum_Pudding = 604;
        const int Red_Cabbage_Seeds = 485;
        const int Red_Cabbage = 266;
        const int Garlic_Seeds = 476;
        const int Garlic = 248;
        const int Rabbits_Foot = 466;
        const int Truffle = 430;
        const int Caviar = 445;
        const int Duck_Feather = 444;
        const int Complete_Breakfast = 201;
        const int Salmon_Dinner = 212;
        const int Hot_Pepper = 260;

        var demands = new[]
        {
            // new Demand(28,  1, new[] { Coffee_Bean }),
            new Demand(68,  1, new[] { Nautilus_Shell }),
            new Demand(68,  1, new[] { Red_Cabbage, Red_Cabbage_Seeds }),
            // new Demand(68,  1, new[] { Truffle }),
            // new Demand(68,  1, new[] { Rabbits_Foot }),
            new Demand(68,  1, new[] { Holly }),
            new Demand(68,  1, new[] { Crocus }),
            new Demand(68,  1, new[] { Snow_Yam }),
            new Demand(68,  1, new[] { Duck_Feather }),
            new Demand(68,  1, new[] { Plum_Pudding }),
            // new Demand(112, 2, new[] { Rabbits_Foot }),
            // new Demand(112, 2, new[] { Caviar }),
            // new Demand(10,  1, new[] { Pink_Cake, Snail, Grape, Cranberry_Candy }),
            // new Demand(32,  1, new[] { Fairy_Rose, Pink_Cake, Plum_Pudding }),
            // new Demand(26,  1, new[] { Fried_Calamari }),
            // new Demand(7,   1, new[] { Autumns_Bounty, Glazed_Yams, Green_Tea, Hot_Pepper, Vegetable_Medley }),
            // new Demand(84,  1, new[] { Garlic, Garlic_Seeds }),
            // new Demand(14,  1, new[] { Coconut, Fruit_Salad, Pink_Cake, Sunflower }),
            // new Demand(40,  1, new[] { Complete_Breakfast, Salmon_Dinner }),
        };

        // Pre-sort once (VERY important)
        var demandsSorted = demands.OrderBy(d => d.DeadlineDaysPlayed).ToArray();

        // watched = union once
        var watched = demandsSorted.SelectMany(d => d.OptionsObjectIds).Distinct().ToArray();

        const ulong startId = 1UL;
        const int count = 100_000;
        const int maxHitsToCollect = 1000; // keep small so collection overhead stays tiny

        var hits = new ConcurrentBag<ulong>();
        int hitCount = 0;

        var sw = Stopwatch.StartNew();

        Parallel.For(0, count,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            // thread-local init: allocate buffers once per worker
            () =>
            {
                var totals = new int[watched.Length];
                var composites = new ulong[TravelingCartSimulator.Candidates.Count];
                return (totals, composites);
            },
            (i, state, local) =>
            {
                if (Volatile.Read(ref hitCount) >= maxHitsToCollect)
                {
                    state.Stop();
                    return local;
                }

                ulong gameId = startId + (ulong)i;

                bool ok = CartSeedDemandEvaluator.SeedPassesEarlyFeasibilityPruneY1Forest(
                    gameId,
                    demandsSorted,
                    watched,
                    local.totals,
                    local.composites);

                if (ok)
                {
                    if (CartSeedDemandEvaluator.SeedSatisfiesDemandsY1Forest(gameId, demandsSorted, watched))
                    {
                        hits.Add(gameId);
                        if (Interlocked.Increment(ref hitCount) >= maxHitsToCollect)
                            state.Stop();
                    }


                }

                return local;
            },
            localFinally: _ => { });

        sw.Stop();

        _output.WriteLine($"Scanned {count:n0} seeds in {sw.Elapsed.TotalSeconds:n3}s");
        _output.WriteLine($"Rate: {count / sw.Elapsed.TotalSeconds:n0} seeds/sec (parallel)");
        _output.WriteLine($"Survivors collected: {hits.Count}");
    }

    private static Demand[] BuildDemands()
    {
        // --- demands (exactly your list) ---
        const int Nautilus_Shell = 392;
        const int Holly = 283;
        const int Snow_Yam = 416;
        const int Crocus = 418;
        const int Coffee_Bean = 433;
        const int Autumns_Bounty = 235;
        const int Glazed_Yams = 208;
        const int Green_Tea = 614;
        const int Vegetable_Medley = 200;
        const int Cranberry_Candy = 238;
        const int Grape = 301;
        const int Pink_Cake = 211;
        const int Snail = 721;
        const int Coconut = 88;
        const int Fruit_Salad = 610;
        const int Sunflower = 421;
        const int Fried_Calamari = 202;
        const int Fairy_Rose = 595;
        const int Plum_Pudding = 604;
        const int Red_Cabbage_Seeds = 485;
        const int Red_Cabbage = 266;
        const int Garlic_Seeds = 476;
        const int Garlic = 248;
        const int Rabbits_Foot = 466;
        const int Truffle = 430;
        const int Caviar = 445;
        const int Duck_Feather = 444;
        const int Complete_Breakfast = 201;
        const int Salmon_Dinner = 212;
        const int Hot_Pepper = 260;

        var demands = new[]
        {
            new Demand(7,   1, new[] { Autumns_Bounty, Glazed_Yams, Green_Tea, Hot_Pepper, Vegetable_Medley }),
            new Demand(10,  1, new[] { Pink_Cake, Snail, Grape, Cranberry_Candy }),
            new Demand(14,  1, new[] { Coconut, Fruit_Salad, Pink_Cake, Sunflower }),
            new Demand(26,  1, new[] { Fried_Calamari }),
            new Demand(28,  1, new[] { Coffee_Bean }),
            new Demand(32,  1, new[] { Fairy_Rose, Pink_Cake, Plum_Pudding }),
            new Demand(40,  1, new[] { Complete_Breakfast, Salmon_Dinner }),
            new Demand(68,  1, new[] { Nautilus_Shell }),
            new Demand(68,  1, new[] { Red_Cabbage, Red_Cabbage_Seeds }),
            new Demand(68,  1, new[] { Truffle }),
            new Demand(68,  1, new[] { Rabbits_Foot }),
            new Demand(68,  1, new[] { Holly }),
            new Demand(68,  1, new[] { Crocus }),
            new Demand(68,  1, new[] { Snow_Yam }),
            new Demand(68,  1, new[] { Duck_Feather }),
            new Demand(68,  1, new[] { Plum_Pudding }),
            new Demand(84,  1, new[] { Garlic, Garlic_Seeds }),
            new Demand(112, 2, new[] { Rabbits_Foot }),
            new Demand(112, 2, new[] { Caviar }),
        };

        return demands;
    }


    private const int Nautilus_Shell = 392;
    private const int Holly = 283;
    private const int Snow_Yam = 416;
    private const int Crocus = 418;
    private const int Coffee_Bean = 433;
    private const int Autumns_Bounty = 235;
    private const int Glazed_Yams = 208;
    private const int Green_Tea = 614;
    private const int Vegetable_Medley = 200;
    private const int Cranberry_Candy = 238;
    private const int Grape = 301;
    private const int Pink_Cake = 211;
    private const int Snail = 721;
    private const int Coconut = 88;
    private const int Fruit_Salad = 610;
    private const int Sunflower = 421;
    private const int Fried_Calamari = 202;
    private const int Fairy_Rose = 595;
    private const int Plum_Pudding = 604;
    private const int Red_Cabbage_Seeds = 485;
    private const int Red_Cabbage = 266;
    private const int Garlic_Seeds = 476;
    private const int Garlic = 248;
    private const int Rabbits_Foot = 466;
    private const int Truffle = 430;
    private const int Caviar = 445;
    private const int Duck_Feather = 444;
    private const int Complete_Breakfast = 201;
    private const int Salmon_Dinner = 212;
    private const int Hot_Pepper = 260;

}


