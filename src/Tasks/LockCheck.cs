// Taken from https://github.com/cklutz/LockCheck, MIT license.
// Copyright (C) Christian Klutz

#if !RUNTIME_TYPE_NETCORE && !MONO

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Build.Tasks
{
    internal class LockCheck
    {
        [Flags]
        internal enum ApplicationStatus
        {
            // Members must have the same values as in NativeMethods.RM_APP_STATUS
            Unknown = 0x0,
            Running = 0x1,
            Stopped = 0x2,
            StoppedOther = 0x4,
            Restarted = 0x8,
            ErrorOnStop = 0x10,
            ErrorOnRestart = 0x20,
            ShutdownMasked = 0x40,
            RestartMasked = 0x80
        }

        internal enum ApplicationType
        {
            // Members must have the same values as in NativeMethods.RM_APP_TYPE

            Unknown = 0,
            MainWindow = 1,
            OtherWindow = 2,
            Service = 3,
            Explorer = 4,
            Console = 5,
            Critical = 1000
        }

        private const string RestartManagerDll = "rstrtmgr.dll";

        [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint pSessionHandle,
            uint nFiles,
            string[] rgsFilenames,
            uint nApplications,
            [In] RM_UNIQUE_PROCESS[] rgApplications,
            uint nServices,
            string[] rgsServiceNames);

        [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle,
            int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport(RestartManagerDll)]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
        public static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
            ref uint lpdwRebootReasons);

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RM_UNIQUE_PROCESS
        {
            public uint dwProcessId;
            public FILETIME ProcessStartTime;
        }

        const int RM_INVALID_SESSION = -1;
        const int RM_INVALID_PROCESS = -1;
        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;
        const int ERROR_SEM_TIMEOUT = 121;
        const int ERROR_BAD_ARGUMENTS = 160;
        const int ERROR_MAX_SESSIONS_REACHED = 353;
        const int ERROR_WRITE_FAULT = 29;
        const int ERROR_OUTOFMEMORY = 14;
        const int ERROR_MORE_DATA = 234;
        const int ERROR_ACCESS_DENIED = 5;
        const int ERROR_INVALID_HANDLE = 6;
        const int ERROR_CANCELLED = 1223;

        static readonly int RM_SESSION_KEY_LEN = Guid.Empty.ToByteArray().Length; // 16-byte
        static readonly int CCH_RM_SESSION_KEY = RM_SESSION_KEY_LEN * 2;

        internal enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        enum RM_APP_STATUS
        {
            RmStatusUnknown = 0x0,
            RmStatusRunning = 0x1,
            RmStatusStopped = 0x2,
            RmStatusStoppedOther = 0x4,
            RmStatusRestarted = 0x8,
            RmStatusErrorOnStop = 0x10,
            RmStatusErrorOnRestart = 0x20,
            RmStatusShutdownMasked = 0x40,
            RmStatusRestartMasked = 0x80
        }

        enum RM_REBOOT_REASON
        {
            RmRebootReasonNone = 0x0,
            RmRebootReasonPermissionDenied = 0x1,
            RmRebootReasonSessionMismatch = 0x2,
            RmRebootReasonCriticalProcess = 0x4,
            RmRebootReasonCriticalService = 0x8,
            RmRebootReasonDetectedSelf = 0x10
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct RM_PROCESS_INFO
        {
            internal RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            internal RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        internal class ProcessInfo
        {
            internal ProcessInfo(RM_PROCESS_INFO processInfo)
            {
                ProcessId = (int)processInfo.Process.dwProcessId;
                // ProcessStartTime is returned as local time, not UTC.
                StartTime = DateTime.FromFileTime((((long)processInfo.Process.ProcessStartTime.dwHighDateTime) << 32) |
                                                  processInfo.Process.ProcessStartTime.dwLowDateTime);
                ApplicationName = processInfo.strAppName;
                ServiceShortName = processInfo.strServiceShortName;
                ApplicationType = (ApplicationType)processInfo.ApplicationType;
                ApplicationStatus = (ApplicationStatus)processInfo.AppStatus;
                Restartable = processInfo.bRestartable;
                TerminalServicesSessionId = (int)processInfo.TSSessionId;
            }

            public int ProcessId { get; }
            public DateTime StartTime { get; }
            public string ApplicationName { get; }
            public string ServiceShortName { get; }
            public ApplicationType ApplicationType { get; }
            public ApplicationStatus ApplicationStatus { get; }
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
                return ProcessId + "@" + StartTime.ToString("s");
            }
        }

        internal static string GetProcessesLockingFile(string filePath)
        {
            return string.Join(", ", GetLockingProcessInfos(filePath).Select(p => $"{p.ApplicationName} ({p.ProcessId})"));
        }

        internal static IEnumerable<ProcessInfo> GetLockingProcessInfos(params string[] paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            const int maxRetries = 6;

            // See http://blogs.msdn.com/b/oldnewthing/archive/2012/02/17/10268840.aspx.
            var key = new StringBuilder(new string('\0', CCH_RM_SESSION_KEY + 1));

            int res = RmStartSession(out uint handle, 0, key);
            if (res != 0)
            {
                throw GetException(res, "RmStartSession", "Failed to begin restart manager session.");
            }

            try
            {
                string[] resources = paths;
                res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);
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
                    uint lpdwRebootReasons = (uint)RM_REBOOT_REASON.RmRebootReasonNone;
                    res = RmGetList(handle, out uint pnProcInfoNeeded, ref pnProcInfo, rgAffectedApps, ref lpdwRebootReasons);
                    if (res == 0)
                    {
                        // If pnProcInfo == 0, then there is simply no locking process (found), in this case rgAffectedApps is "null".
                        if (pnProcInfo == 0)
                        {
                            return Enumerable.Empty<ProcessInfo>();
                        }

                        var lockInfos = new List<ProcessInfo>((int)pnProcInfo);
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            lockInfos.Add(new ProcessInfo(rgAffectedApps[i]));
                        }
                        return lockInfos;
                    }

                    if (res != ERROR_MORE_DATA)
                    {
                        throw GetException(res, "RmGetList", $"Failed to get entries (retry {retry}).");
                    }

                    pnProcInfo = pnProcInfoNeeded;
                    rgAffectedApps = new RM_PROCESS_INFO[pnProcInfo];
                } while ((res == ERROR_MORE_DATA) && (retry++ < maxRetries));
            }
            finally
            {
                res = RmEndSession(handle);
                if (res != 0)
                    throw GetException(res, "RmEndSession", "Failed to end the restart manager session.");
            }

            return Enumerable.Empty<ProcessInfo>();
        }

        private static Exception GetException(int res, string apiName, string message)
        {
            string reason;
            switch (res)
            {
                case ERROR_ACCESS_DENIED:
                    reason = "Access is denied.";
                    break;
                case ERROR_SEM_TIMEOUT:
                    reason = "A Restart Manager function could not obtain a Registry write mutex in the allotted time. " +
                             "A system restart is recommended because further use of the Restart Manager is likely to fail.";
                    break;
                case ERROR_BAD_ARGUMENTS:
                    reason = "One or more arguments are not correct. This error value is returned by the Restart Manager " +
                             "function if a NULL pointer or 0 is passed in a parameter that requires a non-null and non-zero value.";
                    break;
                case ERROR_MAX_SESSIONS_REACHED:
                    reason = "The maximum number of sessions has been reached.";
                    break;
                case ERROR_WRITE_FAULT:
                    reason = "An operation was unable to read or write to the registry.";
                    break;
                case ERROR_OUTOFMEMORY:
                    reason = "A Restart Manager operation could not complete because not enough memory was available.";
                    break;
                case ERROR_CANCELLED:
                    reason = "The current operation is canceled by user.";
                    break;
                case ERROR_MORE_DATA:
                    reason = "More data is available.";
                    break;
                case ERROR_INVALID_HANDLE:
                    reason = "No Restart Manager session exists for the handle supplied.";
                    break;
                default:
                    reason = $"0x{res:x8}";
                    break;
            }

            throw new Win32Exception(res, $"{message} ({apiName}() error {res}: {reason})");
        }
    }
}

#endif
