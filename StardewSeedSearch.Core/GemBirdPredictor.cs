using System.Collections.Generic;
using System.Linq;

namespace StardewSeedSearch.Core;

public static class GemBirdPredictor
{
    public enum IslandRegion
    {
        North,
        South,
        East,
        West
    }

	public enum GemBirdType
	{
		Emerald,
		Aquamarine,
		Ruby,
		Amethyst,
		Topaz,
		MAX
	}

    public record Placement(IslandRegion Region, GemBirdType Type);

   public static IReadOnlyList<Placement> PredictForSave(ulong gameId)
   {
        var rng = StardewRng.CreateRandom(gameId);

        var types = Enumerable.Range(0, 5)
            .Select(i => (GemBirdType)i)
            .ToList();

        StardewRng.Shuffle(rng, types);

        var regions = new[]
        {
            IslandRegion.North,
            IslandRegion.South,
            IslandRegion.East,
            IslandRegion.West
        };

        var placements = new List<Placement>();
        for (int i = 0; i < regions.Length; i++)
            placements.Add(new Placement(regions[i], types[i]));

        return placements;
    }

}


