namespace StardewSeedSearch.Core;

    
public readonly record struct CartItem(string ItemId, string Name, int Price, int Quantity);

public sealed record TravelingCartStock(
    IReadOnlyList<CartItem> RandomObjects,
    CartItem Furniture
    // CartItem? SeasonalSpecial,
    // CartItem? CoffeeBean,
    // CartItem? RedFez,
    // CartItem? Catalogue1,
    // CartItem? Catalogue2,
    // CartItem? Catalogue3,
    // CartItem? TeaSet,
    // CartItem? SkillBook
);

/// <summary>
/// A lightweight representation of Data/Objects.json entries with the fields
/// needed for Traveling Cart RANDOM_ITEMS selection and later price/qty logic.
/// </summary>
public readonly struct RandomObjectCandidate
{
    public RandomObjectCandidate(
        string key,
        int id,
        string name,
        int price,
        int category,
        string type,
        bool excludeFromRandomSale)
    {
        Key = key;
        Id = id;
        Name = name;
        Price = price;
        Category = category;
        Type = type;
        ExcludeFromRandomSale = excludeFromRandomSale;
    }

    /// <summary>
    /// The JSON dictionary key (often used as the lookup key in other codebases).
    /// Kept for debugging and future expansion.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The numeric object ID (what the shop constraint "2..789" applies to).
    /// </summary>
    public int Id { get; }

    public string Name { get; }
    public int Price { get; }
    public int Category { get; }
    public string Type { get; }

    /// <summary>
    /// Matches the traveling cart "@isRandomSale" / "ExcludeFromRandomSale" meaning.
    /// </summary>
    public bool ExcludeFromRandomSale { get; }
}


public readonly struct FurnitureCandidate
{
    public FurnitureCandidate(string key, int id, string name, int price, bool excludeFromRandomSale)
    {
        Key = key;
        Id = id;
        Name = name;
        Price = price;
        ExcludeFromRandomSale = excludeFromRandomSale;
    }

    public string Key { get; }
    public int Id { get; }
    public string Name { get; }
    public int Price { get; }
    public bool ExcludeFromRandomSale { get; }
}