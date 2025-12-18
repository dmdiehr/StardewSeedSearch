namespace StardewSeedSearch.Core;

public sealed record CartItem(
    string QualifiedItemId,
    string Name,
    int Price,
    int Quantity,
    int AvailableStock
);