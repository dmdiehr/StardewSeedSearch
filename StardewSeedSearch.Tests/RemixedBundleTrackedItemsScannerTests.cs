using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using StardewSeedSearch.Core;
using Xunit;
using Xunit.Abstractions;

namespace StardewSeedSearch.Tests;

public sealed class RemixedBundleTrackedItemsScannerTests
{
    private readonly ITestOutputHelper _output;
    public RemixedBundleTrackedItemsScannerTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Scan_IsDeterministic()
    {
        const ulong gameId = 123456789UL;
        Span<int> bufA = stackalloc int[32];
        Span<int> bufB = stackalloc int[32];

        bool okA = RemixedBundleTrackedItemsScanner.TryScan(gameId, bufA, out int countA, out bool disqA, out string? whyA);
        bool okB = RemixedBundleTrackedItemsScanner.TryScan(gameId, bufB, out int countB, out bool disqB, out string? whyB);

        Assert.True(okA);
        Assert.True(okB);

        Assert.Equal(disqA, disqB);
        Assert.Equal(whyA, whyB);
        Assert.Equal(countA, countB);

        for (int i = 0; i < countA; i++)
            Assert.Equal(bufA[i], bufB[i]);
    }

    [Fact]
    public void Scan_WritesNoDuplicates()
    {
        const ulong gameId = 123456789;
        Span<int> buf = stackalloc int[32];

        bool ok = RemixedBundleTrackedItemsScanner.TryScan(gameId, buf, out int count, out bool disq, out _);
        Assert.True(ok);

        for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++)
                Assert.NotEqual(buf[i], buf[j]);

        _output.WriteLine($"disq={disq}, count={count}, ids=[{string.Join(",", buf.Slice(0, count).ToArray())}]");
    }

    [Fact]
    public void Perf_ScanSeeds_PerSecond()
    {
        const ulong startSeed = 1_000_000UL;
        const int warmup = 50_000;
        const int n = 500_000;

        Warmup(startSeed, warmup);

        // Single-thread
        var sw = Stopwatch.StartNew();
        int disq = 0;
        int ok = 0;
        int foundTotal = 0;

        Span<int> buffer = stackalloc int[32];

        for (ulong seed = startSeed; seed < startSeed + (ulong)n; seed++)
        {
            bool success = RemixedBundleTrackedItemsScanner.TryScan(
                seed,
                buffer,
                out int foundCount,
                out bool disqualified,
                out string? reason);

            if (!success)
                throw new Exception("Output buffer too small (increase stackalloc size).");

            if (disqualified) disq++;
            else ok++;

            foundTotal += foundCount;
        }

        sw.Stop();
        PrintStats("Single", n, sw.Elapsed, ok, disq, foundTotal);

        // Parallel
        int okP = 0;
        int disqP = 0;
        int foundTotalP = 0;

        sw.Restart();

        Parallel.For(
            0, n,
            localInit: () => (ok: 0, disq: 0, found: 0, buf: new int[32]),
            body: (i, loopState, local) =>
            {
                ulong seed = startSeed + (ulong)i;

                bool success = RemixedBundleTrackedItemsScanner.TryScan(
                    seed,
                    local.buf,
                    out int foundCount,
                    out bool disqualified,
                    out string? reason);

                if (!success)
                    throw new Exception("Output buffer too small (increase local buf size).");

                if (disqualified) local.disq++;
                else local.ok++;

                local.found += foundCount;
                return local;
            },
            localFinally: local =>
            {
                Interlocked.Add(ref okP, local.ok);
                Interlocked.Add(ref disqP, local.disq);
                Interlocked.Add(ref foundTotalP, local.found);
            });

        sw.Stop();
        PrintStats($"Parallel ({Environment.ProcessorCount} cores)", n, sw.Elapsed, okP, disqP, foundTotalP);
    }

    private void Warmup(ulong startSeed, int warmup)
    {
        Span<int> buffer = stackalloc int[32];
        for (ulong seed = startSeed; seed < startSeed + (ulong)warmup; seed++)
        {
            _ = RemixedBundleTrackedItemsScanner.TryScan(seed, buffer, out _, out _, out _);
        }
    }

    private void PrintStats(string label, int n, TimeSpan elapsed, int ok, int disq, int foundTotal)
    {
        double seconds = elapsed.TotalSeconds;
        double sps = n / seconds;

        _output.WriteLine($"{label}: {sps:N0} seeds/sec");
        _output.WriteLine($"  elapsed: {seconds:N3}s for {n:N0} seeds");
        _output.WriteLine($"  ok={ok:N0}, disq={disq:N0}, foundTotal={foundTotal:N0}");
        _output.WriteLine("");
    }

}
