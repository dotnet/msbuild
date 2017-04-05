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
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAnAppWithLibrariesAndRid : SdkTest
    {
        [Fact]
        public void It_publishes_a_self_contained_runnable_output()
        {
            if (UsingFullFrameworkMSBuild)
            {
                //  Disable this test on full framework, as the current build won't have access to 
                //  https://github.com/Microsoft/msbuild/pull/1674
                //  See https://github.com/dotnet/sdk/issues/877
                return;
            }

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
                $"{FileConstants.DynamicLibPrefix}sqlite3{Constants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}coreclr{Constants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{Constants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}",
            });

            Command.Create(Path.Combine(publishDirectory.FullName, selfContainedExecutable), new string[] { })
                .EnsureExecutable()
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World");
        }

        [Fact]
        public void It_publishes_a_framework_dependent_RID_specific_runnable_output()
        {
            if (UsingFullFrameworkMSBuild)
            {
                //  Disable this test on full framework, as the current build won't have access to 
                //  https://github.com/Microsoft/msbuild/pull/1674
                //  See https://github.com/dotnet/sdk/issues/877
                return;
            }

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
                $"{FileConstants.DynamicLibPrefix}sqlite3{Constants.DynamicLibSuffix}",
            });

            Command.Create(RepoInfo.DotNetHostPath, new[] { Path.Combine(publishDirectory.FullName, "App.dll") })
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

            var restoreCommand = new RestoreCommand(Stage0MSBuild, projectPath, "App.csproj");
            restoreCommand
                .Execute($"/p:TestRuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(Stage0MSBuild, projectPath);

            publishCommand
                .Execute(
                    $"/p:RuntimeIdentifier={runtimeIdentifier}", 
                    $"/p:TestRuntimeIdentifier={runtimeIdentifier}", 
                    $"/p:SelfContained={selfContained}")
                .Should().Pass();

            publishDirectory = publishCommand.GetOutputDirectory("netcoreapp1.1", runtimeIdentifier: runtimeIdentifier);
        }
    }
}
