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
        private static readonly string s_debugDumpPath = GetDebugDumpPath();

        /// <summary>
        /// Gets the location of the directory used for diagnostic log files.
        /// </summary>
        /// <returns></returns>
        private static string GetDebugDumpPath()
        {
            string debugPath = FrameworkDebugUtils.DebugPath;

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

        public static string FindNextAvailableDebugFilePath(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            var fullPath = Path.Combine(FrameworkDebugUtils.DebugPath, fileName);

            var counter = 0;
            while (FileSystems.Default.FileExists(fullPath))
            {
                fileName = $"{fileNameWithoutExtension}_{counter++}{extension}";
                fullPath = Path.Combine(FrameworkDebugUtils.DebugPath, fileName);
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
