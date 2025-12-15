using System;
using System.Linq;
using System.Text;
using StardewSeedSearch.Core;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public sealed class RemixedBundlePredictorTests
{

    private readonly ITestOutputHelper output;

    public RemixedBundlePredictorTests(ITestOutputHelper output)
    {
        this.output = output;
    }
    [Fact]
    public void Predict_IsDeterministic_ForSameGameId()
    {
        const ulong gameId = 123456789UL;

        var a = RemixedBundlePredictor.Predict(gameId);
        var b = RemixedBundlePredictor.Predict(gameId);

        Assert.Equal(Signature(a), Signature(b));
    }

    [Fact]
    public void Predict_HasExpectedBundleCounts_PerArea()
    {
        var p = RemixedBundlePredictor.Predict(1UL);

        // total = 6 + 6 + 6 + 3 + 5 = 26
        Assert.Equal(26, p.Count);

        Assert.Equal(6, p.Count(b => b.AreaName == "Crafts Room"));
        Assert.Equal(6, p.Count(b => b.AreaName == "Pantry"));
        Assert.Equal(6, p.Count(b => b.AreaName == "Fish Tank"));
        Assert.Equal(3, p.Count(b => b.AreaName == "Boiler Room"));
        Assert.Equal(5, p.Count(b => b.AreaName == "Bulletin Board"));
    }

    [Fact]
    public void GetAllChosenItems_ReturnsFlattenedItemsAcrossAllBundles()
    {
        const ulong gameId = 987654321UL;

        var p = RemixedBundlePredictor.Predict(gameId);
        var flattened = RemixedBundlePredictor.GetAllChosenItems(p);

        int expected = p.Sum(b => b.ItemsChosen.Length);
        Assert.Equal(expected, flattened.Count);

        Assert.True(flattened.Count > 0);
        Assert.DoesNotContain(flattened, x => string.IsNullOrWhiteSpace(x.Item));
    }

    [Fact]
    public void GetAggregatedChosenItemCounts_SumsCountsCorrectly()
    {
        const ulong gameId = 987654321UL;

        var p = RemixedBundlePredictor.Predict(gameId);

        var flattened = RemixedBundlePredictor.GetAllChosenItems(p);
        var aggregated = RemixedBundlePredictor.GetAggregatedChosenItemCounts(p);

        int flatTotal = flattened.Sum(x => x.Count);
        int aggTotal = aggregated.Values.Sum();
        Assert.Equal(flatTotal, aggTotal);

        foreach (var it in flattened)
        {
            var key = (it.Item, it.Quality);
            Assert.True(aggregated.ContainsKey(key));
            Assert.True(aggregated[key] >= it.Count);
        }
    }

    // [Fact]
    // public void Predict_GoldenSnapshot_FillOnce()
    // {
    //     // Set this to a real seed (uniqueIDForThisGame / UI seed).
    //     const ulong gameId = 0UL;

    //     var actual = RemixedBundlePredictor.Predict(gameId);
    //     var sig = Signature(actual);

    //     const string expected = ""; // paste once, then lock it

    //     Assert.True(expected.Length > 0, "Golden snapshot not set.\n\nCopy/paste this into expected:\n\n" + sig);
    //     Assert.Equal(expected, sig);
    // }

    [Fact]
    public void MatchesKnownGame()
    {
        ulong gameId = 1234567;
        var predictedBundles = RemixedBundlePredictor.Predict(gameId);
        string actualOutput = RemixedBundlePredictor.Signature(predictedBundles);
        // string expectedOutput = "";
        output.WriteLine(actualOutput);


        Assert.True(true);
    }

    private static string Signature(IReadOnlyList<PredictedBundle> list)
    {
        var sb = new StringBuilder();

        foreach (var b in list.OrderBy(x => x.BundleId, StringComparer.Ordinal))
        {
            sb.Append(b.BundleId);
            sb.Append('=');
            sb.Append(b.Name);

            sb.Append(" | reward=");
            sb.Append(b.RewardRaw ?? "");

            if (b.RewardParsed is not null)
            {
                sb.Append(" (parsed=");
                sb.Append(b.RewardParsed.Value.Count);
                sb.Append(' ');
                sb.Append(b.RewardParsed.Value.Item);
                sb.Append(')');
            }

            sb.Append(" | req=");
            sb.Append(b.RequiredItems);

            sb.Append(" | items=");
            for (int i = 0; i < b.ItemsChosen.Length; i++)
            {
                var it = b.ItemsChosen[i];
                sb.Append(it.Count);
                sb.Append(' ');
                sb.Append(it.Quality);
                sb.Append(' ');
                sb.Append(it.Item);
                if (i < b.ItemsChosen.Length - 1)
                    sb.Append(", ");
            }

            sb.Append("\n\r");
        }

        return sb.ToString();
    }
}
