using System;
using System.Linq;
using StardewSeedSearch.Core.SpecialOrders;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;
public sealed class QiOrderTests
{
    private readonly ITestOutputHelper output;
    public QiOrderTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Print_QiOrders_Weeks14To20()
    {
        ulong gameId = 1; // set this

        var schedule = new SpecialOrderSimSchedule(); // uses your defaults

        for (int week = 14; week <= 20; week++)
        {
            var offers = SpecialOrderPredictor.GetQiOrders(
                gameId: gameId,
                weekIndex: week);

            output.WriteLine($"Week {week}:");

            if (offers.Count == 0)
            {
                output.WriteLine("  (none)");
                continue;
            }

            foreach (var o in offers)
                output.WriteLine($"  - {o.DisplayName} | Key={o.Key} | Perf={o.RequiredForPerfection} | Rank={o.Rank}");
        }
    }

    [Fact]
    public void Sim_Print_Qi_Weeks14To20()
    {
        ulong gameId = 98; // set this

        var sim = QiSpecialOrderSimulator.Simulate(
            gameId: gameId,
            startWeekIndex: 14,
            endWeekIndex: 20,
            schedule: new SpecialOrderSimSchedule());

        foreach (var w in sim.Weeks)
        {
            output.WriteLine($"Week {w.WeekIndex} ({w.Season} {w.DayOfMonth}, DaysPlayed={w.MondayDaysPlayed}):");

            foreach (var o in w.Offers)
                output.WriteLine($"  Offer: {o.DisplayName} | Key={o.Key} | Perf={o.RequiredForPerfection} | Rank={o.Rank}");

            output.WriteLine($"  Chosen: {(w.ChosenKey ?? "(none)")}");
            output.WriteLine($"  Active: {(w.ActiveAfterChoice.Count == 0 ? "(none)" : string.Join(", ", w.ActiveAfterChoice.Select(a => $"{a.Key}@{a.ExpiresOnDaysPlayed}")))}");
            output.WriteLine("");
        }
    }

    [Fact]
    public void CanCompleteQiPerfectionByWeek_IsDeterministic()
    {
        ulong startGameId = 0; // set this
        int seedCount = 100;
        int targetWeek = 20;

        for (ulong i = 0; i < (ulong)seedCount; i++)
        {
            ulong gameId = startGameId + i;

            bool a = QiSpecialOrderSimulator.CanCompleteQiPerfectionByWeek(gameId, targetWeekIndex: targetWeek, startWeekIndex: 14);
            bool b = QiSpecialOrderSimulator.CanCompleteQiPerfectionByWeek(gameId, targetWeekIndex: targetWeek, startWeekIndex: 14);

            Assert.Equal(a, b);
            output.WriteLine($"Seed {gameId}: {a}");
        }
    }

}
