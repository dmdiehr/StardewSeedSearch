
namespace StardewSeedSearch.Core.SpecialOrders;
public sealed record SpecialOrderOffer(
    string Key,
    int GenerationSeed,
    IReadOnlyDictionary<string, string> Randomized // elementName -> resolved value (e.g. chosen fish)
);

public sealed class SpecialOrderDataDto
{
    public string OrderType { get; set; } = "";
    public bool Repeatable { get; set; }
    public string Duration { get; set; } = "";
    public string? RequiredTags { get; set; }
    public string? Condition { get; set; }

    public List<RandomizedElementDto>? RandomizedElements { get; set; } // <-- add this
}


public sealed class RandomizedElementDto
{
    public string Name { get; set; } = "";
    public List<RandomizedElementValueDto> Values { get; set; } = new();
}

public sealed class RandomizedElementValueDto
{
    public string? RequiredTags { get; set; }
    public string Value { get; set; } = "";
}