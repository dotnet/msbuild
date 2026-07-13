// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// A deterministic, self-contained A/B probe for <c>ExpressionShredder</c> transform/separator/
/// function-name captures. It is intentionally NOT BenchmarkDotNet: it runs against whatever
/// Microsoft.Build.dll sits next to this assembly, so the SAME probe binary can be run against
/// two different product binaries (exact-parent A vs final B) by swapping only Microsoft.Build.dll.
///
/// It reports, per scenario:
///  - allocated bytes per full-corpus shred (GC.GetAllocatedBytesForCurrentThread, raw samples retained),
///  - wall time per full-corpus shred (Stopwatch ticks, raw samples retained),
///  - reference-identity dedup: how many DISTINCT string instances back the repeated capture text
///    (weak interning collapses these; the unmodified baseline does not) — a deterministic hit-rate proxy,
///  - retained managed memory after holding all captures alive across a forced GC.
/// </summary>
internal static class ShredderProbe
{
    private sealed class RefComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => ReferenceEquals(x, y);
        public int GetHashCode(string obj) => RuntimeHelpers.GetHashCode(obj);
    }

    public static int Run(string[] args)
    {
        // args: [outputDir] [count] [iterations] [warmup]
        string outDir = args.Length > 0 ? args[0] : ".";
        int count = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 2000;
        int iterations = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 50;
        int warmup = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 10;

        Directory.CreateDirectory(outDir);

        // Provenance of the actually-loaded product binary.
        Assembly buildAsm = typeof(ExpressionShredder).Assembly;
        string buildPath = buildAsm.Location;
        string buildHash = Sha256OfFile(buildPath);
        string buildVersion = buildAsm.GetName().Version?.ToString() ?? "unknown";
        string infoVersion = buildAsm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        List<Scenario> scenarios = BuildScenarios(count);

        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"probe\":\"ExpressionShredder\",");
        sb.Append("\"loadedBuildPath\":").Append(JsonStr(buildPath)).Append(',');
        sb.Append("\"loadedBuildSha256\":").Append(JsonStr(buildHash)).Append(',');
        sb.Append("\"loadedBuildVersion\":").Append(JsonStr(buildVersion)).Append(',');
        sb.Append("\"loadedBuildInformationalVersion\":").Append(JsonStr(infoVersion)).Append(',');
        sb.Append("\"runtime\":").Append(JsonStr(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)).Append(',');
        sb.Append("\"os\":").Append(JsonStr(System.Runtime.InteropServices.RuntimeInformation.OSDescription)).Append(',');
        sb.Append("\"gcServer\":").Append(System.Runtime.GCSettings.IsServerGC ? "true" : "false").Append(',');
        sb.Append("\"count\":").Append(count).Append(',');
        sb.Append("\"iterations\":").Append(iterations).Append(',');
        sb.Append("\"warmup\":").Append(warmup).Append(',');
        sb.Append("\"scenarios\":[");

        using var rawCsv = new StreamWriter(Path.Combine(outDir, "shredder-raw-samples.csv"));
        rawCsv.WriteLine("scenario,iteration,allocated_bytes,elapsed_ticks");

        for (int s = 0; s < scenarios.Count; s++)
        {
            Scenario sc = scenarios[s];
            Result r = Measure(sc, iterations, warmup, rawCsv);
            if (s > 0)
            {
                sb.Append(',');
            }

            sb.Append('{');
            sb.Append("\"name\":").Append(JsonStr(sc.Name)).Append(',');
            sb.Append("\"expressions\":").Append(sc.Expressions.Count).Append(',');
            sb.Append("\"distinctCaptureRefs\":").Append(r.DistinctRefs).Append(',');
            sb.Append("\"distinctCaptureValues\":").Append(r.DistinctValues).Append(',');
            sb.Append("\"allocBytesMin\":").Append(r.AllocMin).Append(',');
            sb.Append("\"allocBytesMedian\":").Append(r.AllocMedian).Append(',');
            sb.Append("\"allocBytesMean\":").Append(r.AllocMean.ToString("F1", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"timeTicksMin\":").Append(r.TicksMin).Append(',');
            sb.Append("\"timeTicksMedian\":").Append(r.TicksMedian).Append(',');
            sb.Append("\"timeUsMedian\":").Append(TicksToUs(r.TicksMedian).ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"retainedBytes\":").Append(r.RetainedBytes).Append(',');
            sb.Append("\"checksum\":").Append(r.Checksum);
            sb.Append('}');
        }

        sb.Append("]}");

        string json = sb.ToString();
        File.WriteAllText(Path.Combine(outDir, "shredder-summary.json"), json);
        Console.WriteLine(json);
        return 0;
    }

    private static List<Scenario> BuildScenarios(int count) =>
    [
        // Repeated capture text — weak interning can collapse duplicates.
        new("repeated_transform", CaptureKind.TransformValue, Repeat("@(Compile->'%(Filename).obj')", count)),
        new("repeated_separator", CaptureKind.Separator, Repeat("@(Compile, ', ')", count)),
        new("repeated_function", CaptureKind.FunctionName, Repeat("@(Compile->Distinct())", count)),
        // Realistic large mixed expression repeated across many "projects".
        new("repeated_mixed", CaptureKind.TransformValue,
            Repeat("@(Compile->'%(RootDir)%(Directory)%(Filename).obj')", count)),
        // CONTROL: every capture text is unique — no dedup possible; isolates pure intern-path cost.
        new("control_unique_transform", CaptureKind.TransformValue, Unique("@(Compile->'%(Filename).obj{0}')", count)),
        // CONTROL: very short capture text — smallest possible per-string saving.
        new("control_short", CaptureKind.TransformValue, Repeat("@(A->'x')", count)),
        // CONTROL: single occurrence — no repetition at all.
        new("control_norepeat", CaptureKind.TransformValue, Repeat("@(Compile->'%(Filename).obj')", 1)),
    ];

    /// <summary>
    /// Cold, single-scenario measurement. Intended to be launched in a FRESH process per scenario so
    /// the process-global weak-intern table starts empty: this isolates the true cold-miss cost that
    /// the warm <see cref="Run"/> loop hides (its warmup + dedup/retention passes prime the table).
    /// With a cold table, repeated identical text still collapses after the first miss, but the
    /// unique/no-repeat controls incur a real miss per distinct value — the honest worst case.
    /// args: [scenarioIndex] [count] [outCsv]
    /// </summary>
    public static int RunCold(string[] args)
    {
        int index = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 0;
        int count = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 2000;
        string? outCsv = args.Length > 2 ? args[2] : null;

        Assembly buildAsm = typeof(ExpressionShredder).Assembly;
        string buildHash = Sha256OfFile(buildAsm.Location);

        List<Scenario> scenarios = BuildScenarios(count);
        Scenario sc = scenarios[index];

        // Force the enumerator/JIT paths that are NOT the string allocation under test to warm up
        // WITHOUT touching this scenario's capture text: shred a single throwaway expression whose
        // captures share no content with any scenario, so the intern table stays cold for 'sc'.
        long sink = ShredCorpus(new Scenario("jit_warm", CaptureKind.TransformValue, Repeat("@(ZzQqJit->'zzqqjit')", 1)), null);

        // Measure exactly one cold shred of the whole corpus.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        long checksum = ShredCorpus(sc, null);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Distinct backing instances for this cold shred (weak interning collapses; baseline does not).
        var refSet = new HashSet<string>(new RefComparer());
        var valSet = new HashSet<string>(StringComparer.Ordinal);
        ShredCorpus(sc, cap =>
        {
            if (cap is not null)
            {
                refSet.Add(cap);
                valSet.Add(cap);
            }
        });

        string line = string.Create(CultureInfo.InvariantCulture,
            $"{sc.Name},{sc.Expressions.Count},{allocated},{refSet.Count},{valSet.Count},{checksum + sink},{buildHash}");
        Console.WriteLine("scenario,expressions,cold_alloc_bytes,distinct_refs,distinct_values,checksum,build_sha256");
        Console.WriteLine(line);
        if (outCsv is not null)
        {
            bool exists = File.Exists(outCsv);
            using var w = new StreamWriter(outCsv, append: true);
            if (!exists)
            {
                w.WriteLine("scenario,expressions,cold_alloc_bytes,distinct_refs,distinct_values,checksum,build_sha256");
            }

            w.WriteLine(line);
        }

        return 0;
    }

    private static Result Measure(Scenario sc, int iterations, int warmup, StreamWriter rawCsv)
    {
        // Warmup (also primes the weak-intern table so steady-state hit behavior is what we measure).
        long checksum = 0;
        for (int i = 0; i < warmup; i++)
        {
            checksum += ShredCorpus(sc, null);
        }

        var allocSamples = new long[iterations];
        var tickSamples = new long[iterations];
        var sw = new Stopwatch();

        for (int i = 0; i < iterations; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            checksum += ShredCorpus(sc, null);
            sw.Stop();
            long after = GC.GetAllocatedBytesForCurrentThread();
            allocSamples[i] = after - before;
            tickSamples[i] = sw.ElapsedTicks;
            rawCsv.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{sc.Name},{i},{allocSamples[i]},{tickSamples[i]}"));
        }

        // Dedup + distinct-value pass: collect the actual capture-text instances.
        var refSet = new HashSet<string>(new RefComparer());
        var valSet = new HashSet<string>(StringComparer.Ordinal);
        ShredCorpus(sc, cap =>
        {
            if (cap is not null)
            {
                refSet.Add(cap);
                valSet.Add(cap);
            }
        });

        // Retention: hold every capture instance alive across a forced GC and measure managed heap growth.
        long retained = MeasureRetained(sc);

        Array.Sort(allocSamples);
        Array.Sort(tickSamples);
        double mean = 0;
        foreach (long a in allocSamples)
        {
            mean += a;
        }

        mean /= iterations;

        return new Result
        {
            DistinctRefs = refSet.Count,
            DistinctValues = valSet.Count,
            AllocMin = allocSamples[0],
            AllocMedian = allocSamples[iterations / 2],
            AllocMean = mean,
            TicksMin = tickSamples[0],
            TicksMedian = tickSamples[iterations / 2],
            RetainedBytes = retained,
            Checksum = checksum,
        };
    }

    // Shreds the whole corpus. If <paramref name="collect"/> is provided, the target capture text
    // (per scenario kind) is passed to it; otherwise a cheap checksum is accumulated to defeat DCE.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ShredCorpus(Scenario sc, Action<string?>? collect)
    {
        long checksum = 0;
        List<string> exprs = sc.Expressions;
        for (int e = 0; e < exprs.Count; e++)
        {
            var it = ExpressionShredder.GetReferencedItemExpressions(exprs[e]);
            while (it.MoveNext())
            {
                var cur = it.Current;
                string? target = Extract(cur, sc.Kind);
                if (collect is not null)
                {
                    collect(target);
                }
                else if (target is not null)
                {
                    checksum += target.Length;
                }
            }
        }

        return checksum;
    }

    private static long MeasureRetained(Scenario sc)
    {
        // Baseline after collecting garbage.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalMemory(true);

        var held = new List<string>(sc.Expressions.Count);
        ShredCorpus(sc, cap =>
        {
            if (cap is not null)
            {
                held.Add(cap);
            }
        });

        long after = GC.GetTotalMemory(true);
        GC.KeepAlive(held);
        return after - before;
    }

    private static string? Extract(ExpressionShredder.ItemExpressionCapture cur, CaptureKind kind)
    {
        switch (kind)
        {
            case CaptureKind.Separator:
                return cur.Separator;
            case CaptureKind.FunctionName:
                if (cur.Captures is { Count: > 0 })
                {
                    return cur.Captures[0].FunctionName;
                }

                return null;
            case CaptureKind.TransformValue:
            default:
                if (cur.Captures is { Count: > 0 })
                {
                    return cur.Captures[0].Value;
                }

                return null;
        }
    }

    private static List<string> Repeat(string expr, int count)
    {
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            // Distinct string INSTANCES with identical content, mimicking the same expression text
            // parsed independently across many projects/evaluations.
            list.Add(new string(expr.AsSpan()));
        }

        return list;
    }

    private static List<string> Unique(string format, int count)
    {
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(string.Format(CultureInfo.InvariantCulture, format, i));
        }

        return list;
    }

    private static double TicksToUs(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

    private static string Sha256OfFile(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        byte[] hash = sha.ComputeHash(fs);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string JsonStr(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    private enum CaptureKind
    {
        TransformValue,
        Separator,
        FunctionName,
    }

    private sealed class Scenario
    {
        public Scenario(string name, CaptureKind kind, List<string> expressions)
        {
            Name = name;
            Kind = kind;
            Expressions = expressions;
        }

        public string Name { get; }

        public CaptureKind Kind { get; }

        public List<string> Expressions { get; }
    }

    private struct Result
    {
        public int DistinctRefs;
        public int DistinctValues;
        public long AllocMin;
        public long AllocMedian;
        public double AllocMean;
        public long TicksMin;
        public long TicksMedian;
        public long RetainedBytes;
        public long Checksum;
    }
}
