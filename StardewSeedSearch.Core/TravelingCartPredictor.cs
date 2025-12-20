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

    //Book data
    private static readonly string[] SkillBookNames =
    {
        "Stardew Valley Almanac",
        "Bait And Bobber",
        "Woodcutter's Weekly",
        "Mining Monthly",
        "Combat Quarterly"
    };

    
    public static TravelingCartStock? GetCartStock(ulong gameId, long daysPlayed, bool communityCenterComplete = false, bool jojaComplete = false)
    {
        CartLocation location;
        if (!IsTravelingCartDay(daysPlayed, out var cartLocation))
            return null;
        
        location = cartLocation;

        var rng = StardewRng.CreateDaySaveRandom(daysPlayed, gameId);

        var randomObjects = GetRandomItems(rng, out bool seenRareSeed);
        var furniture = GetRandomFurniture(rng);
        var skillBook = TryGetSkillBook(gameId, daysPlayed, rng);

        var seasonal = GetSeasonalSpecial(gameId, daysPlayed, seenRareSeed, rng);

        return new TravelingCartStock(
            cartLocation,
            randomObjects,
            furniture,
            seasonal,
            TryGetCoffeeBean(gameId, daysPlayed),
            TryGetRedFez(gameId, daysPlayed),
            TryGetJojaCatalogue(gameId, daysPlayed, communityCenterComplete),
            TryGetJunimoCatalogue(gameId, daysPlayed, communityCenterComplete, jojaComplete),
            TryGetRetroCatalogue(gameId, daysPlayed),
            TryGetTeaSet(gameId, daysPlayed),
            skillBook
        );
    }

    public static bool IsTravelingCartDay(long daysPlayed, out CartLocation location)
    {
        int dayOfYear = (int)(daysPlayed % 112); // 0..111
        int dayOfWeek = (int)(daysPlayed % 7); // 1..7 (Mon..Sun in SDV)

        // Night Market: Winter 15-17 => dayOfYear 98-100
        if (dayOfYear is >= 98 and <= 100)
        {
            location = CartLocation.NightMarket;
            return true;
        }

        // Desert Festival: Spring 15-17 => dayOfYear 14-16
        if (dayOfYear is >= 14 and <= 16)
        {
            location = CartLocation.DesertFestival;
            return true;
        }

        // Regular Forest Cart: Friday/Sunday (day 5 and 7)
        if (dayOfWeek is 5 or 7)
        {
            location = CartLocation.Forest;
            return true;
        }

        location = CartLocation.None;
        return false;
    }

    public static IReadOnlyList<CartItem> GetRandomItems(Random cartRng, out bool seenRareSeed)
    {
        var rng = cartRng;
        seenRareSeed = false;

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

            if (candidate.Id == 347)
                seenRareSeed = true;

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
    
    public static CartItem? TryGetSkillBook(ulong gameId, long daysPlayed, Random cartRng)
    {
        // independent synced RNG determines if a book appears
        var synced = StardewRng.CreateSyncedDayRandom(gameId, daysPlayed, "travelerSkillBook");

        if (synced.NextDouble() >= 0.05)
            return null;

        // IMPORTANT: the specific book is chosen using the main cart RNG stream
        int which = cartRng.Next(SkillBookNames.Length);

        return new CartItem(
            ItemId: "(O)SkillBook",           // you can refine later to SkillBook_0..4 if desired
            Name: SkillBookNames[which],
            Price: 6000,
            Quantity: 1
        );
    }

    public static CartItem? TryGetCoffeeBean(ulong gameId, long daysPlayed)
    {
        // Fall/Winter only
        if (Helper.GetSeasonFromDaysPlayed(daysPlayed) is Season.Spring or Season.Summer)
            return null;

        return SyncedChance(gameId, daysPlayed, "cart_coffee_bean", 0.25)
            ? new CartItem("(O)433", "Coffee Bean", 2500, 1)
            : null;
    }

    public static CartItem? TryGetRedFez(ulong gameId, long daysPlayed)
    {
        return SyncedChance(gameId, daysPlayed, "cart_fez", 0.1)
            ? new CartItem("(H)RedFez", "Red Fez", 8000, 1)
            : null;
    }

    public static CartItem? GetSeasonalSpecial(ulong gameId, long daysPlayed, bool seenRareSeed, Random cartRng)
    {
        if (Helper.GetSeasonFromDaysPlayed(daysPlayed) is Season.Spring or Season.Summer)
        {
            if (seenRareSeed)
                return null;

            int qty = (cartRng.NextDouble() < 0.1) ? 5 : 1; // IMPORTANT: uses cartRng
            return new CartItem("(O)347", "Rare Seed", 1000, qty);
        }

        // Fall/Winter handled by synced rarecrow:
        return TryGetRarecrowSnowman(gameId, daysPlayed);
    }
    
    public static CartItem? TryGetRarecrowSnowman(ulong gameId, long daysPlayed)
    {
        // Fall/Winter only
        if (Helper.GetSeasonFromDaysPlayed(daysPlayed) is not (Season.Fall or Season.Winter))
            return null;

        return SyncedChance(gameId, daysPlayed, "cart_rarecrow", 0.4)
            ? new CartItem("(BC)136", "Rarecrow (Snowman)", 4000, 1)
            : null;
    }

    public static CartItem? TryGetRetroCatalogue(ulong gameId, long daysPlayed)
    {
        return SyncedChance(gameId, daysPlayed, "cart_retroCatalogue", 0.1)
            ? new CartItem("(F)RetroCatalogue", "Retro Catalogue", 110000, 1)
            : null;
    }

    public static CartItem? TryGetJojaCatalogue(ulong gameId, long daysPlayed, bool isCommunityCenterComplete)
    {
        if (!isCommunityCenterComplete)
            return null;

        return SyncedChance(gameId, daysPlayed, "cart_jojaCatalogue", 0.1)
            ? new CartItem("(F)JojaCatalogue", "Joja Catalogue", 30000, 1)
            : null;
    }

    public static CartItem? TryGetJunimoCatalogue(ulong gameId, long daysPlayed, bool isCommunityCenterComplete, bool isJojaComplete)
    {
        if (!(isCommunityCenterComplete || isJojaComplete))
            return null;

        return SyncedChance(gameId, daysPlayed, "cart_junimoCatalogue", 0.1)
            ? new CartItem("(F)JunimoCatalogue", "Junimo Catalogue", 70000, 1)
            : null;
    }

    public static CartItem? TryGetTeaSet(ulong gameId, long daysPlayed)
    {
        // Year 25+: daysPlayed >= 2688
        if (daysPlayed < 2688)
            return null;

        // stardew-predictor’s browse view uses 0.05; its search view had 0.1, but Shops.json says .05
        return SyncedChance(gameId, daysPlayed, "teaset", 0.05)
            ? new CartItem("(O)341", "Tea Set", 1000000, 1)
            : null;
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

    private static bool SyncedChance(ulong gameId, long daysPlayed, string key, double chance)
    {
        var rng = StardewRng.CreateSyncedDayRandom(gameId, daysPlayed, key);
        return rng.NextDouble() < chance;
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


}
