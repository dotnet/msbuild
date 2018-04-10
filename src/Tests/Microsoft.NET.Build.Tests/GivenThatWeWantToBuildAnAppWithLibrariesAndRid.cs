// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithLibrariesAndRid : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithLibrariesAndRid(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_a_RID_specific_runnable_output()
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            string[] msbuildArgs = new[]
            {
                $"/p:RuntimeIdentifier={runtimeIdentifier}",
                $"/p:TestRuntimeIdentifier={runtimeIdentifier}"
            };

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid")
                .WithSource();

            var projectPath = Path.Combine(testAsset.TestRoot, "App");

            var restoreCommand = new RestoreCommand(Log, projectPath, "App.csproj");
            restoreCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, projectPath);

            buildCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.1", runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"App{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

            Command.Create(selfContainedExecutableFullPath, new string[] { })
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void It_builds_a_framework_dependent_RID_specific_runnable_output()
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid", "BuildFrameworkDependentRIDSpecific")
                .WithSource();

            var projectPath = Path.Combine(testAsset.TestRoot, "App");

            var restoreCommand = new RestoreCommand(Log, projectPath, "App.csproj");
            restoreCommand
                .Execute($"/p:TestRuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, projectPath);

            buildCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}", $"/p:TestRuntimeIdentifier={runtimeIdentifier}", "/p:SelfContained=false")
                .Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.1", runtimeIdentifier: runtimeIdentifier);

            outputDirectory.Should().NotHaveSubDirectories();
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "App.dll",
                "App.pdb",
                "App.deps.json",
                "App.runtimeconfig.json",
                "App.runtimeconfig.dev.json",
                "LibraryWithoutRid.dll",
                "LibraryWithoutRid.pdb",
                "LibraryWithRid.dll",
                "LibraryWithRid.pdb",
                "LibraryWithRids.dll",
                "LibraryWithRids.pdb",
            });

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(outputDirectory.FullName, "App.dll") })
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World");
        }
    }
}
