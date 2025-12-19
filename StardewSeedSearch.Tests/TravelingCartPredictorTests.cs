using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Core.Tests;

public sealed class TravelingCartPredictorTests
{
    private readonly ITestOutputHelper _output;

    public TravelingCartPredictorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GetRandomItems_PrintsTenItems()
    {
        // Replace these with the values you're comparing against stardew-predictor.
        ulong gameId = 123456;
        long daysPlayed = 7;

        var predictor = new StardewSeedSearch.Core.TravelingCartPredictor();
        var items = predictor.GetRandomItems(gameId, daysPlayed);

        _output.WriteLine($"gameId={gameId}, daysPlayed={daysPlayed}");
        _output.WriteLine($"Selected {items.Count} items:");
        for (int i = 0; i < items.Count; i++)
            _output.WriteLine($"{i + 1,2}. {items[i]}");

        Assert.Equal(10, items.Count);

        // Optional: sanity-check the ID constraint you called out (2..789).
        Assert.All(items, it => Assert.InRange(it.Id, 2, 789));
    }
}
