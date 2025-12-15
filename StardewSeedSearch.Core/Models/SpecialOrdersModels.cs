
namespace StardewSeedSearch.Core.SpecialOrders;
public sealed record SpecialOrderOffer(string Key, int GenerationSeed);

public sealed class SpecialOrderDataDto
{
    public string OrderType { get; set; } = "";
    public bool Repeatable { get; set; }
    public string Duration { get; set; } = "";     // "Week" / "TwoWeeks" / "Month"
    public string? RequiredTags { get; set; }
    public string? Condition { get; set; }         // town orders are empty in your json
}