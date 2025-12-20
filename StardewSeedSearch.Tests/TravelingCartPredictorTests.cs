using Xunit;
using Xunit.Abstractions;
using StardewSeedSearch.Core;

namespace StardewSeedSearch.Tests;

public sealed class TravelingCartPredictorTests
{
    private readonly ITestOutputHelper _output;
    public TravelingCartPredictorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PrintCartStock_SmokeTest()
    {
        ulong gameId = 123456;
        long daysPlayed = 12;

        var stock = TravelingCartPredictor.GetCartStock(gameId, daysPlayed);

        _output.WriteLine("=== Random Objects ===");
        foreach (var o in stock.RandomObjects)
            _output.WriteLine($"{o.ItemId} {o.Name} - {o.Price}g x{o.Quantity}");

        _output.WriteLine("=== Furniture ===");
        _output.WriteLine($"{stock.Furniture.ItemId} {stock.Furniture.Name} - {stock.Furniture.Price}g x{stock.Furniture.Quantity}");
    }

    [Fact]
    public void TestRedFez()
    {
        ulong gameId = 123456;
        long  daysPlayed = Helper.GetDaysPlayedOneBased(1, Season.Winter, 14);
        bool result = false;

        if (TravelingCartPredictor.TryGetRedFez(gameId, daysPlayed, out var fez))
            result = true; 

        Assert.True(result);
    }
}
