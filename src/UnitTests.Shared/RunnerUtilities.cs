using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.Shared
{
    public static class RunnerUtilities
    {
        public static string PathToCurrentlyRunningMsBuildExe => BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

        /// <summary>
        /// Invoke the currently running msbuild and return the stdout, stderr, and process exit status.
        /// This method may invoke msbuild via other runtimes.
        /// </summary>
        public static string ExecMSBuild(string msbuildParameters, out bool successfulExit, ITestOutputHelper outputHelper = null)
        {
            return ExecMSBuild(PathToCurrentlyRunningMsBuildExe, msbuildParameters, out successfulExit, false, outputHelper);
        }

        /// <summary>
        /// Invoke msbuild.exe with the given parameters and return the stdout, stderr, and process exit status.
        /// This method may invoke msbuild via other runtimes.
        /// </summary>
        public static string ExecMSBuild(string pathToMsBuildExe, string msbuildParameters, out bool successfulExit, bool shellExecute = false, ITestOutputHelper outputHelper = null)
        {
#if FEATURE_RUN_EXE_IN_TESTS
            var pathToExecutable = pathToMsBuildExe;
#else
            var pathToExecutable = ResolveRuntimeExecutableName();
            msbuildParameters = "\"" + pathToMsBuildExe + "\"" + " " + msbuildParameters;
#endif

            return RunProcessAndGetOutput(pathToExecutable, msbuildParameters, out successfulExit, shellExecute, outputHelper);
        }

        private static void AdjustForShellExecution(ref string pathToExecutable, ref string arguments)
        {
            if (NativeMethodsShared.IsWindows)
            {
                var comSpec = Environment.GetEnvironmentVariable("ComSpec");

                // /D: Do not load AutoRun configuration from the registry (perf)
                arguments = $"{(Traits.Instance.EscapeHatches.UseAutoRunWhenLaunchingProcessUnderCmd ? String.Empty : "/D ")}/C \"{pathToExecutable} {arguments}\"";
                pathToExecutable = comSpec;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

#if !FEATURE_RUN_EXE_IN_TESTS
        /// <summary>
        /// Resolve the platform specific path to the runtime executable that msbuild.exe needs to be run in (unix-mono, {unix, windows}-corerun).
        /// </summary>
        private static string ResolveRuntimeExecutableName()
        {
            // Run the child process with the same host as the currently-running process.
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                return currentProcess.MainModule.FileName;
            }
        }
#endif

        /// <summary>
        /// Run the process and get stdout and stderr
        /// </summary>
        public static string RunProcessAndGetOutput(string process, string parameters, out bool successfulExit, bool shellExecute = false, ITestOutputHelper outputHelper = null)
        {
            if (shellExecute)
            {
                // we adjust the psi data manually because on net core using ProcessStartInfo.UseShellExecute throws NotImplementedException
                AdjustForShellExecution(ref process, ref parameters);
            }

            var psi = new ProcessStartInfo(process)
            {
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                Arguments = parameters
            };
            var output = string.Empty;

            using (var p = new Process { EnableRaisingEvents = true, StartInfo = psi })
            {
                p.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args)
                {
                    if (args != null)
                    {
                        output += args.Data + "\r\n";
                    }
                };

                p.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs args)
                {
                    if (args != null)
                    {
                        output += args.Data + "\r\n";
                    }
                };

                outputHelper?.WriteLine("Executing [{0} {1}]", process, parameters);
                Console.WriteLine("Executing [{0} {1}]", process, parameters);

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.StandardInput.Dispose();
                p.WaitForExit();

                successfulExit = p.ExitCode == 0;
            }

            outputHelper?.WriteLine("==== OUTPUT ====");
            outputHelper?.WriteLine(output);
            outputHelper?.WriteLine("==============");

            Console.WriteLine("==== OUTPUT ====");
            Console.WriteLine(output);
            Console.WriteLine("==============");

            return output;
        }
    }
}
