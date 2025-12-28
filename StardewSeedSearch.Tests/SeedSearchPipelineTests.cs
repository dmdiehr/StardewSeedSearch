using StardewSeedSearch.Core;
using StardewSeedSearch.Core.Search;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;
public sealed class SeedSearchPipelineTests
{
    private readonly ITestOutputHelper _out;
    public SeedSearchPipelineTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Pipeline_Bulk()
    {
        var cfg = new SeedSearchPipeline.SeedSearchConfig
        {
            // Optional tuning knobs
            MaxDegreeOfParallelism = System.Environment.ProcessorCount,
            PartitionSize = 200_000,
            TopK = 50,

            // Your chosen tracked-item deadline for the per-item demands
            TrackedItemDeadlineDaysPlayed = 68,

            // 1) Remixed bundles -> tracked items + disqualified
            BundleTryScan = (ulong gid, Span<int> buf, out int found, out bool disq) =>
                RemixedBundleTrackedItemsScanner.TryScan(
                    uniqueIdForThisGame: gid,
                    outputBuffer: buf,
                    foundCount: out found,
                    disqualified: out disq,
                    disqualifyReason: out _),

            // 2) Town special orders completion gate
            CanCompleteTownPerfectionByWeek = (gid, targetWeek) =>
                SpecialOrderSimulator.CanCompleteTownPerfectionByWeek(gid, targetWeek),

            // 3) Qi orders completion gate
            CanCompleteQiPerfectionByWeek = (gid, targetWeek, startWeek) =>
                QiSpecialOrderSimulator.CanCompleteQiPerfectionByWeek(gid, targetWeek, startWeek),
        };

        // Range to scan (adjust as desired)
        ulong start = 0;
        ulong end = 1_000_000;

        var result = SeedSearchPipeline.ScanRange(start, end, cfg);

        _out.WriteLine($"Range [{start:n0}, {end:n0})");
        _out.WriteLine($"Elapsed: {result.Elapsed.TotalSeconds:F2}s");
        _out.WriteLine($"Scanned: {result.SeedsScanned:n0} ({result.SeedsPerSecond:n0} seeds/sec)");
        _out.WriteLine($"Bundle disq: {result.BundleDisqualified:n0}");
        _out.WriteLine($"Orders fail: {result.OrdersFailed:n0}");
        _out.WriteLine($"Cart fail: {result.CartFailed:n0}");
        _out.WriteLine($"Hard pass:  {result.HardPassed:n0}");
        _out.WriteLine("");

        _out.WriteLine("Top candidates:");
        foreach (var c in result.TopCandidates)
        {
            _out.WriteLine($"seed={c.GameId} score={c.Score} " + $"weather=[{WeatherScoring.FormatWeatherMask(c.WeatherMask)}] " + $"bonusCart=[{OptionalCartBonusDefaults.FormatOptionalCartMask(c.OptionalCartMask)}]");
        }

        Assert.True(true);
    }
}
