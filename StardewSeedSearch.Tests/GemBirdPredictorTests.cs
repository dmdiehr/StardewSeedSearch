using StardewSeedSearch.Core;
using Xunit;
using System.Linq;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public class GemBirdPredictorTests
{
    [Fact]
    public void PredictForSave_ReturnsFourPlacements()
    {
        var placements = GemBirdPredictor.PredictForSave(123456789);

        Assert.Equal(4, placements.Count);
    }

    [Fact]
    public void PredictForSave_HasUniqueRegions()
    {
        var placements = GemBirdPredictor.PredictForSave(123456789);

        Assert.Equal(4, placements.Select(p => p.Region).Distinct().Count());
    }

    [Fact]
    public void PredictForSave_SameGameId_StableMapping()
    {
        var a = GemBirdPredictor.PredictForSave(987654321);
        var b = GemBirdPredictor.PredictForSave(987654321);

        Console.WriteLine(a.ToString());

        Assert.Equal(a, b);
    }

    [Fact]
    public void PredictForSave_KnownGameId_MatchesExpectedMapping()
    {
        // TODO: use a real game ID you verified in-game
        ulong gameId = 123456UL;

        var placements = GemBirdPredictor.PredictForSave(gameId);

        // Turn the list into a dictionary for easy lookup by region
        var byRegion = placements.ToDictionary(p => p.Region, p => p.Type);

        Assert.Equal(GemBirdPredictor.GemBirdType.Amethyst,     byRegion[GemBirdPredictor.IslandRegion.North]);
        Assert.Equal(GemBirdPredictor.GemBirdType.Emerald,      byRegion[GemBirdPredictor.IslandRegion.South]);
        Assert.Equal(GemBirdPredictor.GemBirdType.Aquamarine,   byRegion[GemBirdPredictor.IslandRegion.East]);
        Assert.Equal(GemBirdPredictor.GemBirdType.Topaz,        byRegion[GemBirdPredictor.IslandRegion.West]);
    }    
}

