// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

#if NET
using System.IO;
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
            if (NativeMethodsShared.IsWindows)
            {
                try
                {
                    NativeMethodsShared.KillTree(process.Id);
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
        /// <returns>The command line string, or null if it cannot be retrieved.</returns>
        public static string? GetCommandLine(this Process? process)
        {
            if (process is null)
            {
                return null;
            }

            try
            {
                if (process.HasExited)
                {
                    return null;
                }
            }
            catch
            {
                // Process might have exited between null check and HasExited check.
                return null;
            }

            try
            {
#if NET
                return NativeMethodsShared.IsWindows ? Windows.GetCommandLine(process.Id) :
                       NativeMethodsShared.IsOSX ? MacOS.GetCommandLine(process.Id) :
                       NativeMethodsShared.IsLinux ? Linux.GetCommandLine(process.Id) : 
                       throw new NotSupportedException();
#else
                return Windows.GetCommandLine(process.Id);
#endif
            }
            catch
            {
                return null;
            }
        }

#if NET
        /// <summary>
        /// Parses a null-separated byte buffer into a space-joined argument string using span-based slicing.
        /// Used by both Linux (/proc/pid/cmdline) and macOS (sysctl KERN_PROCARGS2) parsing.
        /// </summary>
        private static string ParseNullSeparatedArguments(ReadOnlySpan<byte> data, int maxArgs = int.MaxValue)
        {
            StringBuilder sb = new(data.Length);
            int argsFound = 0;

            while (!data.IsEmpty && argsFound < maxArgs)
            {
                int nullIndex = data.IndexOf((byte)0);
                ReadOnlySpan<byte> segment = nullIndex >= 0 ? data.Slice(0, nullIndex) : data;

                if (!segment.IsEmpty)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    sb.Append(Encoding.UTF8.GetString(segment));
                    argsFound++;
                }

                if (nullIndex < 0)
                {
                    break;
                }

                data = data.Slice(nullIndex + 1);
            }

            return sb.ToString();
        }
#endif

        /// <summary>
        /// Windows-specific P/Invoke bindings and command line retrieval via the Process Environment Block (PEB).
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static partial class Windows
        {
#if NET
            [LibraryImport("kernel32.dll", SetLastError = true)]
            private static partial IntPtr OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static partial bool CloseHandle(IntPtr hObject);

            [LibraryImport("ntdll.dll")]
            private static partial int NtQueryInformationProcess(
                IntPtr processHandle,
                int processInformationClass,
                ref PROCESS_BASIC_INFORMATION processInformation,
                int processInformationLength,
                out int returnLength);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static partial bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                out IntPtr lpBuffer,
                int dwSize,
                out int lpNumberOfBytesRead);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static partial bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                out UNICODE_STRING lpBuffer,
                int dwSize,
                out int lpNumberOfBytesRead);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static partial bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                Span<byte> lpBuffer,
                int dwSize,
                out int lpNumberOfBytesRead);
#else
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle(IntPtr hObject);

            [DllImport("ntdll.dll")]
            private static extern int NtQueryInformationProcess(
                IntPtr processHandle,
                int processInformationClass,
                ref PROCESS_BASIC_INFORMATION processInformation,
                int processInformationLength,
                out int returnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                out IntPtr lpBuffer,
                int dwSize,
                out int lpNumberOfBytesRead);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                out UNICODE_STRING lpBuffer,
                int dwSize,
                out int lpNumberOfBytesRead);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                [Out] byte[] lpBuffer,
                int dwSize,
                out int lpNumberOfBytesRead);
#endif

            [StructLayout(LayoutKind.Sequential)]
            private struct PROCESS_BASIC_INFORMATION
            {
                public IntPtr Reserved1;
                public IntPtr PebBaseAddress;
                public IntPtr Reserved2_0;
                public IntPtr Reserved2_1;
                public IntPtr UniqueProcessId;
                public IntPtr InheritedFromUniqueProcessId;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct UNICODE_STRING
            {
                public ushort Length;
                public ushort MaximumLength;
                public IntPtr Buffer;
            }

            private const int PROCESS_QUERY_INFORMATION = 0x0400;
            private const int PROCESS_VM_READ = 0x0010;

            /// <summary>
            /// Reads the command line from the Process Environment Block (PEB) of a Windows process.
            /// Uses typed ReadProcessMemory overloads to read structured data directly,
            /// avoiding manual byte[] allocation and BitConverter deserialization.
            /// </summary>
            internal static string? GetCommandLine(int processId)
            {
                IntPtr hProcess = IntPtr.Zero;
                try
                {
                    hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
                    if (hProcess == IntPtr.Zero)
                    {
                        return null;
                    }

                    PROCESS_BASIC_INFORMATION pbi = default;
                    int status = NtQueryInformationProcess(hProcess, 0, ref pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);
                    if (status != 0 || pbi.PebBaseAddress == IntPtr.Zero)
                    {
                        return null;
                    }

                    // Read the ProcessParameters pointer directly from PEB.
                    // Offset: 0x20 on 64-bit, 0x10 on 32-bit.
                    int processParametersOffset = IntPtr.Size == 8 ? 0x20 : 0x10;
                    if (!ReadProcessMemory(hProcess, IntPtr.Add(pbi.PebBaseAddress, processParametersOffset), out IntPtr processParametersPtr, IntPtr.Size, out _)
                        || processParametersPtr == IntPtr.Zero)
                    {
                        return null;
                    }

                    // Read the CommandLine UNICODE_STRING struct directly from ProcessParameters.
                    // Offset: 0x70 on 64-bit, 0x40 on 32-bit.
                    // The CLR handles struct alignment (including 4-byte padding on 64-bit) automatically
                    // via [StructLayout(LayoutKind.Sequential)], so no manual layout parsing is needed.
                    int commandLineOffset = IntPtr.Size == 8 ? 0x70 : 0x40;
                    if (!ReadProcessMemory(hProcess, IntPtr.Add(processParametersPtr, commandLineOffset), out UNICODE_STRING commandLineUnicode, Marshal.SizeOf<UNICODE_STRING>(), out _)
                        || commandLineUnicode.Buffer == IntPtr.Zero
                        || commandLineUnicode.Length == 0)
                    {
                        return null;
                    }

                    byte[] commandLineBuffer = new byte[commandLineUnicode.Length];
                    if (!ReadProcessMemory(hProcess, commandLineUnicode.Buffer, commandLineBuffer, commandLineBuffer.Length, out _))
                    {
                        return null;
                    }

                    return Encoding.Unicode.GetString(commandLineBuffer);
                }
                catch
                {
                    return null;
                }
                finally
                {
                    if (hProcess != IntPtr.Zero)
                    {
                        CloseHandle(hProcess);
                    }
                }
            }
        }

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
                    if (!File.Exists(cmdlinePath))
                    {
                        return null;
                    }

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
        /// macOS-specific P/Invoke bindings and command line retrieval via sysctl KERN_PROCARGS2.
        /// </summary>
        [SupportedOSPlatform("macos")]
        private static partial class MacOS
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
            /// then parses the null-separated buffer using span-based slicing.
            /// </summary>
            internal static string? GetCommandLine(int processId)
            {
                try
                {
                    ReadOnlySpan<int> mib = [CTL_KERN, KERN_PROCARGS2, processId];
                    nuint size = 0;

                    if (Sysctl(mib, Span<byte>.Empty, ref size) != 0)
                    {
                        return null;
                    }

                    if (size == 0)
                    {
                        return null;
                    }

                    byte[] buffer = new byte[size];
                    if (Sysctl(mib, buffer, ref size) != 0)
                    {
                        return null;
                    }

                    // Buffer format:
                    //   int argc
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
                    int nullIndex = data.IndexOf((byte)0);
                    if (nullIndex < 0)
                    {
                        return null;
                    }

                    data = data.Slice(nullIndex + 1);

                    // Skip padding null bytes between executable path and argv[0]
                    while (!data.IsEmpty && data[0] == 0)
                    {
                        data = data.Slice(1);
                    }

                    return ParseNullSeparatedArguments(data, argc);
                }
                catch
                {
                    return null;
                }
            }
        }
#endif
    }
}
