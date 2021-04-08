// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Shouldly;
using Xunit;

using Microsoft.Build.Shared;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit.Abstractions;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using System.Management;
using System.Linq;

namespace Microsoft.Build.UnitTests
{
    public class ProcessExtensions_Tests
    {
        private readonly ITestOutputHelper output;

        public ProcessExtensions_Tests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task KillTree()
        {
            Process p = Process.Start("sleep", "600"); // sleep 10m.

#if NET472
            Print(p);
#endif

            // Verify the process is running.
            await Task.Delay(500);
            p.HasExited.ShouldBe(false);

            // Kill the process.
            p.KillTree(timeout: 5000);
            p.HasExited.ShouldBe(true);
            p.ExitCode.ShouldNotBe(0);
        }

        private void Print(Process p)
        {
            var processes = ProcessInformation.GetProcesses();
            var found = processes.Where(process => process.Id == p.Id).First();
            output.WriteLine(found.ExecutablePath);
        }
    }

#if NET472
    public class ProcessInformation
    {
        public int Id { get; private set; }
        public int ParentId { get; set; }
        public string ProcessName { get; private set; }
        public string CommandLine { get; private set; }
        public string ExecutablePath { get; set; }
        public string MainWindowTitle { get; private set; }
        public DateTime CreationDate { get; private set; }
        public bool? Is64Bit { get; set; }

        public static Process CurrentProcess = Process.GetCurrentProcess();
        public static int CurrentProcessId = CurrentProcess.Id;

        public static IEnumerable<ProcessInformation> GetProcesses()
        {
            var list = new List<ProcessInformation>();

            var managementClass = new ManagementClass("Win32_Process");

            foreach (var process in managementClass.GetInstances())
            {
                var creationDate = ManagementDateTimeConverter.ToDateTime(process["CreationDate"].ToString());
                var processInfo = new ProcessInformation();
                int id = (int)(uint)process["ProcessId"];
                processInfo.Id = id;
                processInfo.ParentId = Convert.ToInt32(process["ParentProcessId"]);
                processInfo.ProcessName = process["Name"]?.ToString();
                processInfo.CommandLine = process["CommandLine"]?.ToString();
                processInfo.ExecutablePath = process["ExecutablePath"]?.ToString();
                processInfo.MainWindowTitle = process["Caption"]?.ToString();
                processInfo.CreationDate = creationDate;

                try
                {
                    var is64Bit = Is64BitProcess(id);
                    processInfo.Is64Bit = is64Bit;
                }
                catch
                {
                }

                list.Add(processInfo);
            }

            return list;
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process([In] IntPtr processHandle, [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        public static extern SafeProcessHandle OpenProcess(int access, bool inherit, int processId);

        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int SYNCHRONIZE = 0x00100000;

        public static bool? Is64BitProcess(int id)
        {
            if (!Environment.Is64BitOperatingSystem)
            {
                return false;
            }

            if (id == 0 || id == 4)
            {
                return null;
            }

            using var handle = OpenProcess(PROCESS_QUERY_INFORMATION, false, id);
            if (handle.IsInvalid)
            {
                return null;
            }

            if (!IsWow64Process(handle.DangerousGetHandle(), out bool isWow64Process))
            {
                return null;
            }

            return !isWow64Process;
        }
    }
#endif
}
