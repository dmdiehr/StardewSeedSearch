using System.Reflection;
using System.Text.Json;

namespace StardewSeedSearch.Core;

public static class QiSpecialOrderSimulator
{
    private static readonly Lazy<IReadOnlyDictionary<string, SpecialOrderDataDto>> _data =
        new(LoadEmbeddedSpecialOrders);

    public sealed record ActiveQi(string Key, int ExpiresOnDaysPlayed);

    public sealed record QiSimWeek(
        int WeekIndex,
        int MondayDaysPlayed,
        string Season,
        int DayOfMonth,
        IReadOnlyList<SpecialOrderOffer> Offers,
        string? ChosenKey,
        IReadOnlyList<ActiveQi> ActiveAfterChoice,
        IReadOnlyCollection<string> CompletedQiPerfection
    );

    public sealed record QiSimResult(
        IReadOnlyList<QiSimWeek> Weeks,
        int? PerfectionCompletedWeekIndex,
        int? PerfectionCompletedDaysPlayed
    );

    public static QiSimResult Simulate(
        ulong gameId,
        int startWeekIndex,
        int endWeekIndex,
        SpecialOrderSimSchedule? schedule = null)
    {
        schedule ??= new SpecialOrderSimSchedule(); // you said you updated defaults

        var data = _data.Value;

        var completedQiPerfection = new HashSet<string>(StringComparer.Ordinal);
        var active = new List<ActiveQi>();

        int? doneWeek = null;
        int? doneDaysPlayed = null;

        var weeks = new List<QiSimWeek>();

        for (int week = startWeekIndex; week <= endWeekIndex; week++)
        {
            var (yearIndex, seasonIndex, season, dayOfMonth, mondayDaysPlayed) = WeekIndexToMonday(week);

            // expire + complete
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].ExpiresOnDaysPlayed <= mondayDaysPlayed)
                {
                    ApplyQiCompletion(active[i].Key, completedQiPerfection);
                    active.RemoveAt(i);
                }
            }

            bool gingerIsland = schedule.GingerIslandUnlocked(week);
            bool resort = schedule.IslandResortUnlocked(week);
            bool sewing = schedule.SewingMachineUnlocked(week);

            var offers = SpecialOrderPredictor.GetQiOrders(
                gameId: gameId,
                weekIndex: week,
                gingerIslandUnlocked: gingerIsland,
                islandResortUnlocked: resort,
                sewingMachineUnlocked: sewing,
                completedSpecialOrders: Array.Empty<string>(),
                activeSpecialOrders: active.Select(a => a.Key).ToArray());

            var chosen = ChooseQiOffer(offers, completedQiPerfection);

            if (chosen is not null)
            {
                int expiry = ComputeExpiryDaysPlayed(chosen.Key, data, yearIndex, seasonIndex, mondayDaysPlayed);

                if (!active.Any(a => a.Key == chosen.Key))
                    active.Add(new ActiveQi(chosen.Key, expiry));
            }

            if (doneWeek is null && IsQiPerfectionSatisfied(completedQiPerfection))
            {
                doneWeek = week;
                doneDaysPlayed = mondayDaysPlayed;
            }

            weeks.Add(new QiSimWeek(
                WeekIndex: week,
                MondayDaysPlayed: mondayDaysPlayed,
                Season: season,
                DayOfMonth: dayOfMonth,
                Offers: offers,
                ChosenKey: chosen?.Key,
                ActiveAfterChoice: active.ToArray(),
                CompletedQiPerfection: completedQiPerfection.ToArray()
            ));
        }

        return new QiSimResult(weeks, doneWeek, doneDaysPlayed);
    }

    public static bool CanCompleteQiPerfectionByWeek(
        ulong gameId,
        int targetWeekIndex,
        int startWeekIndex = 14)
    {
        if (targetWeekIndex < startWeekIndex)
            return false;

        var schedule = new SpecialOrderSimSchedule(); // your defaults
        var data = _data.Value;

        var completedQiPerfection = new HashSet<string>(StringComparer.Ordinal);
        var active = new List<ActiveQi>(8);
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int week = startWeekIndex; week <= targetWeekIndex; week++)
        {
            var (yearIndex, seasonIndex, season, dayOfMonth, mondayDaysPlayed) = WeekIndexToMonday(week);
            _ = season; _ = dayOfMonth;

            // Expire + complete
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i];
                if (a.ExpiresOnDaysPlayed <= mondayDaysPlayed)
                {
                    active.RemoveAt(i);
                    activeKeys.Remove(a.Key);

                    // either-or completion
                    if (a.Key is "QiChallenge9" or "QiChallenge10")
                    {
                        completedQiPerfection.Add("QiChallenge9");
                        completedQiPerfection.Add("QiChallenge10");
                    }
                }
            }

            // Done?
            if (completedQiPerfection.Contains("QiChallenge9") || completedQiPerfection.Contains("QiChallenge10"))
                return true;

            bool gingerIsland = schedule.GingerIslandUnlocked(week);
            bool resort = schedule.IslandResortUnlocked(week);
            bool sewing = schedule.SewingMachineUnlocked(week);

            var offers = SpecialOrderPredictor.GetQiOrders(
                gameId: gameId,
                weekIndex: week,
                gingerIslandUnlocked: gingerIsland,
                islandResortUnlocked: resort,
                sewingMachineUnlocked: sewing,
                completedSpecialOrders: Array.Empty<string>(),
                activeSpecialOrders: activeKeys);

            // Choose one
            var chosen = ChooseQiOffer(offers, completedQiPerfection);
            if (chosen is not null)
            {
                int expiry = ComputeExpiryDaysPlayed(chosen.Key, data, yearIndex, seasonIndex, mondayDaysPlayed);

                if (activeKeys.Add(chosen.Key))
                    active.Add(new ActiveQi(chosen.Key, expiry));
            }
        }

        return false;
    }


    private static bool IsQiPerfectionSatisfied(HashSet<string> completedQiPerfection)
    {
        // either QiChallenge9 OR QiChallenge10 satisfies perfection
        return completedQiPerfection.Contains("QiChallenge9") || completedQiPerfection.Contains("QiChallenge10");
    }

    private static void ApplyQiCompletion(string key, HashSet<string> completedQiPerfection)
    {
        // either-or: if either completes, treat both as "done" for convenience (like your Demetrius trick)
        if (key is "QiChallenge9" or "QiChallenge10")
        {
            completedQiPerfection.Add("QiChallenge9");
            completedQiPerfection.Add("QiChallenge10");
        }
    }

    private static SpecialOrderOffer? ChooseQiOffer(
        IReadOnlyList<SpecialOrderOffer> offers,
        HashSet<string> completedQiPerfection)
    {
        if (offers.Count == 0)
            return null;

        // Prefer the perfection quest if not yet satisfied (lowest rank)
        var perf = offers
            .Where(o => o.RequiredForPerfection && !IsQiPerfectionSatisfied(completedQiPerfection))
            .OrderBy(o => o.Rank)
            .ToList();

        if (perf.Count > 0)
            return perf[0];

        // Otherwise lowest rank
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

        if (dur.Equals("OneDay", StringComparison.OrdinalIgnoreCase))
            return mondayDaysPlayed + 1;

        if (dur.Equals("TwoDays", StringComparison.OrdinalIgnoreCase))
            return mondayDaysPlayed + 2;

        if (dur.Equals("ThreeDays", StringComparison.OrdinalIgnoreCase))
            return mondayDaysPlayed + 3;

        if (dur.Equals("Week", StringComparison.OrdinalIgnoreCase))
            return mondayDaysPlayed + 7;

        if (dur.Equals("TwoWeeks", StringComparison.OrdinalIgnoreCase))
            return mondayDaysPlayed + 14;

        if (dur.Equals("Month", StringComparison.OrdinalIgnoreCase))
        {
            int nextSeasonIndex = (seasonIndex + 1) % 4;
            int nextYearIndex = yearIndex + (seasonIndex == 3 ? 1 : 0);
            int nextSeasonStartDaysPlayed = (nextYearIndex * 112) + (nextSeasonIndex * 28) + 1;
            return nextSeasonStartDaysPlayed;
        }

        return mondayDaysPlayed + 7;
    }

    private static (int yearIndex, int seasonIndex, string season, int dayOfMonth, int mondayDaysPlayed) WeekIndexToMonday(int weekIndex)
    {
        string[] seasons = ["spring", "summer", "fall", "winter"];

        int mondayDaysPlayed = 1 + (weekIndex - 1) * 7;
        int dayZero = mondayDaysPlayed - 1;

        int yearIndex = dayZero / 112;
        int dayOfYear = dayZero % 112;

        int seasonIndex = dayOfYear / 28;
        int dayOfMonth = (dayOfYear % 28) + 1;
        string season = seasons[seasonIndex];

        return (yearIndex, seasonIndex, season, dayOfMonth, mondayDaysPlayed);
    }

    private static IReadOnlyDictionary<string, SpecialOrderDataDto> LoadEmbeddedSpecialOrders()
    {
        var asm = typeof(QiSpecialOrderSimulator).Assembly;
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
