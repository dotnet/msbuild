// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Framework;

#if NET
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
#endif

namespace Microsoft.Build.Shared
{
    internal static partial class ProcessExtensions
    {
        public static void KillTree(this Process process, int timeoutMilliseconds)
        {
#if NET
            process.Kill(entireProcessTree: true);
#else
            if (NativeMethods.IsWindows)
            {
                try
                {
                    NativeMethods.KillTree(process.Id);
                }
                catch (InvalidOperationException)
                {
                    // The process already exited, which is fine,
                    // just continue.
                }
            }
            else
            {
                throw new NotSupportedException();
            }
#endif
            // Wait until the process finishes exiting/getting killed.
            // We don't want to wait forever here because the task is already supposed to be dying, we just want to give it long enough
            // to try and flush what it can and stop. If it cannot do that in a reasonable time frame then we will just ignore it.
            process.WaitForExit(timeoutMilliseconds);
        }

        /// <summary>
        /// Retrieves the full command line for a process in a cross-platform manner.
        /// </summary>
        /// <param name="process">The process to get the command line for.</param>
        /// <param name="commandLine">The command line string, or null if it cannot be retrieved.</param>
        /// <returns>True if the command line was successfully retrieved, false if there was an error or the platform doesn't support command line retrieval.</returns>
        public static bool TryGetCommandLine(this Process? process, out string? commandLine)
        {
            commandLine = null;

            if (process?.HasExited != false)
            {
                return false;
            }

            try
            {
#if NET
                if (NativeMethods.IsOSX || NativeMethods.IsBSD)
                {
                    commandLine = BSD.GetCommandLine(process.Id);
                    return true;
                }
                else if (NativeMethods.IsLinux)
                {
                    commandLine = Linux.GetCommandLine(process.Id);
                    return true;
                }
                else
                {
                    // Unsupported OS - return false to fall back to prior behavior
                    commandLine = null;
                    return true;
                }
#else
                // While we technically can do the same COM interop on .NET Framework that we do on modern .NET, VS perf tests yell at us for more assembly loads.
                // Out of deference to those tests, we artificially limit the functionality to just modern .NET.
                commandLine = null;
                return true;
#endif
            }
            catch
            {
                return false;
            }
        }

#if NET
        /// <summary>
        /// Parses a null-separated byte buffer into a space-joined argument string using span-based slicing.
        /// Used by both Linux (/proc/pid/cmdline) and macOS/BSD (sysctl KERN_PROCARGS2) parsing.
        /// Uses ArrayPool to rent char buffers for efficient UTF-8 decoding without intermediate string allocations.
        /// </summary>
        private static string ParseNullSeparatedArguments(ReadOnlySpan<byte> data, int maxArgs = int.MaxValue)
        {
            if (data.IsEmpty)
            {
                return string.Empty;
            }

            // Rent a char buffer for UTF-8 decoding (max char count equals byte count for ASCII-like content)
            char[] charBuffer = ArrayPool<char>.Shared.Rent(data.Length);
            try
            {
                int totalChars = 0;
                int argsFound = 0;

                while (!data.IsEmpty && argsFound < maxArgs)
                {
                    int nullIndex = data.IndexOf((byte)0);
                    ReadOnlySpan<byte> segment = nullIndex >= 0 ? data.Slice(0, nullIndex) : data;

                    if (!segment.IsEmpty)
                    {
                        // Add space separator between arguments
                        if (totalChars > 0)
                        {
                            charBuffer[totalChars++] = ' ';
                        }

                        // Decode UTF-8 directly into the char buffer
                        int charsWritten = Encoding.UTF8.GetChars(segment, charBuffer.AsSpan(totalChars));

                        // UTF-8 decoder converts null bytes to null chars - replace them with spaces for safety
                        Span<char> decodedChars = charBuffer.AsSpan(totalChars, charsWritten);
                        for (int i = 0; i < decodedChars.Length; i++)
                        {
                            if (decodedChars[i] == '\0')
                            {
                                decodedChars[i] = ' ';
                            }
                        }

                        totalChars += charsWritten;
                        argsFound++;
                    }

                    if (nullIndex < 0)
                    {
                        break;
                    }

                    data = data.Slice(nullIndex + 1);
                }

                return new string(charBuffer, 0, totalChars);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(charBuffer);
            }
        }
#endif

#if NET
        /// <summary>
        /// Linux-specific command line retrieval via /proc/{pid}/cmdline.
        /// </summary>
        [SupportedOSPlatform("linux")]
        private static class Linux
        {
            /// <summary>
            /// Reads /proc/{pid}/cmdline where arguments are null-byte separated,
            /// and joins them with spaces.
            /// </summary>
            internal static string? GetCommandLine(int processId)
            {
                try
                {
                    string cmdlinePath = $"/proc/{processId}/cmdline";
                    byte[] cmdlineBytes = File.ReadAllBytes(cmdlinePath);
                    if (cmdlineBytes.Length == 0)
                    {
                        return null;
                    }

                    return ParseNullSeparatedArguments(cmdlineBytes);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// macOS/BSD-specific P/Invoke bindings and command line retrieval via sysctl KERN_PROCARGS2.
        /// </summary>
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("freebsd")]
        private static partial class BSD
        {
            [LibraryImport("libc", SetLastError = true)]
            private static partial int sysctl(
                ReadOnlySpan<int> name,
                uint namelen,
                Span<byte> oldp,
                ref nuint oldlenp,
                ReadOnlySpan<byte> newp,
                nuint newlen);

            /// <summary>
            /// Wrapper over the raw sysctl P/Invoke that is optimized for reading values, not writing.
            /// </summary>
            private static int Sysctl(ReadOnlySpan<int> name, Span<byte> oldp, ref nuint oldlenp)
                => sysctl(name, (uint)name.Length, oldp, ref oldlenp, ReadOnlySpan<byte>.Empty, 0);

            private const int CTL_KERN = 1;
            private const int KERN_PROCARGS2 = 49;

            /// <summary>
            /// Uses sysctl with KERN_PROCARGS2 to read the process arguments,
            /// then parses the null-separated buffer using span-based slicing with ArrayPool for efficient memory management.
            /// Related: https://github.com/dotnet/runtime/issues/101837
            /// </summary>
            internal static string? GetCommandLine(int processId)
            {
                ReadOnlySpan<int> mib = [CTL_KERN, KERN_PROCARGS2, processId];
                nuint size = 0;

                // Get the required buffer size
                if (Sysctl(mib, Span<byte>.Empty, ref size) != 0 || size == 0)
                {
                    return null;
                }

                // Rent a buffer from ArrayPool and pin it for sysctl
                byte[] buffer = ArrayPool<byte>.Shared.Rent((int)size);
                try
                {
                    if (Sysctl(mib, buffer.AsSpan(0, (int)size), ref size) != 0)
                    {
                        return null;
                    }

                    // Buffer format (KERN_PROCARGS2):
                    //   int argc (number of arguments including executable)
                    //   fully-qualified executable path (null-terminated)
                    //   padding null bytes
                    //   argv[0] .. argv[argc-1] (each null-terminated)
                    //   environment variables (not needed)
                    ReadOnlySpan<byte> data = buffer.AsSpan(0, (int)size);

                    if (data.Length < sizeof(int))
                    {
                        return null;
                    }

                    int argc = MemoryMarshal.Read<int>(data);
                    if (argc <= 0)
                    {
                        return null;
                    }

                    data = data.Slice(sizeof(int));

                    // Skip past the executable path (first null terminator)
                    int execPathEnd = data.IndexOf((byte)0);
                    if (execPathEnd < 0)
                    {
                        return null;
                    }

                    data = data.Slice(execPathEnd + 1);

                    // Skip padding null bytes between executable path and argv[0]
                    while (!data.IsEmpty && data[0] == 0)
                    {
                        data = data.Slice(1);
                    }

                    return ParseNullSeparatedArguments(data, argc);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
#endif
    }
}
