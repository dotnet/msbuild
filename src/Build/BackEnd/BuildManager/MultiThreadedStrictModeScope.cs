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
    /// reported but never fails the build. See <see href="https://github.com/dotnet/msbuild/issues/14794"/>.
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
        /// Guards installation, removal, and any write to the process current directory performed by strict mode.
        /// The current directory is process-wide state and must never be written by two scopes at once.
        /// </summary>
        /// <remarks>
        /// This is a leaf lock. Nothing is logged while holding it, because a synchronous logger runs user code
        /// inline and would block worker threads that are verifying process state.
        /// </remarks>
        private static readonly object s_stateLock = new();

        /// <summary>
        /// The scope that currently owns the process current directory, or <see langword="null"/> when strict mode
        /// is not active.
        /// </summary>
        private static MultiThreadedStrictModeScope? s_activeScope;

        /// <summary>
        /// Guards the reported-entry bookkeeping.
        /// </summary>
        private readonly object _reportedEntriesLock = new();

        /// <summary>
        /// Names of sentinel entries that were reported but could not be removed, so that a locked file is not
        /// re-reported by every subsequent task. Seeded with strict mode's own marker file, which must never be
        /// mistaken for a stray write if deleting it did not take effect immediately.
        /// </summary>
        private readonly HashSet<string> _reportedEntries = new(FileUtilities.PathComparer);

        /// <summary>
        /// Directories the process has already been caught in, so that one stray call does not fail every task
        /// running concurrently with it while a second, different offender still gets reported.
        /// </summary>
        private readonly HashSet<string> _reportedCurrentDirectories = new(FileUtilities.PathComparer);

        private readonly string _sentinelDirectory;
        private readonly string _directoryToRestore;

        private MultiThreadedStrictModeScope(string sentinelDirectory, string directoryToRestore, string markerFileName)
        {
            _sentinelDirectory = sentinelDirectory;
            _directoryToRestore = directoryToRestore;
            _reportedEntries.Add(markerFileName);
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
        /// Gets the scope that currently owns the process current directory, or <see langword="null"/> when strict
        /// mode is not active.
        /// </summary>
        internal static MultiThreadedStrictModeScope? ActiveScope => Volatile.Read(ref s_activeScope);

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
            string directoryToRestore;
            try
            {
                directoryToRestore = Directory.GetCurrentDirectory();
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                directoryToRestore = BuildParameters.StartupDirectory;
            }

            string sentinelDirectory = Path.Combine(FileUtilities.TempFileDirectory, SentinelDirectoryName);

            MultiThreadedStrictModeScope? scope = null;
            string? failureReason;

            lock (s_stateLock)
            {
                // Only one scope may own the process current directory at a time.
                if (s_activeScope is not null)
                {
                    failureReason = ResourceUtilities.GetResourceString("MultiThreadedStrictModeAlreadyActive");
                }
                else
                {
                    try
                    {
                        scope = Install(sentinelDirectory, directoryToRestore, out failureReason);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        scope = null;
                        failureReason = e.Message;
                    }

                    if (scope is not null)
                    {
                        // Publish last: until the process is actually sitting in the sentinel directory, a
                        // concurrent verification would see a mismatch that is this method's doing, not a task's.
                        Volatile.Write(ref s_activeScope, scope);
                    }
                }
            }

            if (scope is null)
            {
                // Reported as a message rather than a warning: strict mode is a diagnostic aid, and failing to
                // install it must not fail a build that uses /warnaserror.
                loggingService?.LogComment(
                    BuildEventContext.Invalid,
                    MessageImportance.High,
                    "MultiThreadedStrictModeCouldNotBeEnabled",
                    sentinelDirectory,
                    failureReason);

                return null;
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
                // Synchronous logging runs logger code inline, and a logger that throws here would leave the
                // caller without the scope it needs to restore the process current directory. Uninstall before
                // letting the exception through - a stranded scope would pin the directory for the process
                // lifetime and suppress the legacy per-project reset for every later build.
                scope.Exit();
                throw;
            }

            return scope;
        }

        /// <summary>
        /// Restores the process current directory. Safe to call more than once, and a no-op for a scope that is
        /// no longer the owner.
        /// </summary>
        internal void Exit()
        {
            lock (s_stateLock)
            {
                if (!ReferenceEquals(Volatile.Read(ref s_activeScope), this))
                {
                    return;
                }

                Volatile.Write(ref s_activeScope, null);

                NativeMethodsShared.SetCurrentDirectory(_directoryToRestore);

                // On Windows the process holds a handle to its current directory, so staying in the sentinel would
                // both pin MSBuild's temporary folder and silently move every later build in this process.
                if (IsCurrentDirectory(_sentinelDirectory))
                {
                    NativeMethodsShared.SetCurrentDirectory(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);
                }
            }
        }

        /// <summary>
        /// Verifies that the process-wide state strict mode protects is still intact and logs a diagnostic for
        /// every violation found.
        /// </summary>
        /// <param name="taskLoggingContext">Logging context of the task that just finished running.</param>
        /// <param name="taskName">Name of the task that just finished running.</param>
        /// <param name="taskLocation">Location of the task element, so the diagnostic is clickable.</param>
        /// <param name="convertErrorsToWarnings">
        /// Whether the task declared <c>ContinueOnError="true"</c>, in which case its errors are reported as
        /// warnings. Honoring it keeps the log self-consistent: MSBuild must never print "Build succeeded"
        /// alongside an error count.
        /// </param>
        /// <returns><see langword="true"/> when a violation was reported, in which case the task must be failed.</returns>
        /// <remarks>
        /// Tasks run concurrently in multi-threaded mode, so the task that observes the violation is not
        /// necessarily the task that caused it. The diagnostics say so explicitly.
        /// </remarks>
        internal bool VerifyAndReportProcessState(TaskLoggingContext taskLoggingContext, string taskName, ElementLocation taskLocation, bool convertErrorsToWarnings)
        {
            Violations violations = DetectViolations();

            if (violations.UnexpectedCurrentDirectory is not null)
            {
                Report(
                    "MultiThreadedStrictModeCurrentDirectoryChanged",
                    taskName,
                    violations.UnexpectedCurrentDirectory,
                    _sentinelDirectory);
            }

            if (violations.UnresolvedPathWrites is not null)
            {
                Report(
                    "MultiThreadedStrictModeUnresolvedPathWrite",
                    taskName,
                    violations.UnresolvedPathWrites,
                    _sentinelDirectory);
            }

            return violations.Any;

            void Report(string messageResourceName, params object[] messageArgs)
            {
                if (convertErrorsToWarnings)
                {
                    taskLoggingContext.LogWarning(null, new BuildEventFileInfo(taskLocation), messageResourceName, messageArgs);
                    taskLoggingContext.LogComment(MessageImportance.Normal, "ErrorConvertedIntoWarning");
                }
                else
                {
                    taskLoggingContext.LogError(new BuildEventFileInfo(taskLocation), messageResourceName, messageArgs);
                }
            }
        }

        /// <summary>
        /// Detects violations without logging, and repairs the process current directory if it was moved.
        /// </summary>
        internal Violations DetectViolations()
        {
            string? unexpectedCurrentDirectory = null;

            string currentDirectory;
            try
            {
                currentDirectory = Directory.GetCurrentDirectory();
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Nothing can be concluded about a current directory that cannot be read, so only the sentinel
                // contents are checked.
                return new Violations(null, TakeUnreportedSentinelDirectoryEntries());
            }

            if (!FileUtilities.PathsEqual(currentDirectory, _sentinelDirectory))
            {
                // Put the process back where it belongs so the rest of the build keeps the protection it asked
                // for, but only while this scope is still the owner - otherwise this would fight with Exit(), and
                // a directory that Exit() legitimately restored must not be reported against an innocent task.
                lock (s_stateLock)
                {
                    if (ReferenceEquals(Volatile.Read(ref s_activeScope), this))
                    {
                        NativeMethodsShared.SetCurrentDirectory(_sentinelDirectory);

                        lock (_reportedEntriesLock)
                        {
                            if (_reportedCurrentDirectories.Add(currentDirectory))
                            {
                                unexpectedCurrentDirectory = currentDirectory;
                            }
                        }
                    }
                }
            }

            return new Violations(unexpectedCurrentDirectory, TakeUnreportedSentinelDirectoryEntries());
        }

        /// <summary>
        /// Returns a display list of the sentinel directory entries that have not been reported yet, or
        /// <see langword="null"/> when there are none. Reported entries are removed: the sentinel has to stay
        /// empty, or a stray file written by one task would satisfy another task's unresolved read and hide the
        /// second defect behind the first. The entry name in the diagnostic is what identifies the offender.
        /// </summary>
        private string? TakeUnreportedSentinelDirectoryEntries()
        {
            // Fast path: one lock-free enumeration that stops at the first entry. Running clean is the
            // overwhelmingly common case, so the lock is only taken once something is actually wrong. The
            // directory's write time is deliberately not used as a cheaper gate: file systems update it lazily,
            // so a stray write can land inside the previously observed tick and never be reported at all.
            if (!HasAnyEntry(_sentinelDirectory))
            {
                return null;
            }

            List<string> entries = new();
            bool truncated = false;

            lock (_reportedEntriesLock)
            {
                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(_sentinelDirectory))
                    {
                        if (entries.Count == MaxReportedEntries)
                        {
                            // Leave the remainder unreported so the next check picks it up rather than dropping it.
                            truncated = true;
                            break;
                        }

                        string name = Path.GetFileName(entry);

                        // _reportedEntries only guards against an entry that could not be removed, so that a
                        // locked file is not re-reported by every subsequent task for the rest of the build.
                        if (_reportedEntries.Add(name))
                        {
                            entries.Add(name);
                        }

                        if (TryDelete(entry))
                        {
                            _reportedEntries.Remove(name);
                        }
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

        /// <summary>
        /// Creates and enters the sentinel directory. Must be called while holding <see cref="s_stateLock"/>.
        /// </summary>
        private static MultiThreadedStrictModeScope? Install(string sentinelDirectory, string directoryToRestore, out string? failureReason)
        {
            failureReason = null;

            Directory.CreateDirectory(sentinelDirectory);

            // Anything left over from a previous build would be misattributed to this one.
            TryClearDirectory(sentinelDirectory);

            // The sentinel path cannot simply be compared against the current directory afterwards, because
            // Directory.SetCurrentDirectory resolves symbolic links on Unix and the macOS temporary folder is one.
            // A relative probe for a marker file resolves against the process current directory, which makes it an
            // exact identity check regardless of how the path is spelled.
            string markerFileName = $"strict-mode-marker-{Guid.NewGuid():N}.tmp";
            string markerFilePath = Path.Combine(sentinelDirectory, markerFileName);
            File.WriteAllText(markerFilePath, string.Empty);

            try
            {
                NativeMethodsShared.SetCurrentDirectory(sentinelDirectory);

                if (!File.Exists(markerFileName))
                {
                    string actualDirectory = TryGetCurrentDirectory() ?? sentinelDirectory;

                    // SetCurrentDirectory is best effort on every platform, so leave the process where it was
                    // rather than running a build that believes it is protected when it is not.
                    NativeMethodsShared.SetCurrentDirectory(directoryToRestore);

                    failureReason = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                        "MultiThreadedStrictModeCurrentDirectoryNotChanged",
                        actualDirectory);

                    return null;
                }
            }
            finally
            {
                TryDeleteFile(markerFilePath);
            }

            // Adopt whatever the process actually landed on as the canonical sentinel path.
            return new MultiThreadedStrictModeScope(TryGetCurrentDirectory() ?? sentinelDirectory, directoryToRestore, markerFileName);
        }

        private static bool IsCurrentDirectory(string directory)
        {
            string? currentDirectory = TryGetCurrentDirectory();

            return currentDirectory is not null && FileUtilities.PathsEqual(currentDirectory, directory);
        }

        private static string? TryGetCurrentDirectory()
        {
            try
            {
                return Directory.GetCurrentDirectory();
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                return null;
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

        private static void TryDeleteFile(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Best effort.
            }
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

        /// <summary>
        /// The violations observed by a single verification.
        /// </summary>
        internal readonly struct Violations
        {
            internal Violations(string? unexpectedCurrentDirectory, string? unresolvedPathWrites)
            {
                UnexpectedCurrentDirectory = unexpectedCurrentDirectory;
                UnresolvedPathWrites = unresolvedPathWrites;
            }

            /// <summary>
            /// Gets the directory the process had been moved to, or <see langword="null"/> when the process is
            /// still in the sentinel directory or the move has already been reported.
            /// </summary>
            internal string? UnexpectedCurrentDirectory { get; }

            /// <summary>
            /// Gets a display list of sentinel directory entries not reported before, or <see langword="null"/>.
            /// </summary>
            internal string? UnresolvedPathWrites { get; }

            internal bool Any => UnexpectedCurrentDirectory is not null || UnresolvedPathWrites is not null;
        }
    }
}
