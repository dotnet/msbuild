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
        /// <param name="commandLine">The command line string, or null if it cannot be retrieved.</param>
        /// <returns>True if the command line was successfully retrieved or the current platform doesn't support retrieving command lines, false if there was an error retrieving the command line.</returns>
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
                commandLine = NativeMethodsShared.IsWindows ? Windows.GetCommandLine(process.Id) :
                       NativeMethodsShared.IsOSX ? MacOS.GetCommandLine(process.Id) :
                       NativeMethodsShared.IsLinux ? Linux.GetCommandLine(process.Id) :
                       null; // If we don't have a platform-specific implementation, just return true with a null command line, since the caller should be able to handle that case.
                return true;
#else
                commandLine = Windows.GetCommandLine(process.Id);
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
            private static partial bool IsWow64Process2(
                IntPtr hProcess,
                out ushort processMachine,
                out ushort nativeMachine);

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
                Span<byte> lpBuffer,
                int dwSize,
                out int lpNumberOfBytesRead);
#else
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool IsWow64Process2(
                IntPtr hProcess,
                out ushort processMachine,
                out ushort nativeMachine);

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
            /// The process is the same architecture as the executing host (e.g. 64-bit process on 64-bit Windows or 32-bit process on 32-bit Windows).
            /// </summary>
            private const int IMAGE_FILE_MACHINE_UNKNOWN = 0x0000;
            /// <summary>
            /// The process is a 32-bit process running under WOW64 on 64-bit Windows.
            /// </summary>
            private const int IMAGE_FILE_MACHINE_I386 = 0x014c;

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
                    // Open the process with query and read permissions
                    hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
                    if (hProcess == IntPtr.Zero)
                    {
                        return null;
                    }

                    // Get process basic information to locate PEB
                    PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                    int returnLength;
                    int status = NtQueryInformationProcess(hProcess, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
                    if (status != 0 || pbi.PebBaseAddress == IntPtr.Zero)
                    {
                        return null;
                    }

                    bool is64BitProcess = true;
                    if (IsWow64Process2(hProcess, out ushort processMachine, out ushort nativeMachine))
                    {
                        if (processMachine == IMAGE_FILE_MACHINE_I386 || (IntPtr.Size == 4 && processMachine == IMAGE_FILE_MACHINE_UNKNOWN))
                        {
                            is64BitProcess = false;
                        }
                    }

                    // Read the PEB to get the ProcessParameters pointer
                    // In 64-bit: PEB + 0x20 = ProcessParameters
                    // In 32-bit: PEB + 0x10 = ProcessParameters
                    int processParametersOffset = is64BitProcess ? 0x20 : 0x10;
                    IntPtr processParametersPtr = IntPtr.Zero;

                    byte[] ptrBuffer = new byte[IntPtr.Size];
                    if (!ReadProcessMemory(hProcess, IntPtr.Add(pbi.PebBaseAddress, processParametersOffset), ptrBuffer, ptrBuffer.Length, out _))
                    {
                        return null;
                    }
                    processParametersPtr = is64BitProcess
                        ? new IntPtr(BitConverter.ToInt64(ptrBuffer, 0))
                        : new IntPtr(BitConverter.ToInt32(ptrBuffer, 0));

                    if (processParametersPtr == IntPtr.Zero)
                    {
                        return null;
                    }

                    // Read the CommandLine UNICODE_STRING from ProcessParameters
                    // CommandLine is at offset 0x70 in 64-bit and 0x40 in 32-bit
                    int commandLineOffset = is64BitProcess ? 0x70 : 0x40;
                    byte[] unicodeStringBuffer = new byte[Marshal.SizeOf(typeof(UNICODE_STRING))];
                    if (!ReadProcessMemory(hProcess, IntPtr.Add(processParametersPtr, commandLineOffset), unicodeStringBuffer, unicodeStringBuffer.Length, out _))
                    {
                        return null;
                    }

                    // Parse UNICODE_STRING structure
                    // Layout: ushort Length (2 bytes), ushort MaximumLength (2 bytes), [4 bytes padding on 64-bit], IntPtr Buffer
                    UNICODE_STRING commandLineUnicode = new UNICODE_STRING
                    {
                        Length = BitConverter.ToUInt16(unicodeStringBuffer, 0),
                        MaximumLength = BitConverter.ToUInt16(unicodeStringBuffer, 2),
                        Buffer = is64BitProcess
                            ? new IntPtr(BitConverter.ToInt64(unicodeStringBuffer, 8))  // 4 bytes for ushorts + 4 bytes padding
                            : new IntPtr(BitConverter.ToInt32(unicodeStringBuffer, 4))  // 4 bytes for ushorts, no padding
                    };

                    if (commandLineUnicode.Buffer == IntPtr.Zero || commandLineUnicode.Length == 0)
                    {
                        return null;
                    }

                    // Read the actual command line string
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
