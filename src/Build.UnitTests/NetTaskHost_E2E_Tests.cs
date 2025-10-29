// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
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
        private const string LatestDotNetCoreForMSBuild = "net10.0";

        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(NetTaskHost_E2E_Tests).Assembly.Location) ?? AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets");

        private readonly ITestOutputHelper _output;

        public NetTaskHost_E2E_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHostTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var dotnetPath = env.GetEnvironmentVariable("DOTNET_ROOT");

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"The task is executed in process: dotnet");
            testTaskOutput.ShouldContain($"Process path: {dotnetPath}", customMessage: testTaskOutput);

            var customTaskAssemblyLocation = Path.GetFullPath(Path.Combine(AssemblyLocation, "..", LatestDotNetCoreForMSBuild, "ExampleTask.dll"));
            var resource = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
               "TaskAssemblyLocationMismatch",

               // Microsoft.Build.dll represents TaskHostTask wrapper for the custom task here.
               Path.Combine(RunnerUtilities.BootstrapRootPath, "net472", "MSBuild", "Current", "Bin", "Microsoft.Build.dll"),
               customTaskAssemblyLocation);
            testTaskOutput.ShouldNotContain(resource);
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHost_CorrectPathsEscapingTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var dotnetPath = env.GetEnvironmentVariable("DOTNET_ROOT");
            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");
            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

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

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore  -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"Hello TEST");
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskWithImplicitHostParamsTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var dotnetPath = env.GetEnvironmentVariable("DOTNET_ROOT");

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTaskWithImplicitParams", "TestNetTaskWithImplicitParams.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore  -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"The task is executed in process: dotnet");
            testTaskOutput.ShouldContain($"Process path: {dotnetPath}", customMessage: testTaskOutput);
            testTaskOutput.ShouldContain("/nodereuse:True");
        }
    }
}
