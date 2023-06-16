// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class PortableTasks_Tests
    {
        private readonly ITestOutputHelper _outputHelper;

        private static readonly string PortableTaskFolderPath = Path.GetFullPath(
            Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory,
                        "..", "..", "..", "Samples", "PortableTask"));

        private const string ProjectFileName = "portableTaskTest.proj";

        public PortableTasks_Tests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [WindowsFullFrameworkOnlyFact(additionalMessage: "No Visual Studio install for .NET.")]
        public void TestDesktopMSBuildShouldRunPortableTask()
        {
            RunMSBuildOnProjectWithPortableTaskAndAssertOutput(true);
        }

        [Fact]
        public void TestNonDesktopMSBuildShouldRunPortableTask()
        {
            RunMSBuildOnProjectWithPortableTaskAndAssertOutput(false);
        }

        private void RunMSBuildOnProjectWithPortableTaskAndAssertOutput(bool useDesktopMSBuild)
        {
            using (TestEnvironment env = TestEnvironment.Create(_outputHelper))
            {
                bool successfulExit;

                var folder = env.CreateFolder().Path;
                var projFile = Path.Combine(folder, ProjectFileName);

                // "Debug", "netstandard1.3"
                DirectoryInfo ProjectFileFolder =
                    new DirectoryInfo(PortableTaskFolderPath).EnumerateDirectories().First().EnumerateDirectories().First();

                foreach (var file in ProjectFileFolder.GetFiles())
                {
                    File.Copy(file.FullName, Path.Combine(folder, file.Name));
                    _outputHelper.WriteLine($"Copied {file.FullName} to {Path.Combine(folder, file.Name)}");
                }

                File.Exists(projFile).ShouldBeTrue($"Project file {projFile} does not exist");

                _outputHelper.WriteLine(File.ReadAllText(projFile));

                _outputHelper.WriteLine($"Building project {projFile}");

                var executionOutput = useDesktopMSBuild
                    ? RunnerUtilities.RunProcessAndGetOutput("msbuild", projFile, out successfulExit,
                        shellExecute: true, outputHelper: _outputHelper)
                    : RunnerUtilities.ExecMSBuild(projFile, out successfulExit);

                _outputHelper.WriteLine(executionOutput);

                successfulExit.ShouldBeTrue(
                    $"{(useDesktopMSBuild ? "Desktop MSBuild" : "Non Desktop MSBuild")} failed to execute the portable task");

                Regex.Matches(executionOutput, @"Microsoft\.Build\.(\w+\.)+dll").Count.ShouldBeGreaterThan(1);
            }
        }
    }
}
