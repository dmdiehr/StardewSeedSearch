using Xunit;
using Xunit.Abstractions;
using StardewSeedSearch.Core;

namespace StardewSeedSearch.Tests;

public sealed class TravelingCartPredictorTests
{
    private readonly ITestOutputHelper _output;
    public TravelingCartPredictorTests(ITestOutputHelper output) => _output = output;
    private static string Format(CartItem item)
        => $"{item.ItemId} {item.Name} - {item.Price}g x{item.Quantity}";

    [Fact]
    public void PrintCartStock_SmokeTest()
    {
        ulong gameId = 123456;
        //long  daysPlayed = Helper.GetDaysPlayedOneBased(2, Season.Spring, 17);

        var stock = TravelingCartPredictor.GetCartStock(gameId, 129, true);

        _output.WriteLine("");

        if (stock is null)
        {
            _output.WriteLine("No Cart Today");
        }
        else
        {
            //Location
            _output.WriteLine("==== Location ===");
            _output.WriteLine(stock.CartLocation.ToString());

            // Random objects
            _output.WriteLine("=== Random Items (10) ===");
            foreach (var item in stock.RandomItems)
                _output.WriteLine(Format(item));

            _output.WriteLine("");

            // Furniture
            _output.WriteLine("=== Furniture (1) ===");
            _output.WriteLine(Format(stock.Furniture));
            _output.WriteLine("");

            // Specials (nullable)
            _output.WriteLine("=== Specials ===");
            PrintOptional("Seasonal Special", stock.SeasonalSpecial);
            PrintOptional("Coffee Bean", stock.CoffeeBean);
            PrintOptional("Red Fez", stock.RedFez);
            PrintOptional("Joja Catalogue", stock.JojaCatalogue);
            PrintOptional("Junimo Catalogue", stock.JunimoCatalogue);
            PrintOptional("Retro Catalogue", stock.RetroCatalogue);
            PrintOptional("Tea Set", stock.TeaSet);
            PrintOptional("Skill Book", stock.SkillBook);   
            }
    }

    private void PrintOptional(string label, CartItem? item)
    {
        if (item is null)
            _output.WriteLine($"{label}: (None)");
        else
            _output.WriteLine($"{label}: {Format(item.Value)}");
    }
}
