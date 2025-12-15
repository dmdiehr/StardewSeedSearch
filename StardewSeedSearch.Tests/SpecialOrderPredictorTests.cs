using StardewSeedSearch.Core.SpecialOrders;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;
public class SpecialOrderPredictorTests
{

    private readonly ITestOutputHelper output;

    public SpecialOrderPredictorTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TownOrders_Deterministic()
    {
        var a = SpecialOrderPredictor.GetTownOrders(
            gameId: 123UL,
            weekIndex: 12,
            gingerIslandUnlocked: false,
            islandResortUnlocked: false,
            sewingMachineUnlocked: true,
            completedSpecialOrders: new[] { "Pam" },
            activeSpecialOrders: Array.Empty<string>());

        var b = SpecialOrderPredictor.GetTownOrders(
            gameId: 123UL,
            weekIndex: 12,
            gingerIslandUnlocked: false,
            islandResortUnlocked: false,
            sewingMachineUnlocked: true,
            completedSpecialOrders: new[] { "Pam" },
            activeSpecialOrders: Array.Empty<string>());

        Assert.Equal(a, b);
    }

    [Fact]
    public void Print_TownOrders_Weeks9To19()
    {
        // TODO: set these to match the save you're comparing against
        ulong gameId = 411236192; // <-- put your uniqueIDForThisGame here

        bool gingerIslandUnlocked = false;
        bool islandResortUnlocked = false;
        bool sewingMachineUnlocked = false;

        var completed = Array.Empty<string>();
        var active = Array.Empty<string>();

        for (int week = 9; week <= 19; week++)
        {
            var offers = SpecialOrderPredictor.GetTownOrders(
                gameId: gameId,
                weekIndex: week,
                gingerIslandUnlocked: gingerIslandUnlocked,
                islandResortUnlocked: islandResortUnlocked,
                sewingMachineUnlocked: sewingMachineUnlocked,
                completedSpecialOrders: completed,
                activeSpecialOrders: active);

            string offerText = offers.Count == 0
                ? "(none)"
                : string.Join(", ", offers.Select(o => $"{o.Key} (seed {o.GenerationSeed})"));

            output.WriteLine($"Week {week}: {offerText}");
        }
    }
}
