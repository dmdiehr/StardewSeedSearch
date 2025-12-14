using System;
using System.Linq;
using System.Text;
using StardewSeedSearch.Core;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public sealed class RemixedBundlePredictorTests
{
    private readonly ITestOutputHelper _output;
    public RemixedBundlePredictorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Predict_IsDeterministic_ForSameGameId()
    {
        const ulong gameId = 123456789UL;

        var a = RemixedBundlePredictor.Predict(gameId);
        var b = RemixedBundlePredictor.Predict(gameId);

        Assert.Equal(Signature(a), Signature(b));
    }

    [Fact]
    public void Predict_HasExpectedBundleCounts()
    {
        var p = RemixedBundlePredictor.Predict(1UL);

        // total = 6 + 6 + 6 + 3 + 5 = 26
        Assert.Equal(26, p.Count);

        Assert.Equal(6, p.Keys.Count(k => k.StartsWith("Crafts Room/", StringComparison.Ordinal)));
        Assert.Equal(6, p.Keys.Count(k => k.StartsWith("Pantry/", StringComparison.Ordinal)));
        Assert.Equal(6, p.Keys.Count(k => k.StartsWith("Fish Tank/", StringComparison.Ordinal)));
        Assert.Equal(3, p.Keys.Count(k => k.StartsWith("Boiler Room/", StringComparison.Ordinal)));
        Assert.Equal(5, p.Keys.Count(k => k.StartsWith("Bulletin Board/", StringComparison.Ordinal)));
    }

    [Fact]
    public void Predict_GoldenSnapshot_FillOnce_FromRealSaveUniqueId()
    {
        // Replace with uniqueIDForThisGame from a real save file:
        // (NOT the "Seed" shown in advanced options.)
        const ulong gameId = 123456;

        var actual = RemixedBundlePredictor.Predict(gameId);
        var sig = Signature(actual);

        const string expected = ""; // paste once, then lock it

        Assert.True(expected.Length > 0, "Set gameId + paste expected signature:\n\n" + sig);
        Assert.Equal(expected, sig);
    }

    private static string Signature(System.Collections.Generic.IReadOnlyDictionary<string, PredictedBundle> data)
    {
        var sb = new StringBuilder();

        foreach (var kvp in data.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.Append(kvp.Key);
            sb.Append('=');
            sb.Append(kvp.Value.Name);
            sb.Append(" | items=");
            sb.Append(string.Join(", ", kvp.Value.ItemsChosen));
            sb.Append(" | req=");
            sb.Append(kvp.Value.RequiredItems);
            sb.Append("\n\r");
        }

        return sb.ToString();
    }
}
