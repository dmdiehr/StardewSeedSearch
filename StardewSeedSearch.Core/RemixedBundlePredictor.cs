using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text;


namespace StardewSeedSearch.Core;

public static class RemixedBundlePredictor
{
    private static readonly Lazy<IReadOnlyList<RandomBundleData>> s_randomBundles =
        new(LoadRandomBundles, isThreadSafe: true);

    public static IReadOnlyList<PredictedBundle> Predict(ulong uniqueIdForThisGame)
    {
        var randomBundles = s_randomBundles.Value;

        // Game1.GenerateBundles: Utility.CreateRandom((double)uniqueIDForThisGame * 9.0)
        Random random = StardewRng.CreateRandom((double)uniqueIdForThisGame * 9.0);

        var all = new List<PredictedBundle>(capacity: 32);

        foreach (var area in randomBundles)
        {
            int[] indexLookups = ParseKeys(area.Keys);
            if (indexLookups.Length == 0)
                throw new InvalidDataException($"Area '{area.AreaName}' has no Keys.");

            // selected_bundles: slotIndex -> BundleData
            var selected = new Dictionary<int, BundleData>();
            var insertionOrder = new List<int>(); // matches SDV dict insertion order

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

                // candidates where Index == i
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

                // else: candidates where Index == -1
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

            // IMPORTANT: item selection consumes RNG in SDV dict enumeration order,
            // which matches insertionOrder above.
            var predictedBySlot = new PredictedBundle[indexLookups.Length];

            foreach (int slotIndex in insertionOrder)
            {
                var data = selected[slotIndex];

                int bundleKey = indexLookups[slotIndex];
                string bundleId = $"{area.AreaName}/{bundleKey}";

                var itemsChosen = GenerateItemTuplesLikeGame(
                    random,
                    data.Items,
                    data.Pick,
                    data.RequiredItems,
                    out int effectiveRequiredItems);

                string rewardRaw = data.Reward ?? "";
                var rewardParsed = TryParseCountedString(rewardRaw);

                predictedBySlot[slotIndex] = new PredictedBundle(
                    BundleId: bundleId,
                    AreaName: area.AreaName,
                    SlotIndex: slotIndex,
                    BundleKey: bundleKey,
                    Name: data.Name,
                    ItemsChosen: itemsChosen,
                    RequiredItems: effectiveRequiredItems,
                    RewardRaw: rewardRaw,
                    RewardParsed: rewardParsed);
            }

            // Return in stable slot order (0..N-1)
            for (int slot = 0; slot < predictedBySlot.Length; slot++)
            {
                var b = predictedBySlot[slot];
                if (b is null)
                    throw new InvalidDataException($"Area '{area.AreaName}' missing slot {slot}.");
                all.Add(b);
            }
        }

        return all;
    }

    // ---- public helpers ----

    public static string Signature(IReadOnlyList<PredictedBundle> bundles)
    {
        static string QualityLabel(int q) => q switch
        {
            1 => "Silver",
            2 => "Gold",
            3 => "Iridium",
            _ => ""
        };

        static string ItemToText((string Item, int Count, int Quality) it)
        {
            var sb = new StringBuilder();

            if (it.Count > 1)
            {
                sb.Append(it.Count);
                sb.Append("x ");
            }

            if (it.Quality > 0)
            {
                sb.Append(QualityLabel(it.Quality));
                sb.Append(' ');
            }

            sb.Append(it.Item);
            return sb.ToString();
        }

        var sbAll = new StringBuilder();

        foreach (var b in bundles.OrderBy(x => x.BundleId, StringComparer.Ordinal))
        {
            // Bundle name
            sbAll.Append(b.Name);

            // Choose X/Y (only when required less than offered)
            int offered = b.ItemsChosen.Length;
            if (b.RequiredItems > 0 && b.RequiredItems < offered)
            {
                sbAll.Append("  (Choose ");
                sbAll.Append(b.RequiredItems);
                sbAll.Append('/');
                sbAll.Append(offered);
                sbAll.Append(')');
            }

            sbAll.Append('\n');

            // Items (one per line, indented)
            for (int i = 0; i < b.ItemsChosen.Length; i++)
            {
                sbAll.Append("  - ");
                sbAll.Append(ItemToText(b.ItemsChosen[i]));
                sbAll.Append('\n');
            }

            // blank line between bundles (extra \n makes it much easier to scan)
            sbAll.Append('\n');
        }

        return sbAll.ToString();
    }

    
    public static List<(string Item, int Count, int Quality)> GetAllChosenItems(IReadOnlyList<PredictedBundle> prediction)
    {
        var list = new List<(string Item, int Count, int Quality)>(capacity: 256);
        for (int i = 0; i < prediction.Count; i++)
            list.AddRange(prediction[i].ItemsChosen);
        return list;
    }

    public static Dictionary<(string Item, int Quality), int> GetAggregatedChosenItemCounts(IReadOnlyList<PredictedBundle> prediction)
    {
        var totals = new Dictionary<(string Item, int Quality), int>();
        for (int i = 0; i < prediction.Count; i++)
        {
            foreach (var it in prediction[i].ItemsChosen)
            {
                var key = (it.Item, it.Quality);
                totals[key] = totals.TryGetValue(key, out int cur) ? cur + it.Count : it.Count;
            }
        }
        return totals;
    }

    // ---- item generation ----

    private static (string Item, int Count, int Quality)[] GenerateItemTuplesLikeGame(
        Random rng,
        string itemList,
        int pickCount,
        int requiredItems,
        out int effectiveRequiredItems)
    {
        string expanded = ParseRandomTags(rng, itemList);

        var tokens = expanded.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(t => t.Trim())
                             .ToList();

        if (pickCount < 0)
            pickCount = tokens.Count;

        if (requiredItems < 0)
            requiredItems = pickCount;

        while (tokens.Count > pickCount)
        {
            int removeAt = rng.Next(tokens.Count);
            tokens.RemoveAt(removeAt);
        }

        effectiveRequiredItems = requiredItems;

        var tuples = new (string Item, int Count, int Quality)[tokens.Count];
        for (int i = 0; i < tokens.Count; i++)
            tuples[i] = ParseItemRequirement(tokens[i]);

        return tuples;
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

    private static (string Item, int Count, int Quality) ParseItemRequirement(string token)
    {
        var parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new InvalidDataException($"Invalid bundle item token: '{token}'");

        if (!int.TryParse(parts[0], out int count))
            throw new InvalidDataException($"Invalid bundle item count in token: '{token}'");

        int quality = 0;
        int idx = 1;

        if (parts[idx] is "NQ" or "SQ" or "GQ" or "IQ")
        {
            quality = parts[idx] switch
            {
                "NQ" => 0,
                "SQ" => 1,
                "GQ" => 2,
                "IQ" => 3,
                _ => 0
            };
            idx++;
        }

        if (idx >= parts.Length)
            throw new InvalidDataException($"Missing item name in token: '{token}'");

        string item = string.Join(' ', parts, idx, parts.Length - idx);
        return (item, count, quality);
    }

    private static (string Item, int Count)? TryParseCountedString(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        if (!int.TryParse(parts[0], out int count)) return null;

        string item = string.Join(' ', parts, 1, parts.Length - 1);
        return (item, count);
    }

    // ---- loading RandomBundles.json internally ----

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

        throw new FileNotFoundException("RandomBundles.json not found. Add it as an EmbeddedResource.");
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
    string BundleId,  // e.g. "Crafts Room/13"
    string AreaName,
    int SlotIndex,    // 0..N-1 within that area
    int BundleKey,    // the numeric key from area.Keys (e.g. 13)
    string Name,
    (string Item, int Count, int Quality)[] ItemsChosen,
    int RequiredItems,
    string RewardRaw,
    (string Item, int Count)? RewardParsed);


