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
        /// Number of unexpected sentinel-directory entries listed in a diagnostic before it is truncated.
        /// </summary>
        private const int MaxReportedEntries = 10;

        /// <summary>
        /// Serializes the drain-and-report of the sentinel directory so that concurrently running tasks do not
        /// report the same stray file twice.
        /// </summary>
        private static readonly object s_sentinelDirectoryLock = new();

        /// <summary>
        /// The sentinel directory of the scope that is currently installed, or <see langword="null"/> when strict
        /// mode is not active. Read once per task execution, so it is a static rather than an instance lookup.
        /// </summary>
        private static string? s_activeSentinelDirectory;

        private readonly string _sentinelDirectory;
        private readonly string _directoryToRestore;

        private MultiThreadedStrictModeScope(string sentinelDirectory, string directoryToRestore)
        {
            _sentinelDirectory = sentinelDirectory;
            _directoryToRestore = directoryToRestore;
        }

        /// <summary>
        /// Gets a value indicating whether strict mode is currently installed in this process.
        /// </summary>
        internal static bool IsActive => Volatile.Read(ref s_activeSentinelDirectory) is not null;

        /// <summary>
        /// Gets the sentinel directory of the installed scope, or <see langword="null"/> when strict mode is not active.
        /// </summary>
        internal static string? ActiveSentinelDirectory => Volatile.Read(ref s_activeSentinelDirectory);

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

            string sentinelDirectory = Path.Combine(FileUtilities.TempFileDirectory, SentinelDirectoryName);

            // Only one scope may be installed at a time - the current directory is process-wide state.
            if (Interlocked.CompareExchange(ref s_activeSentinelDirectory, sentinelDirectory, null) is not null)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(sentinelDirectory);

                // Anything left over from a previous build would be misattributed to this one.
                TryClearDirectory(sentinelDirectory);

                NativeMethodsShared.SetCurrentDirectory(sentinelDirectory);

                // SetCurrentDirectory is best-effort on every platform, so confirm it took effect rather than
                // running a build that believes it is protected when it is not.
                if (!FileUtilities.PathsEqual(Directory.GetCurrentDirectory(), sentinelDirectory))
                {
                    throw new DirectoryNotFoundException(sentinelDirectory);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Volatile.Write(ref s_activeSentinelDirectory, null);

                loggingService?.LogWarning(
                    BuildEventContext.Invalid,
                    subcategoryResourceName: null,
                    BuildEventFileInfo.Empty,
                    "MultiThreadedStrictModeCouldNotBeEnabled",
                    sentinelDirectory,
                    e.Message);

                return null;
            }

            loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.Low, "MultiThreadedStrictModeEnabled", sentinelDirectory);

            return new MultiThreadedStrictModeScope(sentinelDirectory, directoryToRestore);
        }

        /// <summary>
        /// Restores the process current directory and removes the sentinel directory contents.
        /// </summary>
        internal void Exit()
        {
            Volatile.Write(ref s_activeSentinelDirectory, null);

            NativeMethodsShared.SetCurrentDirectory(_directoryToRestore);

            TryClearDirectory(_sentinelDirectory);
        }

        /// <summary>
        /// Verifies that the process-wide state strict mode protects is still intact and logs an error for every
        /// violation found. Does nothing when strict mode is not active.
        /// </summary>
        /// <param name="taskLoggingContext">Logging context of the task that just finished running.</param>
        /// <param name="taskName">Name of the task that just finished running.</param>
        /// <param name="taskLocation">Location of the task element, so the diagnostic is clickable.</param>
        /// <returns><see langword="true"/> when a violation was reported, in which case the task must be failed.</returns>
        /// <remarks>
        /// Tasks run concurrently in multi-threaded mode, so the task that observes the violation is not
        /// necessarily the task that caused it. The diagnostics say so explicitly.
        /// </remarks>
        internal static bool VerifyAndReportProcessState(TaskLoggingContext taskLoggingContext, string taskName, ElementLocation taskLocation)
        {
            string? sentinelDirectory = Volatile.Read(ref s_activeSentinelDirectory);
            if (sentinelDirectory is null)
            {
                return false;
            }

            bool violated = false;

            string currentDirectory;
            try
            {
                currentDirectory = Directory.GetCurrentDirectory();
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                currentDirectory = sentinelDirectory;
            }

            if (!FileUtilities.PathsEqual(currentDirectory, sentinelDirectory))
            {
                // Put the process back where it belongs so the rest of the build keeps the protection it asked for
                // and only the first offender is reported.
                NativeMethodsShared.SetCurrentDirectory(sentinelDirectory);

                taskLoggingContext.LogError(
                    new BuildEventFileInfo(taskLocation),
                    "MultiThreadedStrictModeCurrentDirectoryChanged",
                    taskName,
                    currentDirectory,
                    sentinelDirectory);

                violated = true;
            }

            string? strayEntries = TakeSentinelDirectoryEntries(sentinelDirectory);
            if (strayEntries is not null)
            {
                taskLoggingContext.LogError(
                    new BuildEventFileInfo(taskLocation),
                    "MultiThreadedStrictModeUnresolvedPathWrite",
                    taskName,
                    strayEntries,
                    sentinelDirectory);

                violated = true;
            }

            return violated;
        }

        /// <summary>
        /// Returns a display list of the entries found in the sentinel directory and removes them, or
        /// <see langword="null"/> when the directory is empty.
        /// </summary>
        private static string? TakeSentinelDirectoryEntries(string sentinelDirectory)
        {
            // Fast path: one directory enumeration that stops at the first entry. Running clean is the
            // overwhelmingly common case, so the lock is only taken once something is actually wrong.
            if (!HasAnyEntry(sentinelDirectory))
            {
                return null;
            }

            lock (s_sentinelDirectoryLock)
            {
                List<string> entries = new();
                bool truncated = false;

                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(sentinelDirectory))
                    {
                        if (entries.Count == MaxReportedEntries)
                        {
                            truncated = true;
                            break;
                        }

                        entries.Add(Path.GetFileName(entry));
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    // Report whatever was collected before the failure.
                }

                if (entries.Count == 0)
                {
                    // Another task drained the directory between the fast path and the lock.
                    return null;
                }

                TryClearDirectory(sentinelDirectory);

                string entryList = string.Join(", ", entries);
                return truncated ? entryList + ", ..." : entryList;
            }
        }

        private static bool HasAnyEntry(string directory)
        {
            try
            {
                foreach (string unused in Directory.EnumerateFileSystemEntries(directory))
                {
                    return true;
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Treat an unreadable sentinel as clean rather than failing the build it is meant to diagnose.
            }

            return false;
        }

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
