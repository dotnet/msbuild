using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

/// <summary>
///  Pulls execution counts straight out of a binary log's event stream.
/// </summary>
/// <remarks>
///  Comparing replayed *text* is unavoidably noisy: node ids interleave, and MSBuild's console and
///  file loggers only emit a target header when that target happens to log something at the current
///  verbosity, so a target that is silent in one run and chatty in another looks like it only ran in
///  one of them. The events themselves have none of that ambiguity - every target that executes
///  raises exactly one TargetStarted - so these counts are the authoritative answer to "did the two
///  builds do the same work".
/// </remarks>
public static class BinlogStructure
{
    public sealed class Counts
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

    // The three builds run in the same working tree, so project paths already agree verbatim; only
    // the directory separator is normalized, for the case where a log is compared across platforms.
    private static string Project(string path) => string.IsNullOrEmpty(path) ? "<none>" : path.Replace('/', '\\');

    public static Counts Collect(string binlogPath)
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
