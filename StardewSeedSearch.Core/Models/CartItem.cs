namespace StardewSeedSearch.Core;

public sealed class CartItem
{
    public string ItemId { get; set; } = "";   // e.g. "(O)72" or "(F)1234"
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public int Quantity { get; set; }          // stack size you receive
    public int AvailableStock { get; set; }    // how many times you can buy that listing
}