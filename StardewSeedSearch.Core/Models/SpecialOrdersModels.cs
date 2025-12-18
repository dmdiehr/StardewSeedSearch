
namespace StardewSeedSearch.Core;
public sealed record SpecialOrderOffer(
    string Key,
    string DisplayName,
    string? OrderItem,
    bool RequiredForPerfection,
    int Rank
);

public sealed class SpecialOrderDataDto
{
    public string OrderType { get; set; } = "";
    public bool Repeatable { get; set; }
    public string Duration { get; set; } = "";
    public string? RequiredTags { get; set; }
    public string? Condition { get; set; }

    public string? SpecialRule { get; set; }                 // <-- add
    public List<SpecialOrderObjectiveDto>? Objectives { get; set; } // <-- add

    public List<RandomizedElementDto>? RandomizedElements { get; set; }
}

public sealed class SpecialOrderObjectiveDto
{
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public string RequiredCount { get; set; } = "";
    public Dictionary<string, string>? Data { get; set; }
}


public sealed class SpecialOrderAugmentDto
{
    public string? DisplayName { get; set; }
    public bool RequiredForPerfection { get; set; }
    public int Rank { get; set; }
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