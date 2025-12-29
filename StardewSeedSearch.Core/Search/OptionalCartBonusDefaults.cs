using StardewSeedSearch.Core;

namespace StardewSeedSearch.Core.Search;

public static class OptionalCartBonusDefaults
{
    // IMPORTANT: these int[] option arrays are allocated once.
    // Replace these IDs with your actual constants if you have them.

    private static readonly int[] RabbitsFoot = { /* Rabbits_Foot */ 446 };
    private static readonly int[] Caviar = { /* Caviar */ 445 };
    private static readonly int[] VincentBday =
    {
        /* Snail */ 721,
        /* Grape */ 398,
        /* Cranberry_Candy */ 612
    };

    private static readonly int[] JasBday =
    {
        /* Fairy_Rose */ 595,
        /* Pink_Cake */ 221
    };

    private static readonly int[] LewisBday =
    {
        /* Glazed_Yams */ 208,
        /* Green_Tea */ 614,
        /* Hot_Pepper */ 260,
        /* Vegetable_Medley */ 200
    };

    private static readonly int[] AlexBday =
    {
        /* Complete_Breakfast */ 201,
        /* Salmon_Dinner */ 212
    };

    private static readonly int[] HaleyBday =
    {
        /* Coconut */ 88,
        /* Sunflower */ 421
    };

    private static readonly int[] PierreBday = { /* Fried_Calamari */ 202 };

    private static readonly int[] CoffeeBean = { /* Coffee_Bean */ 433 };

    public static readonly Demand[] OptionalBonusDemands =
    {
        new Demand(10,  1, VincentBday),
        new Demand(32,  1, JasBday),
        new Demand(7,   1, LewisBday),
        new Demand(40, 1, AlexBday),
        new Demand(14, 1, HaleyBday),
        new Demand(28, 1, PierreBday),
        new Demand(35, 1, CoffeeBean),
        new Demand(35, 5, CoffeeBean)
    };

    public static string FormatOptionalCartMask(ushort mask)
    {
        if (mask == 0) return "-";

        // Names correspond to OptionalBonusDemands order
        string[] names =
        {
            "Vincent Birthday",
            "Jas Birthday",
            "Lewis Birthday",
            "Alex Birthday",
            "Haley Birthday",
            "Pierre Birthday",
            "Coffee Bean",
            "Lot's of Coffee"

        };

        var parts = new System.Collections.Generic.List<string>(names.Length);
        for (int i = 0; i < names.Length; i++)
            if ((mask & (1 << i)) != 0) parts.Add(names[i]);

        return string.Join(", ", parts);
    }

}
