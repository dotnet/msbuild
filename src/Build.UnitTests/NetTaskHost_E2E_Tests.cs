// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
            using TestEnvironment env = TestEnvironment.Create(_output, setupDotnetHostPath: true);
            var coreDirectory = Path.Combine(RunnerUtilities.BootstrapRootPath, "core");
            env.SetEnvironmentVariable("PATH", $"{coreDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}");
            env.SetEnvironmentVariable("DOTNET_ROOT", coreDirectory);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask);

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
            using TestEnvironment env = TestEnvironment.Create(_output, setupDotnetHostPath: true);
            var coreDirectory = Path.Combine(RunnerUtilities.BootstrapRootPath, "core");
            env.SetEnvironmentVariable("PATH", $"{coreDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}");
            env.SetEnvironmentVariable("DOTNET_ROOT", coreDirectory);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTaskWithImplicitParams", "TestNetTaskWithImplicitParams.csproj");
            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n -p:LatestDotNetCoreForMSBuild={RunnerUtilities.LatestDotNetCoreForMSBuild}", out bool successTestTask);

            _output.WriteLine(testTaskOutput);

            successTestTask.ShouldBeTrue();

            testTaskOutput.ShouldContain("The task is executed in process: MSBuild");
            testTaskOutput.ShouldContain("/nodereuse:True");
        }
    }
}
