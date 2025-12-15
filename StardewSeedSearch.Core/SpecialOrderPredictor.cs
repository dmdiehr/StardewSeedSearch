using System.Text.Json;
using System.Reflection;

namespace StardewSeedSearch.Core.SpecialOrders;

public static class SpecialOrderPredictor
{
    private static readonly Lazy<IReadOnlyDictionary<string, SpecialOrderDataDto>> _data =
        new(LoadEmbeddedSpecialOrders);

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

        var data = _data.Value;

        var (season, dayOfMonth, daysPlayed) = MapWeekIndexToRefreshDay(weekIndex);

        // Mirror board unlock (DaysPlayed >= 58)
        if (weekIndex < 9)
            return Array.Empty<SpecialOrderOffer>();

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

            int generationSeed = r.Next(); // this is exactly what GetSpecialOrder receives
            results.Add(new SpecialOrderOffer(key, generationSeed));

            keyQueue.Remove(key);
            keysIncludingCompleted.Remove(key);
        }

        return results;
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

        // GameStateQuery.CheckConditions(order.Condition) is ignored for town orders
        // because Condition is empty for all town entries in your current json.

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

        // You said: assume all NPCs are known.
        if (tag.StartsWith("knows_", StringComparison.Ordinal))
            return true;

        // For town orders in your json, nothing else should appear.
        return false;
    }

    private static (string season, int dayOfMonth, int daysPlayed) MapWeekIndexToRefreshDay(int weekIndex)
    {
        if (weekIndex <= 0)
            throw new ArgumentOutOfRangeException(nameof(weekIndex), "weekIndex must be 1-based.");

        // weekIndex 1 => Spring 1 (DaysPlayed=1)
        int weekZero = weekIndex - 1;

        int yearIndex = weekZero / 16;               // unused currently
        int seasonIndex = (weekZero / 4) % 4;
        int weekOfSeason = weekZero % 4;

        string season = Seasons[seasonIndex];
        int mondayDayOfMonth = 1 + (weekOfSeason * 7);
        int mondayDaysPlayed = 1 + (weekZero * 7);


        _ = yearIndex;
        return (season, mondayDayOfMonth, mondayDaysPlayed);
    }

    private static IReadOnlyDictionary<string, SpecialOrderDataDto> LoadEmbeddedSpecialOrders()
    {
        var asm = typeof(SpecialOrderPredictor).Assembly;

        // Put the json at: StardewSeedSearch.Core/Assets/Data/SpecialOrders.json
        var resourceName = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("SpecialOrders.json", StringComparison.OrdinalIgnoreCase));

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var dict = JsonSerializer.Deserialize<Dictionary<string, SpecialOrderDataDto>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return dict ?? throw new InvalidOperationException("Failed to parse embedded SpecialOrders.json");
    }
}