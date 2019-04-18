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

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAnAppWithLibrariesAndRid : SdkTest
    {
        public GivenThatWeWantToPublishAnAppWithLibrariesAndRid(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_a_self_contained_runnable_output()
        {
            PublishAppWithLibraryAndRid(true,
                out var publishDirectory,
                out var runtimeIdentifier);

            var selfContainedExecutable = $"App{Constants.ExeSuffix}";

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().HaveFiles(new[] {
                selfContainedExecutable,
                "App.dll",
                "App.pdb",
                "App.deps.json",
                "App.runtimeconfig.json",
                "LibraryWithoutRid.dll",
                "LibraryWithoutRid.pdb",
                "LibraryWithRid.dll",
                "LibraryWithRid.pdb",
                "LibraryWithRids.dll",
                "LibraryWithRids.pdb",
                $"{FileConstants.DynamicLibPrefix}sqlite3{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}coreclr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
            });

            publishDirectory.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });

            Command.Create(Path.Combine(publishDirectory.FullName, selfContainedExecutable), new string[] { })
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World");
        }

        [Fact]
        public void It_publishes_a_framework_dependent_RID_specific_runnable_output()
        {
            PublishAppWithLibraryAndRid(false,
                out var publishDirectory,
                out var runtimeIdentifier);

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().OnlyHaveFiles(new[] {
                "App.dll",
                "App.pdb",
                "App.deps.json",
                "App.runtimeconfig.json",
                "LibraryWithoutRid.dll",
                "LibraryWithoutRid.pdb",
                "LibraryWithRid.dll",
                "LibraryWithRid.pdb",
                "LibraryWithRids.dll",
                "LibraryWithRids.pdb",
                $"{FileConstants.DynamicLibPrefix}sqlite3{FileConstants.DynamicLibSuffix}",
            });

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(publishDirectory.FullName, "App.dll") })
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World");
        }

        private void PublishAppWithLibraryAndRid(bool selfContained, out DirectoryInfo publishDirectory, out string runtimeIdentifier)
        {
            runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid", $"PublishAppWithLibraryAndRid{selfContained}")
                .WithSource();

            var projectPath = Path.Combine(testAsset.TestRoot, "App");

            var msbuildArgs = new[]
            {
                $"/p:RuntimeIdentifier={runtimeIdentifier}",
                $"/p:TestRuntimeIdentifier={runtimeIdentifier}",
                $"/p:SelfContained={selfContained}"
            };

            var restoreCommand = new RestoreCommand(Log, projectPath, "App.csproj");
            restoreCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(Log, projectPath);

            publishCommand
                .Execute(msbuildArgs)
                .Should().Pass();

            publishDirectory = publishCommand.GetOutputDirectory("netcoreapp1.1", runtimeIdentifier: runtimeIdentifier);
        }
    }
}
