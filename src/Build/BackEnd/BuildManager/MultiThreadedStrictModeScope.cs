// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution;

/// <summary>
/// Owns the process current directory during an opt-in strict build.
/// </summary>
internal sealed class MultiThreadedStrictModeScope
{
    internal const string SentinelDirectoryName = "MSBuild-MT-Strict-Sentinel-CWD";
    private const int MaxReportedEntries = 10;

    // Serializes scope installation, restoration and directory repair. Never log under this lock.
    private static readonly object s_stateLock = new();
    private static MultiThreadedStrictModeScope? s_activeScope;

    private readonly object _reportedEntriesLock = new();
    private readonly HashSet<string> _reportedEntries = new(FileUtilities.PathComparer);
    private readonly HashSet<string> _reportedCurrentDirectories = new(FileUtilities.PathComparer);
    private readonly string _directoryToRestore;

    private MultiThreadedStrictModeScope(string sentinelDirectory, string directoryToRestore)
    {
        SentinelDirectory = sentinelDirectory;
        _directoryToRestore = directoryToRestore;
    }

    internal string SentinelDirectory { get; }

    internal static MultiThreadedStrictModeScope? ActiveScope => Volatile.Read(ref s_activeScope);

    internal static MultiThreadedStrictModeScope Enter(ILoggingService? loggingService)
    {
        MultiThreadedStrictModeScope scope;

        lock (s_stateLock)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(s_activeScope is null, "MultiThreadedStrictModeAlreadyActive");

            // Capture only after taking ownership. An exiting scope may still be restoring CWD.
            string directoryToRestore = Directory.GetCurrentDirectory();
            string sentinelDirectory = FileUtilities.GetTemporaryDirectory(subfolder: SentinelDirectoryName);
            scope = Install(sentinelDirectory, directoryToRestore);
            Volatile.Write(ref s_activeScope, scope);
        }

        try
        {
            loggingService?.LogComment(
                BuildEventContext.Invalid,
                MessageImportance.Low,
                "MultiThreadedStrictModeEnabled",
                scope.SentinelDirectory);
        }
        catch
        {
            // A synchronous logger can throw before the caller receives the scope.
            scope.Exit();
            throw;
        }

        return scope;
    }

    private static MultiThreadedStrictModeScope Install(string sentinelDirectory, string directoryToRestore)
    {
        try
        {
            // Unlike NativeMethodsShared, the managed API reports failure by throwing.
            Directory.SetCurrentDirectory(sentinelDirectory);
            // Keep the actual spelling: entering a directory can resolve symlinks.
            return new MultiThreadedStrictModeScope(Directory.GetCurrentDirectory(), directoryToRestore);
        }
        catch
        {
            NativeMethodsShared.SetCurrentDirectory(directoryToRestore);
            throw;
        }
    }

    internal void Exit()
    {
        lock (s_stateLock)
        {
            if (!ReferenceEquals(s_activeScope, this))
            {
                return;
            }

            try
            {
                Directory.SetCurrentDirectory(_directoryToRestore);
            }
            catch
            {
                // Leave the sentinel even if the host directory disappeared, but surface the restoration failure.
                NativeMethodsShared.SetCurrentDirectory(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);
                throw;
            }
            finally
            {
                Volatile.Write(ref s_activeScope, null);
            }
        }
    }

    internal bool VerifyAndReportProcessState(
        TaskLoggingContext taskLoggingContext,
        string taskName,
        ElementLocation taskLocation,
        bool convertErrorsToWarnings)
    {
        Violations violations = DetectViolations();
        if (violations.UnexpectedCurrentDirectory is not null)
        {
            Report("MultiThreadedStrictModeCurrentDirectoryChanged", taskName, violations.UnexpectedCurrentDirectory, SentinelDirectory);
        }

        if (violations.UnresolvedPathWrites is not null)
        {
            Report("MultiThreadedStrictModeUnresolvedPathWrite", taskName, violations.UnresolvedPathWrites, SentinelDirectory);
        }

        return violations.Any;

        void Report(string resourceName, params object[] arguments)
        {
            if (convertErrorsToWarnings)
            {
                taskLoggingContext.LogWarning(null, new BuildEventFileInfo(taskLocation), resourceName, arguments);
                taskLoggingContext.LogComment(MessageImportance.Normal, "ErrorConvertedIntoWarning");
            }
            else
            {
                taskLoggingContext.LogError(new BuildEventFileInfo(taskLocation), resourceName, arguments);
            }
        }
    }

    internal Violations DetectViolations()
    {
        string? unexpectedDirectory = null;
        string currentDirectory = Directory.GetCurrentDirectory();
        if (!FileUtilities.PathsEqual(currentDirectory, SentinelDirectory))
        {
            lock (s_stateLock)
            {
                if (ReferenceEquals(s_activeScope, this))
                {
                    Directory.SetCurrentDirectory(SentinelDirectory);
                    if (_reportedCurrentDirectories.Add(currentDirectory))
                    {
                        unexpectedDirectory = currentDirectory;
                    }
                }
            }
        }

        return new Violations(unexpectedDirectory, TakeUnreportedSentinelDirectoryEntries());
    }

    private string? TakeUnreportedSentinelDirectoryEntries()
    {
        // Timestamps cannot safely replace enumeration: multiple writes can share the same timestamp.
        if (!HasAnyEntry(SentinelDirectory))
        {
            return null;
        }

        List<string> entries = [];
        bool truncated = false;
        lock (_reportedEntriesLock)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(SentinelDirectory))
            {
                if (entries.Count == MaxReportedEntries)
                {
                    truncated = true;
                    break;
                }

                string name = Path.GetFileName(entry);
                if (!_reportedEntries.Add(name))
                {
                    continue;
                }

                entries.Add(name);
                // Remove stray outputs so they cannot satisfy later unresolved reads.
                // Remember undeletable entries to avoid repeatedly blaming subsequent tasks.
                if (TryDelete(entry))
                {
                    _reportedEntries.Remove(name);
                }
            }
        }

        if (entries.Count == 0)
        {
            return null;
        }

        string entryList = string.Join(", ", entries);
        return truncated ? entryList + ", ..." : entryList;
    }

    private static bool HasAnyEntry(string directory)
    {
        foreach (string unused in Directory.EnumerateFileSystemEntries(directory))
        {
            return true;
        }

        return false;
    }

    private static bool TryDelete(string entry)
    {
        try
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }

            return true;
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            return false;
        }
    }

    internal readonly struct Violations(string? unexpectedCurrentDirectory, string? unresolvedPathWrites)
    {
        internal string? UnexpectedCurrentDirectory { get; } = unexpectedCurrentDirectory;
        internal string? UnresolvedPathWrites { get; } = unresolvedPathWrites;
        internal bool Any => UnexpectedCurrentDirectory is not null || UnresolvedPathWrites is not null;
    }
}
