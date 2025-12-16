using System;
using System.Linq;
using StardewSeedSearch.Core.SpecialOrders;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public sealed class SpecialOrderSimulator_PrintTests
{
    private readonly ITestOutputHelper _output;
    public SpecialOrderSimulator_PrintTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Sim_Print_Town_Weeks9To20()
    {
        ulong gameId = 10019; // <-- set this

        var schedule = new SpecialOrderSimSchedule();

        var result = SpecialOrderSimulator.SimulateTown(
            gameId: gameId,
            startWeekIndex: 9,
            endWeekIndex: 20,
            schedule: schedule);

        foreach (var w in result.Weeks)
        {
            _output.WriteLine($"Week {w.WeekIndex} ({w.Season} {w.DayOfMonth}, DaysPlayed={w.MondayDaysPlayed}):");

            if (w.Offers.Count == 0)
            {
                _output.WriteLine("  Offers: (none)");
            }
            else
            {
                foreach (var o in w.Offers)
                    _output.WriteLine($"  Offer: {o.DisplayName} | Key={o.Key} | Perf={o.RequiredForPerfection} | Rank={o.Rank}");
            }

            _output.WriteLine($"  Chosen: {(w.ChosenKey ?? "(none)")}");
            _output.WriteLine($"  Active: {(w.ActiveAfterChoice.Count == 0 ? "(none)" : string.Join(", ", w.ActiveAfterChoice.Select(a => $"{a.Key}@{a.ExpiresOnDaysPlayed}")))}");
            _output.WriteLine($"  CompletedForGamePool: {w.CompletedForGamePool.Count}");
            _output.WriteLine($"  CompletedPerfection: {w.CompletedPerfection.Count}");
            _output.WriteLine("");
        }
    }
        private sealed record SeedResult(ulong GameId, int? CompletedWeek, int? CompletedDaysPlayed);
        [Fact]
        public void Scan_ConsecutiveSeeds_TownPerfectionCompletion_UpToWeek28()
        {
            // ---- Configure ----
            ulong startGameId = 2000;      // set this
            int seedCount = 10000;         // how many consecutive seeds to scan
            int startWeek = 9;
            int capWeek = 28;

            // Adjust these if you want to simulate unlock timing.
            // For now, keep "never" unless you want island-dependent orders to be eligible.
            var schedule = new SpecialOrderSimSchedule();
            // -------------------

            for (ulong offset = 0; offset < (ulong)seedCount; offset++)
            {
                ulong gameId = startGameId + offset;

                var sim = SpecialOrderSimulator.SimulateTown(
                    gameId: gameId,
                    startWeekIndex: startWeek,
                    endWeekIndex: capWeek,
                    schedule: schedule);

                if (sim.PerfectionCompletedWeekIndex is int w)
                {
                    _output.WriteLine($"Seed {gameId}: COMPLETED at week {w} (DaysPlayed={sim.PerfectionCompletedDaysPlayed})");
                }
                else
                {
                    _output.WriteLine($"Seed {gameId}: not completed by week {capWeek}");
                }
            }
        }

            [Fact]
            public void Scan_ConsecutiveSeeds_TownPerfection_Best10_ByWeek28()
            {
                // ---- Configure ----
                ulong startGameId = 10000;  // set this
                int seedCount = 10000;    // scan this many consecutive seeds
                int startWeek = 9;
                int capWeek = 28;

                var schedule = new SpecialOrderSimSchedule();
                // -------------------

                var results = new List<SeedResult>(seedCount);

                for (ulong offset = 0; offset < (ulong)seedCount; offset++)
                {
                    ulong gameId = startGameId + offset;

                    var sim = SpecialOrderSimulator.SimulateTown(
                        gameId: gameId,
                        startWeekIndex: startWeek,
                        endWeekIndex: capWeek,
                        schedule: schedule);

                    results.Add(new SeedResult(
                        GameId: gameId,
                        CompletedWeek: sim.PerfectionCompletedWeekIndex,
                        CompletedDaysPlayed: sim.PerfectionCompletedDaysPlayed));
                }

                var completed = results
                    .Where(r => r.CompletedWeek.HasValue)
                    .OrderBy(r => r.CompletedWeek!.Value)
                    .ThenBy(r => r.CompletedDaysPlayed ?? int.MaxValue)
                    .ThenBy(r => r.GameId)
                    .Take(10)
                    .ToList();

                _output.WriteLine($"Scanned {seedCount} seeds starting at {startGameId}.");
                _output.WriteLine($"Completed by week {capWeek}: {results.Count(r => r.CompletedWeek.HasValue)}");
                _output.WriteLine("");

                if (completed.Count == 0)
                {
                    _output.WriteLine("No seeds completed within the cap.");
                    return;
                }

                _output.WriteLine("Best 10 seeds:");
                for (int i = 0; i < completed.Count; i++)
                {
                    var r = completed[i];
                    _output.WriteLine(
                        $"{i + 1,2}. Seed {r.GameId}: week {r.CompletedWeek} (DaysPlayed={r.CompletedDaysPlayed})");
                }
            }
}
