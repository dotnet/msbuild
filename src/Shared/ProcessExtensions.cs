// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.Text;
#if NET
using System;
using System.Runtime.InteropServices;
#endif

#nullable disable

namespace Microsoft.Build.Shared
{
    internal static class ProcessExtensions
    {
#if NET
        // P/Invoke declarations for getting process command line on Windows (.NET Core)
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
            [Out] byte[] lpBuffer,
            int dwSize,
            out int lpNumberOfBytesRead);

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
#endif

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
        /// Retrieves the command line on Windows.
        /// On .NET Framework: Uses WMI (System.Management).
        /// On .NET Core+: Uses Windows API P/Invoke to read from PEB.
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
            // On .NET Core/5+, use native Windows API to read command line from process PEB
            try
            {
                return GetCommandLineWindowsNative(process.Id);
            }
            catch
            {
                // Native API calls failed
                return null;
            }
#endif
        }

#if NET
        /// <summary>
        /// Retrieves the command line for a Windows process using native APIs.
        /// This reads the command line from the Process Environment Block (PEB).
        /// </summary>
        private static string GetCommandLineWindowsNative(int processId)
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

                // Read the PEB to get the ProcessParameters pointer
                // In 64-bit: PEB + 0x20 = ProcessParameters
                // In 32-bit: PEB + 0x10 = ProcessParameters
                int processParametersOffset = IntPtr.Size == 8 ? 0x20 : 0x10;
                IntPtr processParametersPtr = IntPtr.Zero;
                
                byte[] ptrBuffer = new byte[IntPtr.Size];
                if (!ReadProcessMemory(hProcess, IntPtr.Add(pbi.PebBaseAddress, processParametersOffset), ptrBuffer, ptrBuffer.Length, out _))
                {
                    return null;
                }
                processParametersPtr = IntPtr.Size == 8 
                    ? new IntPtr(BitConverter.ToInt64(ptrBuffer, 0))
                    : new IntPtr(BitConverter.ToInt32(ptrBuffer, 0));

                if (processParametersPtr == IntPtr.Zero)
                {
                    return null;
                }

                // Read the CommandLine UNICODE_STRING from ProcessParameters
                // CommandLine is at offset 0x70 in 64-bit and 0x40 in 32-bit
                int commandLineOffset = IntPtr.Size == 8 ? 0x70 : 0x40;
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
                    Buffer = IntPtr.Size == 8
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
#endif


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
                            string currentArg = System.Text.Encoding.UTF8.GetString(cmdlineBytes, start, i - start);
                            if (sb.Length > 0)
                            {
                                sb.Append(' ');
                            }
                            sb.Append(currentArg);
                        }
                        start = i + 1;
                    }
                }
                
                // Handle any remaining bytes after the last null terminator
                if (start < cmdlineBytes.Length)
                {
                    string remainingArg = System.Text.Encoding.UTF8.GetString(cmdlineBytes, start, cmdlineBytes.Length - start);
                    if (remainingArg.Length > 0)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(' ');
                        }
                        sb.Append(remainingArg);
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
