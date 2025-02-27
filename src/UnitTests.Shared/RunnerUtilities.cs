// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Shared;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Xunit.Abstractions;
using System.Linq;

#nullable disable

namespace Microsoft.Build.UnitTests.Shared
{
    public static class RunnerUtilities
    {
        public static string PathToCurrentlyRunningMsBuildExe => BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

        public static ArtifactsLocationAttribute ArtifactsLocationAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<ArtifactsLocationAttribute>()
                                                   ?? throw new InvalidOperationException("This test assembly does not have the ArtifactsLocationAttribute");
#if !FEATURE_RUN_EXE_IN_TESTS
        private static readonly string s_dotnetExePath = EnvironmentProvider.GetDotnetExePath();

        public static void ApplyDotnetHostPathEnvironmentVariable(TestEnvironment testEnvironment)
        {
            // Built msbuild.dll executed by dotnet.exe needs this environment variable for msbuild tasks such as RoslynCodeTaskFactory.
            testEnvironment.SetEnvironmentVariable("DOTNET_HOST_PATH", s_dotnetExePath);
        }
#endif

        /// <summary>
        /// Invoke the currently running msbuild and return the stdout, stderr, and process exit status.
        /// This method may invoke msbuild via other runtimes.
        /// </summary>
        public static string ExecMSBuild(string msbuildParameters, out bool successfulExit, ITestOutputHelper outputHelper = null)
        {
            return ExecMSBuild(PathToCurrentlyRunningMsBuildExe, msbuildParameters, out successfulExit, outputHelper: outputHelper);
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
            var pathToExecutable = s_dotnetExePath;
            msbuildParameters = FileUtilities.EnsureDoubleQuotes(pathToMsBuildExe) + " " + msbuildParameters;
#endif

            return RunProcessAndGetOutput(pathToExecutable, msbuildParameters, out successfulExit, shellExecute, outputHelper);
        }

        public static string ExecBootstrapedMSBuild(
            string msbuildParameters,
            out bool successfulExit,
            bool shellExecute = false,
            ITestOutputHelper outputHelper = null,
            bool attachProcessId = true,
            int timeoutMilliseconds = 30_000)
        {
            BootstrapLocationAttribute attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<BootstrapLocationAttribute>()
                                                   ?? throw new InvalidOperationException("This test assembly does not have the BootstrapLocationAttribute");

            string binaryFolder = attribute.BootstrapMsBuildBinaryLocation;
#if NET
            string pathToExecutable = EnvironmentProvider.GetDotnetExePathFromFolder(binaryFolder);
            msbuildParameters = Path.Combine(binaryFolder, "sdk", attribute.BootstrapSdkVersion, "MSBuild.dll") + " " + msbuildParameters;
#else
            string pathToExecutable = Path.Combine(binaryFolder, "MSBuild.exe");
#endif
            return RunProcessAndGetOutput(pathToExecutable, msbuildParameters, out successfulExit, shellExecute, outputHelper, attachProcessId, timeoutMilliseconds);
        }

        private static void AdjustForShellExecution(ref string pathToExecutable, ref string arguments)
        {
            if (NativeMethodsShared.IsWindows)
            {
                var comSpec = Environment.GetEnvironmentVariable("ComSpec");

                // /D: Do not load AutoRun configuration from the registry (perf)
                arguments = $"/D /C \"{pathToExecutable} {arguments}\"";
                pathToExecutable = comSpec;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Run the process and get stdout and stderr.
        /// </summary>
        public static string RunProcessAndGetOutput(
            string process,
            string parameters,
            out bool successfulExit,
            bool shellExecute = false,
            ITestOutputHelper outputHelper = null,
            bool attachProcessId = true,
            int timeoutMilliseconds = 30_000)
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
            string output = string.Empty;
            int pid = -1;

            using (var p = new Process { EnableRaisingEvents = true, StartInfo = psi })
            {
                DataReceivedEventHandler handler = delegate (object sender, DataReceivedEventArgs args)
                {
                    if (args != null && args.Data != null)
                    {
                        WriteOutput(args.Data);
                        output += args.Data + "\r\n";
                    }
                };

                p.OutputDataReceived += handler;
                p.ErrorDataReceived += handler;

                WriteOutput($"Executing [{process} {parameters}]");
                WriteOutput("==== OUTPUT ====");
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.StandardInput.Dispose();

                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                if (Traits.Instance.DebugUnitTests)
                {
                    p.WaitForExit();
                }
                else if (!p.WaitForExit(timeoutMilliseconds))
                {
                    // Let's not create a unit test for which we need more than requested timeout to execute.
                    // Please consider carefully if you would like to increase the timeout.
                    p.KillTree(1000);
                    throw new TimeoutException($"Test failed due to timeout: process {p.Id} is active for more than {timeout.TotalSeconds} sec.");
                }

                // We need the WaitForExit call without parameters because our processing of output/error streams is not synchronous.
                // See https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-6.0#system-diagnostics-process-waitforexit(system-int32).
                // The overload WaitForExit() waits for the error and output to be handled. The WaitForExit(int timeout) overload does not, so we could lose the data.
                p.WaitForExit();

                pid = p.Id;
                successfulExit = p.ExitCode == 0;
            }

            if (attachProcessId)
            {
                output += "Process ID is " + pid + "\r\n";
                WriteOutput("Process ID is " + pid + "\r\n");
                WriteOutput("==============");
            }

            return output;

            void WriteOutput(string data)
            {
                outputHelper?.WriteLine(data);
                Console.WriteLine(data);
            }
        }
    }
}
