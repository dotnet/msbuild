// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        internal static bool ResetDebugDumpPathInRunningTests
        {
            get => false;
            set
            {
                if (value)
                {
                    FrameworkDebugUtils.ResetDebugDumpPath();
                }
            }
        }

        /// <summary>
        /// The directory used for diagnostic log files.
        /// </summary>
        internal static string DebugDumpPath => FrameworkDebugUtils.DebugDumpPath;

        /// <summary>
        /// The file used for diagnostic log files.
        /// </summary>
        internal static string DumpFilePath => FrameworkDebugUtils.DumpFilePath;

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
            FrameworkDebugUtils.DumpExceptionToFile(ex);
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
