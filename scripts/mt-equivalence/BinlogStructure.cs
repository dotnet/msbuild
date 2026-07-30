using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

/// <summary>
///  Writes the target/task/project execution counts of a binary log to a JSON file.
/// </summary>
/// <remarks>
///  Comparing replayed *text* is unavoidably noisy: node ids interleave, and MSBuild's loggers only
///  emit a target header when that target happens to log something at the current verbosity, so a
///  target that is silent in one run and chatty in another looks like it only ran in one of them.
///  Diagnostic verbosity does not fix it either - engine-assigned TargetIds are not unique across
///  projects, and a measurable share of TaskStarted events never reach the text at all. The event
///  stream has none of that ambiguity: every target that executes raises exactly one TargetStarted.
///  <para>
///   This is a standalone program rather than something loaded into the calling PowerShell because
///   Microsoft.Build targets a newer framework than the pwsh on the build agents runs on, so
///   Add-Type cannot even compile against it there (CS1705). Running on the SDK's own runtime side-
///   steps the whole question, and keeps MSBuild's assemblies out of the caller.
///  </para>
/// </remarks>
internal static class BinlogStructure
{
    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("usage: binlogstructure <binlog> <output.json> <msbuildAssemblyDirectory>");
            return 2;
        }

        string binlog = args[0];
        string outputPath = args[1];
        string assemblyDirectory = args[2];

        // Microsoft.Build drags in a good number of its neighbours; resolve them all out of the
        // directory it came from rather than listing them.
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string candidate = Path.Combine(assemblyDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        Counts counts = Collect(binlog);

        using (FileStream stream = File.Create(outputPath))
        {
            JsonSerializer.Serialize(stream, counts);
        }

        Console.WriteLine($"targets={Total(counts.Targets)} tasks={Total(counts.Tasks)} projects={Total(counts.Projects)}");
        return 0;
    }

    private static int Total(Dictionary<string, int> counts)
    {
        int total = 0;
        foreach (int n in counts.Values)
        {
            total += n;
        }

        return total;
    }

    private sealed class Counts
    {
        public Dictionary<string, int> Targets { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> TargetsByProject { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> Tasks { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> Projects { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> Diagnostics { get; } = new(StringComparer.Ordinal);
    }

    private static void Bump(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int n);
        counts[key] = n + 1;
    }

    // The runs being compared share a working tree, so project paths already agree verbatim; only
    // the directory separator is normalized, for the case where logs are compared across platforms.
    private static string Project(string? path) => string.IsNullOrEmpty(path) ? "<none>" : path.Replace('/', '\\');

    private static Counts Collect(string binlogPath)
    {
        Counts counts = new();
        BinaryLogReplayEventSource reader = new();

        reader.TargetStarted += (_, e) =>
        {
            Bump(counts.Targets, "target " + e.TargetName);
            Bump(counts.TargetsByProject, "target " + e.TargetName + " in " + Project(e.ProjectFile));
        };
        reader.TaskStarted += (_, e) => Bump(counts.Tasks, "task " + e.TaskName);
        reader.ProjectStarted += (_, e) => Bump(counts.Projects, "project " + Project(e.ProjectFile));
        reader.WarningRaised += (_, e) => Bump(counts.Diagnostics, "warning " + e.Code);
        reader.ErrorRaised += (_, e) => Bump(counts.Diagnostics, "error " + e.Code);

        reader.Replay(binlogPath);
        return counts;
    }
}
