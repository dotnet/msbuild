// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MSBuild.Benchmarks;

/// <summary>
/// Driver for <see cref="BinlogAllocationHarness"/>. Runs a fixed workload matrix and writes
/// machine-readable CSVs, output hashes, timing, environment, and assembly provenance into an
/// output directory so a full A/B can be reconstructed from retained artifacts.
///
/// Usage: MSBuild.Benchmarks --alloc-harness &lt;label&gt; &lt;outputDir&gt;
/// </summary>
public static class AllocHarnessRunner
{
    private readonly record struct Cell(int RecordCount, int PropsPerEvent, int ValueLen, bool Distinct);

    public static int Run(List<string> args)
    {
        if (args.Count < 2)
        {
            Console.Error.WriteLine("Usage: --alloc-harness <label> <outputDir>");
            return 2;
        }

        string label = args[0];
        string outDir = args[1];
        Directory.CreateDirectory(outDir);

        // Allocation + timing matrix. Distinct=true means every event has a unique property bag
        // (worst case for the writer, best case for the fix). Distinct=false means all events
        // share one bag (dedups to a single record: control for "no repeated savings").
        var cells = new List<Cell>
        {
            new(0, 20, 8, true),      // control: no records at all
            new(1, 20, 8, true),
            new(10, 20, 8, true),
            new(100, 20, 8, true),
            new(1000, 20, 8, true),
            new(10000, 20, 8, true),
            new(1000, 1, 8, true),    // payload variation: tiny bags
            new(1000, 50, 8, true),   // payload variation: large bags
            new(1000, 20, 64, true),  // payload variation: long values
            new(1000, 20, 8, false),  // duplicate bags -> single record (fix saves ~0 beyond first)
        };

        var allocCsv = new StringBuilder();
        allocCsv.AppendLine("label,recordCount,propsPerEvent,valueLen,distinct,bytesPerOp,bytesPerRecord,outputLength");

        var timeCsv = new StringBuilder();
        timeCsv.AppendLine("label,recordCount,propsPerEvent,valueLen,distinct,medianNsPerOp");

        foreach (Cell c in cells)
        {
            int iterations = Math.Clamp(200_000 / Math.Max(1, c.RecordCount * c.PropsPerEvent), 20, 2000);
            (long bytesPerOp, long outputLength) = BinlogAllocationHarness.MeasureAllocations(
                c.RecordCount, c.PropsPerEvent, c.ValueLen, c.Distinct, warmup: 5, iterations: iterations);

            double bytesPerRecord = c.RecordCount > 0 ? (double)bytesPerOp / c.RecordCount : 0;

            allocCsv.AppendLine(string.Join(',',
                label, c.RecordCount, c.PropsPerEvent, c.ValueLen, c.Distinct,
                bytesPerOp, bytesPerRecord.ToString("F2", CultureInfo.InvariantCulture), outputLength));

            int timeIters = Math.Clamp(50_000 / Math.Max(1, c.RecordCount), 50, 500);
            double medianNs = BinlogAllocationHarness.MeasureTimeNs(
                c.RecordCount, c.PropsPerEvent, c.ValueLen, c.Distinct, warmup: 10, iterations: timeIters);

            timeCsv.AppendLine(string.Join(',',
                label, c.RecordCount, c.PropsPerEvent, c.ValueLen, c.Distinct,
                medianNs.ToString("F1", CultureInfo.InvariantCulture)));

            Console.WriteLine($"[{label}] rc={c.RecordCount} props={c.PropsPerEvent} vlen={c.ValueLen} distinct={c.Distinct} " +
                $"=> {bytesPerOp} B/op ({bytesPerRecord:F2} B/rec), {medianNs:F1} ns/op, out={outputLength}");
        }

        // Cross-arm output-byte hashes for a few deterministic workloads.
        var hashSb = new StringBuilder();
        hashSb.AppendLine($"# Output byte hashes for label={label}");
        foreach (Cell c in new[]
        {
            new Cell(100, 20, 8, true),
            new Cell(1000, 20, 8, true),
            new Cell(1000, 20, 8, false),
        })
        {
            (string sha, long len) = BinlogAllocationHarness.OutputHash(c.RecordCount, c.PropsPerEvent, c.ValueLen, c.Distinct);
            hashSb.AppendLine($"rc={c.RecordCount},props={c.PropsPerEvent},vlen={c.ValueLen},distinct={c.Distinct},len={len},sha256={sha}");
            Console.WriteLine($"[{label}] HASH rc={c.RecordCount} distinct={c.Distinct} len={len} sha256={sha}");
        }

        // Provenance + environment.
        var env = new StringBuilder();
        env.AppendLine($"label={label}");
        env.AppendLine($"utc={DateTime.UtcNow:O}");
        env.AppendLine($"os={RuntimeInformation.OSDescription}");
        env.AppendLine($"arch={RuntimeInformation.ProcessArchitecture}");
        env.AppendLine($"framework={RuntimeInformation.FrameworkDescription}");
        env.AppendLine($"processorCount={Environment.ProcessorCount}");
        env.AppendLine($"gcServer={System.Runtime.GCSettings.IsServerGC}");
        env.AppendLine($"gcLatency={System.Runtime.GCSettings.LatencyMode}");
        env.AppendLine(BinlogAllocationHarness.AssemblyProvenance());

        File.WriteAllText(Path.Combine(outDir, $"alloc-{label}.csv"), allocCsv.ToString());
        File.WriteAllText(Path.Combine(outDir, $"time-{label}.csv"), timeCsv.ToString());
        File.WriteAllText(Path.Combine(outDir, $"hash-{label}.txt"), hashSb.ToString());
        File.WriteAllText(Path.Combine(outDir, $"provenance-{label}.txt"), env.ToString());

        Console.WriteLine();
        Console.WriteLine(env.ToString());
        Console.WriteLine($"Artifacts written to {outDir}");
        return 0;
    }
}
