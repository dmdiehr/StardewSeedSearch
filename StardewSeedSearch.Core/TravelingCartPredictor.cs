using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace StardewSeedSearch.Core;

public sealed class TravelingCartPredictor
{
    /// <summary>
    /// A lightweight representation of Data/Objects.json entries with the fields
    /// needed for Traveling Cart RANDOM_ITEMS selection and later price/qty logic.
    /// </summary>
    public readonly struct RandomObjectCandidate
    {
        public RandomObjectCandidate(
            string key,
            int id,
            string name,
            int price,
            int category,
            string type,
            bool excludeFromRandomSale)
        {
            Key = key;
            Id = id;
            Name = name;
            Price = price;
            Category = category;
            Type = type;
            ExcludeFromRandomSale = excludeFromRandomSale;
        }

        /// <summary>
        /// The JSON dictionary key (often used as the lookup key in other codebases).
        /// Kept for debugging and future expansion.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The numeric object ID (what the shop constraint "2..789" applies to).
        /// </summary>
        public int Id { get; }

        public string Name { get; }
        public int Price { get; }
        public int Category { get; }
        public string Type { get; }

        /// <summary>
        /// Matches the traveling cart "@isRandomSale" / "ExcludeFromRandomSale" meaning.
        /// </summary>
        public bool ExcludeFromRandomSale { get; }
    }

    /// <summary>
    /// Output of the random selection step. Price/Quantity are nullable for now
    /// because this method only selects the 10 random items (not full stock generation).
    /// </summary>
    public readonly struct RandomObjectResult
    {
        public RandomObjectResult(int id, string name, int basePrice, string key, int? price = null, int? quantity = null)
        {
            Id = id;
            Name = name;
            BasePrice = basePrice;
            Key = key;
            Price = price;
            Quantity = quantity;
        }

        public int Id { get; }
        public string Name { get; }
        public int BasePrice { get; }
        public string Key { get; }

        public int? Price { get; }
        public int? Quantity { get; }

        public override string ToString()
            => $"{Id} - {Name} (BasePrice={BasePrice}, Key={Key}, Price={(Price?.ToString() ?? "null")}, Qty={(Quantity?.ToString() ?? "null")})";
    }

    private const string ObjectsResourceName = "StardewSeedSearch.Core.Data.Objects.json";
    private static readonly Lazy<IReadOnlyList<RandomObjectCandidate>> _objectCandidates =
        new(LoadObjectCandidatesPreserveJsonOrder);

    /// <summary>
    /// Replicates stardew-predictor's getRandomItems(...) behavior for Traveling Cart's 10 random objects:
    /// - Consume rng.Next() once per object in the full Objects.json list (regardless of passing checks).
    /// - If ItemIdCheck passes, store candidate in a dictionary keyed by that random key (overwriting on collisions).
    /// - Iterate candidates by ascending key and apply PerItemConditionCheck until 10 items are selected.
    /// </summary>
    public IReadOnlyList<RandomObjectResult> GetRandomItems(ulong gameId, long daysPlayed)
    {
        var rng = StardewRng.CreateDaySaveRandom(daysPlayed, gameId);

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

        var results = new List<RandomObjectResult>(capacity: 10);

        foreach (int key in keyed.Keys.OrderBy(k => k))
        {
            var candidate = keyed[key];

            if (!PerItemConditionCheck(candidate))
                continue;

            int computedPrice = Math.Max(
                rng.Next(1, 11) * 100,
                rng.Next(3, 6) * candidate.Price);

            int computedQty = (rng.NextDouble() < 0.1) ? 5 : 1;    

            results.Add(new RandomObjectResult(
                id: candidate.Id,
                name: candidate.Name,
                basePrice: candidate.Price,
                key: candidate.Key,
                price: computedPrice,
                quantity: computedQty));

            if (results.Count >= 10)
                break;
        }
        return results;
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

    private static IReadOnlyList<RandomObjectCandidate> LoadObjectCandidatesPreserveJsonOrder()
    {
        // IMPORTANT:
        // 1) We must preserve the original JSON property order (to match stardew-predictor's loop order).
        // 2) Dictionary deserialization does not guarantee order; JsonDocument property enumeration does.

        string json = ReadEmbeddedObjectsJson();


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

private static string ReadEmbeddedObjectsJson()
{
    var asm = typeof(TravelingCartPredictor).Assembly;

    using Stream? stream = asm.GetManifestResourceStream(ObjectsResourceName);
    if (stream is null)
    {
        // Helpful crash message if the default namespace differs.
        var names = asm.GetManifestResourceNames();
        throw new FileNotFoundException(
            $"Embedded resource not found: '{ObjectsResourceName}'.\n" +
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
}
