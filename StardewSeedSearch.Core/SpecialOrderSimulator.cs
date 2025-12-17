using System.Reflection;
using System.Text.Json;

namespace StardewSeedSearch.Core.SpecialOrders;

public sealed record SpecialOrderSimSchedule(int GingerIslandUnlockWeek = 11,int IslandResortUnlockWeek = 13,int SewingMachineUnlockWeek = 9)
{
    public bool GingerIslandUnlocked(int week) => week >= GingerIslandUnlockWeek;
    public bool IslandResortUnlocked(int week) => week >= IslandResortUnlockWeek;
    public bool SewingMachineUnlocked(int week) => week >= SewingMachineUnlockWeek;
}

public sealed record SpecialOrderActive(string Key, int ExpiresOnDaysPlayed);

public sealed record SpecialOrderSimWeek(
    int WeekIndex,
    int MondayDaysPlayed,
    string Season,
    int DayOfMonth,
    IReadOnlyList<SpecialOrderOffer> Offers,
    string? ChosenKey,
    IReadOnlyList<SpecialOrderActive> ActiveAfterChoice,
    IReadOnlyCollection<string> CompletedForGamePool,
    IReadOnlyCollection<string> CompletedPerfection
);

public sealed record SpecialOrderSimResult(
    IReadOnlyList<SpecialOrderSimWeek> Weeks,
    int? PerfectionCompletedWeekIndex,
    int? PerfectionCompletedDaysPlayed
);

public static class SpecialOrderSimulator
{
    private static readonly string[][] PerfectionEitherOrGroups =
    {
        new[] { "Demetrius", "Demetrius2" }
    };
    private static readonly Lazy<IReadOnlyDictionary<string, SpecialOrderDataDto>> _data =
        new(LoadEmbeddedSpecialOrders);

    private static readonly Lazy<IReadOnlyDictionary<string, SpecialOrderAugmentDto>> _augment =
        new(LoadEmbeddedSpecialOrdersAugment);

    private static readonly string[] Seasons = ["spring", "summer", "fall", "winter"];

    /// <summary>
    /// Deterministic simulation: each week, pick ONE order from the two offers,
    /// keep it active until its expiry, and mark it completed at expiry.
    ///
    /// Active orders only prevent THAT SAME KEY from being offered again (matches CanStartOrderNow).
    /// Active orders do NOT prevent picking a new order next Monday.
    /// </summary>
    public static SpecialOrderSimResult SimulateTown(
        ulong gameId,
        int startWeekIndex,
        int endWeekIndex,
        SpecialOrderSimSchedule schedule,
        IReadOnlyCollection<string>? initialCompletedForGamePool = null,
        IReadOnlyCollection<string>? initialCompletedPerfection = null,
        IReadOnlyCollection<SpecialOrderActive>? initialActive = null)
    {
        if (startWeekIndex <= 0) throw new ArgumentOutOfRangeException(nameof(startWeekIndex));
        if (endWeekIndex < startWeekIndex) throw new ArgumentOutOfRangeException(nameof(endWeekIndex));

        var data = _data.Value;
        var augment = _augment.Value;

        // Targets: all orders marked RequiredForPerfection in augment
        var perfectionTargets = augment
            .Where(kv => kv.Value.RequiredForPerfection)
            .Select(kv => kv.Key)
            .ToHashSet();

        var completedForGamePool = initialCompletedForGamePool is null
            ? new HashSet<string>()
            : new HashSet<string>(initialCompletedForGamePool);

        var completedPerfection = initialCompletedPerfection is null
            ? new HashSet<string>()
            : new HashSet<string>(initialCompletedPerfection);

        var active = initialActive is null
            ? new List<SpecialOrderActive>()
            : new List<SpecialOrderActive>(initialActive);

        int? perfectionDoneWeek = null;
        int? perfectionDoneDaysPlayed = null;

        var weeks = new List<SpecialOrderSimWeek>();

        for (int week = startWeekIndex; week <= endWeekIndex; week++)
        {
            var (yearIndex, seasonIndex, season, dayOfMonth, mondayDaysPlayed) = WeekIndexToMonday(week);

            // 1) Expire + complete anything whose expiry is <= this Monday
            ExpireAndComplete(
                mondayDaysPlayed,
                active,
                data,
                augment,
                completedForGamePool,
                completedPerfection);

            // 2) Compute flags for this week
            bool gingerIsland = schedule.GingerIslandUnlocked(week);
            bool resort = schedule.IslandResortUnlocked(week);
            bool sewing = schedule.SewingMachineUnlocked(week);

            // 3) Predict offers (active keys prevent re-offering those keys)
            var offers = SpecialOrderPredictor.GetTownOrders(
                gameId: gameId,
                weekIndex: week,
                gingerIslandUnlocked: gingerIsland,
                islandResortUnlocked: resort,
                sewingMachineUnlocked: sewing,
                completedSpecialOrders: completedForGamePool.ToArray(),
                activeSpecialOrders: active.Select(a => a.Key).ToArray());

            // 4) Choose ONE to accept (perfection-first, then lowest rank)
            var chosen = ChooseOffer(offers, completedPerfection);

            // 5) Add chosen as active (can coexist with other active orders)
            if (chosen is not null)
            {
                int expiry = ComputeExpiryDaysPlayed(
                    chosen.Key,
                    data,
                    yearIndex,
                    seasonIndex,
                    mondayDaysPlayed);

                // If for some reason it's already active, don't duplicate.
                if (!active.Any(a => a.Key == chosen.Key))
                    active.Add(new SpecialOrderActive(chosen.Key, expiry));
            }

            // 6) Check completion milestone AFTER any expiries this week (and after choice doesn’t count yet)
            if (perfectionDoneWeek is null && perfectionTargets.Count > 0)
            {
                if (perfectionTargets.All(completedPerfection.Contains))
                {
                    perfectionDoneWeek = week;
                    perfectionDoneDaysPlayed = mondayDaysPlayed;
                }
            }

            weeks.Add(new SpecialOrderSimWeek(
                WeekIndex: week,
                MondayDaysPlayed: mondayDaysPlayed,
                Season: season,
                DayOfMonth: dayOfMonth,
                Offers: offers,
                ChosenKey: chosen?.Key,
                ActiveAfterChoice: active.ToArray(),
                CompletedForGamePool: completedForGamePool.ToArray(),
                CompletedPerfection: completedPerfection.ToArray()
            ));
        }

        return new SpecialOrderSimResult(
            Weeks: weeks,
            PerfectionCompletedWeekIndex: perfectionDoneWeek,
            PerfectionCompletedDaysPlayed: perfectionDoneDaysPlayed
        );
    }

    public static bool CanCompleteTownPerfectionByWeek(
    ulong gameId,
    int targetWeekIndex)
    {
        if (targetWeekIndex < 9)
            return false;

        var schedule = new SpecialOrderSimSchedule(); // defaults (never unlock)
        var sim = SimulateTown(
            gameId: gameId,
            startWeekIndex: 9,
            endWeekIndex: targetWeekIndex,
            schedule: schedule);

        return sim.PerfectionCompletedWeekIndex.HasValue
            && sim.PerfectionCompletedWeekIndex.Value <= targetWeekIndex;
    }


    private static void ExpireAndComplete(
        int mondayDaysPlayed,
        List<SpecialOrderActive> active,
        IReadOnlyDictionary<string, SpecialOrderDataDto> data,
        IReadOnlyDictionary<string, SpecialOrderAugmentDto> augment,
        HashSet<string> completedForGamePool,
        HashSet<string> completedPerfection)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var a = active[i];
            if (a.ExpiresOnDaysPlayed <= mondayDaysPlayed)
            {
                active.RemoveAt(i);
                ApplyCompletion(a.Key, data, augment, completedForGamePool, completedPerfection);
            }
        }
    }

    private static void ApplyCompletion(
        string key,
        IReadOnlyDictionary<string, SpecialOrderDataDto> data,
        IReadOnlyDictionary<string, SpecialOrderAugmentDto> augment,
        HashSet<string> completedForGamePool,
        HashSet<string> completedPerfection)
    {
        if (augment.TryGetValue(key, out var a) && a.RequiredForPerfection)
        {
            if (key is "Demetrius" or "Demetrius2")
            {
                completedPerfection.Add("Demetrius");
                completedPerfection.Add("Demetrius2");
            }
            else
            {
                completedPerfection.Add(key);
            }
        }
        // mimic pool behavior you observed/targeted: only non-repeatables should land in completedForGamePool
        if (data.TryGetValue(key, out var d) && !d.Repeatable)
            completedForGamePool.Add(key);
    }

    private static SpecialOrderOffer? ChooseOffer(
        IReadOnlyList<SpecialOrderOffer> offers,
        HashSet<string> completedPerfection)
    {
        if (offers.Count == 0)
            return null;

        // 1) Prefer perfection-required orders not yet completed (lowest rank)
        var perf = offers
            .Where(o => o.RequiredForPerfection && !completedPerfection.Contains(o.Key))
            .OrderBy(o => o.Rank)
            .ToList();

        if (perf.Count > 0)
            return perf[0];

        // 2) Otherwise prefer non-perfection orders (lowest rank)
        var nonPerf = offers
            .Where(o => !o.RequiredForPerfection)
            .OrderBy(o => o.Rank)
            .ToList();

        if (nonPerf.Count > 0)
            return nonPerf[0];

        // 3) Fallback (shouldn’t happen): lowest rank overall
        return offers.OrderBy(o => o.Rank).First();
    }

    private static int ComputeExpiryDaysPlayed(
        string key,
        IReadOnlyDictionary<string, SpecialOrderDataDto> data,
        int yearIndex,
        int seasonIndex,
        int mondayDaysPlayed)
    {
        if (!data.TryGetValue(key, out var d))
            return mondayDaysPlayed + 7;

        var dur = d.Duration?.Trim() ?? "Week";

        if (dur.Equals("Week", StringComparison.OrdinalIgnoreCase))
            return mondayDaysPlayed + 7;

        if (dur.Equals("TwoWeeks", StringComparison.OrdinalIgnoreCase))
            return mondayDaysPlayed + 14;

        if (dur.Equals("Month", StringComparison.OrdinalIgnoreCase))
        {
            // Expires at start of next season (day 1 next season)
            int nextSeasonIndex = (seasonIndex + 1) % 4;
            int nextYearIndex = yearIndex + (seasonIndex == 3 ? 1 : 0);
            int nextSeasonStartDaysPlayed = (nextYearIndex * 112) + (nextSeasonIndex * 28) + 1;
            return nextSeasonStartDaysPlayed;
        }

        // Default
        return mondayDaysPlayed + 7;
    }

    private static (int yearIndex, int seasonIndex, string season, int dayOfMonth, int mondayDaysPlayed) WeekIndexToMonday(int weekIndex)
    {
        // Week 1 -> DaysPlayed 1, Week 2 -> 8, ...
        int mondayDaysPlayed = 1 + (weekIndex - 1) * 7;
        int dayZero = mondayDaysPlayed - 1;

        int yearIndex = dayZero / 112;
        int dayOfYear = dayZero % 112;

        int seasonIndex = dayOfYear / 28;              // 0..3
        int dayOfMonth = (dayOfYear % 28) + 1;         // 1..28
        string season = Seasons[seasonIndex];

        return (yearIndex, seasonIndex, season, dayOfMonth, mondayDaysPlayed);
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
        var json = ReadEmbeddedTextOrNull("SpecialOrders.augment.json");
        if (json is null)
            return new Dictionary<string, SpecialOrderAugmentDto>();

        var dict = JsonSerializer.Deserialize<Dictionary<string, SpecialOrderAugmentDto>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return dict ?? new Dictionary<string, SpecialOrderAugmentDto>();
    }

    private static string ReadEmbeddedTextOrThrow(string endsWith)
    {
        var asm = typeof(SpecialOrderSimulator).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase));

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? ReadEmbeddedTextOrNull(string endsWith)
    {
        var asm = typeof(SpecialOrderSimulator).Assembly;
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
