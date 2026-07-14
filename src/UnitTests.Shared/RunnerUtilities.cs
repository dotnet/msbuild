// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Xunit;
using Constants = Microsoft.Build.Framework.Constants;

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

        public static string BootstrapMSBuildExecutablePath
#if NET
            => Path.Combine(BootstrapMsBuildBinaryLocation, "sdk", BootstrapLocationAttribute.BootstrapSdkVersion, Constants.MSBuildExecutableName);
#else
            => Path.Combine(BootstrapMsBuildBinaryLocation, Constants.MSBuildExecutableName);
#endif

        public static string BootstrapMSBuildCommand
#if NET
            => NativeMethodsShared.IsWindows
                ? $"\"{BootstrapMSBuildExecutablePath}\""
                : $"\"{s_dotnetExePath}\" \"{Path.Combine(BootstrapMsBuildBinaryLocation, "sdk", BootstrapLocationAttribute.BootstrapSdkVersion, Constants.MSBuildAssemblyName)}\"";
#else
            => $"\"{BootstrapMSBuildExecutablePath}\"";
#endif

        internal static BootstrapLocationAttribute BootstrapLocationAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<BootstrapLocationAttribute>()
                                           ?? throw new InvalidOperationException("This test assembly does not have the BootstrapLocationAttribute");

#if !FEATURE_RUN_EXE_IN_TESTS
        private static readonly string s_dotnetExePath = EnvironmentProvider.GetDotnetExePath();

        // The dotnet host that ships inside the bootstrap layout. When we launch the bootstrapped MSBuild we must
        // point DOTNET_HOST_PATH at this host (rather than an ambient dotnet on PATH such as the repo-local .dotnet)
        // so that any task hosts it spawns resolve their runtime from the bootstrap. The bootstrap can target an
        // earlier runtime (e.g. net10.0) than the SDK overlaid into .dotnet, so a task host resolving .dotnet would
        // fail to find its runtime. See eng/BootStrapMsBuild.targets.
        private static readonly string s_bootstrapDotnetHostPath = EnvironmentProvider.GetDotnetExePathFromFolder(BootstrapMsBuildBinaryLocation);

        // Architecture-specific DOTNET_ROOT_<ARCH> variables take precedence over DOTNET_ROOT for the .NET app host.
        // X86/X64/ARM64 cover every CI agent architecture; these are cleared when launching the bootstrapped MSBuild.
        private static readonly string[] s_archSpecificDotnetRootVars =
        [
            "DOTNET_ROOT_X86",
            "DOTNET_ROOT_X64",
            "DOTNET_ROOT_ARM64",
        ];

        public static void ApplyDotnetHostPathEnvironmentVariable(TestEnvironment testEnvironment)
        {
            // Built msbuild.dll executed by dotnet.exe needs this environment variable for msbuild tasks such as RoslynCodeTaskFactory.
            testEnvironment.SetEnvironmentVariable(Constants.DotnetHostPathEnvVarName, s_dotnetExePath);
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
            return RunProcessAndGetOutput(pathToMsBuildExe, msbuildParameters, out successfulExit, shellExecute, outputHelper, environmentVariables: GetMSBuildEnvironmentVariables(useBootstrapHost: false));
        }

        public static Task<(bool SuccessfulExit, string BuildOutput)> ExecBootstrappedMSBuildAsync(
            string msbuildParameters,
            bool shellExecute = false,
            ITestOutputHelper outputHelper = null,
            bool attachProcessId = true,
            int timeoutMilliseconds = 30_000)
            => Task.Run(() =>
            {
                string buildOutput = ExecBootstrapedMSBuild(msbuildParameters, out bool successfulExit, shellExecute, outputHelper, attachProcessId, timeoutMilliseconds);
                return (successfulExit, buildOutput);
            });

        public static string ExecBootstrapedMSBuild(
            string msbuildParameters,
            out bool successfulExit,
            bool shellExecute = false,
            ITestOutputHelper outputHelper = null,
            bool attachProcessId = true,
            int timeoutMilliseconds = 30_000)
        {
            return RunProcessAndGetOutput(BootstrapMSBuildExecutablePath, msbuildParameters, out successfulExit, shellExecute, outputHelper, attachProcessId, timeoutMilliseconds, environmentVariables: GetMSBuildEnvironmentVariables(useBootstrapHost: true));
        }

        /// <summary>
        /// Returns environment variables that should be set when launching MSBuild as a child process.
        /// On .NET Core, this includes DOTNET_HOST_PATH so that tasks like RoslynCodeTaskFactory
        /// can locate the dotnet host even when MSBuild runs as a native app host, and so that any
        /// task hosts MSBuild spawns resolve their runtime (DOTNET_ROOT) from the same host.
        /// A null value in the returned dictionary means the variable should be removed from the child environment.
        /// </summary>
        /// <param name="useBootstrapHost">
        /// When <see langword="true"/>, the whole child process tree is pinned to the bootstrap layout: DOTNET_HOST_PATH,
        /// DOTNET_ROOT and DOTNET_INSTALL_DIR all point at the bootstrap, and any architecture-specific DOTNET_ROOT_&lt;ARCH&gt;
        /// variables (which take precedence over DOTNET_ROOT) are cleared. This is required because the CI two-stage build
        /// exports DOTNET_ROOT/DOTNET_HOST_PATH/DOTNET_INSTALL_DIR pointing at the stage 1 bootstrap (it is the build tool
        /// for stage 2). Without this override those leak into the stage 2 test run, so the launched stage 2 bootstrapped
        /// MSBuild would resolve the stage 1 SDK/runtime and spawn a mismatched task host, failing with MSB4216. The
        /// bootstrap can also target an earlier runtime (e.g. net10.0) than the SDK overlaid into .dotnet, so an ambient
        /// dotnet (e.g. the repo-local .dotnet) may not contain the runtime the bootstrap targets. See
        /// eng/BootStrapMsBuild.targets and eng/cibuild_bootstrapped_msbuild.sh.
        /// </param>
        private static Dictionary<string, string> GetMSBuildEnvironmentVariables(bool useBootstrapHost)
        {
#if !FEATURE_RUN_EXE_IN_TESTS
            if (!useBootstrapHost)
            {
                return new Dictionary<string, string>
                {
                    [Constants.DotnetHostPathEnvVarName] = s_dotnetExePath,
                };
            }

            string bootstrapRoot = BootstrapMsBuildBinaryLocation.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // DOTNET_HOST_PATH/DOTNET_ROOT/DOTNET_INSTALL_DIR must point at the bootstrap layout under test so the whole
            // child process tree resolves its SDK and runtime from the bootstrap rather than from the stage 1 build tool
            // or an ambient dotnet. (The MSBuildSDKsPath/MSBuildExtensionsPath the CI two-stage build leaks from the
            // stage 1 bootstrap are cleared once centrally in MSBuildTestPipelineStartup (see TestAssemblyInfo.cs), so
            // they are already absent from this test host's environment, which the child inherits.)
            var environmentVariables = new Dictionary<string, string>
            {
                [Constants.DotnetHostPathEnvVarName] = s_bootstrapDotnetHostPath,
                ["DOTNET_ROOT"] = bootstrapRoot,
                ["DOTNET_INSTALL_DIR"] = bootstrapRoot,
            };

            // Architecture-specific DOTNET_ROOT_<ARCH> variables take precedence over DOTNET_ROOT for the app host, so a
            // stale value leaked from the CI environment would silently override the bootstrap root. Remove them (null value).
            foreach (string archSpecificRootVar in s_archSpecificDotnetRootVars)
            {
                environmentVariables[archSpecificRootVar] = null;
            }

            return environmentVariables;
#else
            return null;
#endif
        }

        /// <summary>
        /// The environment variables a test must apply when it launches the bootstrapped MSBuild (or an executable
        /// from the bootstrap layout, such as an apphost) as a child process, so the whole child process tree resolves
        /// its SDK and runtime from the bootstrap under test rather than from the CI two-stage build's stage 1 build
        /// tool or an ambient dotnet. <see cref="ExecBootstrapedMSBuild(string, out bool, bool, ITestOutputHelper, bool, int)"/>
        /// applies these automatically, so prefer it. Only tests that must start the process themselves (e.g. launching
        /// an apphost directly via <see cref="RunProcessAndGetOutput"/>) need this: seed their environment dictionary
        /// from here and then override only the entries the scenario requires.
        /// </summary>
        public static Dictionary<string, string> GetBootstrapMSBuildEnvironmentVariables()
            => GetMSBuildEnvironmentVariables(useBootstrapHost: true) ?? new Dictionary<string, string>();

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
        /// <remarks>
        /// If <paramref name="process"/> is the bootstrapped MSBuild (or an executable from the bootstrap layout),
        /// prefer <see cref="ExecBootstrapedMSBuild(string, out bool, bool, ITestOutputHelper, bool, int)"/>, which
        /// applies the required bootstrap environment automatically. If the process must be launched here directly,
        /// seed <paramref name="environmentVariables"/> from <see cref="GetBootstrapMSBuildEnvironmentVariables"/> so
        /// the child resolves its SDK and runtime from the bootstrap under test rather than a leaked stage 1 / ambient
        /// dotnet; otherwise the launch can fail with MSB4216 / MSB4062 in the CI two-stage build.
        /// </remarks>
        public static string RunProcessAndGetOutput(
            string process,
            string parameters,
            out bool successfulExit,
            bool shellExecute = false,
            ITestOutputHelper outputHelper = null,
            bool attachProcessId = true,
            int timeoutMilliseconds = 30_000,
            Dictionary<string, string> environmentVariables = null)
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

            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                {
                    // A null value means the variable should be removed from the child environment
                    // (e.g. arch-specific DOTNET_ROOT_<ARCH> vars leaked from the CI build environment).
                    if (kvp.Value is null)
                    {
                        psi.Environment.Remove(kvp.Key);
                    }
                    else
                    {
                        psi.Environment[kvp.Key] = kvp.Value;
                    }
                }
            }
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
                Stopwatch sw = Stopwatch.StartNew();
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

                // Capture elapsed at process exit, before the post-exit drain below, so the
                // telemetry below reports the budget-relevant time only (the drain is bookkeeping).
                long exitElapsedMs = sw.ElapsedMilliseconds;

                // We need the WaitForExit call without parameters because our processing of output/error streams is not synchronous.
                // See https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-6.0#system-diagnostics-process-waitforexit(system-int32).
                // The overload WaitForExit() waits for the error and output to be handled. The WaitForExit(int timeout) overload does not, so we could lose the data.
                p.WaitForExit();
                sw.Stop();

                pid = p.Id;
                successfulExit = p.ExitCode == 0;

                // Telemetry: log actual elapsed time so callers tuning their timeouts can see
                // how close to the budget we ran. In DebugUnitTests mode the budget is not
                // enforced (WaitForExit without timeout above), so the budget/headroom fields
                // would be misleading and are omitted.
                if (Traits.Instance.DebugUnitTests)
                {
                    WriteOutput($"Process {pid} exited in {exitElapsedMs}ms (DebugUnitTests: budget not enforced)");
                }
                else
                {
                    WriteOutput($"Process {pid} exited in {exitElapsedMs}ms (budget {timeoutMilliseconds}ms, {timeoutMilliseconds - exitElapsedMs}ms remaining)");
                }
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
