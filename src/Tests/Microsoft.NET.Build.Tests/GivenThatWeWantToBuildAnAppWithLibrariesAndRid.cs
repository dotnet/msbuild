// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithLibrariesAndRid : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithLibrariesAndRid(ITestOutputHelper log) : base(log)
        {
        }

        // Libuv version used by LibraryWithRid/LibraryWithRids
        private const string LibuvVersion = "1.10.0";

        [Fact]
        public void It_builds_a_RID_specific_runnable_output()
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            string[] msbuildArgs = new[]
            {
                $"/p:RuntimeIdentifier={runtimeIdentifier}",
                $"/p:TestRuntimeIdentifier={runtimeIdentifier}"
            };

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid")
                .WithSource();

            var restoreCommand = new RestoreCommand(testAsset, "App");
            restoreCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(testAsset, "App");

            buildCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"App{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"{LibuvVersion} '{runtimeIdentifier}' {LibuvVersion} '{runtimeIdentifier}' Hello World")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void It_builds_a_framework_dependent_RID_specific_runnable_output()
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid", "BuildFrameworkDependentRIDSpecific")
                .WithSource();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  .NET Core 2.1.0 packages don't support latest versions of OS X, so roll forward to the
                //  latest patch which does
                testAsset = testAsset.WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("TargetLatestRuntimePatch", true));
                });
            }

            var restoreCommand = new RestoreCommand(testAsset, "App");
            restoreCommand
                .Execute($"/p:TestRuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(testAsset, "App");

            buildCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}", $"/p:TestRuntimeIdentifier={runtimeIdentifier}", "/p:SelfContained=false")
                .Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: runtimeIdentifier);

            outputDirectory.Should().NotHaveSubDirectories();

            string[] expectedFiles = new[] {
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
                $"libuv{FileConstants.DynamicLibSuffix}"
            };

            outputDirectory.Should().OnlyHaveFiles(expectedFiles.Where(x => !string.IsNullOrEmpty(x)).ToList());

            new DotnetCommand(Log, Path.Combine(outputDirectory.FullName, "App.dll"))
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"{LibuvVersion} '{runtimeIdentifier}' {LibuvVersion} '{runtimeIdentifier}' Hello World");
        }
    }
}
