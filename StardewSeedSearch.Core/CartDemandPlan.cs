using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewSeedSearch.Core;

/// <summary>
/// Precompiled, allocation-minimized representation of the cart demand problem.
/// Build once, reuse across millions of gameIds.
/// </summary>
public sealed class CartDemandPlan
{
    public CartDemandPlan(
        int[] watchedObjectIdsSorted,
        CompiledDemand[] demandsSortedByDeadline)
    {
        WatchedObjectIdsSorted = watchedObjectIdsSorted ?? throw new ArgumentNullException(nameof(watchedObjectIdsSorted));
        DemandsSortedByDeadline = demandsSortedByDeadline ?? throw new ArgumentNullException(nameof(demandsSortedByDeadline));
    }

    /// <summary>
    /// Sorted ascending. Enables Array.BinarySearch for id->index.
    /// </summary>
    public int[] WatchedObjectIdsSorted { get; }

    /// <summary>
    /// Sorted by deadline ascending.
    /// Each demand has its options already mapped to watched-indexes.
    /// </summary>
    public CompiledDemand[] DemandsSortedByDeadline { get; }

    public readonly record struct CompiledDemand(int DeadlineDaysPlayed, int Quantity, int[] WatchedOptionIndexes);
}

public static class CartDemandPlanCompiler
{
    public static CartDemandPlan Compile(IReadOnlyList<Demand> demands)
    {
        if (demands is null) throw new ArgumentNullException(nameof(demands));
        if (demands.Count == 0)
            return new CartDemandPlan(Array.Empty<int>(), Array.Empty<CartDemandPlan.CompiledDemand>());

        // watched = union of all option IDs
        int[] watched = demands.SelectMany(d => d.OptionsObjectIds).Distinct().ToArray();
        Array.Sort(watched);

        int FindWatchedIndex(int objectId)
        {
            int ix = Array.BinarySearch(watched, objectId);
            if (ix < 0)
                throw new InvalidOperationException("watched set missing an option id (should be impossible).");
            return ix;
        }

        var compiled = demands
            .OrderBy(d => d.DeadlineDaysPlayed)
            .Select(d => new CartDemandPlan.CompiledDemand(
                DeadlineDaysPlayed: d.DeadlineDaysPlayed,
                Quantity: d.Quantity,
                WatchedOptionIndexes: d.OptionsObjectIds.Select(FindWatchedIndex).ToArray()
            ))
            .ToArray();

        return new CartDemandPlan(watched, compiled);
    }
}
