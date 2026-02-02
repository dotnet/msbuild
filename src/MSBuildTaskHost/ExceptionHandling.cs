// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Utility methods for classifying and handling exceptions.
    /// </summary>
    internal static class ExceptionHandling
    {
        /// <summary>
        /// The directory used for diagnostic log files.
        /// </summary>
        public static string DebugDumpPath { get; } = GetDebugDumpPath();

        /// <summary>
        /// Gets the location of the directory used for diagnostic log files.
        /// </summary>
        /// <returns></returns>
        private static string GetDebugDumpPath()
        {
            string debugPath = Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH");

            return !string.IsNullOrEmpty(debugPath)
                ? debugPath
                : FileUtilities.TempFileDirectory;
        }

        /// <summary>
        /// The filename that exceptions will be dumped to
        /// </summary>
        private static string s_dumpFileName;

        /// <summary>
        /// If the given exception is "ignorable under some circumstances" return false.
        /// Otherwise it's "really bad", and return true.
        /// This makes it possible to catch(Exception ex) without catching disasters.
        /// </summary>
        /// <param name="e"> The exception to check. </param>
        /// <returns> True if exception is critical. </returns>
        internal static bool IsCriticalException(Exception e)
        {
            if (e is OutOfMemoryException
             || e is StackOverflowException
             || e is ThreadAbortException
             || e is ThreadInterruptedException
             || e is AccessViolationException
             || e is InternalErrorException)
            {
                // Ideally we would include NullReferenceException, because it should only ever be thrown by CLR (use ArgumentNullException for arguments)
                // but we should handle it if tasks and loggers throw it.

                // ExecutionEngineException has been deprecated by the CLR
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determine whether the exception is file-IO related.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        /// <returns>True if exception is IO related.</returns>
        internal static bool IsIoRelatedException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            return e is UnauthorizedAccessException
                   || e is NotSupportedException
                   || (e is ArgumentException && !(e is ArgumentNullException))
                   || e is SecurityException
                   || e is IOException;
        }

        /// <summary>
        /// Dump any unhandled exceptions to a file so they can be diagnosed
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "It is called by the CLR")]
        internal static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            DumpExceptionToFile(ex);
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
    }
}
