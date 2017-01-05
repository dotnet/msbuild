// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithDependencies : SdkTest
    {
        //[Fact]
        public void It_publishes_projects_with_simple_dependencies()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleDependencies")
                .WithSource()
                .Restore();

            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
            publishCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "SimpleDependencies.dll",
                "SimpleDependencies.pdb",
                "SimpleDependencies.deps.json",
                "SimpleDependencies.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                "System.Runtime.Serialization.Primitives.dll",
                "System.Collections.NonGeneric.dll",
            });

            string appPath = publishCommand.GetPublishedAppPath("SimpleDependencies");

            Command runAppCommand = Command.Create(
                RepoInfo.DotNetHostPath,
                new[] { appPath, "one", "two" });

            string expectedOutput =
@"{
  ""one"": ""one"",
  ""two"": ""two""
}";

            runAppCommand
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(expectedOutput);
        }

        //[Fact]
        public void It_publishes_the_app_config_if_necessary()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource()
                .Restore();

            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, testAsset.TestRoot);
            publishCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory("net452", "Debug", "win7-x86");

            publishDirectory.Should().HaveFiles(new[]
            {
                "DesktopNeedsBindingRedirects.exe",
                "DesktopNeedsBindingRedirects.exe.config"
            });
        }

        //[Fact]
        public void It_publishes_projects_targeting_netcoreapp11_with_p2p_targeting_netcoreapp11()
        {
            // Microsoft.NETCore.App 1.1.0 added a dependency on Microsoft.DiaSymReader.Native.
            // Microsoft.DiaSymReader.Native package adds a "Content" item for its native assemblies,
            // which means an App project will get duplicate "Content" items for each P2P it references
            // that targets netcoreapp1.1.  Ensure Publish works correctly with these duplicate Content items.

            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreApp11WithP2P")
                .WithSource()
                .Restore("App");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "App");
            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);
            publishCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}