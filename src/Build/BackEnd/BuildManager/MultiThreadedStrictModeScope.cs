// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private static readonly LockObject s_stateLock = NativeMethodsShared.CurrentDirectoryLock;
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

    internal static CurrentDirectorySnapshot CaptureCurrentDirectory()
    {
        lock (s_stateLock)
        {
            return new(Directory.GetCurrentDirectory(), s_activeScope);
        }
    }

    internal static MultiThreadedStrictModeScope Enter(int buildId) => Enter(buildId, CaptureCurrentDirectory());

    internal static MultiThreadedStrictModeScope Enter(int buildId, CurrentDirectorySnapshot directoryToRestore)
        => Enter(buildId, directoryToRestore, Directory.SetCurrentDirectory);

    internal static MultiThreadedStrictModeScope Enter(
        int buildId,
        CurrentDirectorySnapshot snapshot,
        Action<string> setCurrentDirectory)
    {
        lock (s_stateLock)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(s_activeScope is null, "MultiThreadedStrictModeAlreadyActive");

            string directoryToRestore = snapshot.Directory;
            string temporaryDirectory = FileUtilities.GetTemporaryDirectory(createDirectory: false);
            string sentinelDirectory = Path.Combine(temporaryDirectory, SentinelDirectoryName);
            try
            {
                Directory.CreateDirectory(sentinelDirectory);
                // Resolve the owned path, never a mutable process CWD that another thread could replace.
                sentinelDirectory = NativeMethodsShared.IsWindows
                    ? NativeMethodsShared.GetLongFilePath(sentinelDirectory)
                    : NativeMethodsShared.RealPath(sentinelDirectory) ?? throw new Win32Exception();
                string canonicalTemporaryDirectory = NativeMethodsShared.IsWindows
                    ? NativeMethodsShared.GetLongFilePath(temporaryDirectory)
                    : NativeMethodsShared.RealPath(temporaryDirectory) ?? throw new Win32Exception();
                Assumed.True(
                    FileUtilities.PathComparer.Equals(Path.GetDirectoryName(sentinelDirectory), canonicalTemporaryDirectory),
                    "The strict-mode sentinel must remain within its temporary directory.");
                MultiThreadedStrictModeScope scope = new(buildId, sentinelDirectory, directoryToRestore, temporaryDirectory);
                setCurrentDirectory(sentinelDirectory);
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

                FileUtilities.TryDeleteFileOrDirectory(temporaryDirectory);
                throw;
            }
        }
    }

    internal readonly struct CurrentDirectorySnapshot(string directory, MultiThreadedStrictModeScope? owner)
    {
        internal string Directory =>
            owner is not null && FileUtilities.PathComparer.Equals(directory, owner.SentinelDirectory)
                ? owner._directoryToRestore
                : directory;

        internal void Restore()
        {
            lock (s_stateLock)
            {
                // A saved sentinel expires with its owner; an unowned snapshot must not displace a new owner.
                if (ReferenceEquals(owner, s_activeScope))
                {
                    NativeMethodsShared.SetCurrentDirectory(owner?.SentinelDirectory ?? directory);
                }
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

        FileUtilities.TryDeleteFileOrDirectory(_temporaryDirectory);
    }

    internal bool VerifyAndReportCurrentDirectory(
        TaskLoggingContext taskLoggingContext,
        string taskName,
        ElementLocation taskLocation,
        bool convertErrorsToWarnings)
    {
        string? unexpectedDirectory = DetectCurrentDirectoryViolation();
        if (unexpectedDirectory is null)
        {
            return false;
        }

        if (convertErrorsToWarnings)
        {
            taskLoggingContext.LogWarning(null, new BuildEventFileInfo(taskLocation),
                "MultiThreadedStrictModeCurrentDirectoryChanged", taskName, unexpectedDirectory, SentinelDirectory);
            taskLoggingContext.LogComment(MessageImportance.Normal, "ErrorConvertedIntoWarning");
        }
        else
        {
            taskLoggingContext.LogError(new BuildEventFileInfo(taskLocation),
                "MultiThreadedStrictModeCurrentDirectoryChanged", taskName, unexpectedDirectory, SentinelDirectory);
        }

        return true;
    }

    internal string? DetectCurrentDirectoryViolation()
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

        return unexpectedDirectory;
    }

    internal void VerifyUnresolvedPathWrites(ElementLocation location)
    {
        string? entries = DetectUnresolvedPathWrites();
        if (entries is not null)
        {
            ProjectErrorUtilities.ThrowInvalidProject(location,
                "MultiThreadedStrictModeUnresolvedPathWrite", entries, SentinelDirectory);
        }
    }

    internal string? DetectUnresolvedPathWrites()
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

                // Remove stray outputs at project/build boundaries and retry locked leftovers without duplicate diagnostics.
                if (FileUtilities.TryDeleteFileOrDirectory(entry))
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
}
