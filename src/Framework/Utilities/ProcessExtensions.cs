// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Framework;

#if NET
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Build.Utilities;
#endif
#if FEATURE_WINDOWSINTEROP && NET
using Microsoft.Build.Shared.Win32.Wmi;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Diagnostics.Debug.Extensions;
using Windows.Win32.System.Variant;
using IWbemClassObject = Microsoft.Build.Shared.Win32.Wmi.IWbemClassObject;
using IWbemLocator = Microsoft.Build.Shared.Win32.Wmi.IWbemLocator;
using IWbemServices = Microsoft.Build.Shared.Win32.Wmi.IWbemServices;
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
#if FEATURE_WINDOWSINTEROP
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
#endif
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
        /// On Windows, command-line retrieval is opt-in via the <c>MSBUILDPROCESSCOMMANDLINESOURCE</c>
        /// environment variable (values: <c>Wmi</c> or <c>DebugEngine</c>); when unset, the command line
        /// is not retrieved.
        /// </summary>
        /// <param name="process">The process to get the command line for.</param>
        /// <param name="commandLine">The command line string, or null if it cannot be retrieved.</param>
        /// <returns>True if the command line was successfully retrieved, false if there was an error or the platform doesn't support command line retrieval.</returns>
        public static bool TryGetCommandLine(this Process? process, out string? commandLine)
            => TryGetCommandLine(process, GetConfiguredCommandLineSource(), out commandLine);

        private static CommandLineSource GetConfiguredCommandLineSource()
        {
            string? value = Environment.GetEnvironmentVariable("MSBUILDPROCESSCOMMANDLINESOURCE");
            if (string.IsNullOrEmpty(value))
            {
                return CommandLineSource.None;
            }

            return Enum.TryParse(value, ignoreCase: true, out CommandLineSource parsed)
                ? parsed
                : CommandLineSource.None;
        }

        /// <summary>
        /// Retrieves the full command line for a process, allowing the caller to choose the
        /// underlying Windows API via <paramref name="source"/>.
        /// On non-Windows platforms <paramref name="source"/> is ignored.
        /// </summary>
        public static bool TryGetCommandLine(this Process? process, CommandLineSource source, out string? commandLine)
        {
            commandLine = null;

            if (process?.HasExited != false)
            {
                return false;
            }

            try
            {
#if FEATURE_WINDOWSINTEROP && NET
                if (NativeMethods.IsWindows)
                {
                    if (source == CommandLineSource.None)
                    {
                        commandLine = null;
                        return true;
                    }

                    commandLine = Windows.GetCommandLine(process.Id, source);
                    return true;
                }
#endif
#if NET
                if (NativeMethods.IsWindows)
                {
                    // Windows without CsWin32 (source builds) - cannot query WMI/DebugEngine
                    commandLine = null;
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
            using BufferScope<char> charBuffer = new(data.Length);

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
                    int charsWritten = Encoding.UTF8.GetChars(segment, charBuffer.AsSpan().Slice(totalChars));

                    // UTF-8 decoder converts null bytes to null chars - replace them with spaces for safety
                    Span<char> decodedChars = charBuffer.Slice(totalChars, charsWritten);
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

            return charBuffer.Slice(0, totalChars).ToString();
        }
#endif

        /// <summary>
        /// Selects the underlying Windows API used to retrieve another process's command line.
        /// On non-Windows platforms the value is accepted but ignored.
        /// </summary>
        public enum CommandLineSource
        {
            /// <summary>
            /// Do not attempt to retrieve the command line. Default behavior; <see cref="TryGetCommandLine(Process?, out string?)"/>
            /// returns <see langword="true"/> with a <see langword="null"/> command line on Windows.
            /// </summary>
            None = 0,

            /// <summary>
            /// Query WMI's <c>Win32_Process.CommandLine</c> via <c>IWbemLocator</c>/<c>IWbemServices</c>.
            /// </summary>
            Wmi,

            /// <summary>
            /// Call <c>dbgeng!IDebugClient4::GetRunningProcessDescriptionWide</c>. Avoids the WMI service
            /// and returns UTF-16 text directly (no ANSI-to-Unicode conversion).
            /// </summary>
            DebugEngine,
        }

#if FEATURE_WINDOWSINTEROP && NET
        /// <summary>
        /// Windows-specific command line retrieval.
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        private static class Windows
        {
            // WBEM status codes
            private static readonly HRESULT WBEM_S_FALSE = (HRESULT)1; // No more objects in enumeration
            private const int WBEM_FLAG_FORWARD_ONLY = 0x00000020;
            private const int WBEM_FLAG_RETURN_IMMEDIATELY = 0x00000010;
            private const int WBEM_INFINITE = -1;

            // Flags for IDebugClient4::GetRunningProcessDescriptionWide. By default the Description output
            // concatenates service names, MTS package names, command line, session id, and user name; we
            // exclude everything except the command line.
            private const uint DebugProcessDescriptionFlags =
                PInvoke.DEBUG_PROC_DESC_NO_PATHS
                | PInvoke.DEBUG_PROC_DESC_NO_SERVICES
                | PInvoke.DEBUG_PROC_DESC_NO_MTS_PACKAGES
                | PInvoke.DEBUG_PROC_DESC_NO_SESSION_ID
                | PInvoke.DEBUG_PROC_DESC_NO_USER_NAME;

            /// <summary>
            /// Retrieves the command line for a process using the requested <paramref name="source"/>.
            /// </summary>
            internal static string? GetCommandLine(int processId, CommandLineSource source) => source switch
            {
                CommandLineSource.Wmi => GetCommandLineViaWmi(processId),
                CommandLineSource.DebugEngine => GetCommandLineViaDebugEngine(processId),
                _ => null,
            };

            /// <summary>
            /// Retrieves the command line for a process by querying WMI Win32_Process via COM.
            /// Runs: SELECT CommandLine FROM Win32_Process WHERE ProcessId='<paramref name="processId"/>'
            /// Uses CsWin32-generated P/Invoke for ole32.dll functions and manually defined COM structs
            /// for WMI interfaces (which are not in Win32 metadata).
            /// </summary>
            internal static unsafe string? GetCommandLineViaWmi(int processId)
            {
                HRESULT hr = PInvoke.CoInitializeSecurity(
                    pSecDesc: default,
                    cAuthSvc: -1,
                    asAuthSvc: null,
                    dwAuthnLevel: RPC_C_AUTHN_LEVEL.RPC_C_AUTHN_LEVEL_DEFAULT,
                    dwImpLevel: RPC_C_IMP_LEVEL.RPC_C_IMP_LEVEL_IMPERSONATE,
                    pAuthList: null,
                    dwCapabilities: EOLE_AUTHENTICATION_CAPABILITIES.EOAC_NONE);

                // RPC_E_TOO_LATE (0x80010119) means another call already set security — not fatal.
                if (hr.Failed && hr != HRESULT.RPC_E_TOO_LATE)
                {
                    throw new InvalidOperationException(
                        $"WMI CoInitializeSecurity failed for PID {processId}. HRESULT: 0x{hr.Value:X8}");
                }

                Guid clsid = IWbemLocator.CLSID;
                hr = PInvoke.CoCreateInstance(in clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.Get<IWbemLocator>(), out void* locatorPtr);
                using ComScope<IWbemLocator> locator = new(locatorPtr);
                if (hr.Failed)
                {
                    throw new InvalidOperationException(
                        $"WMI CoCreateInstance failed for PID {processId}. HRESULT: 0x{hr.Value:X8}");
                }

                using ComScope<IWbemServices> services = new();

                fixed (char* networkResource = @"ROOT\CIMV2")
                {
                    hr = locator.Pointer->ConnectServer(
                        networkResource,
                        strUser: null, strPassword: null, strLocale: null,
                        lSecurityFlags: 0, strAuthority: null,
                        pCtx: null,
                        services);
                }

                if (hr.Failed)
                {
                    throw new InvalidOperationException(
                        $"WMI ConnectServer failed for PID {processId}. HRESULT: 0x{hr.Value:X8}");
                }

                hr = PInvoke.CoSetProxyBlanket(
                    pProxy: (IUnknown*)services.Pointer,
                    dwAuthnSvc: PInvoke.RPC_C_AUTHN_WINNT,
                    dwAuthzSvc: PInvoke.RPC_C_AUTHZ_NONE,
                    pServerPrincName: default,
                    dwAuthnLevel: RPC_C_AUTHN_LEVEL.RPC_C_AUTHN_LEVEL_CALL,
                    dwImpLevel: RPC_C_IMP_LEVEL.RPC_C_IMP_LEVEL_IMPERSONATE,
                    pAuthInfo: null,
                    dwCapabilities: EOLE_AUTHENTICATION_CAPABILITIES.EOAC_NONE);

                if (hr.Failed)
                {
                    throw new InvalidOperationException(
                        $"WMI CoSetProxyBlanket failed for PID {processId}. HRESULT: 0x{hr.Value:X8}");
                }

                string query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId='{processId}'";
                using ComScope<IEnumWbemClassObject> enumerator = new();

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement
                fixed (char* queryLanguage = "WQL")
                fixed (char* queryStr = query)
#pragma warning restore SA1519
                {
                    hr = services.Pointer->ExecQuery(
                        queryLanguage,
                        queryStr,
                        WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
                        pCtx: null,
                        enumerator);
                }

                if (hr.Failed)
                {
                    throw new InvalidOperationException(
                        $"WMI ExecQuery failed for PID {processId}. HRESULT: 0x{hr.Value:X8}");
                }


                using ComScope<IWbemClassObject> obj = new();
                uint returned;
                hr = enumerator.Pointer->Next(WBEM_INFINITE, 1, obj, &returned);
                if (hr == WBEM_S_FALSE || returned == 0)
                {
                    // No matching process found.
                    return null;
                }

                if (hr.Failed)
                {
                    throw new InvalidOperationException(
                        $"WMI IEnumWbemClassObject.Next failed for PID {processId}. HRESULT: 0x{hr.Value:X8}");
                }

                using VARIANT val = default;
                fixed (char* propName = "CommandLine")
                {
                    hr = obj.Pointer->Get(propName, 0, &val, pType: null, plFlavor: null);
                }

                if (hr.Failed)
                {
                    throw new InvalidOperationException(
                        $"WMI IWbemClassObject.Get(\"CommandLine\") failed for PID {processId}. HRESULT: 0x{hr.Value:X8}");
                }

                if (val.Type == VARENUM.VT_BSTR)
                {
                    return ((BSTR)val).ToString();
                }

                return null;
            }

            /// <summary>
            /// Retrieves the command line for a process via <c>dbgeng!IDebugClient4::GetRunningProcessDescriptionWide</c>.
            /// <c>IDebugClient4</c> is the oldest interface version that exposes the Wide variant, so the returned
            /// text is already UTF-16 and does not need to be converted from ANSI. Returns <c>null</c> if the target
            /// cannot be inspected (for example, access denied, protected process, or the debug engine is unavailable).
            /// </summary>
            internal static unsafe string? GetCommandLineViaDebugEngine(int processId)
            {
                HRESULT hr = PInvoke.DebugCreate(IID.Get<IDebugClient4>(), out void* clientPtr);
                using ComScope<IDebugClient4> client = new(clientPtr);
                if (hr.Failed || client.Pointer is null)
                {
                    return null;
                }

                // First call with null buffers to discover required sizes (in characters, including the
                // trailing null terminator).
                uint exeSize;
                uint descSize;
                hr = client.Pointer->GetRunningProcessDescriptionWide(
                    Server: 0,
                    SystemId: (uint)processId,
                    Flags: DebugProcessDescriptionFlags,
                    ExeName: null,
                    ExeNameSize: 0,
                    ActualExeNameSize: &exeSize,
                    Description: null,
                    DescriptionSize: 0,
                    ActualDescriptionSize: &descSize);

                // A hard failure with no sizes reported means the PID can't be inspected.
                if (hr.Failed && exeSize == 0 && descSize == 0)
                {
                    return null;
                }

                using BufferScope<char> exeBuffer = new((int)exeSize);
                using BufferScope<char> descBuffer = new((int)descSize);

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement
                fixed (char* pExe = exeBuffer)
                fixed (char* pDesc = descBuffer)
#pragma warning restore SA1519
                {
                    hr = client.Pointer->GetRunningProcessDescriptionWide(
                        Server: 0,
                        SystemId: (uint)processId,
                        Flags: DebugProcessDescriptionFlags,
                        ExeName: pExe,
                        ExeNameSize: exeSize,
                        ActualExeNameSize: &exeSize,
                        Description: pDesc,
                        DescriptionSize: descSize,
                        ActualDescriptionSize: &descSize);
                }

                if (hr.Failed)
                {
                    return null;
                }

                // Sizes include the trailing null terminator.
                string desc = descSize > 1 ? descBuffer.Slice(0, (int)descSize - 1).ToString() : string.Empty;
                if (!string.IsNullOrEmpty(desc))
                {
                    return desc;
                }

                // With our exclusion flags the Description contains just the command line; fall back to
                // the executable name for protected/system processes where the command line is not returned.
                string exe = exeSize > 1 ? exeBuffer.Slice(0, (int)exeSize - 1).ToString() : string.Empty;
                return string.IsNullOrEmpty(exe) ? null : exe;
            }
        }
#endif // FEATURE_WINDOWSINTEROP && NET

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

                // Rent a buffer for sysctl
                using BufferScope<byte> buffer = new((int)size);

                if (Sysctl(mib, buffer.AsSpan().Slice(0, (int)size), ref size) != 0)
                {
                    return null;
                }

                // Buffer format (KERN_PROCARGS2):
                //   int argc (number of arguments including executable)
                //   fully-qualified executable path (null-terminated)
                //   padding null bytes
                //   argv[0] .. argv[argc-1] (each null-terminated)
                //   environment variables (not needed)
                ReadOnlySpan<byte> data = buffer.AsSpan().Slice(0, (int)size);

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
        }
#endif
    }
}
