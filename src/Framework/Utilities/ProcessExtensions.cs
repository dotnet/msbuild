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
                if (NativeMethods.IsWindows)
                {
                    commandLine = Windows.GetCommandLine(process.Id);
                    return true;
                }
                else if (NativeMethods.IsOSX || NativeMethods.IsBSD)
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
        /// Windows-specific command line retrieval via the Windows Debug Engine COM interface (dbgeng.dll).
        /// Uses <c>IDebugClient::GetRunningProcessDescription</c> to obtain the target process's command line
        /// without querying WMI and without directly calling <c>NtQueryInformationProcess</c>. This avoids the
        /// performance and reliability issues of the WMI service while still using only documented public APIs.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static class Windows
        {
            // Flags for IDebugClient::GetRunningProcessDescription.
            // By default the Description output contains a concatenation of service names, MTS package names,
            // command line, session id, and user name. We exclude everything except the command line.
            private const int DEBUG_PROC_DESC_NO_PATHS = 0x00000001;
            private const int DEBUG_PROC_DESC_NO_SERVICES = 0x00000002;
            private const int DEBUG_PROC_DESC_NO_MTS_PACKAGES = 0x00000004;
            private const int DEBUG_PROC_DESC_NO_SESSION_ID = 0x00000010;
            private const int DEBUG_PROC_DESC_NO_USER_NAME = 0x00000020;

            private const int DescriptionFlags =
                DEBUG_PROC_DESC_NO_PATHS
                | DEBUG_PROC_DESC_NO_SERVICES
                | DEBUG_PROC_DESC_NO_MTS_PACKAGES
                | DEBUG_PROC_DESC_NO_SESSION_ID
                | DEBUG_PROC_DESC_NO_USER_NAME;

            // IID_IDebugClient (dbgeng.h).
            private static readonly Guid IID_IDebugClient = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");

            /// <summary>
            /// Creates an <c>IDebugClient</c> instance. Exported from dbgeng.dll; acts as the COM-style factory
            /// for the Debug Engine interfaces (not a standard CoCreateInstance target).
            /// </summary>
            [DllImport("dbgeng.dll", ExactSpelling = true)]
            private static extern int DebugCreate(
                ref Guid interfaceId,
                [MarshalAs(UnmanagedType.Interface)] out IDebugClient iface);

            /// <summary>
            /// Minimal <c>IDebugClient</c> declaration. The vtable order of the eight preceding methods must be
            /// preserved even though we never call them; only <see cref="GetRunningProcessDescription"/> is used.
            /// Signatures use <see cref="IntPtr"/> for parameters we don't care about to avoid marshalling costs.
            /// </summary>
            [ComImport]
            [Guid("27fe5639-8407-4f47-8364-ee118fb08ac8")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IDebugClient
            {
                [PreserveSig]
                int AttachKernel(uint flags, IntPtr connectOptions);

                [PreserveSig]
                int GetKernelConnectionOptions(IntPtr buffer, uint bufferSize, out uint optionsSize);

                [PreserveSig]
                int SetKernelConnectionOptions(IntPtr options);

                [PreserveSig]
                int StartProcessServer(uint flags, IntPtr options, IntPtr reserved);

                [PreserveSig]
                int ConnectProcessServer(IntPtr remoteOptions, out ulong server);

                [PreserveSig]
                int DisconnectProcessServer(ulong server);

                [PreserveSig]
                int GetRunningProcessSystemIds(ulong server, IntPtr ids, uint count, out uint actualCount);

                [PreserveSig]
                int GetRunningProcessSystemIdByExecutableName(ulong server, IntPtr exeName, uint flags, out uint id);

                [PreserveSig]
                int GetRunningProcessDescription(
                    ulong server,
                    uint systemId,
                    uint flags,
                    // Explicit LPArray marshalling is required here. Without it the default COM marshalling for a
                    // managed array parameter on a COM interface is UnmanagedType.SafeArray, which causes the CLR's
                    // MngdSafeArrayMarshaler to attempt to interpret the raw ANSI buffer returned by dbgeng as a
                    // SAFEARRAY on the way back out, corrupting marshaller state and surfacing as an
                    // ExecutionEngineException. SizeParamIndex points at the corresponding length parameter.
                    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[]? exeName,
                    uint exeNameSize,
                    out uint actualExeNameSize,
                    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 7)] byte[]? description,
                    uint descriptionSize,
                    out uint actualDescriptionSize);
            }

            /// <summary>
            /// Retrieves the command line for a process via <c>dbgeng!IDebugClient::GetRunningProcessDescription</c>.
            /// Returns <c>null</c> if the target cannot be inspected (e.g. access denied, protected process, or the
            /// debug engine is unavailable).
            /// </summary>
            internal static string? GetCommandLine(int processId)
            {
                Guid iid = IID_IDebugClient;
                int hr = DebugCreate(ref iid, out IDebugClient? client);
                if (hr < 0 || client is null)
                {
                    return null;
                }

                try
                {
                    // First call with null buffers to discover required sizes.
                    hr = client.GetRunningProcessDescription(
                        server: 0,
                        systemId: (uint)processId,
                        flags: DescriptionFlags,
                        exeName: null, exeNameSize: 0, actualExeNameSize: out uint exeSize,
                        description: null, descriptionSize: 0, actualDescriptionSize: out uint descSize);

                    // hr can be S_OK or an inline status indicating buffer too small; either way sizes come back populated
                    // on supported OS versions. A hard failure means the PID can't be inspected.
                    if (hr < 0 && exeSize == 0 && descSize == 0)
                    {
                        return null;
                    }

                    byte[]? exeBuffer = null;
                    byte[]? descBuffer = null;
                    try
                    {
                        exeBuffer = exeSize > 0 ? ArrayPool<byte>.Shared.Rent((int)exeSize) : null;
                        descBuffer = descSize > 0 ? ArrayPool<byte>.Shared.Rent((int)descSize) : null;

                        hr = client.GetRunningProcessDescription(
                            server: 0,
                            systemId: (uint)processId,
                            flags: DescriptionFlags,
                            exeName: exeBuffer,
                            exeNameSize: exeBuffer is null ? 0 : (uint)exeBuffer.Length,
                            actualExeNameSize: out exeSize,
                            description: descBuffer,
                            descriptionSize: descBuffer is null ? 0 : (uint)descBuffer.Length,
                            actualDescriptionSize: out descSize);
                        if (hr < 0)
                        {
                            return null;
                        }

                        // Buffers are ANSI and include the trailing null terminator in the returned size.
                        string exe = DecodeAnsi(exeBuffer, exeSize);
                        string desc = DecodeAnsi(descBuffer, descSize);

                        // With our exclusion flags the Description should contain just the command line (may be empty
                        // for protected or system processes, in which case fall back to the executable name).
                        if (!string.IsNullOrEmpty(desc))
                        {
                            return desc;
                        }

                        return string.IsNullOrEmpty(exe) ? null : exe;
                    }
                    finally
                    {
                        if (exeBuffer is not null)
                        {
                            ArrayPool<byte>.Shared.Return(exeBuffer);
                        }
                        if (descBuffer is not null)
                        {
                            ArrayPool<byte>.Shared.Return(descBuffer);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(client);
                }
            }

            /// <summary>
            /// Decodes an ANSI byte buffer whose <paramref name="length"/> includes a trailing null terminator.
            /// Returns <see cref="string.Empty"/> if the buffer is null, empty, or contains only a terminator.
            /// </summary>
            private static string DecodeAnsi(byte[]? buffer, uint length)
            {
                if (buffer is null || length == 0)
                {
                    return string.Empty;
                }

                // Strip the trailing null terminator that dbgeng includes in the reported size.
                int effective = (int)length;
                if (effective > 0 && buffer[effective - 1] == 0)
                {
                    effective--;
                }

                return effective > 0 ? Encoding.Default.GetString(buffer, 0, effective) : string.Empty;
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
