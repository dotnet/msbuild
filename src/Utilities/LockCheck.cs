// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WINDOWSINTEROP
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.RestartManager;
#endif

#nullable disable

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This class implements checking what processes are locking a file on Windows.
    /// It uses the Restart Manager API to do this. Other platforms are skipped.
    /// Use the method <see cref="GetLockedFileMessage"/> to get a message to inform the user which processes have a lock on a given file.
    /// </summary>
    public static class LockCheck
    {
#if FEATURE_WINDOWSINTEROP
        private static readonly int RM_SESSION_KEY_LEN = Guid.Empty.ToByteArray().Length; // 16-byte
        private static readonly int CCH_RM_SESSION_KEY = RM_SESSION_KEY_LEN * 2;

        internal class ProcessInfo
        {
            internal ProcessInfo(RM_PROCESS_INFO processInfo)
            {
                ProcessId = (int)processInfo.Process.dwProcessId;
                // ProcessStartTime is returned as local time, not UTC.
                StartTime = processInfo.Process.ProcessStartTime.ToDateTime();
                ApplicationName = processInfo.strAppName.ToString();
                ServiceShortName = processInfo.strServiceShortName.ToString();
                ApplicationType = processInfo.ApplicationType;
                ApplicationStatus = processInfo.AppStatus;
                Restartable = processInfo.bRestartable;
                TerminalServicesSessionId = (int)processInfo.TSSessionId;
            }

            public int ProcessId { get; }
            public DateTime StartTime { get; }
            public string ApplicationName { get; }
            public string ServiceShortName { get; }
            public RM_APP_TYPE ApplicationType { get; }
            public RM_APP_STATUS ApplicationStatus { get; }
            public int TerminalServicesSessionId { get; }
            public bool Restartable { get; }

            public override int GetHashCode()
            {
                var h1 = ProcessId.GetHashCode();
                var h2 = StartTime.GetHashCode();
                return ((h1 << 5) + h1) ^ h2;
            }

            public override bool Equals(object obj)
            {
                if (obj is ProcessInfo other)
                {
                    return other.ProcessId == ProcessId && other.StartTime == StartTime;
                }
                return false;
            }

            public override string ToString()
            {
                return $"{ProcessId}@{StartTime:s}";
            }
        }
#endif

#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows6.0.6000")]
        internal static string GetProcessesLockingFile(string filePath)
        {
            return string.Join(", ", GetLockingProcessInfos(filePath).Select(p => $"{p.ApplicationName} ({p.ProcessId})"));
        }
#endif

        /// <summary>
        /// Try to get a message to inform the user which processes have a lock on a given file. On Windows it uses the Restart Manager API.
        /// </summary>
        /// <param name="filePath">The path of the file to check.</param>
        /// <returns>A message to inform the user which processes have a lock on the file if known, <see cref="string.Empty"/> otherwise. Always returns <see cref="string.Empty"/> on operating systems other than Windows.</returns>
        public static string GetLockedFileMessage(string filePath)
        {
#if FEATURE_WINDOWSINTEROP
#if NET
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
#else
            if (NativeMethodsShared.IsWindows)
#endif
            {
                return GetLockedFileMessageWindows(filePath);
            }
#endif
            return string.Empty;
        }

#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows6.0.6000")]
        private static string GetLockedFileMessageWindows(string filePath)
        {
            string message = string.Empty;

            try
            {
                var processes = GetProcessesLockingFile(filePath);
                message = !string.IsNullOrEmpty(processes)
                    ? ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("LockCheck.FileLocked", processes)
                    : String.Empty;
            }
            catch (Exception)
            {
                // Never throw if we can't get the processes locking the file.
            }

            return message;
        }

        [SupportedOSPlatform("windows6.0.6000")]
        internal static IEnumerable<ProcessInfo> GetLockingProcessInfos(params string[] paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            const int maxRetries = 6;
            uint handle;
            WIN32_ERROR res;

            unsafe
            {
                // See http://blogs.msdn.com/b/oldnewthing/archive/2012/02/17/10268840.aspx.
                Span<char> key = stackalloc char[CCH_RM_SESSION_KEY + 1];
                res = PInvoke.RmStartSession(out handle, key);
            }

            if (res != 0)
            {
                throw GetException(res, "RmStartSession", "Failed to begin restart manager session.");
            }

            try
            {
                string[] resources = paths;
                res = PInvoke.RmRegisterResources(handle, resources, default(ReadOnlySpan<RM_UNIQUE_PROCESS>), default(ReadOnlySpan<string>));
                if (res != 0)
                {
                    throw GetException(res, "RmRegisterResources", "Could not register resources.");
                }

                //
                // Obtain the list of affected applications/services.
                //
                // NOTE: Restart Manager returns the results into the buffer allocated by the caller. The first call to
                // RmGetList() will return the size of the buffer (i.e. nProcInfoNeeded) the caller needs to allocate.
                // The caller then needs to allocate the buffer (i.e. rgAffectedApps) and make another RmGetList()
                // call to ask Restart Manager to write the results into the buffer. However, since Restart Manager
                // refreshes the list every time RmGetList()is called, it is possible that the size returned by the first
                // RmGetList()call is not sufficient to hold the results discovered by the second RmGetList() call. Therefore,
                // it is recommended that the caller follows the following practice to handle this race condition:
                //
                //    Use a loop to call RmGetList() in case the buffer allocated according to the size returned in previous
                //    call is not enough.
                //
                uint pnProcInfo = 0;
                RM_PROCESS_INFO[] rgAffectedApps = null;
                int retry = 0;
                do
                {
                    uint pnProcInfoNeeded;
                    uint lpdwRebootReasons;
                    unsafe
                    {
                        fixed (RM_PROCESS_INFO* pAffected = rgAffectedApps)
                        {
                            res = PInvoke.RmGetList(handle, &pnProcInfoNeeded, &pnProcInfo, pAffected, &lpdwRebootReasons);
                        }
                    }
                    if (res == 0)
                    {
                        // If pnProcInfo == 0, then there is simply no locking process (found), in this case rgAffectedApps is "null".
                        if (pnProcInfo == 0)
                        {
                            return [];
                        }

                        var lockInfos = new List<ProcessInfo>((int)pnProcInfo);
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            lockInfos.Add(new ProcessInfo(rgAffectedApps[i]));
                        }
                        return lockInfos;
                    }

                    if (res != WIN32_ERROR.ERROR_MORE_DATA)
                    {
                        throw GetException(res, "RmGetList", $"Failed to get entries (retry {retry}).");
                    }

                    pnProcInfo = pnProcInfoNeeded;
                    rgAffectedApps = new RM_PROCESS_INFO[pnProcInfo];
                } while ((res == WIN32_ERROR.ERROR_MORE_DATA) && (retry++ < maxRetries));
            }
            finally
            {
                res = PInvoke.RmEndSession(handle);
                if (res != 0)
                {
                    throw GetException(res, "RmEndSession", "Failed to end the restart manager session.");
                }
            }

            return [];
        }

        private static Exception GetException(WIN32_ERROR res, string apiName, string message)
        {
            string reason = res switch
            {
                WIN32_ERROR.ERROR_ACCESS_DENIED => "Access is denied.",
                WIN32_ERROR.ERROR_SEM_TIMEOUT => "A Restart Manager function could not obtain a Registry write mutex in the allotted time. " +
   "A system restart is recommended because further use of the Restart Manager is likely to fail.",
                WIN32_ERROR.ERROR_BAD_ARGUMENTS => "One or more arguments are not correct. This error value is returned by the Restart Manager " +
"function if a NULL pointer or 0 is passed in a parameter that requires a non-null and non-zero value.",
                WIN32_ERROR.ERROR_MAX_SESSIONS_REACHED => "The maximum number of sessions has been reached.",
                WIN32_ERROR.ERROR_WRITE_FAULT => "An operation was unable to read or write to the registry.",
                WIN32_ERROR.ERROR_OUTOFMEMORY => "A Restart Manager operation could not complete because not enough memory was available.",
                WIN32_ERROR.ERROR_CANCELLED => "The current operation is canceled by user.",
                WIN32_ERROR.ERROR_MORE_DATA => "More data is available.",
                WIN32_ERROR.ERROR_INVALID_HANDLE => "No Restart Manager session exists for the handle supplied.",
                _ => $"0x{(uint)res:x8}",
            };
            throw new Win32Exception((int)res, $"{message} ({apiName}() error {(uint)res}: {reason})");
        }
#endif
    }
}
