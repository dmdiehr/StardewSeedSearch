using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using StardewSeedSearch.Core;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public sealed class OrdersSimulatorPerfomanceTests
{
    private readonly ITestOutputHelper _output;
    public OrdersSimulatorPerfomanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Perf_ScanSeeds_Week18_TownAndQi_Sequential_And_Parallel()
    {
        // ----------------- CONFIG -----------------
        ulong startGameId = 0;          // set this
        long seedCount = 1_000_000;     // adjust (start smaller, then increase)
        const int targetWeek = 18;

        // Parallel settings
        int degree = Environment.ProcessorCount; // or set explicitly
        // ------------------------------------------

        // Warmup (JIT etc.)
        Warmup(startGameId, targetWeek);

        // 1) Sequential
        {
            long matches = 0;
            var sw = Stopwatch.StartNew();

            for (long i = 0; i < seedCount; i++)
            {
                ulong gameId = startGameId + (ulong)i;

                if (SpecialOrderSimulator.CanCompleteTownPerfectionByWeek(gameId, targetWeek) &&
                    QiSpecialOrderSimulator.CanCompleteQiPerfectionByWeek(gameId, targetWeek, startWeekIndex: 14))
                {
                    matches++;
                }
            }

            sw.Stop();
            PrintStats("Sequential", seedCount, matches, sw.Elapsed);
        }

        // 2) Parallel (range partitioning, low contention)
        {
            long matches = 0;
            var sw = Stopwatch.StartNew();

            Parallel.ForEach(
                Partitioner.Create(0L, seedCount, Math.Max(50_000, seedCount / (degree * 8))),
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                range =>
                {
                    long localMatches = 0;

                    for (long i = range.Item1; i < range.Item2; i++)
                    {
                        ulong gameId = startGameId + (ulong)i;

                        if (SpecialOrderSimulator.CanCompleteTownPerfectionByWeek(gameId, targetWeek) &&
                            QiSpecialOrderSimulator.CanCompleteQiPerfectionByWeek(gameId, targetWeek, startWeekIndex: 14))
                        {
                            localMatches++;
                        }
                    }

                    if (localMatches != 0)
                        Interlocked.Add(ref matches, localMatches);
                });

            sw.Stop();
            PrintStats($"Parallel (deg={degree})", seedCount, matches, sw.Elapsed);
        }
    }

    private void Warmup(ulong startGameId, int targetWeek)
    {
        // A few calls so JIT and lazy loads happen before timing
        for (int k = 0; k < 10; k++)
        {
            ulong gameId = startGameId + (ulong)k;
            _ = SpecialOrderSimulator.CanCompleteTownPerfectionByWeek(gameId, targetWeek);
            _ = QiSpecialOrderSimulator.CanCompleteQiPerfectionByWeek(gameId, targetWeek, startWeekIndex: 14);
        }
    }

    private void PrintStats(string label, long seeds, long matches, TimeSpan elapsed)
    {
        double seconds = Math.Max(1e-9, elapsed.TotalSeconds);
        double sps = seeds / seconds;

        _output.WriteLine($"[{label}]");
        _output.WriteLine($"  Seeds:   {seeds:n0}");
        _output.WriteLine($"  Matches: {matches:n0}");
        _output.WriteLine($"  Time:    {elapsed.TotalMilliseconds:n0} ms");
        _output.WriteLine($"  Rate:    {sps:n0} seeds/sec");
        _output.WriteLine("");
    }
}
