using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace StardewSeedSearch.Core;

public static class RemixedBundlePredictor
{
    private static readonly Lazy<IReadOnlyList<RandomBundleData>> s_randomBundles =
        new(LoadRandomBundles, isThreadSafe: true);

    public static IReadOnlyDictionary<string, PredictedBundle> Predict(ulong uniqueIdForThisGame)
    {
        var randomBundles = s_randomBundles.Value;

        // Game1.GenerateBundles: Utility.CreateRandom((double)uniqueIDForThisGame * 9.0)
        Random random = StardewRng.CreateRandom((double)uniqueIdForThisGame * 9.0);

        var result = new Dictionary<string, PredictedBundle>(StringComparer.Ordinal);

        foreach (var area in randomBundles)
        {
            int[] indexLookups = ParseKeys(area.Keys);
            if (indexLookups.Length == 0)
                throw new InvalidDataException($"Area '{area.AreaName}' has no Keys.");

            var selected = new Dictionary<int, BundleData>();
            var insertionOrder = new List<int>(); // match Dictionary enumeration order

            // bundle_set = random.ChooseFrom(area_data.BundleSets)
            var bundleSet = StardewRng.ChooseFrom(random, area.BundleSets);
            if (bundleSet != null)
            {
                foreach (var b in bundleSet.Bundles)
                {
                    if (!selected.ContainsKey(b.Index))
                        insertionOrder.Add(b.Index);
                    selected[b.Index] = b;
                }
            }

            var pool = new List<BundleData>(area.Bundles);

            for (int i = 0; i < indexLookups.Length; i++)
            {
                if (selected.ContainsKey(i))
                    continue;

                // index_bundles: Index == i
                var indexBundles = new List<BundleData>();
                for (int p = 0; p < pool.Count; p++)
                    if (pool[p].Index == i)
                        indexBundles.Add(pool[p]);

                if (indexBundles.Count > 0)
                {
                    var chosen = StardewRng.ChooseFrom(random, indexBundles)!;
                    pool.Remove(chosen);
                    selected[i] = chosen;
                    insertionOrder.Add(i);
                    continue;
                }

                // else: Index == -1 pool
                indexBundles.Clear();
                for (int p = 0; p < pool.Count; p++)
                    if (pool[p].Index == -1)
                        indexBundles.Add(pool[p]);

                if (indexBundles.Count > 0)
                {
                    var chosen = StardewRng.ChooseFrom(random, indexBundles)!;
                    pool.Remove(chosen);
                    selected[i] = chosen;
                    insertionOrder.Add(i);
                }
                else
                {
                    throw new InvalidDataException($"Area '{area.AreaName}' couldn't fill slot {i}.");
                }
            }

            // Now apply item randomization in the SAME order BundleGenerator does:
            // foreach (int key in selected_bundles.Keys) { ParseItemList(...) }
            foreach (int slotIndex in insertionOrder)
            {
                var data = selected[slotIndex];
                int bundleKey = indexLookups[slotIndex];
                string dictKey = $"{area.AreaName}/{bundleKey}";

                var itemTokens = GenerateItemTokensLikeGame(
                    random,
                    data.Items,
                    data.Pick,
                    data.RequiredItems,
                    out int effectiveRequiredItems);

                result[dictKey] = new PredictedBundle(
                    AreaName: area.AreaName,
                    SlotIndex: slotIndex,
                    BundleKey: bundleKey,
                    Name: data.Name,
                    ItemsChosen: itemTokens,
                    RequiredItems: effectiveRequiredItems);
            }
        }

        return result;
    }

    // --- item generation (matches BundleGenerator.ParseRandomTags + ParseItemList removal behavior) ---

    private static string[] GenerateItemTokensLikeGame(
        Random rng,
        string itemList,
        int pickCount,
        int requiredItems,
        out int effectiveRequiredItems)
    {
        // ParseRandomTags
        string expanded = ParseRandomTags(rng, itemList);

        // item_list.Split(',') in game
        // Keep raw item strings (including counts/qualities/names) so you can compare against UI.
        var tokens = expanded.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(t => t.Trim())
                             .ToList();

        if (pickCount < 0)
            pickCount = tokens.Count;

        if (requiredItems < 0)
            requiredItems = pickCount;

        // while item_strings.Count > pick_count: remove random.Next(count)
        while (tokens.Count > pickCount)
        {
            int removeAt = rng.Next(tokens.Count);
            tokens.RemoveAt(removeAt);
        }

        effectiveRequiredItems = requiredItems;
        return tokens.ToArray();
    }

    private static string ParseRandomTags(Random rng, string data)
    {
        int openIndex;
        do
        {
            openIndex = data.LastIndexOf('[');
            if (openIndex >= 0)
            {
                int closeIndex = data.IndexOf(']', openIndex);
                if (closeIndex == -1)
                    return data;

                string inner = data.Substring(openIndex + 1, closeIndex - openIndex - 1);
                string[] options = inner.Split('|');
                string chosen = StardewRng.ChooseFrom(rng, options)!;

                data = data.Remove(openIndex, closeIndex - openIndex + 1);
                data = data.Insert(openIndex, chosen);
            }
        }
        while (openIndex >= 0);

        return data;
    }

    // --- loading RandomBundles.json internally ---

    private static IReadOnlyList<RandomBundleData> LoadRandomBundles()
    {
        var asm = typeof(RemixedBundlePredictor).Assembly;

        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("RandomBundles.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            using Stream? s = asm.GetManifestResourceStream(resourceName);
            if (s is null)
                throw new InvalidOperationException($"Found resource '{resourceName}', but stream was null.");
            return DeserializeRandomBundles(s);
        }

        // fallback (optional)
        string baseDir = AppContext.BaseDirectory;
        string p1 = Path.Combine(baseDir, "RandomBundles.json");
        string p2 = Path.Combine(baseDir, "Data", "RandomBundles.json");

        foreach (var path in new[] { p1, p2 })
        {
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                return DeserializeRandomBundles(fs);
            }
        }

        throw new FileNotFoundException(
            "RandomBundles.json not found. Add it as an EmbeddedResource (recommended).");
    }

    private static IReadOnlyList<RandomBundleData> DeserializeRandomBundles(Stream s)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<List<RandomBundleData>>(s, opts);
        return data ?? throw new InvalidDataException("RandomBundles.json deserialized to null.");
    }

    private static int[] ParseKeys(string keys)
    {
        if (string.IsNullOrWhiteSpace(keys))
            return Array.Empty<int>();

        return keys.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(int.Parse)
                   .ToArray();
    }
}

public sealed record PredictedBundle(
    string AreaName,
    int SlotIndex,
    int BundleKey,
    string Name,
    string[] ItemsChosen,
    int RequiredItems);
