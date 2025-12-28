using StardewSeedSearch.Core;

namespace StardewSeedSearch.Core.Search;

public static class OptionalCartBonusDefaults
{
    // IMPORTANT: these int[] option arrays are allocated once.
    // Replace these IDs with your actual constants if you have them.

    private static readonly int[] CoffeeBean = { /* Coffee_Bean */ 433 };
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
        /* Pink_Cake */ 221, 
        /* Plum_Pudding */ 604
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

    public static readonly Demand[] OptionalBonusDemands =
    {
        new Demand(35,  1, CoffeeBean),
        new Demand(10,  1, VincentBday),
        new Demand(32,  1, JasBday),
        new Demand(7,   1, LewisBday),
        new Demand(112, 2, RabbitsFoot),
    };

    public static string FormatOptionalCartMask(ushort mask)
    {
        if (mask == 0) return "-";

        // Names correspond to OptionalBonusDemands order
        string[] names =
        {
            "Coffee Bean",
            "Vincent Birthday",
            "Jas Birthday",
            "Lewis Birthday",
            "Rabbit's Foot",
        };

        var parts = new System.Collections.Generic.List<string>(names.Length);
        for (int i = 0; i < names.Length; i++)
            if ((mask & (1 << i)) != 0) parts.Add(names[i]);

        return string.Join(", ", parts);
    }

}
