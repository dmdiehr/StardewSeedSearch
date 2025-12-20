using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace StardewSeedSearch.Core;

public static class TravelingCartPredictor
{

    //Load furntiure data
    private const string FurnitureResourceName = "StardewSeedSearch.Core.Data.Furniture.json";
    private static readonly Lazy<IReadOnlyList<FurnitureCandidate>> _furnitureCandidates =
    new(LoadFurnitureCandidatesPreserveJsonOrder);


    //Load object data
    private const string ObjectsResourceName = "StardewSeedSearch.Core.Data.Objects.json";
    private static readonly Lazy<IReadOnlyList<RandomObjectCandidate>> _objectCandidates =
        new(LoadObjectCandidatesPreserveJsonOrder);



    /// <summary>
    /// Replicates stardew-predictor's getRandomItems(...) behavior for Traveling Cart's 10 random objects:
    /// - Consume rng.Next() once per object in the full Objects.json list (regardless of passing checks).
    /// - If ItemIdCheck passes, store candidate in a dictionary keyed by that random key (overwriting on collisions).
    /// - Iterate candidates by ascending key and apply PerItemConditionCheck until 10 items are selected.
    /// </summary>
    /// 
    
    public static TravelingCartStock GetCartStock(ulong gameId, long daysPlayed)
    {
        // Main cart RNG (gameId/2) — must be the single shared RNG stream
        var rng = StardewRng.CreateDaySaveRandom(daysPlayed, gameId);

        // 10 random objects (selection + price/qty consumes rng)
        var randomObjects = GetRandomItems(rng);

        // furniture (continues consuming the same rng)
        var furniture = GetRandomFurniture(rng);

        return new TravelingCartStock(
            RandomObjects: randomObjects,
            Furniture: furniture
        );
    }
    public static IReadOnlyList<CartItem> GetRandomItems(Random random)
    {
        var rng = random;

        // JS uses an object literal shuffledItems[key] = id;
        // That implies:
        //  - key collisions overwrite earlier entries
        //  - enumeration is effectively ascending by numeric key for integer-like keys
        var keyed = new Dictionary<int, RandomObjectCandidate>(capacity: 2048);

        foreach (var candidate in _objectCandidates.Value)
        {
            int key = rng.Next(); // IMPORTANT: consumes RNG once per object, before any checks (matches JS).

            if (!ItemIdCheck(candidate))
                continue;

            keyed[key] = candidate; // overwrite on collisions (matches JS object behavior).
        }

        var results = new List<CartItem>(capacity: 10);

        foreach (int key in keyed.Keys.OrderBy(k => k))
        {
            var candidate = keyed[key];

            if (!PerItemConditionCheck(candidate))
                continue;

            int computedPrice = Math.Max(
                rng.Next(1, 11) * 100,
                rng.Next(3, 6) * candidate.Price);

            int computedQty = (rng.NextDouble() < 0.1) ? 5 : 1;

            results.Add(new CartItem(
                ItemId: $"(O){candidate.Id}",
                Name: candidate.Name,
                Price: computedPrice,
                Quantity: computedQty));

            if (results.Count >= 10)
                break;
        }

        return results;
    }

    public static CartItem GetRandomFurniture(Random cartRng)
    {
        var keyed = new Dictionary<int, FurnitureCandidate>(capacity: 2048);

        foreach (var candidate in _furnitureCandidates.Value)
        {
            int key = cartRng.Next(); // IMPORTANT: consume once per furniture entry
            if (!FurnitureItemIdCheck(candidate))
                continue;

            keyed[key] = candidate; // overwrite on collisions
        }

        // pick first in ascending key order (howMany = 1, no per-item checks)
        FurnitureCandidate picked = default;
        bool found = false;

        foreach (var k in keyed.Keys.OrderBy(k => k))
        {
            picked = keyed[k];
            found = true;
            break;
        }

        if (!found)
            throw new InvalidOperationException("No valid furniture candidates found for traveling cart.");

        int price = cartRng.Next(1, 11) * 250; // MUST be after the furniture pick
        return new CartItem(ItemId: $"(F){picked.Id}", Name: picked.Name, Price: price, Quantity: 1);
    }
    private static bool ItemIdCheck(RandomObjectCandidate c)
    {
        // Corresponds to:
        // - id is numeric (we store int; invalid parses become <= 0 and fail range)
        // - requirePrice => price != 0
        // - isRandomSale => not offlimits (ExcludeFromRandomSale == false)
        // - min/max constraint from shop.json: 2..789 inclusive
        if (c.Id < 2 || c.Id > 789)
            return false;

        if (c.Price == 0)
            return false;

        if (c.ExcludeFromRandomSale)
            return false;

        return true;
    }

    private static bool PerItemConditionCheck(RandomObjectCandidate c)
    {
        // Matches stardew-predictor's doCategoryChecks:
        // if (category >= 0 || category === -999) continue;
        if (c.Category >= 0 || c.Category == -999)
            return false;

        // and type is not Arch, Minerals, Quest
        // (string compare exactly like the JSON provides; adjust to OrdinalIgnoreCase if needed)
        if (c.Type == "Arch" || c.Type == "Minerals" || c.Type == "Quest")
            return false;

        return true;
    }

    private static bool FurnitureItemIdCheck(FurnitureCandidate c)
    {
        if (c.Id < 0 || c.Id > 1612) return false;
        if (c.Price == 0) return false;               // @requirePrice
        if (c.ExcludeFromRandomSale) return false;    // @isRandomSale
        return true;
    }

    private static IReadOnlyList<FurnitureCandidate> LoadFurnitureCandidatesPreserveJsonOrder()
    {
        string json = ReadEmbeddedJson(FurnitureResourceName);

        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Furniture.json root must be a JSON object/dictionary.");

        var list = new List<FurnitureCandidate>(capacity: 2048);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            string key = prop.Name;

            // IMPORTANT: never skip an entry; if id can't parse, force it to fail checks
            int id = int.TryParse(key, out int parsedId) ? parsedId : int.MinValue;

            string raw = prop.Value.ValueKind == JsonValueKind.String ? (prop.Value.GetString() ?? "") : "";

            // IMPORTANT: never skip an entry; best-effort parse, fallback to "invalid"
            string name = "";
            int price = 0;
            bool exclude = false;

            if (!string.IsNullOrEmpty(raw) && TryParseFurnitureRaw(raw, out var n, out var p, out var ex))
            {
                name = n;
                price = p;
                exclude = ex;
            }

            list.Add(new FurnitureCandidate(
                key: key,
                id: id,
                name: name,
                price: price,
                excludeFromRandomSale: exclude));
        }

        return list;
    }

    private static bool TryParseFurnitureRaw(string raw, out string name, out int price, out bool exclude)
    {
        // Example:
        // "Crystal Chair/chair/-1/-1/4/3000/-1/[LocalizedText ...]///true"
        // We split on the "///" extension delimiter first.
        name = "";
        price = 0;
        exclude = false;

        string basePart = raw;
        string? extrasPart = null;

        int extrasIdx = raw.IndexOf("///", StringComparison.Ordinal);
        if (extrasIdx >= 0)
        {
            basePart = raw[..extrasIdx];
            extrasPart = raw[(extrasIdx + 3)..];
        }

        // base fields are slash-separated
        // We only need:
        //   [0] name
        //   [5] price  (your observed mapping; matches examples)
        var parts = basePart.Split('/');

        if (parts.Length < 6)
            return false;

        name = parts[0];

        // price at index 5 (0-based)
        if (!int.TryParse(parts[5], out price))
            price = 0;

        // extras often includes "true" meaning excluded from random sale
        if (!string.IsNullOrEmpty(extrasPart))
        {
            // extras can contain multiple flags separated by "///" in some cases;
            // we already removed the first delimiter, so split remaining similarly.
            foreach (var extra in extrasPart.Split(new[] { "///" }, StringSplitOptions.None))
            {
                if (extra.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    exclude = true;
                    break;
                }
            }
        }

        return true;
    }    
    private static IReadOnlyList<RandomObjectCandidate> LoadObjectCandidatesPreserveJsonOrder()
    {
        // IMPORTANT:
        // 1) We must preserve the original JSON property order (to match stardew-predictor's loop order).
        // 2) Dictionary deserialization does not guarantee order; JsonDocument property enumeration does.

        string json = ReadEmbeddedJson(ObjectsResourceName);


        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Objects.json root must be a JSON object/dictionary.");

        var list = new List<RandomObjectCandidate>(capacity: 2048);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            string key = prop.Name;
            JsonElement obj = prop.Value;

            int id = GetInt(obj, "id") ?? GetInt(obj, "Id") ?? TryParseIdFromKey(key) ?? int.MinValue;
            string name = GetString(obj, "name") ?? GetString(obj, "Name") ?? "";
            int price = GetInt(obj, "price") ?? GetInt(obj, "Price") ?? 0;
            int category = GetInt(obj, "category") ?? GetInt(obj, "Category") ?? 0;
            string type = GetString(obj, "type") ?? GetString(obj, "Type") ?? "";

            // stardew-predictor calls it "offlimits"; your shop rules refer to ExcludeFromRandomSale.
            // Support multiple field names to be resilient to different Objects.json variants.
            bool exclude =
                GetBool(obj, "ExcludeFromRandomSale") ??
                GetBool(obj, "excludeFromRandomSale") ??
                GetBool(obj, "offlimits") ??
                false;

            list.Add(new RandomObjectCandidate(
                key: key,
                id: id,
                name: name,
                price: price,
                category: category,
                type: type,
                excludeFromRandomSale: exclude));
        }

        return list;
    }

    private static int? TryParseIdFromKey(string key)
    {
        // Some JSON dumps prefix keys with "_" (e.g. "_485").
        // If that’s the case, parse numeric portion.
        if (string.IsNullOrWhiteSpace(key))
            return null;

        int start = 0;
        while (start < key.Length && !char.IsDigit(key[start]) && key[start] != '-')
            start++;

        if (start >= key.Length)
            return null;

        if (int.TryParse(key[start..], out int v))
            return v;

        return null;
    }

    private static string ReadEmbeddedJson(string resourceName)
    {
        var asm = typeof(TravelingCartPredictor).Assembly;

        using Stream? stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            // Helpful crash message if the default namespace differs.
            var names = asm.GetManifestResourceNames();
            throw new FileNotFoundException(
                $"Embedded resource not found: '{resourceName}'.\n" +
                $"Available resources:\n- {string.Join("\n- ", names)}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    
    private static int? GetInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el))
            return null;

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out int v) => v,
            JsonValueKind.String when int.TryParse(el.GetString(), out int v) => v,
            _ => null
        };
    }

    private static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el))
            return null;

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            _ => null
        };
    }

    private static bool? GetBool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el))
            return null;

        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out bool v) => v,
            _ => null
        };
    }

    public static bool TryGetRedFez(ulong gameId, long daysPlayed, out (string itemId, string name, int price, int qty) item)
    {
        var rng = StardewRng.CreateSyncedDayRandom(gameId, daysPlayed, "cart_fez");
        if (rng.NextDouble() < 0.1)
        {
            item = ("(H)RedFez", "Red Fez", 8000, 1);
            return true;
        }

        item = default;
        return false;
    }
}
