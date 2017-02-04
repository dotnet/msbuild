// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using Microsoft.DotNet.InternalAbstractions;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithDependencies : SdkTest
    {
        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void It_publishes_projects_with_simple_dependencies_with_filter_profile()
        {
            string project = "SimpleDependencies";
            string tfm = "netcoreapp1.0";
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset(project)
                .WithSource()
                .Restore("", $"/p:TargetFramework={tfm}");

            string filterProjDir = _testAssetsManager.GetAndValidateTestProjectDirectory("NewtonsoftFilterProfile");
            string filterProjFile = Path.Combine(filterProjDir, "NewtonsoftFilterProfile.csproj");

            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
            publishCommand
                .Execute($"/p:TargetFramework={tfm}", $"/p:FilterProjFile={filterProjFile}")
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                $"{project}.dll",
                $"{project}.pdb",
                $"{project}.deps.json",
                $"{project}.runtimeconfig.json",
                "System.Collections.NonGeneric.dll"
            });

           var runtimeConfig = ReadJson(System.IO.Path.Combine(publishDirectory.ToString(), $"{project}.runtimeconfig.json"));

           runtimeConfig["runtimeOptions"]["tfm"].ToString().Should().Be(tfm);
            
//TODO: Enable testing the run once dotnet host has the notion of looking up shared packages
        }

        [Fact]
        public void It_publishes_projects_with_filter_and_rid()
        {
            string project = "SimpleDependencies";
            var rid = RuntimeEnvironment.GetRuntimeIdentifier();
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset(project)
                .WithSource()
                .Restore("", $"/p:RuntimeIdentifier={rid}");

            string filterProjDir = _testAssetsManager.GetAndValidateTestProjectDirectory("NewtonsoftFilterProfile");
            string filterProjFile = Path.Combine(filterProjDir, "NewtonsoftFilterProfile.csproj");
            

            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
            publishCommand
                .Execute($"/p:RuntimeIdentifier={rid}", $"/p:FilterProjFile={filterProjFile}")
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(runtimeIdentifier: rid);

            string libPrefix = "";
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libPrefix = "lib";
            }

            publishDirectory.Should().HaveFiles(new[] {
                $"{project}.dll",
                $"{project}.pdb",
                $"{project}.deps.json",
                $"{project}.runtimeconfig.json",
                "System.Collections.NonGeneric.dll",
                $"{libPrefix}coreclr{Constants.DynamicLibSuffix}"
            });

            publishDirectory.Should().NotHaveFiles(new[] {
                "Newtonsoft.Json.dll",
                "System.Runtime.Serialization.Primitives.dll"
            });

//TODO: Enable testing the run once dotnet host has the notion of looking up shared packages
        }
        private static JObject ReadJson(string path)
        {
            using (JsonTextReader jsonReader = new JsonTextReader(File.OpenText(path)))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(jsonReader);
            }
        }
    }
}