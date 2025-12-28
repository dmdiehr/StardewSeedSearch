using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using StardewSeedSearch.Core.Search;
using StardewSeedSearch.Core;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        ulong start = GetUlong(args, "--start", 10_000_000_000);
        ulong end = GetUlong(args, "--end", 11_000_000_000);
        ulong chunk = GetUlong(args, "--chunk", 1_000_000);
        
        int minScore = GetInt(args, "--minScore", 6);
        string outPath = GetString(args, "--out", "close_hits.txt");

        int threads = GetInt(args, "--threads", 0); // 0 => all cores

        Console.WriteLine($"Scanning [{start:n0}, {end:n0}) in chunks of {chunk:n0}");
        Console.WriteLine($"minScore={minScore} out={Path.GetFullPath(outPath)} threads={(threads == 0 ? Environment.ProcessorCount : threads)}");
        Console.WriteLine("Ctrl+C to stop after current chunk.");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("Stopping after current chunk...");
            cts.Cancel();
        };

        // Single-writer channel (worker threads enqueue hits here)
        var ch = Channel.CreateUnbounded<SeedSearchPipeline.SeedCandidate>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        long hitsWritten = 0;

        // Writer task: appends lines to file
        var writerTask = Task.Run(async () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".");
            await using var fs = new FileStream(outPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var sw = new StreamWriter(fs) { AutoFlush = true };

            while (await ch.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
            {
                while (ch.Reader.TryRead(out var cand))
                {
                    // Keep this formatting OUT of the hot loop.
                    string weather = WeatherScoring.FormatWeatherMask(cand.WeatherMask);

                    // If you don't have optional cart yet, this is still fine.
                    // If you do, you can format it however you like:
                    string optional = OptionalCartBonusDefaults.FormatOptionalCartMask(cand.OptionalCartMask);

                    await sw.WriteLineAsync($"{cand.GameId}\tScore={cand.Score}\tWeather=[{weather}]\tCart={optional}")
                            .ConfigureAwait(false);

                    Interlocked.Increment(ref hitsWritten);
                }
            }
        }, cts.Token);

        // Build config (only the stuff that *must* be set)
        var cfg = new SeedSearchPipeline.SeedSearchConfig
        {
            MaxDegreeOfParallelism = (threads == 0 ? Environment.ProcessorCount : threads),
            PartitionSize = 200_000,
            TopK = 1, // irrelevant if you're writing hits to a file

            MinScoreToRecord = minScore,
            OnRecordedCandidate = cand => ch.Writer.TryWrite(cand),

            // your existing wiring:
            BundleTryScan = (ulong gid, Span<int> buf, out int found, out bool disq) =>
                RemixedBundleTrackedItemsScanner.TryScan(gid, buf, out found, out disq, out _),


            CanCompleteTownPerfectionByWeek = (gid, wk) =>
                SpecialOrderSimulator.CanCompleteTownPerfectionByWeek(gid, wk),

            CanCompleteQiPerfectionByWeek = (gid, wk, startWk) =>
                QiSpecialOrderSimulator.CanCompleteQiPerfectionByWeek(gid, wk, startWk),
        };

        var overallStart = DateTime.UtcNow;
        long overallScanned = 0;

        try
        {
            for (ulong chunkStart = start; chunkStart < end; chunkStart += chunk)
            {
                if (cts.IsCancellationRequested) break;

                ulong chunkEnd = chunkStart + chunk;
                if (chunkEnd > end) chunkEnd = end;

                var t0 = DateTime.UtcNow;

                var result = SeedSearchPipeline.ScanRange(chunkStart, chunkEnd, cfg);

                overallScanned += result.SeedsScanned;

                var chunkElapsed = DateTime.UtcNow - t0;
                var overallElapsed = DateTime.UtcNow - overallStart;

                double chunkSps = result.SeedsScanned / Math.Max(0.001, chunkElapsed.TotalSeconds);
                double overallSps = overallScanned / Math.Max(0.001, overallElapsed.TotalSeconds);

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] chunk [{chunkStart:n0}, {chunkEnd:n0}) " +
                    $"scanned={result.SeedsScanned:n0} sps={chunkSps:n0} hardPass={result.HardPassed:n0} " +
                    $"hitsWritten={Interlocked.Read(ref hitsWritten):n0} totalScanned={overallScanned:n0} totalSps={overallSps:n0}");
            }
        }
        finally
        {
            ch.Writer.TryComplete();
            try { await writerTask.ConfigureAwait(false); } catch { /* ignore on cancel */ }
        }

        Console.WriteLine("Done.");
        return 0;
    }

    // ----- tiny arg helpers -----
    private static string GetString(string[] args, string key, string def)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return def;
    }

    private static int GetInt(string[] args, string key, int def)
    {
        string s = GetString(args, key, "");
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;
    }

    private static ulong GetUlong(string[] args, string key, ulong def)
    {
        string s = GetString(args, key, "");
        return ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;
    }
}
