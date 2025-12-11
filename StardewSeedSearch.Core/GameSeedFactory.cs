namespace StardewSeedSearch.Core;

/// <summary>
/// Responsible for generating seeds for different game systems.
/// Does NOT hard-code formulas yet; those will be filled in
/// once we extract them from decompiled Stardew code.
/// </summary>
public static class GameSeedFactory
{
    public static int ForDailyFeature(long gameId, int daysPlayed)
    {
        // TEMP placeholder formula, deterministic and testable.
        // Will replace with actual game logic later. Look at CreateRandomDaySave
        unchecked
        {
            // Combine values into a 32-bit int
            int seed = (int)(gameId ^ daysPlayed * 16777619);
            return seed;
        }
    }
}
