// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAnAppWithLibrariesAndRid : SdkTest
    {
        public GivenThatWeWantToPublishAnAppWithLibrariesAndRid(ITestOutputHelper log) : base(log)
        {
        }

        // Libuv version used by LibraryWithRid/LibraryWithRids
        private const string LibuvVersion = "1.10.0";

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
                $"libuv{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}coreclr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
            });

            publishDirectory.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });

            new RunExeCommand(Log, Path.Combine(publishDirectory.FullName, selfContainedExecutable))
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"{LibuvVersion} '{runtimeIdentifier}' {LibuvVersion} '{runtimeIdentifier}' Hello World");
        }

        [Fact]
        public void It_publishes_a_framework_dependent_RID_specific_runnable_output()
        {
            PublishAppWithLibraryAndRid(false,
                out var publishDirectory,
                out var runtimeIdentifier);

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().OnlyHaveFiles(new[] {
                $"App{Constants.ExeSuffix}",
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
                $"libuv{FileConstants.DynamicLibSuffix}",
            });

            new DotnetCommand(Log, Path.Combine(publishDirectory.FullName, "App.dll"))
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"{LibuvVersion} '{runtimeIdentifier}' {LibuvVersion} '{runtimeIdentifier}' Hello World");
        }

        private void PublishAppWithLibraryAndRid(bool selfContained, out DirectoryInfo publishDirectory, out string runtimeIdentifier)
        {
            runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid", $"PublishAppWithLibraryAndRid{selfContained}")
                .WithSource();

            var msbuildArgs = new List<string>()
            {
                $"/p:RuntimeIdentifier={runtimeIdentifier}",
                $"/p:TestRuntimeIdentifier={runtimeIdentifier}",
                $"/p:SelfContained={selfContained}"
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  .NET Core 2.1.0 packages don't support latest versions of OS X, so roll forward to the
                //  latest patch which does
                msbuildArgs.Add("/p:TargetLatestRuntimePatch=true");
            }

            var restoreCommand = new RestoreCommand(testAsset, "App");
            restoreCommand
                .Execute(msbuildArgs.ToArray())
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(testAsset, "App");

            publishCommand
                .Execute(msbuildArgs.ToArray())
                .Should().Pass();

            publishDirectory = publishCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: runtimeIdentifier);
        }
    }
}
