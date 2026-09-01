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

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Implements the opt-in multi-threaded strict diagnostic mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In multi-threaded mode every project shares one process, so the process current directory is no longer a
    /// per-project value; each build request instead carries its own <see cref="TaskEnvironment"/>. Code that still
    /// resolves a relative path against the process current directory therefore resolves it against whichever
    /// directory the process happens to sit in - normally the directory MSBuild was launched from, which is
    /// frequently "close enough" for the entry project and silently wrong for every other project. The resulting
    /// defects are load dependent: they pass locally, pass in CI most of the time, and occasionally fail on one
    /// machine under load.
    /// </para>
    /// <para>
    /// Strict mode moves the process current directory to an empty sentinel directory for the duration of the
    /// build. Reads through an unresolved relative path then fail immediately and identically on every machine,
    /// and writes through an unresolved relative path land in the sentinel directory, where they are detected and
    /// reported against the task that was running instead of silently polluting the launch directory.
    /// </para>
    /// <para>
    /// This is a diagnostic aid, not a correctness feature: it is off by default, and failing to install it is
    /// never fatal. See <see href="https://github.com/dotnet/msbuild/issues/14794"/>.
    /// </para>
    /// </remarks>
    internal sealed class MultiThreadedStrictModeScope
    {
        /// <summary>
        /// Name of the sentinel directory. Deliberately verbose: it is frequently the only breadcrumb a user gets
        /// when a task reports a path that it resolved against the current directory.
        /// </summary>
        internal const string SentinelDirectoryName = "MSBuild-MT-Strict-Sentinel-CWD";

        /// <summary>
        /// Number of unexpected sentinel-directory entries listed in a single diagnostic before it is truncated.
        /// </summary>
        private const int MaxReportedEntries = 10;

        /// <summary>
        /// Guards installation, removal and any write to the process current directory performed by strict mode.
        /// The current directory is process-wide state and must never be written by two scopes at once.
        /// </summary>
        private static readonly object s_stateLock = new();

        /// <summary>
        /// The scope that is currently installed, or <see langword="null"/> when strict mode is not active.
        /// </summary>
        private static MultiThreadedStrictModeScope? s_activeScope;

        /// <summary>
        /// Gets the currently installed scope, or <see langword="null"/> when strict mode is not active.
        /// </summary>
        internal static MultiThreadedStrictModeScope? ActiveScope => Volatile.Read(ref s_activeScope);

        /// <summary>
        /// Entries already reported out of the sentinel directory. Stray files are deliberately left on disk for
        /// inspection, so they must not be reported again by every subsequent task.
        /// </summary>
        private readonly HashSet<string> _reportedEntries = new(StringComparer.Ordinal);

        private readonly string _sentinelDirectory;
        private readonly string _directoryToRestore;

        private MultiThreadedStrictModeScope(string sentinelDirectory, string directoryToRestore)
        {
            _sentinelDirectory = sentinelDirectory;
            _directoryToRestore = directoryToRestore;
        }

        /// <summary>
        /// Gets the sentinel directory that the process current directory is pinned to.
        /// </summary>
        /// <remarks>
        /// This is the directory the process actually landed on, which is not necessarily the path that was passed
        /// to <c>SetCurrentDirectory</c>: on Unix that call resolves symbolic links, and the temporary folder is
        /// symlinked on macOS.
        /// </remarks>
        internal string SentinelDirectory => _sentinelDirectory;

        /// <summary>
        /// Moves the process current directory to an empty sentinel directory.
        /// </summary>
        /// <param name="loggingService">Logging service used to report what strict mode did. May be <see langword="null"/>.</param>
        /// <returns>
        /// The installed scope, or <see langword="null"/> when strict mode could not be installed because another
        /// scope is already active or the sentinel directory could not be created or entered.
        /// </returns>
        internal static MultiThreadedStrictModeScope? TryEnter(ILoggingService? loggingService)
        {
            // Read the startup directory before the process is moved off it so that $(MSBuildStartupDirectory)
            // keeps reporting the directory the build was launched from.
            string directoryToRestore = BuildParameters.StartupDirectory;

            try
            {
                directoryToRestore = Directory.GetCurrentDirectory();
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Fall back to the startup directory as the restore target.
            }

            string requestedSentinelDirectory = Path.Combine(FileUtilities.TempFileDirectory, SentinelDirectoryName);

            lock (s_stateLock)
            {
                // Only one scope may be installed at a time - the current directory is process-wide state.
                if (s_activeScope is not null)
                {
                    LogCouldNotEnable(
                        loggingService,
                        requestedSentinelDirectory,
                        ResourceUtilities.GetResourceString("MultiThreadedStrictModeAlreadyActive"));

                    return null;
                }

                string effectiveSentinelDirectory;

                try
                {
                    Directory.CreateDirectory(requestedSentinelDirectory);

                    // Anything left over from a previous build would be misattributed to this one.
                    TryClearDirectory(requestedSentinelDirectory);

                    NativeMethodsShared.SetCurrentDirectory(requestedSentinelDirectory);

                    // SetCurrentDirectory is best-effort on every platform, so confirm it took effect rather than
                    // running a build that believes it is protected when it is not. The path cannot be compared
                    // directly because on Unix the call resolves symbolic links (the macOS temporary folder is one),
                    // so compare the leaf name and adopt whatever the process actually landed on as canonical.
                    effectiveSentinelDirectory = Directory.GetCurrentDirectory();

                    if (!string.Equals(GetLeafName(effectiveSentinelDirectory), SentinelDirectoryName, StringComparison.Ordinal))
                    {
                        LogCouldNotEnable(
                            loggingService,
                            requestedSentinelDirectory,
                            ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
                                "MultiThreadedStrictModeCurrentDirectoryNotChanged",
                                effectiveSentinelDirectory));

                        return null;
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    LogCouldNotEnable(loggingService, requestedSentinelDirectory, e.Message);

                    return null;
                }

                loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.Low, "MultiThreadedStrictModeEnabled", effectiveSentinelDirectory);

                // Publish last: until the process is actually sitting in the sentinel directory, a concurrent task
                // verification would see a mismatch that is this method's doing rather than a task's.
                MultiThreadedStrictModeScope scope = new(effectiveSentinelDirectory, directoryToRestore);
                Volatile.Write(ref s_activeScope, scope);

                return scope;
            }
        }

        /// <summary>
        /// Restores the process current directory. Safe to call more than once.
        /// </summary>
        internal void Exit()
        {
            lock (s_stateLock)
            {
                if (!ReferenceEquals(s_activeScope, this))
                {
                    return;
                }

                Volatile.Write(ref s_activeScope, null);

                NativeMethodsShared.SetCurrentDirectory(_directoryToRestore);
            }
        }

        /// <summary>
        /// Verifies that the process-wide state strict mode protects is still intact and logs an error for every
        /// violation found.
        /// </summary>
        /// <param name="taskLoggingContext">Logging context of the task that just finished running.</param>
        /// <param name="taskName">Name of the task that just finished running.</param>
        /// <param name="taskLocation">Location of the task element, so the diagnostic is clickable.</param>
        /// <returns><see langword="true"/> when a violation was reported, in which case the task must be failed.</returns>
        /// <remarks>
        /// Tasks run concurrently in multi-threaded mode, so the task that observes the violation is not
        /// necessarily the task that caused it. The diagnostics say so explicitly.
        /// </remarks>
        internal bool VerifyAndReportProcessState(TaskLoggingContext taskLoggingContext, string taskName, ElementLocation taskLocation)
        {
            bool violated = false;

            string currentDirectory;
            try
            {
                currentDirectory = Directory.GetCurrentDirectory();
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                currentDirectory = _sentinelDirectory;
            }

            if (!FileUtilities.PathsEqual(currentDirectory, _sentinelDirectory))
            {
                // Put the process back where it belongs so the rest of the build keeps the protection it asked for,
                // but only while this scope is still the installed one - otherwise this would fight with Exit().
                lock (s_stateLock)
                {
                    if (ReferenceEquals(s_activeScope, this))
                    {
                        NativeMethodsShared.SetCurrentDirectory(_sentinelDirectory);
                    }
                }

                taskLoggingContext.LogError(
                    new BuildEventFileInfo(taskLocation),
                    "MultiThreadedStrictModeCurrentDirectoryChanged",
                    taskName,
                    currentDirectory,
                    _sentinelDirectory);

                violated = true;
            }

            string? strayEntries = TakeUnreportedSentinelDirectoryEntries();
            if (strayEntries is not null)
            {
                taskLoggingContext.LogError(
                    new BuildEventFileInfo(taskLocation),
                    "MultiThreadedStrictModeUnresolvedPathWrite",
                    taskName,
                    strayEntries,
                    _sentinelDirectory);

                violated = true;
            }

            return violated;
        }

        /// <summary>
        /// Returns a display list of the sentinel directory entries that have not been reported yet, or
        /// <see langword="null"/> when there are none. Entries are left on disk so that the file a task wrote can
        /// still be inspected after the build fails.
        /// </summary>
        private string? TakeUnreportedSentinelDirectoryEntries()
        {
            List<string> entries = new();
            bool truncated = false;

            lock (_reportedEntries)
            {
                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(_sentinelDirectory))
                    {
                        string name = Path.GetFileName(entry);

                        if (!_reportedEntries.Add(name))
                        {
                            continue;
                        }

                        if (entries.Count == MaxReportedEntries)
                        {
                            truncated = true;
                            break;
                        }

                        entries.Add(name);
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    // Report whatever was collected before the failure rather than failing the build that strict
                    // mode is meant to diagnose.
                }
            }

            if (entries.Count == 0)
            {
                return null;
            }

            string entryList = string.Join(", ", entries);
            return truncated ? entryList + ", ..." : entryList;
        }

        private static void LogCouldNotEnable(ILoggingService? loggingService, string sentinelDirectory, string reason)
        {
            loggingService?.LogWarning(
                BuildEventContext.Invalid,
                subcategoryResourceName: null,
                BuildEventFileInfo.Empty,
                "MultiThreadedStrictModeCouldNotBeEnabled",
                sentinelDirectory,
                reason);
        }

        private static string GetLeafName(string directory)
            => Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        private static void TryClearDirectory(string directory)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    File.Delete(file);
                }

                foreach (string subdirectory in Directory.EnumerateDirectories(directory))
                {
                    Directory.Delete(subdirectory, recursive: true);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Best effort.
            }
        }
    }
}
