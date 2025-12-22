using Xunit;
using Xunit.Abstractions;
using StardewSeedSearch.Core;

namespace StardewSeedSearch.Tests;

public sealed class TravelingCartSimulatorTests
{
    private readonly ITestOutputHelper _output;
    public TravelingCartSimulatorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AccumulateUnitsUpTo_Smoke()
    {
        ulong gameId = 123456;
        int cutoff = 112; // daysPlayed inclusive (1-based)

        // object numeric IDs from Objects.json
        int[] watched = [392,283,416,418,433,235,208,614,200,238,301,211,721,88,610,421,202,595,604,485,266,476,248,466,430,445,787,444,201,212];
        Span<int> totals = stackalloc int[watched.Length];

        TravelingCartSimulator.AccumulateUnitsUpTo(gameId, cutoff, watched, totals);

        _output.WriteLine($"GameId={gameId} cutoffDaysPlayed={cutoff}");
        for (int i = 0; i < watched.Length; i++)
            _output.WriteLine($"{watched[i]} totalUnits={totals[i]}");
    }
}
