using System;
using System.Linq;
using StardewSeedSearch.Core.SpecialOrders;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public sealed class SpecialOrderPredictor_PrintWeeksTests
{
    private readonly ITestOutputHelper _output;

    public SpecialOrderPredictor_PrintWeeksTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Print_TownOrders_Weeks9To20()
    {
        // TODO: set these to match the save you're comparing against
        ulong gameId = 0; // uniqueIDForThisGame

        bool gingerIslandUnlocked = false;
        bool islandResortUnlocked = false;
        bool sewingMachineUnlocked = false;

        var completed = Array.Empty<string>();
        var active = Array.Empty<string>();

        for (int week = 9; week <= 20; week++)
        {
            var offers = SpecialOrderPredictor.GetTownOrders(
                gameId: gameId,
                weekIndex: week,
                gingerIslandUnlocked: gingerIslandUnlocked,
                islandResortUnlocked: islandResortUnlocked,
                sewingMachineUnlocked: sewingMachineUnlocked,
                completedSpecialOrders: completed,
                activeSpecialOrders: active);

            _output.WriteLine($"Week {week}:");

            if (offers.Count == 0)
            {
                _output.WriteLine("  (none)");
                continue;
            }

            foreach (var o in offers)
            {
                var item = string.IsNullOrWhiteSpace(o.OrderItem) ? "" : $" | Item: {o.OrderItem}";
                _output.WriteLine(
                    $"  - {o.DisplayName}{item} | Perfection: {o.RequiredForPerfection} | Rank: {o.Rank} | Key: {o.Key}");
            }
        }
    }
}
