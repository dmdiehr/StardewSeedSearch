using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewSeedSearch.Core;

public static class CartSeedDemandEvaluator
{
    /// <summary>
    /// Returns true if this seed can satisfy all demands using only Y1 Forest cart random objects,
    /// respecting deadlines and quantity capacities (qty 1 or 5).
    /// </summary>
    public static bool SeedSatisfiesDemandsY1Forest(ulong gameId, IReadOnlyList<Demand> demands, ReadOnlySpan<int> watchedObjectIds)
        {
            if (demands is null) throw new ArgumentNullException(nameof(demands));
            if (demands.Count == 0) return true;
            if (watchedObjectIds.Length == 0) return false;

            // Cutoff is max deadline
            int cutoff = 0;
            int totalDemandUnits = 0;
            for (int i = 0; i < demands.Count; i++)
            {
                cutoff = Math.Max(cutoff, demands[i].DeadlineDaysPlayed);
                totalDemandUnits += demands[i].Quantity;
            }

            // Build daily supply table: dayCount x watchedCount
            int dayCount = GetForestCartDayCountUpToCutoff(cutoff);
            int watchedCount = watchedObjectIds.Length;

            // dailyUnits[d*watchedCount + i] == units of watchedObjectIds[i] that appeared on forest cart day index d
            int[] dailyUnits = new int[dayCount * watchedCount];

            TravelingCartSimulator.AccumulateDailyUnitsUpTo(
                gameId,
                cutoff,
                watchedObjectIds,
                dailyUnits);

            // Cheap prune: each demand must have enough total supply among its options by its deadline
            if (!PassesCheapFeasibilityPrune(demands, watchedObjectIds, dailyUnits, dayCount, watchedCount))
                return false;

            // Full solve: max-flow allocation with capacities
            return MaxFlowCanSatisfyAll(demands, watchedObjectIds, dailyUnits, dayCount, watchedCount, totalDemandUnits);
        }

    public static bool SeedPassesEarlyFeasibilityPruneY1Forest(ulong gameId, IReadOnlyList<Demand> demands, ReadOnlySpan<int> watchedObjectIds)
    {
        var ordered = demands.OrderBy(d => d.DeadlineDaysPlayed).ToArray();

        Span<int> totals = stackalloc int[watchedObjectIds.Length];
        totals.Clear();

        ReadOnlySpan<int> forestDays = TravelingCartSimulator.ForestDaysYear1;

        var pool = System.Buffers.ArrayPool<ulong>.Shared;
        ulong[] composites = pool.Rent(TravelingCartPredictor.GetObjectCandidatesForAnalysis().Count); // or Candidates.Count if accessible
        try
        {
            int demandIndex = 0;

            for (int di = 0; di < forestDays.Length; di++)
            {
                int day = forestDays[di];

                if (demandIndex >= ordered.Length)
                    return true;

                if (day > ordered[demandIndex].DeadlineDaysPlayed)
                    return false;

                TravelingCartSimulator.ProcessOneCartDay(gameId, day, watchedObjectIds, totals, composites);

                int nextCartDay = (di + 1 < forestDays.Length) ? forestDays[di + 1] : int.MaxValue;

                while (demandIndex < ordered.Length && ordered[demandIndex].DeadlineDaysPlayed < nextCartDay)
                {
                    var dem = ordered[demandIndex];

                    int available = 0;
                    foreach (int opt in dem.OptionsObjectIds)
                    {
                        int wi = IndexOf(watchedObjectIds, opt);
                        if (wi >= 0) available += totals[wi];
                    }

                    if (available < dem.Quantity)
                        return false;

                    demandIndex++;
                }
            }

            return demandIndex >= ordered.Length;
        }
        finally
        {
            pool.Return(composites, clearArray: false);
        }
    }

    public static bool SeedPassesEarlyFeasibilityPruneY1Forest(ulong gameId, CartDemandPlan plan)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (plan.DemandsSortedByDeadline.Length == 0) return true;
        if (plan.WatchedObjectIdsSorted.Length == 0) return false;

        ReadOnlySpan<int> watchedIds = plan.WatchedObjectIdsSorted;
        var demands = plan.DemandsSortedByDeadline;

        Span<int> totals = stackalloc int[watchedIds.Length];
        totals.Clear();

        ReadOnlySpan<int> forestDays = TravelingCartSimulator.ForestDaysYear1;

        // You said Candidates is public now, so we can use it for buffer length
        var pool = System.Buffers.ArrayPool<ulong>.Shared;
        ulong[] composites = pool.Rent(TravelingCartSimulator.Candidates.Count);

        try
        {
            int demandIndex = 0;

            for (int di = 0; di < forestDays.Length; di++)
            {
                if (demandIndex >= demands.Length)
                    return true;

                int day = forestDays[di];

                // If we skipped past a deadline, it's impossible now
                if (day > demands[demandIndex].DeadlineDaysPlayed)
                    return false;

                // Simulate this cart day, add qty capacities into totals
                TravelingCartSimulator.ProcessOneCartDay(gameId, day, watchedIds, totals, composites);

                int nextCartDay = (di + 1 < forestDays.Length) ? forestDays[di + 1] : int.MaxValue;

                // Finalize all demands whose deadline is before next cart day
                while (demandIndex < demands.Length && demands[demandIndex].DeadlineDaysPlayed < nextCartDay)
                {
                    var dem = demands[demandIndex];

                    int available = 0;
                    foreach (int wi in dem.WatchedOptionIndexes)
                        available += totals[wi];

                    if (available < dem.Quantity)
                        return false;

                    demandIndex++;
                }
            }

            return demandIndex >= demands.Length;
        }
        finally
        {
            pool.Return(composites, clearArray: false);
        }
    }
    
    public static bool SeedPassesEarlyFeasibilityPruneY1Forest(ulong gameId, IReadOnlyList<Demand> demands, ReadOnlySpan<int> watchedObjectIds, Span<int> totalsBuffer, ulong[] compositesBuffer)
    {
        totalsBuffer.Clear();

        // If you already have your demands sorted elsewhere, great; if not, keep it as-is for now.
        // Best is to pre-sort once outside parallel loop and pass that list in.
        // For now, assume demands is already sorted by DeadlineDaysPlayed.
        int demandIndex = 0;

        ReadOnlySpan<int> forestDays = TravelingCartSimulator.ForestDaysYear1;

        for (int di = 0; di < forestDays.Length; di++)
        {
            if (demandIndex >= demands.Count)
                return true;

            int day = forestDays[di];

            if (day > demands[demandIndex].DeadlineDaysPlayed)
                return false;

            TravelingCartSimulator.ProcessOneCartDay(gameId, day, watchedObjectIds, totalsBuffer, compositesBuffer);

            int nextCartDay = (di + 1 < forestDays.Length) ? forestDays[di + 1] : int.MaxValue;

            while (demandIndex < demands.Count && demands[demandIndex].DeadlineDaysPlayed < nextCartDay)
            {
                var dem = demands[demandIndex];

                int available = 0;
                foreach (int opt in dem.OptionsObjectIds)
                {
                    // watched is tiny; linear scan is fine (or you can pass in a plan later)
                    for (int i = 0; i < watchedObjectIds.Length; i++)
                    {
                        if (watchedObjectIds[i] == opt) { available += totalsBuffer[i]; break; }
                    }
                }

                if (available < dem.Quantity)
                    return false;

                demandIndex++;
            }
        }

        return demandIndex >= demands.Count;
    }

    private static int IndexOf(ReadOnlySpan<int> span, int value)
{
    for (int i = 0; i < span.Length; i++)
        if (span[i] == value) return i;
    return -1;
}

    private static int GetForestCartDayCountUpToCutoff(int cutoffDaysPlayedInclusive)
    {
        int count = 0;
        var days = TravelingCartSimulator.ForestDaysYear1; // expose as internal/public on simulator (see note below)
        while (count < days.Length && days[count] <= cutoffDaysPlayedInclusive)
            count++;
        return count;
    }

    private static bool PassesCheapFeasibilityPrune(IReadOnlyList<Demand> demands, ReadOnlySpan<int> watchedIds, int[] dailyUnits, int dayCount, int watchedCount)
    {
        var forestDays = TravelingCartSimulator.ForestDaysYear1;

        for (int di = 0; di < demands.Count; di++)
        {
            var demand = demands[di];

            int required = demand.Quantity;
            int available = 0;

            // Sum all units of ANY allowed option on ANY day <= deadline.
            for (int d = 0; d < dayCount; d++)
            {
                if (forestDays[d] > demand.DeadlineDaysPlayed)
                    break;

                int rowBase = d * watchedCount;

                // watchedIds is small; options list is usually small too
                foreach (int optId in demand.OptionsObjectIds)
                {
                    int wi = IndexOfWatched(watchedIds, optId);
                    if (wi >= 0)
                        available += dailyUnits[rowBase + wi];
                }
            }

            if (available < required)
                return false;
        }

        return true;
    }

    private static int IndexOfWatched(ReadOnlySpan<int> watchedIds, int id)
    {
        for (int i = 0; i < watchedIds.Length; i++)
            if (watchedIds[i] == id) return i;
        return -1;
    }

    private static bool MaxFlowCanSatisfyAll(
        IReadOnlyList<Demand> demands,
        ReadOnlySpan<int> watchedIds,
        int[] dailyUnits,
        int dayCount,
        int watchedCount,
        int totalDemandUnits)
    {
        var forestDays = TravelingCartSimulator.ForestDaysYear1;

        // Node layout:
        // 0 = source
        // 1..S = supply nodes (one per (dayIndex, watchedIndex))
        // then demand nodes
        // last = sink
        int supplyNodes = dayCount * watchedCount;
        int demandNodes = demands.Count;

        int source = 0;
        int firstSupply = 1;
        int firstDemand = firstSupply + supplyNodes;
        int sink = firstDemand + demandNodes;
        int nodeCount = sink + 1;

        var dinic = new Dinic(nodeCount);

        // source -> supply with capacity = units on that day for that item
        for (int d = 0; d < dayCount; d++)
        {
            int rowBase = d * watchedCount;
            for (int w = 0; w < watchedCount; w++)
            {
                int cap = dailyUnits[rowBase + w];
                if (cap <= 0) continue;

                int supplyNode = firstSupply + (d * watchedCount + w);
                dinic.AddEdge(source, supplyNode, cap);
            }
        }

        // supply -> demand edges if:
        // - the supply day <= demand.deadline
        // - the item is in demand options
        // capacity can be large; supply cap already limits it
        for (int demandIndex = 0; demandIndex < demands.Count; demandIndex++)
        {
            var demand = demands[demandIndex];
            int demandNode = firstDemand + demandIndex;

            // demand -> sink
            dinic.AddEdge(demandNode, sink, demand.Quantity);

            // For each eligible day and option, add edge from (day,item) supply node to this demand
            for (int d = 0; d < dayCount; d++)
            {
                if (forestDays[d] > demand.DeadlineDaysPlayed)
                    break;

                foreach (int optId in demand.OptionsObjectIds)
                {
                    int w = IndexOfWatched(watchedIds, optId);
                    if (w < 0) continue;

                    int supplyCap = dailyUnits[d * watchedCount + w];
                    if (supplyCap <= 0) continue; // skip edges from empty supplies

                    int supplyNode = firstSupply + (d * watchedCount + w);
                    dinic.AddEdge(supplyNode, demandNode, supplyCap);
                }
            }
        }

        int flow = dinic.MaxFlow(source, sink);
        return flow == totalDemandUnits;
    }

    /// <summary>Simple Dinic max flow for small graphs.</summary>
    private sealed class Dinic
    {
        private sealed class Edge
        {
            public int To;
            public int Rev;
            public int Cap;
            public Edge(int to, int rev, int cap) { To = to; Rev = rev; Cap = cap; }
        }

        private readonly List<Edge>[] _g;
        private readonly int[] _level;
        private readonly int[] _it;

        public Dinic(int n)
        {
            _g = new List<Edge>[n];
            for (int i = 0; i < n; i++) _g[i] = new List<Edge>(4);
            _level = new int[n];
            _it = new int[n];
        }

        public void AddEdge(int fr, int to, int cap)
        {
            var fwd = new Edge(to, _g[to].Count, cap);
            var rev = new Edge(fr, _g[fr].Count, 0);
            _g[fr].Add(fwd);
            _g[to].Add(rev);
        }

        public int MaxFlow(int s, int t)
        {
            int flow = 0;
            while (Bfs(s, t))
            {
                Array.Fill(_it, 0);
                int f;
                while ((f = Dfs(s, t, int.MaxValue)) > 0)
                    flow += f;
            }
            return flow;
        }

        private bool Bfs(int s, int t)
        {
            Array.Fill(_level, -1);
            var q = new Queue<int>();
            _level[s] = 0;
            q.Enqueue(s);

            while (q.Count > 0)
            {
                int v = q.Dequeue();
                foreach (var e in _g[v])
                {
                    if (e.Cap <= 0) continue;
                    if (_level[e.To] >= 0) continue;
                    _level[e.To] = _level[v] + 1;
                    q.Enqueue(e.To);
                }
            }
            return _level[t] >= 0;
        }

        private int Dfs(int v, int t, int f)
        {
            if (v == t) return f;

            for (int i = _it[v]; i < _g[v].Count; i++, _it[v] = i)
            {
                var e = _g[v][i];
                if (e.Cap <= 0) continue;
                if (_level[e.To] != _level[v] + 1) continue;

                int pushed = Dfs(e.To, t, Math.Min(f, e.Cap));
                if (pushed <= 0) continue;

                e.Cap -= pushed;
                _g[e.To][e.Rev].Cap += pushed;
                return pushed;
            }

            return 0;
        }
    }
}
