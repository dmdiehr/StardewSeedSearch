namespace StardewSeedSearch.Core;

public static class Helper
{

    public static long GetDaysPlayedZeroBased(int year, Season season, int dayOfMonth)
    {
        return GetDaysPlayedOneBased(year, season, dayOfMonth) - 1;
    }

    public static long GetDaysPlayedOneBased(int year, Season season, int dayOfMonth)
    {
                int seasonIndex = season switch
        {
            Season.Spring => 0,
            Season.Summer => 1,
            Season.Fall   => 2,
            Season.Winter => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(season))
        };

        // 01based: Spring 1 Y1 => 1
        return ((year - 1) * 112L) + (seasonIndex * 28L) + dayOfMonth;
    }

    public static string GetSeasonName(Season season) => season switch
    {
        Season.Spring => "spring",
        Season.Summer => "summer",
        Season.Fall   => "fall",
        Season.Winter => "winter",
        _ => throw new ArgumentOutOfRangeException(nameof(season))
    };

    public static Season GetSeasonFromDaysPlayed(long daysPlayed)
    {
        // SDV: 28 days/season, 4 seasons/year (112 days/year)
        // daysPlayed is 0-based (Spring 1 Y1 => 0)
        // season index = (daysPlayed / 28) % 4
        int seasonIndex = (int)((daysPlayed / 28) % 4);

        return seasonIndex switch
        {
            0 => Season.Spring,
            1 => Season.Summer,
            2 => Season.Fall,
            3 => Season.Winter,
            _ => throw new ArgumentOutOfRangeException(nameof(daysPlayed), daysPlayed, "Invalid season index derived from daysPlayed.")
        };
    }
}

