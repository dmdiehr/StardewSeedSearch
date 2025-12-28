using System.Collections.Concurrent;
using System.Diagnostics;

namespace StardewSeedSearch.Core.Search;

public static class SeedSearchPipeline
{
    // Custom delegate (can't express out params with Func<>)
    public delegate bool BundleScannerTryScan(ulong gameId, Span<int> outputBuffer, out int foundCount, out bool disqualified);

    public static SeedSearchResult ScanRange(ulong startSeedInclusive, ulong endSeedExclusive, SeedSearchConfig config)
    {
        if (endSeedExclusive < startSeedInclusive)
            throw new ArgumentOutOfRangeException(nameof(endSeedExclusive));

        var sw = Stopwatch.StartNew();

        long scanned = 0;
        long bundleDisqualified = 0;
        long ordersFailed = 0;
        long cartFailed = 0;
        long hardPassed = 0;

        var globalTopK = new TopK(config.TopK);

        var po = new ParallelOptions { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism };

        Parallel.ForEach(
            Partitioner.Create((long)startSeedInclusive, (long)endSeedExclusive, config.PartitionSize),
            po,
            () => new WorkerState(config),
            (range, _, state) =>
            {
                for (long s = range.Item1; s < range.Item2; s++)
                {
                    ulong gameId = unchecked((ulong)s);
                    ProcessOneSeed(gameId, config, state);
                }

                return state;
            },
            state =>
            {
                Interlocked.Add(ref scanned, state.Scanned);
                Interlocked.Add(ref bundleDisqualified, state.BundleDisqualified);
                Interlocked.Add(ref ordersFailed, state.OrdersFailed);
                Interlocked.Add(ref cartFailed, state.CartFailed);
                Interlocked.Add(ref hardPassed, state.HardPassed);

                globalTopK.MergeFrom(state.TopK);

                state.Dispose();
            });

        sw.Stop();

        return new SeedSearchResult(
            Elapsed: sw.Elapsed,
            SeedsScanned: scanned,
            BundleDisqualified: bundleDisqualified,
            OrdersFailed: ordersFailed,
            CartFailed: cartFailed,
            HardPassed: hardPassed,
            TopCandidates: globalTopK.ToSortedListDesc());
    }

    /// <summary>
    /// Overload for rescoring/retsting a specific set of game IDs.
    /// Note: must take an array/list (not ReadOnlySpan) because Parallel.ForEach captures it in lambdas.
    /// </summary>
    public static SeedSearchResult ScanRange(IReadOnlyList<ulong> gameIds, SeedSearchConfig config)
    {
        if (gameIds is null) throw new ArgumentNullException(nameof(gameIds));

        var sw = Stopwatch.StartNew();

        long scanned = 0;
        long bundleDisqualified = 0;
        long ordersFailed = 0;
        long cartFailed = 0;
        long hardPassed = 0;

        var globalTopK = new TopK(config.TopK);

        var po = new ParallelOptions { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism };

        Parallel.ForEach(
            Partitioner.Create(0, gameIds.Count, config.PartitionSize),
            po,
            () => new WorkerState(config),
            (range, _, state) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    ProcessOneSeed(gameIds[i], config, state);
                }

                return state;
            },
            state =>
            {
                Interlocked.Add(ref scanned, state.Scanned);
                Interlocked.Add(ref bundleDisqualified, state.BundleDisqualified);
                Interlocked.Add(ref ordersFailed, state.OrdersFailed);
                Interlocked.Add(ref cartFailed, state.CartFailed);
                Interlocked.Add(ref hardPassed, state.HardPassed);

                globalTopK.MergeFrom(state.TopK);

                state.Dispose();
            });

        sw.Stop();

        return new SeedSearchResult(
            Elapsed: sw.Elapsed,
            SeedsScanned: scanned,
            BundleDisqualified: bundleDisqualified,
            OrdersFailed: ordersFailed,
            CartFailed: cartFailed,
            HardPassed: hardPassed,
            TopCandidates: globalTopK.ToSortedListDesc());
    }

    private static void ProcessOneSeed(ulong gameId, SeedSearchConfig config, WorkerState state)
    {
        state.ResetPerSeed();
        state.Scanned++;

        // ---- Stage 1: bundle tracked items ----
        if (!config.BundleTryScan(gameId, state.BundleItems, out int foundCount, out bool disq))
        {
            // Treat scan failure as disqualified for now (or throw if you prefer)
            state.BundleDisqualified++;
            return;
        }

        if (disq)
        {
            state.BundleDisqualified++;
            return;
        }

        // ---- Stage 2: orders gate ----
        if (!config.CanCompleteTownPerfectionByWeek(gameId, config.TargetWeekIndexTown)
            || !config.CanCompleteQiPerfectionByWeek(gameId, config.TargetWeekIndexQi, config.StartWeekIndexQi))
        {
            state.OrdersFailed++;
            return;
        }

        // ---- Stage 3: build hard demands (always + tracked items) ----
        BuildHardDemandsAndWatched(
            config,
            state,
            state.BundleItems,
            foundCount);

        // ---- Stage 4: cart hard requirements ----
        if (state.HardDemandsCount != 0)
        {
            if (state.HardWatchedCount == 0)
            {
                state.CartFailed++;
                return;
            }

            // Provide a count-limited IReadOnlyList view without allocations
            state.HardDemandsView.Count = state.HardDemandsCount;

            var watchedSpan = state.HardWatchedIdsArray.AsSpan(0, state.HardWatchedCount);

            if (!Core.CartSeedDemandEvaluator.SeedSatisfiesDemandsY1Forest(
                    gameId,
                    state.HardDemandsView,
                    watchedSpan))
            {
                state.CartFailed++;
                return;
            }
        }

        // ---- Hard passed ----
        state.HardPassed++;

        // ---- Candidate-only scoring ----
        byte weatherMask;
        int weatherScore = WeatherScoring.ScoreWeatherY1(gameId, out weatherMask);

        ushort optionalCartMask;
        int optionalCartScore = ScoreOptionalCart(
            gameId,
            config.OptionalBonusDemands,
            state,
            out optionalCartMask);

        int totalScore = weatherScore + optionalCartScore;

        var cand = new SeedCandidate(
            GameId: gameId,
            Score: totalScore,
            WeatherMask: weatherMask,
            OptionalCartMask: optionalCartMask);

        // Record to CLI output (only if score >= threshold)
        RecordIfQualifies(config, cand);

        // Keep TopK too if you still want it (optional)
        state.TopK.TryAdd(cand);
    }

    // -------------------- hard demand builder --------------------

    private static void BuildHardDemandsAndWatched(
        SeedSearchConfig cfg,
        WorkerState st,
        Span<int> trackedItemsBuffer,
        int trackedCount)
    {
        // Fill demands: always hard + tracked item single-option demands
        int di = 0;

        // Copy always-hard demands first
        var always = cfg.AlwaysHardDemands;
        if (always != null)
        {
            if (always.Length > st.HardDemandsArray.Length)
                throw new InvalidOperationException("HardDemandsBufferSize too small for AlwaysHardDemands.");

            Array.Copy(always, 0, st.HardDemandsArray, 0, always.Length);
            di = always.Length;
        }

        // Add tracked item demands
        for (int i = 0; i < trackedCount; i++)
        {
            int itemId = trackedItemsBuffer[i];

            if (di >= st.HardDemandsArray.Length)
                throw new InvalidOperationException("HardDemandsBufferSize too small for tracked items.");

            st.HardDemandsArray[di++] = new Core.Demand(
                DeadlineDaysPlayed: cfg.TrackedItemDeadlineDaysPlayed,
                Quantity: 1,
                OptionsObjectIds: OptionArrayCache.Single(itemId));
        }

        st.HardDemandsCount = di;

        // Build watched ids = unique union of all option ids used by demands
        int wi = 0;

        for (int d = 0; d < st.HardDemandsCount; d++)
        {
            var dem = st.HardDemandsArray[d];
            var opts = dem.OptionsObjectIds;

            for (int oi = 0; oi < opts.Length; oi++)
            {
                int id = opts[oi];
                if (!Contains(st.HardWatchedIdsArray, wi, id))
                {
                    if (wi >= st.HardWatchedIdsArray.Length)
                        throw new InvalidOperationException("HardWatchedIdsBufferSize too small.");

                    st.HardWatchedIdsArray[wi++] = id;
                }
            }
        }

        // Sort watched ids (recommended for determinism; also helps any binary-search plans later)
        Array.Sort(st.HardWatchedIdsArray, 0, wi);
        st.HardWatchedCount = wi;
    }

    private static bool Contains(int[] arr, int count, int value)
    {
        for (int i = 0; i < count; i++)
            if (arr[i] == value) return true;
        return false;
    }

    // Cache singleton option arrays so we don't allocate int[1] per seed.
    private static class OptionArrayCache
    {
        private static readonly ConcurrentDictionary<int, int[]> _single = new();

        public static int[] Single(int id) => _single.GetOrAdd(id, static v => new[] { v });
    }

    // -------------------- config + DTOs --------------------

    public sealed class SeedSearchConfig
    {
        public int MinScoreToRecord { get; init; } = int.MaxValue;
        public Action<SeedCandidate>? OnRecordedCandidate { get; init; }

        public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
        public int PartitionSize { get; init; } = 200_000;

        public int TopK { get; init; } = 200;

        public int TargetWeekIndexTown { get; init; } = 18;
        public int TargetWeekIndexQi { get; init; } = 18;
        public int StartWeekIndexQi { get; init; } = 14;

        public int TrackedItemDeadlineDaysPlayed { get; init; } = 68;

        public int BundleItemsBufferSize { get; init; } = 64;
        public int HardDemandsBufferSize { get; init; } = 128;
        public int HardWatchedIdsBufferSize { get; init; } = 256;

        public required BundleScannerTryScan BundleTryScan { get; init; }

        public required Func<ulong, int, bool> CanCompleteTownPerfectionByWeek { get; init; }
        public required Func<ulong, int, int, bool> CanCompleteQiPerfectionByWeek { get; init; }

        /// <summary>
        /// Hard demands that are always present for every seed.
        /// These should have stable OptionsObjectIds arrays (no per-seed allocations).
        /// </summary>
        public Core.Demand[] AlwaysHardDemands { get; init; } = HardDemandDefaults.AlwaysHardDemands;

        public Core.Demand[] OptionalBonusDemands { get; init; } = OptionalCartBonusDefaults.OptionalBonusDemands;
    }

    public readonly record struct SeedCandidate(
        ulong GameId,
        int Score,
        byte WeatherMask,
        ushort OptionalCartMask);

    public sealed record SeedSearchResult(
        TimeSpan Elapsed,
        long SeedsScanned,
        long BundleDisqualified,
        long OrdersFailed,
        long CartFailed,
        long HardPassed,
        IReadOnlyList<SeedCandidate> TopCandidates)
    {
        public double SeedsPerSecond => SeedsScanned / Math.Max(1e-9, Elapsed.TotalSeconds);
    }

    // -------------------- worker state --------------------

    private sealed class WorkerState
    {
        public readonly int[] BundleItemsArray;
        public Span<int> BundleItems => BundleItemsArray;

        public readonly Core.Demand[] HardDemandsArray;
        public int HardDemandsCount;

        public readonly int[] HardWatchedIdsArray;
        public int HardWatchedCount;

        public readonly DemandBufferView HardDemandsView;

        public readonly TopK TopK;

        // counters
        public long Scanned;
        public long BundleDisqualified;
        public long OrdersFailed;
        public long CartFailed;
        public long HardPassed;

        public readonly ulong[] CompositesBuffer;
        public readonly SingleDemandView SingleDemand;

        public WorkerState(SeedSearchConfig cfg)
        {
            BundleItemsArray = new int[cfg.BundleItemsBufferSize];
            HardDemandsArray = new Core.Demand[cfg.HardDemandsBufferSize];
            HardWatchedIdsArray = new int[cfg.HardWatchedIdsBufferSize];

            HardDemandsView = new DemandBufferView(HardDemandsArray);

            TopK = new TopK(cfg.TopK);

            CompositesBuffer = System.Buffers.ArrayPool<ulong>.Shared.Rent(Core.TravelingCartSimulator.Candidates.Count);
            SingleDemand = new SingleDemandView();
        }

        public void ResetPerSeed()
        {
            HardDemandsCount = 0;
            HardWatchedCount = 0;
        }

        public void Dispose()
        {
            System.Buffers.ArrayPool<ulong>.Shared.Return(CompositesBuffer, clearArray: false);
        }
    }

    // Count-limited IReadOnlyList view over a Demand[] (allocated once per thread)
    private sealed class DemandBufferView : IReadOnlyList<Core.Demand>
    {
        private readonly Core.Demand[] _arr;
        public int Count { get; set; }

        public DemandBufferView(Core.Demand[] arr) => _arr = arr;

        public Core.Demand this[int index] => _arr[index];

        public IEnumerator<Core.Demand> GetEnumerator()
        {
            for (int i = 0; i < Count; i++) yield return _arr[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // -------------------- top-k --------------------

    private sealed class TopK
    {
        private readonly int _k;
        private readonly List<SeedCandidate> _items;

        private int _minIndex;
        private int _minScore;

        public TopK(int k)
        {
            _k = Math.Max(1, k);
            _items = new List<SeedCandidate>(_k);
            _minIndex = 0;
            _minScore = int.MaxValue;
        }

        public void TryAdd(SeedCandidate cand)
        {
            if (_items.Count < _k)
            {
                _items.Add(cand);
                if (_items.Count == 1 || cand.Score < _minScore)
                {
                    _minScore = cand.Score;
                    _minIndex = _items.Count - 1;
                }
                return;
            }

            if (cand.Score <= _minScore)
                return;

            _items[_minIndex] = cand;
            RecomputeMin();
        }

        public void MergeFrom(TopK other)
        {
            foreach (var c in other._items)
                TryAdd(c);
        }

        public IReadOnlyList<SeedCandidate> ToSortedListDesc()
        {
            var copy = new List<SeedCandidate>(_items);
            copy.Sort(static (a, b) => b.Score.CompareTo(a.Score));
            return copy;
        }

        private void RecomputeMin()
        {
            int minScore = int.MaxValue;
            int minIndex = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                int s = _items[i].Score;
                if (s < minScore)
                {
                    minScore = s;
                    minIndex = i;
                }
            }

            _minScore = minScore;
            _minIndex = minIndex;
        }
    }

    private static class HardDemandDefaults
    {
        // These option arrays are allocated once, at type init.
        private static readonly int[] GarlicOrSeeds = new[] { 248, 476 };
        private static readonly int[] CabbageOrSeeds = new[] { 266, 485 };
        private static readonly int[] HaleyBday =
        {
            /* Coconut */ 88,
            /* Sunflower */ 421
        };

        private static readonly int[] PierreBday = { /* Fried_Calamari */ 202 };

        public static readonly Demand[] AlwaysHardDemands =
        {
            new Demand(14, 1, HaleyBday),
            new Demand(28, 1, PierreBday),
            new Demand(84, 1, GarlicOrSeeds),
            new Demand(84, 1, CabbageOrSeeds)
        };
    }

    private sealed class SingleDemandView : IReadOnlyList<Core.Demand>
    {
        public Core.Demand Value;

        public int Count => 1;
        public Core.Demand this[int index] => index == 0 ? Value : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<Core.Demand> GetEnumerator()
        {
            yield return Value;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static int ScoreOptionalCart(
        ulong gameId,
        Core.Demand[] optionalDemands,
        WorkerState state,
        out ushort optionalMask)
    {
        optionalMask = 0;
        int score = 0;

        for (int i = 0; i < optionalDemands.Length; i++)
        {
            var dem = optionalDemands[i];

            // Single-demand view (avoid per-demand list allocations)
            state.SingleDemand.Value = dem;

            // watched ids for this single demand are just its options
            // totals buffer must match watched length
            Span<int> totals = stackalloc int[dem.OptionsObjectIds.Length];

            bool ok = Core.CartSeedDemandEvaluator.SeedPassesEarlyFeasibilityPruneY1Forest(
                gameId,
                demands: state.SingleDemand,
                watchedObjectIds: dem.OptionsObjectIds,
                totalsBuffer: totals,
                compositesBuffer: state.CompositesBuffer);

            if (ok)
            {
                score++;
                optionalMask |= (ushort)(1 << i);
            }
        }

        return score;
    }

    private static void RecordIfQualifies(SeedSearchConfig cfg, in SeedCandidate cand)
    {
        if (cand.Score < cfg.MinScoreToRecord) return;
        cfg.OnRecordedCandidate?.Invoke(cand);
    }
}
