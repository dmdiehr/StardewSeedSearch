using System.Reflection;
using System.Text.Json;

namespace StardewSeedSearch.Core.SpecialOrders;

public static class SpecialOrderPredictor
{
    private static readonly Lazy<IReadOnlyDictionary<string, SpecialOrderDataDto>> _data =
        new(LoadEmbeddedSpecialOrders);

    private static readonly Lazy<IReadOnlyDictionary<string, SpecialOrderAugmentDto>> _augment =
        new(LoadEmbeddedSpecialOrdersAugment);

    private static readonly string[] Seasons = ["spring", "summer", "fall", "winter"];

    public static IReadOnlyList<SpecialOrderOffer> GetTownOrders(
        ulong gameId,
        int weekIndex,
        bool gingerIslandUnlocked = false,
        bool islandResortUnlocked = false,
        bool sewingMachineUnlocked = false,
        IReadOnlyCollection<string>? completedSpecialOrders = null,
        IReadOnlyCollection<string>? activeSpecialOrders = null)
    {
        completedSpecialOrders ??= Array.Empty<string>();
        activeSpecialOrders ??= Array.Empty<string>();

        if (weekIndex < 9)
            return Array.Empty<SpecialOrderOffer>();

        var data = _data.Value;
        var augment = _augment.Value;

        var (season, dayOfMonth, daysPlayed) = MapWeekIndexToRefreshDay(weekIndex);

        // Random r = Utility.CreateRandom(Game1.uniqueIDForThisGame, (double)DaysPlayed * 1.3);
        var r = StardewRng.CreateRandom((double)gameId, (double)daysPlayed * 1.3);

        // Build candidate list via CanStartOrderNow(key, data)
        var keyQueue = new List<string>();
        foreach (var (key, order) in data)
        {
            if (order.OrderType.Trim() != "")
                continue;

            if (CanStartOrderNow(
                    orderId: key,
                    order: order,
                    season: season,
                    dayOfMonth: dayOfMonth,
                    gingerIslandUnlocked: gingerIslandUnlocked,
                    islandResortUnlocked: islandResortUnlocked,
                    sewingMachineUnlocked: sewingMachineUnlocked,
                    completedSpecialOrders: completedSpecialOrders,
                    activeSpecialOrders: activeSpecialOrders))
            {
                keyQueue.Add(key);
            }
        }

        var keysIncludingCompleted = new List<string>(keyQueue);

        // Matches UpdateAvailableSpecialOrders:
        // if (orderType=="") keyQueue.RemoveAll(id => completed.Contains(id));
        keyQueue.RemoveAll(id => completedSpecialOrders.Contains(id));

        var results = new List<SpecialOrderOffer>(2);

        for (int i = 0; i < 2; i++)
        {
            if (keyQueue.Count == 0)
            {
                if (keysIncludingCompleted.Count == 0)
                    break;

                keyQueue = new List<string>(keysIncludingCompleted);
            }

            var key = r.ChooseFrom(keyQueue);
            if (string.IsNullOrEmpty(key))
                break;

            // This is exactly what GetSpecialOrder(key, r.Next()) uses internally.
            int generationSeed = r.Next();

            string? orderItem = ResolveOrderItem(
                orderKey: key,
                orderData: data[key],
                generationSeed: generationSeed,
                season: season,
                gingerIslandUnlocked: gingerIslandUnlocked,
                islandResortUnlocked: islandResortUnlocked,
                sewingMachineUnlocked: sewingMachineUnlocked,
                completedSpecialOrders: completedSpecialOrders);

            var (baseName, requiredForPerfection, rank) = GetAugment(augment, key);

            // Compose display name in the style you want: "Cave Patrol - Grub"
            string displayName = baseName;
            if (!string.IsNullOrWhiteSpace(orderItem))
                displayName = $"{displayName} - {orderItem}";

            results.Add(new SpecialOrderOffer(
                Key: key,
                DisplayName: displayName,
                OrderItem: orderItem,
                RequiredForPerfection: requiredForPerfection,
                Rank: rank));

            keyQueue.Remove(key);
            keysIncludingCompleted.Remove(key);
        }

        return results;
    }

    private static (string DisplayName, bool RequiredForPerfection, int Rank) GetAugment(
        IReadOnlyDictionary<string, SpecialOrderAugmentDto> augment,
        string key)
    {
        if (augment.TryGetValue(key, out var a))
        {
            var name = string.IsNullOrWhiteSpace(a.DisplayName) ? key : a.DisplayName!;
            return (name, a.RequiredForPerfection, a.Rank);
        }

        return (key, false, 0);
    }

    private static bool CanStartOrderNow(
        string orderId,
        SpecialOrderDataDto order,
        string season,
        int dayOfMonth,
        bool gingerIslandUnlocked,
        bool islandResortUnlocked,
        bool sewingMachineUnlocked,
        IReadOnlyCollection<string> completedSpecialOrders,
        IReadOnlyCollection<string> activeSpecialOrders)
    {
        // if (!order.Repeatable && completed.Contains(orderId)) return false;
        if (!order.Repeatable && completedSpecialOrders.Contains(orderId))
            return false;

        // if (dayOfMonth >= 16 && order.Duration == Month) return false;
        if (dayOfMonth >= 16 && string.Equals(order.Duration, "Month", StringComparison.OrdinalIgnoreCase))
            return false;

        // if (!CheckTags(order.RequiredTags)) return false;
        if (!CheckTags(
                order.RequiredTags,
                season,
                gingerIslandUnlocked,
                islandResortUnlocked,
                sewingMachineUnlocked,
                completedSpecialOrders))
            return false;

        // GameStateQuery.CheckConditions(order.Condition) ignored for town orders
        // (Condition is empty for all OrderType=="" entries in your current json).

        // foreach (active specialOrders) if (questKey == orderId) return false;
        if (activeSpecialOrders.Contains(orderId))
            return false;

        return true;
    }

    private static bool CheckTags(
        string? tagList,
        string season,
        bool gingerIslandUnlocked,
        bool islandResortUnlocked,
        bool sewingMachineUnlocked,
        IReadOnlyCollection<string> completedSpecialOrders)
    {
        if (tagList is null)
            return true;

        var tags = tagList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in tags)
        {
            var tag = raw;
            bool shouldMatch = true;

            if (tag.StartsWith('!'))
            {
                shouldMatch = false;
                tag = tag[1..];
            }

            bool actual = CheckTag(
                tag, season, gingerIslandUnlocked, islandResortUnlocked, sewingMachineUnlocked, completedSpecialOrders);

            if (actual != shouldMatch)
                return false;
        }

        return true;
    }

    private static bool CheckTag(
        string tag,
        string season,
        bool gingerIslandUnlocked,
        bool islandResortUnlocked,
        bool sewingMachineUnlocked,
        IReadOnlyCollection<string> completedSpecialOrders)
    {
        if (tag == "NOT_IMPLEMENTED")
            return false;

        if (tag.StartsWith("season_", StringComparison.Ordinal))
            return string.Equals(season, tag["season_".Length..], StringComparison.OrdinalIgnoreCase);

        if (tag == "island")
            return gingerIslandUnlocked;

        if (tag == "mail_Island_Resort")
            return islandResortUnlocked;

        if (tag == "event_992559")
            return sewingMachineUnlocked;

        if (tag.StartsWith("completed_", StringComparison.Ordinal))
            return completedSpecialOrders.Contains(tag["completed_".Length..]);

        // assume all NPCs are known
        if (tag.StartsWith("knows_", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string? ResolveOrderItem(
        string orderKey,
        SpecialOrderDataDto orderData,
        int generationSeed,
        string season,
        bool gingerIslandUnlocked,
        bool islandResortUnlocked,
        bool sewingMachineUnlocked,
        IReadOnlyCollection<string> completedSpecialOrders)
    {
        if (orderData.RandomizedElements is null || orderData.RandomizedElements.Count == 0)
            return null;

        // Mirrors: Random r = Utility.CreateRandom(generation_seed.Value);
        var r = StardewRng.CreateRandom(generationSeed);

        foreach (var element in orderData.RandomizedElements)
        {
            // pick index exactly like the game
            var validIndices = new List<int>();
            for (int i = 0; i < element.Values.Count; i++)
            {
                if (CheckTags(element.Values[i].RequiredTags, season,
                        gingerIslandUnlocked, islandResortUnlocked, sewingMachineUnlocked, completedSpecialOrders))
                    validIndices.Add(i);
            }

            int selectedIndex = validIndices.Count > 0 ? r.ChooseFrom(validIndices) : 0;
            if (selectedIndex < 0 || selectedIndex >= element.Values.Count)
                selectedIndex = 0;

            string value = element.Values[selectedIndex].Value;

            // Case 1: PICK_ITEM <list>
            if (value.StartsWith("PICK_ITEM", StringComparison.Ordinal))
            {
                string list = value.Substring("PICK_ITEM".Length);
                var options = list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var chosen = options.Length > 0 ? (r.ChooseFrom(options) ?? "") : "";
                return string.IsNullOrWhiteSpace(chosen) ? null : chosen;
            }

            // Case 2: Clint style "Target|Grub|LocalizedName|[...]"
            if (value.StartsWith("Target|", StringComparison.Ordinal))
            {
                var parts = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                    return parts[1]; // e.g. "Grub"
            }

            // Case 3: Demetrius2 fish pool "…|Tags|fish_river"
            if (value.Contains("|Tags|fish_", StringComparison.Ordinal))
            {
                if (value.Contains("fish_river", StringComparison.Ordinal)) return "River Fish";
                if (value.Contains("fish_ocean", StringComparison.Ordinal)) return "Ocean Fish";
                if (value.Contains("fish_lake", StringComparison.Ordinal)) return "Lake Fish";
            }

            // Otherwise: ignore (text randomization etc.)
        }

        return null;
    }

    private static (string season, int dayOfMonth, int daysPlayed) MapWeekIndexToRefreshDay(int weekIndex)
    {
        if (weekIndex <= 0)
            throw new ArgumentOutOfRangeException(nameof(weekIndex), "weekIndex must be 1-based.");

        // weekIndex 1 => Spring 1 (DaysPlayed=1)
        int weekZero = weekIndex - 1;

        int seasonIndex = (weekZero / 4) % 4;
        int weekOfSeason = weekZero % 4;

        string season = Seasons[seasonIndex];
        int mondayDayOfMonth = 1 + (weekOfSeason * 7);
        int mondayDaysPlayed = 1 + (weekZero * 7);

        return (season, mondayDayOfMonth, mondayDaysPlayed);
    }

    private static IReadOnlyDictionary<string, SpecialOrderDataDto> LoadEmbeddedSpecialOrders()
    {
        var json = ReadEmbeddedTextOrThrow("SpecialOrders.json");
        var dict = JsonSerializer.Deserialize<Dictionary<string, SpecialOrderDataDto>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return dict ?? throw new InvalidOperationException("Failed to parse embedded SpecialOrders.json");
    }

    private static IReadOnlyDictionary<string, SpecialOrderAugmentDto> LoadEmbeddedSpecialOrdersAugment()
    {
        // Optional: if you haven't added the file yet, we just return empty and fall back to defaults.
        var json = ReadEmbeddedTextOrNull("SpecialOrders.augment.json");
        if (json is null)
            return new Dictionary<string, SpecialOrderAugmentDto>();

        var dict = JsonSerializer.Deserialize<Dictionary<string, SpecialOrderAugmentDto>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return dict ?? new Dictionary<string, SpecialOrderAugmentDto>();
    }

    private static string ReadEmbeddedTextOrThrow(string endsWith)
    {
        var asm = typeof(SpecialOrderPredictor).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase));

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? ReadEmbeddedTextOrNull(string endsWith)
    {
        var asm = typeof(SpecialOrderPredictor).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return null;

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

public sealed class SpecialOrderAugmentDto
{
    public string? DisplayName { get; set; }
    public bool RequiredForPerfection { get; set; }
    public int Rank { get; set; }
}
