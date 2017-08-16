using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.EndToEnd
{
    public class ProcessWrapper
    {
        public int? RunProcess(string fileName, string arguments, string workingDirectory, out int? processId, bool createDirectoryIfNotExists = true, bool waitForExit = true)
        {
            if (createDirectoryIfNotExists && !Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = false;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.FileName = fileName;
            if (!string.IsNullOrEmpty(arguments))
            {
                startInfo.Arguments = arguments;
            }

            Process testProcess = Process.Start(startInfo);
            processId = testProcess?.Id;
            if (waitForExit)
            {
                testProcess.WaitForExit();
                return testProcess?.ExitCode;
            }

            return -1;
        }

        public static void KillProcessTree(int processId)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                if (process != null && !process.HasExited)
                {
                    KillProcessTreeInternal(processId);
                }
            }
            catch (Exception)
            {
            }

        }

        private static void KillProcessTreeInternal(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessTreeInternal(Convert.ToInt32(mo["ProcessID"]));
            }

            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // vramak: Process might have already exited.
            }
        }
    }
}
