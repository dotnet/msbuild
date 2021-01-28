// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Shared
{
    internal static class ProcessExtensions
    {
        public static void KillTree(this Process process, int timeout)
        {
            if (NativeMethodsShared.IsWindows)
            {
                try
                {
                    // issue the kill command
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
                var children = new HashSet<int>();
                GetAllChildIdsUnix(process.Id, children);
                foreach (var childId in children)
                {
                    KillProcessUnix(childId);
                }

                KillProcessUnix(process.Id);
            }

            // wait until the process finishes exiting/getting killed. 
            // We don't want to wait forever here because the task is already supposed to be dieing, we just want to give it long enough
            // to try and flush what it can and stop. If it cannot do that in a reasonable time frame then we will just ignore it.
            process.WaitForExit(timeout);
        }

        private static void GetAllChildIdsUnix(int parentId, ISet<int> children)
        {
            var exitCode = RunProcessAndWaitForExit(
                "pgrep",
                $"-P {parentId}",
                out string stdout);

            if (exitCode == 0 && !string.IsNullOrEmpty(stdout))
            {
                using (var reader = new StringReader(stdout))
                {
                    while (true)
                    {
                        var text = reader.ReadLine();
                        if (text == null)
                        {
                            return;
                        }

                        int id;
                        if (int.TryParse(text, out id))
                        {
                            children.Add(id);
                            // Recursively get the children
                            GetAllChildIdsUnix(id, children);
                        }
                    }
                }
            }
        }

        private static void KillProcessUnix(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                process.Kill();
            }
            catch (ArgumentException)
            {
                // Process already terminated.
                return;
            }
            catch (InvalidOperationException)
            {
                // Process already terminated.
                return;
            }
        }

        private static int RunProcessAndWaitForExit(string fileName, string arguments, out string stdout)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var process = Process.Start(startInfo);

            stdout = null;
            if (process.WaitForExit((int) TimeSpan.FromSeconds(30).TotalMilliseconds))
            {
                stdout = process.StandardOutput.ReadToEnd();
            }
            else
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                { }

                // Kill is asynchronous so we should still wait a little
                //
                process.WaitForExit((int) TimeSpan.FromSeconds(1).TotalMilliseconds);
            }

            return process.HasExited ? process.ExitCode : -1;
        }
    }
}
