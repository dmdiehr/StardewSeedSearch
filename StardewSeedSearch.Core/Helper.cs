namespace StardewSeedSearch.Core;

public static class Helper
{

    public static long GetDaysPlayed(int year, Season season, int dayOfMonth)
    {
        int seasonIndex = season switch
        {
            Season.Spring => 0,
            Season.Summer => 1,
            Season.Fall   => 2,
            Season.Winter => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(season))
        };

    // 0-based: Spring 1 Y1 => 0
    return ((year - 1) * 112L) + (seasonIndex * 28L) + (dayOfMonth - 1);
    }

    public static string GetSeasonName(Season season) => season switch
    {
        Season.Spring => "spring",
        Season.Summer => "summer",
        Season.Fall   => "fall",
        Season.Winter => "winter",
        _ => throw new ArgumentOutOfRangeException(nameof(season))
    };

}

