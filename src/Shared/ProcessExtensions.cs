// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.Text;

#nullable disable

namespace Microsoft.Build.Shared
{
    internal static class ProcessExtensions
    {
        public static void KillTree(this Process process, int timeoutMilliseconds)
        {
#if NET
            process.Kill(entireProcessTree: true);
#else
            if (NativeMethodsShared.IsWindows)
            {
                try
                {
                    // issue the kill command
                    NativeMethodsShared.KillTree(process.Id);
                }
                catch (System.InvalidOperationException)
                {
                    // The process already exited, which is fine,
                    // just continue.
                }
            }
            else
            {
                throw new System.NotSupportedException();
            }
#endif
            // wait until the process finishes exiting/getting killed.
            // We don't want to wait forever here because the task is already supposed to be dieing, we just want to give it long enough
            // to try and flush what it can and stop. If it cannot do that in a reasonable time frame then we will just ignore it.
            process.WaitForExit(timeoutMilliseconds);
        }

        /// <summary>
        /// Retrieves the full command line for a process in a cross-platform manner.
        /// </summary>
        /// <param name="process">The process to get the command line for</param>
        /// <returns>The command line string, or null if it cannot be retrieved</returns>
        public static string GetCommandLine(this Process process)
        {
            if (process is null)
            {
                return null;
            }

            try
            {
                // Check if the process has exited
                if (process.HasExited)
                {
                    return null;
                }
            }
            catch
            {
                // Process might have exited between null check and HasExited check
                return null;
            }

            try
            {
                if (NativeMethodsShared.IsWindows)
                {
                    return GetCommandLineWindows(process);
                }
                else
                {
                    return GetCommandLineUnix(process.Id);
                }
            }
            catch
            {
                // If we can't retrieve the command line, return null
                return null;
            }
        }

        /// <summary>
        /// Retrieves the command line on Windows using WMI.
        /// </summary>
        private static string GetCommandLineWindows(Process process)
        {
#if NETFRAMEWORK
            try
            {
                // On .NET Framework, we can use WMI via System.Management
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
                
                using System.Management.ManagementObjectCollection objects = searcher.Get();
                foreach (System.Management.ManagementBaseObject obj in objects)
                {
                    return obj["CommandLine"]?.ToString();
                }
            }
            catch
            {
                // WMI query failed, fall through to return null
            }
            return null;
#else
            // On .NET Core/5+, WMI via System.Management requires a separate package.
            // For now, we'll use an alternative approach or return null.
            // TODO: Consider using native Windows API calls or System.Management package
            return null;
#endif
        }

        /// <summary>
        /// Retrieves the command line on Unix/Linux by reading /proc/{pid}/cmdline.
        /// </summary>
        private static string GetCommandLineUnix(int processId)
        {
            try
            {
                string cmdlinePath = $"/proc/{processId}/cmdline";
                if (!File.Exists(cmdlinePath))
                {
                    return null;
                }

                // Read the cmdline file. Arguments are separated by null characters.
                // The file is typically encoded in the system's default encoding (usually UTF-8 on modern Linux).
                byte[] cmdlineBytes = File.ReadAllBytes(cmdlinePath);
                if (cmdlineBytes.Length == 0)
                {
                    return null;
                }

                // Convert bytes to string, replacing null terminators with spaces.
                // We need to handle null bytes specially since they're argument separators in /proc/pid/cmdline.
                StringBuilder sb = new(cmdlineBytes.Length);
                
                int start = 0;
                for (int i = 0; i < cmdlineBytes.Length; i++)
                {
                    if (cmdlineBytes[i] == 0)
                    {
                        if (i > start)
                        {
                            // Decode the argument using UTF-8 encoding
                            string arg = System.Text.Encoding.UTF8.GetString(cmdlineBytes, start, i - start);
                            if (sb.Length > 0)
                            {
                                sb.Append(' ');
                            }
                            sb.Append(arg);
                        }
                        start = i + 1;
                    }
                }
                
                // Handle any remaining bytes after the last null terminator
                if (start < cmdlineBytes.Length)
                {
                    string arg = System.Text.Encoding.UTF8.GetString(cmdlineBytes, start, cmdlineBytes.Length - start);
                    if (arg.Length > 0)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(' ');
                        }
                        sb.Append(arg);
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
