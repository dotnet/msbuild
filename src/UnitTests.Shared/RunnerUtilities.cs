// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests.Shared
{
    public static class RunnerUtilities
    {
        public static string PathToCurrentlyRunningMsBuildExe => BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

        public static ArtifactsLocationAttribute ArtifactsLocationAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<ArtifactsLocationAttribute>()
                                                   ?? throw new InvalidOperationException("This test assembly does not have the ArtifactsLocationAttribute");

        public static string BootstrapMsBuildBinaryLocation => BootstrapLocationAttribute.BootstrapMsBuildBinaryLocation;

        public static string BootstrapSdkVersion => BootstrapLocationAttribute.BootstrapSdkVersion;

        public static string BootstrapRootPath => BootstrapLocationAttribute.BootstrapRoot;

        public static string LatestDotNetCoreForMSBuild => BootstrapLocationAttribute.LatestDotNetCoreForMSBuild;

        internal static BootstrapLocationAttribute BootstrapLocationAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<BootstrapLocationAttribute>()
                                           ?? throw new InvalidOperationException("This test assembly does not have the BootstrapLocationAttribute");

#if !FEATURE_RUN_EXE_IN_TESTS
        private static readonly string s_dotnetExePath = EnvironmentProvider.GetDotnetExePath();

        public static void ApplyDotnetHostPathEnvironmentVariable(TestEnvironment testEnvironment)
        {
            // Built msbuild.dll executed by dotnet.exe needs this environment variable for msbuild tasks such as RoslynCodeTaskFactory.
            testEnvironment.SetEnvironmentVariable("DOTNET_HOST_PATH", s_dotnetExePath);
        }

        /// <summary>
        /// Checks if the given file is a native executable (app host) rather than a managed .NET assembly.
        /// </summary>
        private static bool IsNativeExecutable(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                using var peReader = new PEReader(stream);

                // If it's not a valid PE file, it's not a native exe we care about
                if (!peReader.HasMetadata)
                {
                    // No .NET metadata means it's a native executable (like an app host)
                    return true;
                }

                // Has .NET metadata, so it's a managed assembly
                return false;
            }
            catch
            {
                // If we can't read it, assume it's not a native exe
                return false;
            }
        }
#endif

        /// <summary>
        /// Gets the base path to the MSBuild executable without extension.
        /// Useful for checking command line output which may log either the EXE or DLL path.
        /// </summary>
        public static string MSBuildBasePath => Path.Combine(
            Path.GetDirectoryName(PathToCurrentlyRunningMsBuildExe)!,
            Path.GetFileNameWithoutExtension(PathToCurrentlyRunningMsBuildExe));

        /// <summary>
        /// Gets the executable path and arguments needed to run MSBuild with the given parameters.
        /// Handles both native app host and managed assembly cases.
        /// </summary>
        /// <param name="msbuildArgs">The arguments to pass to MSBuild.</param>
        /// <returns>A tuple of (executablePath, arguments) suitable for Process.StartInfo.</returns>
        public static (string Executable, string Arguments) GetMSBuildExeAndArgs(string msbuildArgs) =>
#if FEATURE_RUN_EXE_IN_TESTS
            (PathToCurrentlyRunningMsBuildExe, msbuildArgs);
#else
            // Native app host can be run directly
            IsNativeExecutable(PathToCurrentlyRunningMsBuildExe)
                ? (PathToCurrentlyRunningMsBuildExe, msbuildArgs)
                : (s_dotnetExePath, FileUtilities.EnsureDoubleQuotes(PathToCurrentlyRunningMsBuildExe) + " " + msbuildArgs);
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
            // Strip quotes from the path before checking if it's a native executable
            string unquotedPath = pathToMsBuildExe.Trim('"');

            // Check if the MSBuild path is a native app host (e.g., MSBuild.exe created with UseAppHost=true)
            // If so, run it directly. Otherwise, use dotnet exec for managed assemblies.
            string pathToExecutable;
            if (IsNativeExecutable(unquotedPath))
            {
                // Native app host - run directly
                pathToExecutable = pathToMsBuildExe;
            }
            else
            {
                // Managed assembly - use dotnet exec
                pathToExecutable = s_dotnetExePath;
                msbuildParameters = FileUtilities.EnsureDoubleQuotes(unquotedPath) + " " + msbuildParameters;
            }
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
#if NET
            string pathToExecutable = EnvironmentProvider.GetDotnetExePathFromFolder(BootstrapMsBuildBinaryLocation);
            msbuildParameters = Path.Combine(BootstrapMsBuildBinaryLocation, "sdk", BootstrapLocationAttribute.BootstrapSdkVersion, Constants.MSBuildAssemblyName) + " " + msbuildParameters;
#else
            string pathToExecutable = Path.Combine(BootstrapMsBuildBinaryLocation, Constants.MSBuildExecutableName);
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
