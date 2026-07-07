// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public class NetTaskHost_E2E_Tests
    {
        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(NetTaskHost_E2E_Tests).Assembly.Location) ?? AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets");

        private readonly ITestOutputHelper _output;

        public NetTaskHost_E2E_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHostTest_FallbackToDotnet()
        {
            // This test verifies the fallback behavior when app host is not used.
            // When DOTNET_HOST_PATH points to system dotnet, it uses dotnet.exe + MSBuild.dll.
            using TestEnvironment env = TestEnvironment.Create(_output);
            var dotnetPath = env.GetEnvironmentVariable("DOTNET_ROOT");

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"The task is executed in process: dotnet");
            testTaskOutput.ShouldContain($"Process path: {dotnetPath}", customMessage: testTaskOutput);

            var customTaskAssemblyLocation = Path.GetFullPath(Path.Combine(AssemblyLocation, "..", RunnerUtilities.LatestDotNetCoreForMSBuild, "ExampleTask.dll"));
            var resource = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
               "TaskAssemblyLocationMismatch",

               // Microsoft.Build.dll represents TaskHostTask wrapper for the custom task here.
               Path.Combine(RunnerUtilities.BootstrapRootPath, "net472", "MSBuild", "Current", "Bin", "Microsoft.Build.dll"),
               customTaskAssemblyLocation);
            testTaskOutput.ShouldNotContain(resource);
        }

        [WindowsFullFrameworkOnlyFact] // Verifies that when MSBuild.exe app host is available in the SDK, it is used instead of dotnet.exe + MSBuild.dll.
        public void NetTaskHostTest_AppHostUsedWhenAvailable()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Set the .NET SDK's SDK resolver override to point to our bootstrap, which is guaranteed to have the apphost.
            var coreDirectory = Path.Combine(RunnerUtilities.BootstrapRootPath, "core");
            env.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", coreDirectory);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask, outputHelper: _output);

            successTestTask.ShouldBeTrue();

            // When app host is used, the process name should be "MSBuild" not "dotnet"
            testTaskOutput.ShouldContain("The task is executed in process: MSBuild");

            // The process path should point to MSBuild.exe, not dotnet.exe
            testTaskOutput.ShouldContain(Constants.MSBuildExecutableName, customMessage: "Expected MSBuild.exe app host to be used");
            testTaskOutput.ShouldNotContain("Process path: " + Path.Combine(env.GetEnvironmentVariable("DOTNET_ROOT") ?? "", "dotnet.exe"));
        }

        [WindowsFullFrameworkOnlyFact] // Verifies that when using the app host, DOTNET_ROOT is properly set for child processes to find the runtime.
        public void NetTaskHostTest_AppHostSetsDotnetRoot()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Clear DOTNET_ROOT to ensure app host sets it
            env.SetEnvironmentVariable("DOTNET_ROOT", null);
            env.SetEnvironmentVariable("DOTNET_ROOT_X64", null);
            env.SetEnvironmentVariable("DOTNET_ROOT_X86", null);
            env.SetEnvironmentVariable("DOTNET_ROOT_ARM64", null);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask);

            _output.WriteLine(testTaskOutput);

            // The build should succeed - this proves DOTNET_ROOT was properly set for the task host
            // to find the runtime, even though we cleared it from the parent environment
            successTestTask.ShouldBeTrue(customMessage: "Build should succeed with app host setting DOTNET_ROOT");
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHost_CorrectPathsEscapingTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var dotnetPath = env.GetEnvironmentVariable("DOTNET_ROOT");
            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");
            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"Process path: {dotnetPath}", customMessage: testTaskOutput);

            // Explicitly validate escaping for paths with spaces
            var msBuildDllPathMatch = System.Text.RegularExpressions.Regex.Match(
                testTaskOutput,
                @"Arg\[0\]:\s*(.+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            msBuildDllPathMatch.Success.ShouldBeTrue();
            string msBuildDllPath = msBuildDllPathMatch.Groups[1].Value.Trim();

            if (msBuildDllPath.Contains(" "))
            {
                // Extract all space-delimited parts of the path and verify none are in subsequent args
                var pathParts = msBuildDllPath.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length > 1)
                {
                    // Check that later path segments don't appear as separate command line arguments
                    var arg1Match = System.Text.RegularExpressions.Regex.Match(
                        testTaskOutput,
                        @"Arg\[1\]:\s*(.+)",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                    if (arg1Match.Success)
                    {
                        string arg1Value = arg1Match.Groups[1].Value.Trim();
                        // Arg[1] should be a flag (starts with /), not a path fragment
                        arg1Value.ShouldMatch(@"^[/-]", "Arg[1] should be a flag, not a continuation of the path");
                    }
                }
            }

            // Validate if path is in quotes in command line
            var cmdLineMatch = System.Text.RegularExpressions.Regex.Match(
                testTaskOutput,
                @"Command line:\s*(.+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            if (cmdLineMatch.Success)
            {
                string cmdLine = cmdLineMatch.Groups[1].Value.Trim();
                string quotedArg0 = $"\"{msBuildDllPath}\"";
                cmdLine.ShouldContain(quotedArg0);
            }
        }

        [WindowsFullFrameworkOnlyFact]
        public void MSBuildTaskInNetHostTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestMSBuildTaskInNet", "TestMSBuildTaskInNet.csproj");
            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore  -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"Hello TEST");
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHost_CallbackIsRunningMultipleNodesTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1");

            // Point dotnet resolution to the bootstrap layout so the .NET Core TaskHost
            // uses the locally-built MSBuild.exe (with callback support) instead of the system SDK.
            var coreDirectory = Path.Combine(RunnerUtilities.BootstrapRootPath, "core");
            env.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", coreDirectory);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTaskCallback", "TestNetTaskCallback.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild} -t:TestTask", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain("CallbackResult: IsRunningMultipleNodes = False");
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHost_CallbackRequestCoresTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1");

           // Set the .NET SDK's SDK resolver override to point to our bootstrap, which is guaranteed to have the apphost.
            var coreDirectory = Path.Combine(RunnerUtilities.BootstrapRootPath, "core");
            env.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", coreDirectory);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTaskResourceCallback", "TestNetTaskResourceCallback.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild} -t:TestTask", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain("CallbackResult: RequestCores(2) =");
        }

        [WindowsFullFrameworkOnlyFact] // This test verifies the fallback behavior with implicit host parameters.
        public void NetTaskWithImplicitHostParamsTest_FallbackToDotnet()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var dotnetPath = env.GetEnvironmentVariable("DOTNET_ROOT");

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTaskWithImplicitParams", "TestNetTaskWithImplicitParams.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore  -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();

            // Output from the task where only Runtime was specified
            testTaskOutput.ShouldContain($"The task is executed in process: dotnet");
            testTaskOutput.ShouldContain($"Process path: {dotnetPath}", customMessage: testTaskOutput);
            testTaskOutput.ShouldContain("/nodereuse:True");

            // Output from the task where only TaskHost was specified
            testTaskOutput.ShouldContain($"Hello TEST FROM MESSAGE");

            // Output from the task where neither TaskHost nor Runtime were specified
            testTaskOutput.ShouldContain("Found item: Banana");
        }

        [WindowsFullFrameworkOnlyFact] // This test verifies app host behavior with implicit host parameters.
        public void NetTaskWithImplicitHostParamsTest_AppHost()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var coreDirectory = Path.Combine(RunnerUtilities.BootstrapRootPath, "core");
            env.SetEnvironmentVariable("PATH", $"{coreDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}");
            env.SetEnvironmentVariable("DOTNET_ROOT", coreDirectory);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTaskWithImplicitParams", "TestNetTaskWithImplicitParams.csproj");
            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask, outputHelper: _output);

            _output.WriteLine(testTaskOutput);

            successTestTask.ShouldBeTrue();

            testTaskOutput.ShouldContain("The task is executed in process: MSBuild");
            testTaskOutput.ShouldContain("/nodereuse:True");
        }

#if NET
        /// <summary>
        /// Regression test: proves that launching the MSBuild task host through a symlinked
        /// SDK path causes MSB4216 due to handshake mismatch.
        ///
        /// On macOS, /tmp is a symlink to /private/tmp. When the SDK is under /tmp, the
        /// MSBuild property $(NetCoreSdkRoot) = $(MSBuildThisFileDirectory) preserves the
        /// unresolved /tmp form. But the child task host's AppContext.BaseDirectory resolves
        /// to /private/tmp. The parent and child compute different handshake hashes → different
        /// pipe names → MSB4216.
        ///
        /// This test recreates the scenario by symlinking the bootstrap SDK directory and
        /// running MSBuild through the symlink.
        /// </summary>
        [UnixOnlyFact]
        public void NetTaskHost_SymlinkedSdkPath_ShouldNotCauseMSB4216()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Create a symlink pointing to the bootstrap SDK binary location.
            // This simulates the macOS /tmp → /private/tmp scenario.
            string realSdkPath = RunnerUtilities.BootstrapMsBuildBinaryLocation;
            string symlinkPath = Path.Combine(Path.GetTempPath(), $"msbuild_symlink_test_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateSymbolicLink(symlinkPath, realSdkPath);

                // Launch the MSBuild apphost through the symlink path.
                // This causes $(MSBuildThisFileDirectory) to use the symlink form,
                // while the child's AppContext.BaseDirectory resolves to the real path.
                string apphostPath = Path.Combine(symlinkPath, "sdk", RunnerUtilities.BootstrapSdkVersion, Constants.MSBuildExecutableName);

                if (!File.Exists(apphostPath))
                {
                    // If the apphost isn't present, we can't test the symlink scenario.
                    // Fail explicitly so this doesn't silently pass in broken environments.
                    Assert.Fail($"MSBuild apphost not found at: {apphostPath}. " +
                        "The bootstrap layout must include the MSBuild apphost for this test.");
                }

                string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

                string testTaskOutput = RunnerUtilities.RunProcessAndGetOutput(
                    apphostPath,
                    $"\"{testProjectPath}\" -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}",
                    out bool successTestTask,
                    shellExecute: false,
                    outputHelper: _output,
                    environmentVariables: new Dictionary<string, string>
                    {
                        [Constants.DotnetHostPathEnvVarName] = Path.Combine(realSdkPath, "dotnet"),
                    });

                _output.WriteLine(testTaskOutput);

                // Without the fix, this fails with MSB4216 because the parent's handshake
                // uses the symlink path from $(NetCoreSdkRoot) while the child resolves
                // to the real path via AppContext.BaseDirectory.
                testTaskOutput.ShouldNotContain("MSB4216");

                successTestTask.ShouldBeTrue(
                    "TaskHostFactory task should execute successfully when MSBuild runs from a symlinked SDK path.");
            }
            finally
            {
                if (Directory.Exists(symlinkPath))
                {
                    Directory.Delete(symlinkPath);
                }
            }
        }
#endif
    }
}
