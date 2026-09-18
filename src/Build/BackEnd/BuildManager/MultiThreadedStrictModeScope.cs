// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution;

/// <summary>
/// Owns the process current directory during a strict multi-threaded build.
/// </summary>
internal sealed class MultiThreadedStrictModeScope
{
    internal const string SentinelDirectoryName = "MT-sentinel-CWD";

    // Serializes scope installation, restoration and directory repair. Never log under this lock.
    private static readonly LockType s_stateLock = new();
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

    internal static MultiThreadedStrictModeScope Enter(int buildId, CurrentDirectorySnapshot snapshot)
        => Enter(buildId, snapshot, Directory.SetCurrentDirectory);

    // The test callback runs under the CWD lock and must not wait for engine work.
    internal static MultiThreadedStrictModeScope Enter(
        int buildId,
        CurrentDirectorySnapshot snapshot,
        Action<string> setCurrentDirectory)
    {
        lock (s_stateLock)
        {
            if (s_activeScope is not null)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("MultiThreadedStrictModeAlreadyActive"));
            }

            string directoryToRestore = snapshot.RestoreTarget;
            string temporaryDirectory = FileUtilities.GetTemporaryDirectory(createDirectory: false);
            string sentinelDirectory = Path.Combine(temporaryDirectory, SentinelDirectoryName);
            bool entered = false;
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
                entered = true;
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

                throw;
            }
            finally
            {
                if (!entered)
                {
                    FileUtilities.TryDeleteFileOrDirectory(temporaryDirectory);
                }
            }
        }
    }

    internal readonly struct CurrentDirectorySnapshot(string directory, MultiThreadedStrictModeScope? owner)
    {
        internal string RestoreTarget =>
            owner is not null && FileUtilities.PathComparer.Equals(directory, owner.SentinelDirectory)
                ? owner._directoryToRestore
                : directory;
    }

    internal void Exit()
    {
        // When both locks are needed, take the scan lock before the CWD lock.
        lock (_reportedEntriesLock)
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
    }

    internal bool VerifyAndReportCurrentDirectory(
        TaskLoggingContext taskLoggingContext,
        string taskName,
        ElementLocation taskLocation,
        bool convertErrorsToWarnings)
    {
        string? unexpectedDirectory;
        try
        {
            unexpectedDirectory = DetectCurrentDirectoryViolation();
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            if (RecoverSentinelDirectory(taskLocation, e))
            {
                taskLoggingContext.LogWarning(null, new BuildEventFileInfo(taskLocation),
                    "MultiThreadedStrictModeSentinelMissing", SentinelDirectory);
            }

            return false;
        }

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

    internal string? VerifyUnresolvedPathWrites(ElementLocation location, out bool recovered)
    {
        recovered = false;
        bool trace = MSBuildEventSource.Log.IsEnabled();
        if (trace)
        {
            MSBuildEventSource.Log.StrictModeDirectoryScanStart(BuildId, location.File);
        }

        try
        {
            return DetectUnresolvedPathWrites();
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            recovered = RecoverSentinelDirectory(location, e);
            return null;
        }
        finally
        {
            if (trace)
            {
                MSBuildEventSource.Log.StrictModeDirectoryScanStop(BuildId, location.File);
            }
        }
    }

    private bool RecoverSentinelDirectory(ElementLocation location, Exception failure)
    {
        lock (_reportedEntriesLock)
        {
            lock (s_stateLock)
            {
                if (!ReferenceEquals(s_activeScope, this))
                {
                    return false;
                }

                try
                {
                    bool sentinelExists = Directory.Exists(SentinelDirectory);
                    if (failure is not (DirectoryNotFoundException or FileNotFoundException) && sentinelExists)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(location,
                            "MultiThreadedStrictModeSentinelRecoveryFailed", SentinelDirectory, failure.Message);
                    }

                    string? currentDirectory = null;
                    if (sentinelExists)
                    {
                        try
                        {
                            currentDirectory = Directory.GetCurrentDirectory();
                        }
                        catch (Exception e) when (e is DirectoryNotFoundException or FileNotFoundException)
                        {
                            // A removed Unix CWD can remain unlinked even after the path is recreated.
                        }
                    }

                    // Another observer may have completed recovery while this one waited.
                    if (sentinelExists && FileUtilities.PathComparer.Equals(currentDirectory, SentinelDirectory))
                    {
                        return false;
                    }

                    Directory.CreateDirectory(SentinelDirectory);
                    Directory.SetCurrentDirectory(SentinelDirectory);
                    _reportedEntries.Clear();
                    return true;
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(location,
                        "MultiThreadedStrictModeSentinelRecoveryFailed", SentinelDirectory, e.Message);
                    throw;
                }
            }
        }
    }

    internal string? DetectUnresolvedPathWrites()
    {
        if (!ReferenceEquals(ActiveScope, this))
        {
            return null;
        }

        lock (_reportedEntriesLock)
        {
            if (!ReferenceEquals(ActiveScope, this))
            {
                return null;
            }

            // Finish enumeration before deleting entries from the directory.
            string[] snapshot = Directory.GetFileSystemEntries(SentinelDirectory);
            HashSet<string>? noLongerPresent = _reportedEntries.Count == 0 ? null : new(_reportedEntries, FileUtilities.PathComparer);
            List<string>? entries = null;
            foreach (string entry in snapshot)
            {
                string name = Path.GetFileName(entry);
                noLongerPresent?.Remove(name);
                if (_reportedEntries.Add(name))
                {
                    (entries ??= []).Add(name);
                }

                // Retry locked leftovers without duplicate diagnostics.
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
