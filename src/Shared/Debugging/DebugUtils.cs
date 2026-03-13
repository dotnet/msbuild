// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Shared.Debugging
{
    internal static class DebugUtils
    {
#pragma warning disable CA1810 // Intentional: static constructor catches exceptions to prevent TypeInitializationException
        static DebugUtils()
#pragma warning restore CA1810
        {
            try
            {
                SetDebugPath();
            }
            catch (Exception ex)
            {
                // A failure in SetDebugPath must not prevent MSBuild from starting.
                // DebugPath will remain null — debugging/logging features will be
                // unavailable for this session, but the build can still proceed.
                //
                // Known failure scenarios:
                // - Directory.GetCurrentDirectory() throws DirectoryNotFoundException
                //   if the working directory was deleted before MSBuild started.
                // - FileUtilities.EnsureDirectoryExists() throws UnauthorizedAccessException
                //   or IOException when the target path is on a read-only volume or an
                //   offline network share.
                // - Path.Combine() throws ArgumentException when MSBUILDDEBUGPATH contains
                //   illegal path characters (e.g., '<', '>', '|').
                // - PathTooLongException when the resolved path exceeds MAX_PATH on
                //   .NET Framework without long-path support.
                try
                {
                    Console.Error.WriteLine("MSBuild debug path initialization failed: " + ex);
                }
                catch
                {
                    // Console may not be available.
                }
            }

            // Initialize diagnostic fields inside the static constructor so failures
            // are caught here rather than poisoning the type with an unrecoverable
            // TypeInitializationException. On .NET Framework, EnvironmentUtilities
            // accesses Process.GetCurrentProcess() which can throw Win32Exception
            // in restricted environments or when performance counters are corrupted.
            try
            {
                ProcessInfoString = GetProcessInfoString();
                ShouldDebugCurrentProcess = CurrentProcessMatchesDebugName();
            }
            catch
            {
                ProcessInfoString ??= "Unknown";
                ShouldDebugCurrentProcess = false;
            }
        }

        // DebugUtils are initialized early on by the test runner - during preparing data for DataMemeberAttribute of some test,
        // for that reason it is not easily possible to inject the DebugPath in tests via env var (unless we want to run expensive exec style test).
        internal static void SetDebugPath()
        {
            string environmentDebugPath = FileUtilities.TrimAndStripAnyQuotes(Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH"));
            string debugDirectory = environmentDebugPath;
            if (Traits.Instance.DebugEngine)
            {
                if (!string.IsNullOrWhiteSpace(debugDirectory) && FileUtilities.CanWriteToDirectory(debugDirectory))
                {
                    // Add a dedicated ".MSBuild_Logs" folder inside the user-specified path, either always or when in solution directory.
                    debugDirectory = Path.Combine(debugDirectory, ".MSBuild_Logs");
                }
                else if (FileUtilities.CanWriteToDirectory(Directory.GetCurrentDirectory()))
                {
                    debugDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".MSBuild_Logs");
                }
                else
                {
                    debugDirectory = Path.Combine(FileUtilities.TempFileDirectory, ".MSBuild_Logs");
                }

                // Out of proc nodes do not know the startup directory so set the environment variable for them.
                if (string.IsNullOrWhiteSpace(environmentDebugPath))
                {
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGPATH", debugDirectory);
                }
            }

            if (debugDirectory is not null)
            {
                FileUtilities.EnsureDirectoryExists(debugDirectory);
            }

            DebugPath = debugDirectory;
        }

        private static readonly string s_debugDumpPath = GetDebugDumpPath();

        /// <summary>
        /// Gets the location of the directory used for diagnostic log files.
        /// </summary>
        /// <returns></returns>
        private static string GetDebugDumpPath()
        {
            string debugPath = DebugPath;

            return !string.IsNullOrEmpty(debugPath)
                    ? debugPath
                    : FileUtilities.TempFileDirectory;
        }

        private static string s_debugDumpPathInRunningTests = GetDebugDumpPath();
        internal static bool ResetDebugDumpPathInRunningTests = false;

        /// <summary>
        /// The directory used for diagnostic log files.
        /// </summary>
        internal static string DebugDumpPath
        {
            get
            {
                if (BuildEnvironmentHelper.Instance.RunningTests)
                {
                    if (ResetDebugDumpPathInRunningTests)
                    {
                        s_debugDumpPathInRunningTests = GetDebugDumpPath();
                        // reset dump file name so new one is created in new path
                        s_dumpFileName = null;
                        ResetDebugDumpPathInRunningTests = false;
                    }

                    return s_debugDumpPathInRunningTests;
                }

                return s_debugDumpPath;
            }
        }

        /// <summary>
        /// The file used for diagnostic log files.
        /// </summary>
        internal static string DumpFilePath => s_dumpFileName;

        /// <summary>
        /// The filename that exceptions will be dumped to
        /// </summary>
        private static string s_dumpFileName;

        private static readonly Lazy<NodeMode?> ProcessNodeMode = new(
            () => NodeModeHelper.ExtractFromCommandLine(Environment.CommandLine));

        private static bool CurrentProcessMatchesDebugName()
        {
            var processNameToBreakInto = Environment.GetEnvironmentVariable("MSBuildDebugProcessName");
            var thisProcessMatchesName = string.IsNullOrWhiteSpace(processNameToBreakInto) ||
                                         EnvironmentUtilities.ProcessName.Contains(processNameToBreakInto);

            return thisProcessMatchesName;
        }

        /// <summary>
        /// Builds a diagnostic string identifying this process (node mode, name, PID, bitness).
        /// Must be called from the static constructor rather than as a field initializer because
        /// on .NET Framework, <see cref="EnvironmentUtilities.ProcessName"/> and
        /// <see cref="EnvironmentUtilities.CurrentProcessId"/> access
        /// <c>Process.GetCurrentProcess()</c> which can throw <see cref="System.ComponentModel.Win32Exception"/>
        /// in restricted environments or when performance counters are corrupted.
        /// A field-initializer failure would produce an unrecoverable <see cref="TypeInitializationException"/>
        /// that poisons the entire <see cref="DebugUtils"/> type, whereas the static constructor's
        /// try/catch lets the type initialize successfully with a safe fallback value.
        /// </summary>
        private static string GetProcessInfoString() => $"{(ProcessNodeMode.Value?.ToString() ?? "CentralNode")}_{EnvironmentUtilities.ProcessName}_PID={EnvironmentUtilities.CurrentProcessId}_x{(Environment.Is64BitProcess ? "64" : "86")}";

        public static readonly string ProcessInfoString;

        public static readonly bool ShouldDebugCurrentProcess;

        public static string DebugPath { get; private set; }

        /// <summary>
        /// Returns true if the current process is an out-of-proc TaskHost node.
        /// </summary>
        /// <returns>
        /// True if this process was launched with /nodemode:2 (indicating it's a TaskHost process),
        /// false otherwise. This is useful for conditionally enabling debugging or other behaviors
        /// based on whether the code is running in the main MSBuild process or a child TaskHost process.
        /// </returns>
        public static bool IsInTaskHostNode() => ProcessNodeMode.Value == NodeMode.OutOfProcTaskHostNode;

        public static string FindNextAvailableDebugFilePath(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            var fullPath = Path.Combine(DebugPath, fileName);

            var counter = 0;
            while (FileSystems.Default.FileExists(fullPath))
            {
                fileName = $"{fileNameWithoutExtension}_{counter++}{extension}";
                fullPath = Path.Combine(DebugPath, fileName);
            }

            return fullPath;
        }


        /// <summary>
        /// Dump any unhandled exceptions to a file so they can be diagnosed
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "It is called by the CLR")]
        internal static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            DumpExceptionToFile(ex);
            RecordCrashTelemetryForUnhandledException(ex);
        }

        /// <summary>
        /// Records and immediately flushes crash telemetry for an unhandled exception.
        /// Best effort - must never throw, as the process is already crashing.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void RecordCrashTelemetryForUnhandledException(Exception ex)
        {
            CrashTelemetryRecorder.RecordAndFlushCrashTelemetry(
                ex,
                exitType: CrashExitType.UnhandledException,
                isUnhandled: true,
                isCritical: ExceptionHandling.IsCriticalException(ex));
        }

        /// <summary>
        /// Dump the exception information to a file
        /// </summary>
        internal static void DumpExceptionToFile(Exception ex)
        {
            try
            {
                // Locking on a type is not recommended.  However, we are doing it here to be extra cautious about compatibility because
                //  this method previously had a [MethodImpl(MethodImplOptions.Synchronized)] attribute, which does lock on the type when
                //  applied to a static method.
                lock (typeof(ExceptionHandling))
                {
                    if (s_dumpFileName == null)
                    {
                        Guid guid = Guid.NewGuid();

                        // For some reason we get Watson buckets because GetTempPath gives us a folder here that doesn't exist.
                        // Either because %TMP% is misdefined, or because they deleted the temp folder during the build.
                        // If this throws, no sense catching it, we can't log it now, and we're here
                        // because we're a child node with no console to log to, so die
                        Directory.CreateDirectory(DebugDumpPath);

                        var pid = EnvironmentUtilities.CurrentProcessId;
                        // This naming pattern is assumed in ReadAnyExceptionFromFile
                        s_dumpFileName = Path.Combine(DebugDumpPath, $"MSBuild_pid-{pid}_{guid:n}.failure.txt");

                        using (StreamWriter writer = FileUtilities.OpenWrite(s_dumpFileName, append: true))
                        {
                            writer.WriteLine("UNHANDLED EXCEPTIONS FROM PROCESS {0}:", pid);
                            writer.WriteLine("=====================");
                        }
                    }

                    using (StreamWriter writer = FileUtilities.OpenWrite(s_dumpFileName, append: true))
                    {
                        // "G" format is, e.g., 6/15/2008 9:15:07 PM
                        writer.WriteLine(DateTime.Now.ToString("G", CultureInfo.CurrentCulture));
                        writer.WriteLine(ex.ToString());
                        writer.WriteLine("===================");
                    }
                }
            }

            // Some customers experience exceptions such as 'OutOfMemory' errors when msbuild attempts to log errors to a local file.
            // This catch helps to prevent the application from crashing in this best-effort dump-diagnostics path,
            // but doesn't prevent the overall crash from going to Watson.
            catch
            {
            }
        }

        /// <summary>
        /// Returns the content of any exception dump files modified
        /// since the provided time, otherwise returns an empty string.
        /// </summary>
        internal static string ReadAnyExceptionFromFile(DateTime fromTimeUtc)
        {
            var builder = new StringBuilder();
            IEnumerable<string> files = FileSystems.Default.EnumerateFiles(DebugDumpPath, "MSBuild*failure.txt");

            foreach (string file in files)
            {
                if (FileSystems.Default.GetLastWriteTimeUtc(file) >= fromTimeUtc)
                {
                    builder.Append(Environment.NewLine);
                    builder.Append(file);
                    builder.Append(':');
                    builder.Append(Environment.NewLine);
                    builder.Append(FileSystems.Default.ReadFileAllText(file));
                    builder.Append(Environment.NewLine);
                }
            }

            return builder.ToString();
        }
    }
}
