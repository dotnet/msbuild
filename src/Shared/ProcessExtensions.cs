// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#if NET
using System.Buffers;
using System.Text;
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
                if (NativeMethodsShared.IsWindows)
                {
                    commandLine = Windows.GetCommandLine(process.Id);
                    return true;
                }
                else if (NativeMethodsShared.IsOSX || NativeMethodsShared.IsBSD)
                {
                    commandLine = BSD.GetCommandLine(process.Id);
                    return true;
                }
                else if (NativeMethodsShared.IsLinux)
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

        /// <summary>
        /// Windows-specific command line retrieval via WMI COM interfaces.
        /// Queries Win32_Process for the CommandLine property using IWbemLocator/IWbemServices.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static class Windows
        {
            // WMI COM interface GUIDs
            private static readonly Guid CLSID_WbemLocator = new Guid("4590F811-1D3A-11D0-891F-00AA004B2E24");
            private static readonly Guid IID_IWbemLocator = new Guid("DC12A687-737F-11CF-884D-00AA004B2E24");

            // WBEM status codes
            private const int WBEM_S_NO_ERROR = 0;
            private const int WBEM_S_FALSE = 1; // No more objects in enumeration
            private const int WBEM_FLAG_FORWARD_ONLY = 0x00000020;
            private const int WBEM_FLAG_RETURN_IMMEDIATELY = 0x00000010;
            private const int WBEM_INFINITE = -1;

            
            // RPC authentication/impersonation constants (used by CoInitializeSecurity and CoSetProxyBlanket)
            private const int RPC_C_AUTHN_LEVEL_DEFAULT = 0;
            private const int RPC_C_AUTHN_LEVEL_CALL = 3;
            private const int RPC_C_IMP_LEVEL_IMPERSONATE = 3;
            private const int RPC_C_AUTHN_WINNT = 10;
            private const int RPC_C_AUTHZ_NONE = 0;
            private const int EOAC_NONE = 0;

            // CoCreateInstance: in-process server
            private const int CLSCTX_INPROC_SERVER = 1;

            // HRESULTs for conditions that are not fatal failures
            private const int RPC_E_TOO_LATE = unchecked((int)0x80010119);     // CoInitializeSecurity already called

            [DllImport("ole32.dll")]
            private static extern int CoInitializeEx(IntPtr pvReserved, int dwCoInit);

            [DllImport("ole32.dll")]
            private static extern int CoInitializeSecurity(
                IntPtr pSecDesc,
                int cAuthSvc,
                IntPtr asAuthSvc,
                IntPtr pReserved,
                int dwAuthnLevel,
                int dwImpLevel,
                IntPtr pAuthList,
                int dwCapabilities,
                IntPtr pReserved3);

            [DllImport("ole32.dll")]
            private static extern int CoCreateInstance(
                ref Guid rclsid,
                IntPtr pUnkOuter,
                int dwClsContext,
                ref Guid riid,
                [MarshalAs(UnmanagedType.Interface)] out IWbemLocator ppv);

            [DllImport("ole32.dll")]
            private static extern int CoSetProxyBlanket(
                [MarshalAs(UnmanagedType.IUnknown)] object pProxy,
                int dwAuthnSvc,
                int dwAuthzSvc,
                IntPtr pServerPrincName,
                int dwAuthnLevel,
                int dwImpLevel,
                IntPtr pAuthInfo,
                int dwCapabilities);

#if !NET
            [DllImport("ole32.dll", EntryPoint = "CoSetProxyBlanket")]
            private static extern int CoSetProxyBlanketPtr(
                IntPtr pProxy,
                int dwAuthnSvc,
                int dwAuthzSvc,
                IntPtr pServerPrincName,
                int dwAuthnLevel,
                int dwImpLevel,
                IntPtr pAuthInfo,
                int dwCapabilities);
#endif

            [ComImport]
            [Guid("DC12A687-737F-11CF-884D-00AA004B2E24")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IWbemLocator
            {
                [PreserveSig]
                int ConnectServer(
                    [MarshalAs(UnmanagedType.BStr)] string strNetworkResource,
                    [MarshalAs(UnmanagedType.BStr)] string? strUser,
                    [MarshalAs(UnmanagedType.BStr)] string? strPassword,
                    [MarshalAs(UnmanagedType.BStr)] string? strLocale,
                    int lSecurityFlags,
                    [MarshalAs(UnmanagedType.BStr)] string? strAuthority,
                    IntPtr pCtx,
                    [MarshalAs(UnmanagedType.Interface)] out IWbemServices ppNamespace);
            }

            [Guid("44ACA674-E8FC-11D0-A07C-00C04FB68820")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            [ComImport]
            internal interface IWbemContext
            {
                [PreserveSig]
                int Clone([MarshalAs(UnmanagedType.Interface)] out IWbemContext ppNewCopy);

                [PreserveSig]
                int GetNames(int lFlags, IntPtr pNames);

                [PreserveSig]
                int BeginEnumeration(int lFlags);

                [PreserveSig]
                int Next(int lFlags, [MarshalAs(UnmanagedType.BStr)] out string pstrName, IntPtr pValue);

                [PreserveSig]
                int EndEnumeration();

                [PreserveSig]
                int SetValue([MarshalAs(UnmanagedType.LPWStr)] string wszName, int lFlags, IntPtr pValue);

                [PreserveSig]
                int GetValue([MarshalAs(UnmanagedType.LPWStr)] string wszName, int lFlags, IntPtr pValue);

                [PreserveSig]
                int DeleteValue([MarshalAs(UnmanagedType.LPWStr)] string wszName, int lFlags);

                [PreserveSig]
                int DeleteAll();
            }

            [ComImport]
            [Guid("9556DC99-828C-11CF-A37E-00AA003240C7")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IWbemServices
            {
                [PreserveSig]
                int OpenNamespace(
                    [MarshalAs(UnmanagedType.BStr)] string strNamespace,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr ppWorkingNamespace,
                    IntPtr ppResult);

                [PreserveSig]
                int CancelAsyncCall(IntPtr pSink);

                [PreserveSig]
                int QueryObjectSink(int lFlags, IntPtr ppResponseHandler);

                [PreserveSig]
                int GetObject(
                    [MarshalAs(UnmanagedType.BStr)] string strObjectPath,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr ppObject,
                    IntPtr ppCallResult);

                [PreserveSig]
                int GetObjectAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strObjectPath,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pResponseHandler);

                [PreserveSig]
                int PutClass(IntPtr pObject, int lFlags, IntPtr pCtx, IntPtr ppCallResult);

                [PreserveSig]
                int PutClassAsync(IntPtr pObject, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);

                [PreserveSig]
                int DeleteClass(
                    [MarshalAs(UnmanagedType.BStr)] string strClass,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr ppCallResult);

                [PreserveSig]
                int DeleteClassAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strClass,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pResponseHandler);

                [PreserveSig]
                int CreateClassEnum(
                    [MarshalAs(UnmanagedType.BStr)] string strSuperclass,
                    int lFlags,
                    IntPtr pCtx,
                    [MarshalAs(UnmanagedType.Interface)] out IEnumWbemClassObject ppEnum);

                [PreserveSig]
                int CreateClassEnumAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strSuperclass,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pResponseHandler);

                [PreserveSig]
                int PutInstance(IntPtr pInst, int lFlags, IntPtr pCtx, IntPtr ppCallResult);

                [PreserveSig]
                int PutInstanceAsync(IntPtr pInst, int lFlags, IntPtr pCtx, IntPtr pResponseHandler);

                [PreserveSig]
                int DeleteInstance(
                    [MarshalAs(UnmanagedType.BStr)] string strObjectPath,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr ppCallResult);

                [PreserveSig]
                int DeleteInstanceAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strObjectPath,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pResponseHandler);

                [PreserveSig]
                int CreateInstanceEnum(
                    [MarshalAs(UnmanagedType.BStr)] string strFilter,
                    int lFlags,
                    IntPtr pCtx,
                    [MarshalAs(UnmanagedType.Interface)] out IEnumWbemClassObject ppEnum);

                [PreserveSig]
                int CreateInstanceEnumAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strFilter,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pResponseHandler);

                [PreserveSig]
                int ExecQuery(
                    [In][MarshalAs(UnmanagedType.BStr)] string strQueryLanguage,
                    [In][MarshalAs(UnmanagedType.BStr)] string strQuery,
                    [In] int lFlags,
                    [In] IWbemContext? pCtx,
                    [MarshalAs(UnmanagedType.Interface)] out IEnumWbemClassObject ppEnum);

                [PreserveSig]
                int ExecQueryAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strQueryLanguage,
                    [MarshalAs(UnmanagedType.BStr)] string strQuery,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pResponseHandler);

                [PreserveSig]
                int ExecNotificationQuery(
                    [MarshalAs(UnmanagedType.BStr)] string strQueryLanguage,
                    [MarshalAs(UnmanagedType.BStr)] string strQuery,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr ppEnum);

                [PreserveSig]
                int ExecNotificationQueryAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strQueryLanguage,
                    [MarshalAs(UnmanagedType.BStr)] string strQuery,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pResponseHandler);

                [PreserveSig]
                int ExecMethod(
                    [MarshalAs(UnmanagedType.BStr)] string strObjectPath,
                    [MarshalAs(UnmanagedType.BStr)] string strMethodName,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pInParams,
                    IntPtr ppOutParams,
                    IntPtr ppCallResult);

                [PreserveSig]
                int ExecMethodAsync(
                    [MarshalAs(UnmanagedType.BStr)] string strObjectPath,
                    [MarshalAs(UnmanagedType.BStr)] string strMethodName,
                    int lFlags,
                    IntPtr pCtx,
                    IntPtr pInParams,
                    IntPtr pResponseHandler);
            }

            [ComImport]
            [Guid("027947E1-D731-11CE-A357-000000000001")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IEnumWbemClassObject
            {
                [PreserveSig]
                int Reset();

                [PreserveSig]
                int Next(
                    int lTimeout,
                    uint uCount,
                    [MarshalAs(UnmanagedType.Interface)] out IWbemClassObject apObjects,
                    out uint puReturned);

                [PreserveSig]
                int NextAsync(uint uCount, IntPtr pSink);

                [PreserveSig]
                int Clone([MarshalAs(UnmanagedType.Interface)] out IEnumWbemClassObject ppEnum);

                [PreserveSig]
                int Skip(int lTimeout, uint nCount);
            }

            [ComImport]
            [Guid("DC12A681-737F-11CF-884D-00AA004B2E24")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IWbemClassObject
            {
                [PreserveSig]
                int GetQualifierSet(IntPtr ppQualSet);

                [PreserveSig]
                int Get(
                    [MarshalAs(UnmanagedType.LPWStr)] string wszName,
                    int lFlags,
                    ref object pVal,
                    IntPtr pType,
                    IntPtr plFlavor);

                [PreserveSig]
                int Put([MarshalAs(UnmanagedType.LPWStr)] string wszName, int lFlags, ref object pVal, int type);

                [PreserveSig]
                int Delete([MarshalAs(UnmanagedType.LPWStr)] string wszName);

                [PreserveSig]
                int GetNames([MarshalAs(UnmanagedType.LPWStr)] string wszQualifierName, int lFlags, ref object pQualifierVal, IntPtr pNames);

                [PreserveSig]
                int BeginEnumeration(int lEnumFlags);

                [PreserveSig]
                int Next(int lFlags, [MarshalAs(UnmanagedType.BStr)] out string strName, ref object pVal, IntPtr pType, IntPtr plFlavor);

                [PreserveSig]
                int EndEnumeration();

                [PreserveSig]
                int GetPropertyQualifierSet([MarshalAs(UnmanagedType.LPWStr)] string wszProperty, IntPtr ppQualSet);

                [PreserveSig]
                int Clone([MarshalAs(UnmanagedType.Interface)] out IWbemClassObject ppCopy);

                [PreserveSig]
                int GetObjectText(int lFlags, [MarshalAs(UnmanagedType.BStr)] out string pstrObjectText);

                [PreserveSig]
                int SpawnDerivedClass(int lFlags, IntPtr ppNewClass);

                [PreserveSig]
                int SpawnInstance(int lFlags, IntPtr ppNewInstance);

                [PreserveSig]
                int CompareTo(int lFlags, IntPtr pCompareTo);

                [PreserveSig]
                int GetPropertyOrigin([MarshalAs(UnmanagedType.LPWStr)] string wszName, [MarshalAs(UnmanagedType.BStr)] out string pstrClassName);

                [PreserveSig]
                int InheritsFrom([MarshalAs(UnmanagedType.LPWStr)] string strAncestor);

                [PreserveSig]
                int GetMethod([MarshalAs(UnmanagedType.LPWStr)] string wszName, int lFlags, IntPtr ppInSignature, IntPtr ppOutSignature);

                [PreserveSig]
                int PutMethod([MarshalAs(UnmanagedType.LPWStr)] string wszName, int lFlags, IntPtr pInSignature, IntPtr pOutSignature);

                [PreserveSig]
                int DeleteMethod([MarshalAs(UnmanagedType.LPWStr)] string wszName);

                [PreserveSig]
                int BeginMethodEnumeration(int lEnumFlags);

                [PreserveSig]
                int NextMethod(int lFlags, [MarshalAs(UnmanagedType.BStr)] out string pstrName, IntPtr ppInSignature, IntPtr ppOutSignature);

                [PreserveSig]
                int EndMethodEnumeration();

                [PreserveSig]
                int GetMethodQualifierSet([MarshalAs(UnmanagedType.LPWStr)] string wszMethod, IntPtr ppQualSet);

                [PreserveSig]
                int GetMethodOrigin([MarshalAs(UnmanagedType.LPWStr)] string wszMethodName, [MarshalAs(UnmanagedType.BStr)] out string pstrClassName);
            }

#if !NET
            /// <summary>
            /// Sets the proxy blanket on a COM proxy via raw interface pointers.
            /// On .NET Framework, the RCW caches separate proxy stubs for IUnknown and each
            /// specific COM interface. CoSetProxyBlanket must be called on both the IUnknown
            /// pointer and the specific interface pointer, otherwise WMI calls fail with
            /// WBEM_E_ACCESS_DENIED (0x80041003).
            /// </summary>
            private static void SetProxyBlanketForNetFx<T>(object comProxy)
            {
                IntPtr pUnk = Marshal.GetIUnknownForObject(comProxy);
                try
                {
                    CoSetProxyBlanketPtr(
                        pUnk,
                        RPC_C_AUTHN_WINNT,
                        RPC_C_AUTHZ_NONE,
                        IntPtr.Zero,
                        RPC_C_AUTHN_LEVEL_CALL,
                        RPC_C_IMP_LEVEL_IMPERSONATE,
                        IntPtr.Zero,
                        EOAC_NONE);
                }
                finally
                {
                    Marshal.Release(pUnk);
                }

                IntPtr pInterface = Marshal.GetComInterfaceForObject(comProxy, typeof(T));
                try
                {
                    CoSetProxyBlanketPtr(
                        pInterface,
                        RPC_C_AUTHN_WINNT,
                        RPC_C_AUTHZ_NONE,
                        IntPtr.Zero,
                        RPC_C_AUTHN_LEVEL_CALL,
                        RPC_C_IMP_LEVEL_IMPERSONATE,
                        IntPtr.Zero,
                        EOAC_NONE);
                }
                finally
                {
                    Marshal.Release(pInterface);
                }
            }
#endif

            /// <summary>
            /// Retrieves the command line for a process by querying WMI Win32_Process via COM.
            /// Runs: SELECT CommandLine FROM Win32_Process WHERE ProcessId='<paramref name="processId"/>'
            /// </summary>
            internal static string? GetCommandLine(int processId)
            {
                int hr = CoInitializeSecurity(
                    IntPtr.Zero,
                    -1,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    RPC_C_AUTHN_LEVEL_DEFAULT,
                    RPC_C_IMP_LEVEL_IMPERSONATE,
                    IntPtr.Zero,
                    EOAC_NONE,
                    IntPtr.Zero);
                // RPC_E_TOO_LATE (0x80010119) means another call already set security — not fatal.
                if (hr != WBEM_S_NO_ERROR && hr != RPC_E_TOO_LATE)
                {
                    throw new InvalidOperationException(
                        $"WMI CoInitializeSecurity failed for PID {processId}. HRESULT: 0x{hr:X8}");
                }

                Guid clsid = CLSID_WbemLocator;
                Guid iid = IID_IWbemLocator;
                hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER, ref iid, out IWbemLocator locator);
                if (hr != WBEM_S_NO_ERROR)
                {
                    throw new InvalidOperationException(
                        $"WMI CoCreateInstance failed for PID {processId}. HRESULT: 0x{hr:X8}");
                }

                hr = locator.ConnectServer(
                    @"ROOT\CIMV2",
                    strUser: null, strPassword: null, strLocale: null,
                    lSecurityFlags: 0, strAuthority: null,
                    pCtx: IntPtr.Zero,
                    out IWbemServices services);
                if (hr != WBEM_S_NO_ERROR)
                {
                    throw new InvalidOperationException(
                        $"WMI ConnectServer failed for PID {processId}. HRESULT: 0x{hr:X8}");
                }

#if NET
                hr = CoSetProxyBlanket(
                    services,
                    RPC_C_AUTHN_WINNT,
                    RPC_C_AUTHZ_NONE,
                    IntPtr.Zero,
                    RPC_C_AUTHN_LEVEL_CALL,
                    RPC_C_IMP_LEVEL_IMPERSONATE,
                    IntPtr.Zero,
                    EOAC_NONE);
                if (hr != WBEM_S_NO_ERROR)
                {
                    throw new InvalidOperationException(
                        $"WMI CoSetProxyBlanket failed for PID {processId}. HRESULT: 0x{hr:X8}");
                }
#else
                // On .NET Framework, the RCW caches separate proxy stubs for IUnknown and
                // each specific COM interface. CoSetProxyBlanket must be called on BOTH
                // the IUnknown pointer AND the specific interface pointer, because the
                // blanket set on IUnknown doesn't propagate to the specific interface's proxy.
                SetProxyBlanketForNetFx<IWbemServices>(services);
#endif

                string query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId='{processId}'";
                hr = services.ExecQuery(
                    "WQL",
                    query,
                    WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
                    null,
                    out IEnumWbemClassObject enumerator);
                if (hr != WBEM_S_NO_ERROR)
                {
                    throw new InvalidOperationException(
                        $"WMI ExecQuery failed for PID {processId}. HRESULT: 0x{hr:X8}");
                }

#if !NET
                // The enumerator is a separate COM proxy that also needs its security blanket set.
                SetProxyBlanketForNetFx<IEnumWbemClassObject>(enumerator);
#endif

                hr = enumerator.Next(WBEM_INFINITE, 1, out IWbemClassObject obj, out uint returned);
                if (hr == WBEM_S_FALSE || returned == 0)
                {
                    // No matching process found.
                    return null;
                }
                if (hr != WBEM_S_NO_ERROR)
                {
                    throw new InvalidOperationException(
                        $"WMI IEnumWbemClassObject.Next failed for PID {processId}. HRESULT: 0x{hr:X8}");
                }

                object val = null!;
                hr = obj.Get("CommandLine", 0, ref val, IntPtr.Zero, IntPtr.Zero);
                if (hr != WBEM_S_NO_ERROR)
                {
                    throw new InvalidOperationException(
                        $"WMI IWbemClassObject.Get(\"CommandLine\") failed for PID {processId}. HRESULT: 0x{hr:X8}");
                }

                return val as string;
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
