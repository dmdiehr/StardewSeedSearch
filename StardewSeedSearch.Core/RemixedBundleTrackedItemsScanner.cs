using System.Text.Json;

namespace StardewSeedSearch.Core;

/// <summary>
/// Fast scanner over remixed bundles for a seed:
/// - early-out on disqualifying bundle ("Spirit's Eve" bundle)
/// - returns only tracked item IDs (qty=1, quality=0)
/// - special-case: track Holly (283) only when in Winter Foraging bundle
///
/// Does NOT build bundle strings or PredictedBundle records.
/// </summary>
public static class RemixedBundleTrackedItemsScanner
{
    // NOTE: update these two constants to match the exact bundle.Name strings in your RandomBundles.json.
    // (Use your HumanSignature output once to confirm exact spelling.)
    public const string DisqualifyBundleName = "Spirit's Eve";
    public const string HollyAllowedBundleName = "Winter Foraging";

    private static readonly Lazy<IReadOnlyList<RandomBundleData>> s_randomBundles =
        new(LoadRandomBundles, isThreadSafe: true);

    // tracked ids (excluding Holly which is conditional)
    // size only needs to cover max id you care about; 700 is enough for your list
    private static readonly bool[] s_trackId = BuildTrackArray(maxIdExclusive: 700);

    // Resolve only the small set of names you care about (no full Objects.json needed).
    // OrdinalIgnoreCase to match common SDV naming.
    private static readonly Dictionary<string, int> s_nameToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Truffle"] = 430,
        ["Duck Feather"] = 444,
        ["Red Cabbage"] = 266,
        ["Nautilus Shell"] = 392,
        ["Rabbit's Foot"] = 466,
        ["Plum Pudding"] = 604,
        ["Snow Yam"] = 416,
        ["Crocus"] = 418,
        ["Holly"] = 283,
        ["Large Milk"] = 186,
        ["L. Goat Milk"] = 438,
    };

    /// <summary>
    /// Scan the seed. Writes found tracked item IDs into outputBuffer (deduped).
    /// Returns false only if outputBuffer was too small.
    /// </summary>
    public static bool TryScan(ulong uniqueIdForThisGame, Span<int> outputBuffer, out int foundCount, out bool disqualified, out string? disqualifyReason)
    {
        foundCount = 0;
        disqualified = false;
        disqualifyReason = null;

        var randomBundles = s_randomBundles.Value;

        // Same RNG seed as Game1.GenerateBundles(Remixed)
        var rng = StardewRng.CreateRandom((double)uniqueIdForThisGame * 9.0);

        foreach (var area in randomBundles)
        {
            int[] indexLookups = ParseKeys(area.Keys);
            if (indexLookups.Length == 0)
            {
                disqualified = true;
                disqualifyReason = $"Invalid area Keys for '{area.AreaName}'";
                return true;
            }

            var selected = new Dictionary<int, BundleData>();
            var insertionOrder = new List<int>(capacity: indexLookups.Length);

            // bundle_set = random.ChooseFrom(area_data.BundleSets)
            var bundleSet = StardewRng.ChooseFrom(rng, area.BundleSets);
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

            for (int slot = 0; slot < indexLookups.Length; slot++)
            {
                if (selected.ContainsKey(slot))
                    continue;

                // Index == slot candidates
                BundleData? chosen = ChooseAndRemove(rng, pool, matchIndex: slot);
                if (chosen != null)
                {
                    selected[slot] = chosen;
                    insertionOrder.Add(slot);
                    continue;
                }

                // fallback: Index == -1 candidates
                chosen = ChooseAndRemove(rng, pool, matchIndex: -1);
                if (chosen != null)
                {
                    selected[slot] = chosen;
                    insertionOrder.Add(slot);
                    continue;
                }

                disqualified = true;
                disqualifyReason = $"Couldn't fill slot {slot} in '{area.AreaName}'";
                return true;
            }

            // IMPORTANT: item RNG consumption follows SDV dictionary enumeration order.
            // We preserved that as insertionOrder.
            foreach (int slotIndex in insertionOrder)
            {
                var bundle = selected[slotIndex];

                // disqualify seed on bundle name
                if (string.Equals(bundle.Name, DisqualifyBundleName, StringComparison.Ordinal))
                {
                    disqualified = true;
                    disqualifyReason = $"Disqualifying bundle: {bundle.Name}";
                    return true;
                }

                // Expand tags + apply pick removal (same logic as predictor)
                var itemTokens = GenerateItemTokensLikeGame(rng, bundle.Items, bundle.Pick);

                for (int i = 0; i < itemTokens.Count; i++)
                {
                    if (!TryParseItemToken(itemTokens[i], out int count, out int quality, out string itemNameOrId))
                        continue;

                    if (count != 1 || quality != 0)
                        continue;

                    if (!TryResolveItemId(itemNameOrId, out int itemId))
                        continue;

                    // Holly special-case
                    if (itemId == 283 && !string.Equals(bundle.Name, HollyAllowedBundleName, StringComparison.Ordinal))
                        continue;

                    // Track list (fast)
                    if (itemId >= 0 && itemId < s_trackId.Length && s_trackId[itemId])
                    {
                        if (!Contains(outputBuffer, foundCount, itemId))
                        {
                            if (foundCount >= outputBuffer.Length)
                            {
                                disqualified = true;
                                disqualifyReason = "Output buffer too small";
                                return false;
                            }

                            outputBuffer[foundCount++] = itemId;
                        }
                    }
                }
            }
        }

        return true;
    }

    // ---------------- helpers ----------------

    private static bool[] BuildTrackArray(int maxIdExclusive)
    {
        var a = new bool[maxIdExclusive];

        // Always tracked:
        // 430 Truffle
        // 444 Duck Feather
        // 266 Red Cabbage
        // 392 Nautilus Shell
        // 466 Rabbit's Foot
        // 604 Plum Pudding
        // 416 Snow Yam
        // 418 Crocus
        // 186 Large Milk
        // 438 Large Goat Milk
        // 283 Holly (conditional, but still "tracked" here; conditional check happens at runtime)
        a[430] = true;
        a[444] = true;
        a[266] = true;
        a[392] = true;
        a[466] = true;
        a[604] = true;
        a[416] = true;
        a[418] = true;
        a[283] = true;
        a[186] = true;
        a[438] = true;

        return a;
    }

    private static BundleData? ChooseAndRemove(Random rng, List<BundleData> pool, int matchIndex)
    {
        // gather candidates
        int count = 0;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].Index == matchIndex)
                count++;

        if (count == 0)
            return null;

        int pick = rng.Next(count);
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Index != matchIndex) continue;
            if (pick-- == 0)
            {
                var chosen = pool[i];
                pool.RemoveAt(i);
                return chosen;
            }
        }

        return null;
    }

    private static List<string> GenerateItemTokensLikeGame(Random rng, string itemList, int pickCount)
    {
        string expanded = ParseRandomTags(rng, itemList);

        var tokens = expanded.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(t => t.Trim())
                             .ToList();

        if (pickCount < 0)
            pickCount = tokens.Count;

        while (tokens.Count > pickCount)
        {
            int removeAt = rng.Next(tokens.Count);
            tokens.RemoveAt(removeAt);
        }

        return tokens;
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

    private static bool TryParseItemToken(string token, out int count, out int quality, out string itemNameOrId)
    {
        count = 0;
        quality = 0;
        itemNameOrId = "";

        var parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out count)) return false;

        int idx = 1;
        if (parts[idx] is "NQ" or "SQ" or "GQ" or "IQ")
        {
            quality = parts[idx] switch { "SQ" => 1, "GQ" => 2, "IQ" => 3, _ => 0 };
            idx++;
        }

        if (idx >= parts.Length) return false;

        itemNameOrId = string.Join(' ', parts, idx, parts.Length - idx);
        return true;
    }

    private static bool TryResolveItemId(string itemNameOrId, out int itemId)
    {
        if (int.TryParse(itemNameOrId, out itemId))
            return true;

        return s_nameToId.TryGetValue(itemNameOrId, out itemId);
    }

    private static bool Contains(Span<int> buffer, int count, int value)
    {
        for (int i = 0; i < count; i++)
            if (buffer[i] == value)
                return true;
        return false;
    }

    private static IReadOnlyList<RandomBundleData> LoadRandomBundles()
    {
        var asm = typeof(RemixedBundleTrackedItemsScanner).Assembly;

        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("RandomBundles.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new FileNotFoundException("RandomBundles.json not found. Add it as an EmbeddedResource.");

        using Stream? s = asm.GetManifestResourceStream(resourceName);
        if (s is null)
            throw new InvalidOperationException($"Found resource '{resourceName}', but stream was null.");

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<RandomBundleData>>(s, opts)
            ?? throw new InvalidDataException("RandomBundles.json deserialized to null.");
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
