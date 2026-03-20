// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ═══════════════════════════════════════════════════════════════════════════════
// DEMO: Helper class opt-in with [MSBuildMultiThreadableTaskAnalyzed],
//       new banned APIs, and type-level Console detection
// ═══════════════════════════════════════════════════════════════════════════════

#nullable disable

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.Build.Framework
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MSBuildMultiThreadableTaskAnalyzedAttribute : Attribute { }
}

// ─────────────────────────────────────────────────────────────────────────
// HELPER CLASS 1: Opted-in helper class - should be analyzed
// ─────────────────────────────────────────────────────────────────────────
[MSBuildMultiThreadableTaskAnalyzed]
public class FileProcessingHelper
{
    // ❌ MSBuildTask0003: File.Exists with unwrapped path
    public bool FileExists(string path) => File.Exists(path);

    // ❌ MSBuildTask0002: Environment.GetEnvironmentVariable
    public string GetEnv(string name) => Environment.GetEnvironmentVariable(name);

    // ❌ MSBuildTask0001: Console.WriteLine
    public void Log(string msg) => Console.WriteLine(msg);

    // ✅ Safe: uses AbsolutePath
    public bool FileExistsSafe(AbsolutePath path) => File.Exists(path);

    // ✅ Safe: uses ITaskItem.GetMetadata("FullPath")
    public bool FileExistsMeta(ITaskItem item) => File.Exists(item.GetMetadata("FullPath"));
}

// ─────────────────────────────────────────────────────────────────────────
// HELPER CLASS 2: NOT opted-in - should NOT be analyzed
// ─────────────────────────────────────────────────────────────────────────
public class RegularHelperClass
{
    // ✅ Not analyzed (no attribute, not a task)
    public bool FileExists(string path) => File.Exists(path);
    public string GetEnv(string name) => Environment.GetEnvironmentVariable(name);
    public void Log(string msg) => Console.WriteLine(msg);
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 51: Directory.SetCurrentDirectory (CriticalError)
// ─────────────────────────────────────────────────────────────────────────
public class SetCurrentDirTask : Task
{
    public override bool Execute()
    {
        // ❌ MSBuildTask0001: Directory.SetCurrentDirectory is process-wide
        Directory.SetCurrentDirectory("/tmp");
        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 52: Directory.GetCurrentDirectory (TaskEnvironment)
// ─────────────────────────────────────────────────────────────────────────
public class GetCurrentDirTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }

    public override bool Execute()
    {
        // ❌ MSBuildTask0002: Directory.GetCurrentDirectory
        var dir = Directory.GetCurrentDirectory();
        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 53: Path.GetTempPath and Path.GetTempFileName
// ─────────────────────────────────────────────────────────────────────────
public class TempPathTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }

    public override bool Execute()
    {
        // ❌ MSBuildTask0002: Path.GetTempPath depends on TMP/TEMP env vars
        var tmp = Path.GetTempPath();

        // ❌ MSBuildTask0002: Path.GetTempFileName depends on TMP/TEMP env vars
        var tempFile = Path.GetTempFileName();

        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 54: Environment.GetFolderPath
// ─────────────────────────────────────────────────────────────────────────
public class FolderPathTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }

    public override bool Execute()
    {
        // ❌ MSBuildTask0002: Environment.GetFolderPath
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // ❌ MSBuildTask0002: Environment.GetFolderPath with option
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);

        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 55: Type-level Console ban - catches exotic Console members
// ─────────────────────────────────────────────────────────────────────────
public class ExoticConsoleTask : Task
{
    public override bool Execute()
    {
        // ❌ MSBuildTask0001: Console.ForegroundColor
        Console.ForegroundColor = ConsoleColor.Red;

        // ❌ MSBuildTask0001: Console.ResetColor
        Console.ResetColor();

        // ❌ MSBuildTask0001: Console.Beep
        Console.Beep();

        // ❌ MSBuildTask0001: Console.Clear
        Console.Clear();

        // ❌ MSBuildTask0001: Console.BufferWidth
        var w = Console.BufferWidth;

        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 56: Process.Start(ProcessStartInfo) overload
// ─────────────────────────────────────────────────────────────────────────
public class ProcessStartPsiTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }

    public override bool Execute()
    {
        // ❌ MSBuildTask0002: new ProcessStartInfo
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet");

        // ❌ MSBuildTask0002: Process.Start(ProcessStartInfo)
        System.Diagnostics.Process.Start(psi);

        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 57: FileSystemWatcher constructor (file path type)
// ─────────────────────────────────────────────────────────────────────────
public class FileWatcherTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }
    public string WatchDir { get; set; }

    public override bool Execute()
    {
        // ❌ MSBuildTask0003: FileSystemWatcher with unwrapped path
        using var watcher = new FileSystemWatcher(WatchDir);

        // ✅ Safe: with GetAbsolutePath
        using var watcher2 = new FileSystemWatcher(TaskEnvironment.GetAbsolutePath(WatchDir));

        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// TASK 58: Non-path string params NOT flagged (File.WriteAllText fix)
// ─────────────────────────────────────────────────────────────────────────
public class WriteAllTextSafeTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }
    public string OutputFile { get; set; }

    public override bool Execute()
    {
        AbsolutePath safePath = TaskEnvironment.GetAbsolutePath(OutputFile);

        // ✅ Safe: path is absolutized, "contents" param should NOT be flagged
        File.WriteAllText(safePath, "these are contents, not a path");
        File.AppendAllText(safePath, "more contents");

        // ❌ MSBuildTask0003: path param is NOT absolutized
        File.WriteAllText(OutputFile, "contents");

        return true;
    }
}
