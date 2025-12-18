namespace StardewSeedSearch.Core;

/// <summary>
/// One Traveling Cart listing (item + price + quantity).
/// </summary>
public sealed record CartItem(
    string QualifiedItemId,
    string Name,
    int Price,
    int Quantity
);