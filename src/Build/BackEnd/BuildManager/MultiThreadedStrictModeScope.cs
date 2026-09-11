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
/// Owns the process current directory during a strict multi-threaded build.
/// </summary>
internal sealed class MultiThreadedStrictModeScope
{
    internal const string SentinelDirectoryName = "MSBuild-MT-Strict-Sentinel-CWD";

    // Serializes scope installation, restoration and directory repair. Never log under this lock.
    private static readonly object s_stateLock = new();
    private static MultiThreadedStrictModeScope? s_activeScope;

    private readonly object _reportedEntriesLock = new();
    private readonly HashSet<string> _reportedEntries = new(FileUtilities.PathComparer);
    private readonly string _directoryToRestore;
    private readonly string _temporaryDirectory;

    private MultiThreadedStrictModeScope(int buildId, string sentinelDirectory, string directoryToRestore, string temporaryDirectory)
    {
        BuildId = buildId;
        SentinelDirectory = sentinelDirectory;
        _directoryToRestore = directoryToRestore;
        _temporaryDirectory = temporaryDirectory;
    }

    internal int BuildId { get; }

    internal string SentinelDirectory { get; }

    internal static MultiThreadedStrictModeScope? ActiveScope => Volatile.Read(ref s_activeScope);

    internal static MultiThreadedStrictModeScope Enter(int buildId)
    {
        lock (s_stateLock)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(s_activeScope is null, "MultiThreadedStrictModeAlreadyActive");

            // Capture only after taking ownership. An exiting scope may still be restoring CWD.
            string directoryToRestore = Directory.GetCurrentDirectory();
            string temporaryDirectory = FileUtilities.GetTemporaryDirectory(createDirectory: false);
            string sentinelDirectory = Path.Combine(temporaryDirectory, SentinelDirectoryName);
            Directory.CreateDirectory(sentinelDirectory);
            Directory.SetCurrentDirectory(sentinelDirectory);
            try
            {
                // Keep the actual spelling: entering a directory can resolve symlinks.
                MultiThreadedStrictModeScope scope = new(buildId, Directory.GetCurrentDirectory(), directoryToRestore, temporaryDirectory);
                Volatile.Write(ref s_activeScope, scope);
                return scope;
            }
            catch (Exception entryFailure) when (!ExceptionHandling.IsCriticalException(entryFailure))
            {
                try
                {
                    Directory.SetCurrentDirectory(directoryToRestore);
                }
                catch (Exception restorationFailure) when (!ExceptionHandling.IsCriticalException(restorationFailure))
                {
                    throw new AggregateException(entryFailure, restorationFailure);
                }

                TryDelete(temporaryDirectory);
                throw;
            }
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
            finally
            {
                Volatile.Write(ref s_activeScope, null);
            }
        }

        TryDelete(_temporaryDirectory);
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
        if (!FileUtilities.PathComparer.Equals(currentDirectory, SentinelDirectory))
        {
            lock (s_stateLock)
            {
                if (ReferenceEquals(s_activeScope, this))
                {
                    // Another checker may have repaired the directory while this one waited for the lock.
                    currentDirectory = Directory.GetCurrentDirectory();
                    if (!FileUtilities.PathComparer.Equals(currentDirectory, SentinelDirectory))
                    {
                        Directory.SetCurrentDirectory(SentinelDirectory);
                        unexpectedDirectory = currentDirectory;
                    }
                }
            }
        }

        return new Violations(unexpectedDirectory, TakeUnreportedSentinelDirectoryEntries());
    }

    private string? TakeUnreportedSentinelDirectoryEntries()
    {
        lock (_reportedEntriesLock)
        {
            // Only failed deletions remain remembered between checks.
            HashSet<string>? noLongerPresent = _reportedEntries.Count == 0 ? null : new(_reportedEntries, FileUtilities.PathComparer);
            List<string>? entries = null;
            // Timestamps cannot safely replace enumeration: multiple writes can share the same timestamp.
            foreach (string entry in Directory.EnumerateFileSystemEntries(SentinelDirectory))
            {
                string name = Path.GetFileName(entry);
                noLongerPresent?.Remove(name);
                if (_reportedEntries.Add(name))
                {
                    (entries ??= []).Add(name);
                }

                // Remove stray outputs so they cannot satisfy later unresolved reads.
                // Retry locked leftovers without repeatedly blaming subsequent tasks.
                if (TryDelete(entry))
                {
                    _reportedEntries.Remove(name);
                }
            }

            if (noLongerPresent is not null)
            {
                _reportedEntries.ExceptWith(noLongerPresent);
            }

            return entries is null ? null : string.Join(", ", entries);
        }
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
