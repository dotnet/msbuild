// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithDependencies : SdkTest
    {
        public GivenThatWeWantToPublishAProjectWithDependencies(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_projects_with_simple_dependencies()
        {
            string targetFramework = ToolsetInfo.CurrentTargetFramework;
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleDependencies")
                .WithSource();

            PublishCommand publishCommand = new PublishCommand(simpleDependenciesAsset);
            publishCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(targetFramework);

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "SimpleDependencies.dll",
                "SimpleDependencies.pdb",
                "SimpleDependencies.deps.json",
                "SimpleDependencies.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                $"SimpleDependencies{EnvironmentInfo.ExecutableExtension}"
            });

            string appPath = publishCommand.GetPublishedAppPath("SimpleDependencies", targetFramework);

            TestCommand runAppCommand = new DotnetCommand(Log,  appPath, "one", "two" );

            string expectedOutput =
@"{
  ""one"": ""one"",
  ""two"": ""two""
}";

            runAppCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(expectedOutput);
        }

        [WindowsOnlyFact]
        public void It_publishes_the_app_config_if_necessary()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource();

            PublishCommand publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory("net452", "Debug", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86");

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
                .WithSource();

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "App");
            PublishCommand publishCommand = new PublishCommand(Log, appProjectDirectory);
            publishCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_publishes_projects_with_simple_dependencies_with_filter_profile()
        {
            string project = "SimpleDependencies";
            string targetFramework = "netcoreapp2.0";

            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset(project)
                .WithSource()
                .WithProjectChanges(projectFile =>
                {
                    var ns = projectFile.Root.Name.Namespace;

                    var targetFrameworkElement = projectFile.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").Single();
                    targetFrameworkElement.SetValue(targetFramework);
                });

            string filterProjDir = _testAssetsManager.CopyTestAsset("StoreManifests").WithSource().Path;
            string manifestFileName1 = "NewtonsoftFilterProfile.xml";
            string manifestFileName2 = "NewtonsoftMultipleVersions.xml";
            string manifestFile1 = Path.Combine(filterProjDir, manifestFileName1);
            string manifestFile2 = Path.Combine(filterProjDir, manifestFileName2);

            PublishCommand publishCommand = new PublishCommand(simpleDependenciesAsset);
            publishCommand
                .Execute($"/p:TargetManifestFiles={manifestFile1}%3b{manifestFile2}")
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(targetFramework);

            publishDirectory.Should().OnlyHaveFiles(new[] {
                $"{project}.dll",
                $"{project}.pdb",
                $"{project}.deps.json",
                $"{project}.runtimeconfig.json",
            });

            var runtimeConfig = ReadJson(Path.Combine(publishDirectory.FullName, $"{project}.runtimeconfig.json"));
            runtimeConfig["runtimeOptions"]["tfm"].ToString().Should().Be(targetFramework);
            var depsJson = ReadJson(Path.Combine(publishDirectory.FullName, $"{project}.deps.json"));
            depsJson["libraries"][$"Newtonsoft.Json/{ToolsetInfo.GetNewtonsoftJsonPackageVersion()}"]["runtimeStoreManifestName"].ToString().Should().Be($"{manifestFileName1};{manifestFileName2}");

            // The end-to-end test of running the published app happens in the dotnet/cli repo.
            // See https://github.com/dotnet/cli/blob/358568b07f16749108dd33e7fea2f2c84ccf4563/test/dotnet-store.Tests/GivenDotnetStoresAndPublishesProjects.cs
        }

        [Fact]
        public void It_publishes_projects_with_filter_and_rid()
        {
            string project = "SimpleDependencies";
            string targetFramework = "netcoreapp2.1";
            var rid = RuntimeInformation.RuntimeIdentifier;
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset(project)
                .WithSource()
                .WithProjectChanges(projectFile =>
                {
                    var ns = projectFile.Root.Name.Namespace;

                    var targetFrameworkElement = projectFile.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").Single();
                    targetFrameworkElement.SetValue(targetFramework);
                });

            string filterProjDir = _testAssetsManager.CopyTestAsset("StoreManifests").WithSource().Path;
            string manifestFile = Path.Combine(filterProjDir, "NewtonsoftFilterProfile.xml");

            // According to https://github.com/dotnet/sdk/issues/1362 publish should throw an error
            // since this scenario is not supported. Running the published app doesn't work currently.
            // This test should be updated when that bug is fixed.

            PublishCommand publishCommand = new PublishCommand(simpleDependenciesAsset);
            publishCommand
                .Execute($"/p:RuntimeIdentifier={rid}", $"/p:TargetManifestFiles={manifestFile}")
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid);

            publishDirectory.Should().HaveFiles(new[] {
                $"{project}.dll",
                $"{project}.pdb",
                $"{project}.deps.json",
                $"{project}.runtimeconfig.json",
                "System.Collections.NonGeneric.dll",
                $"{FileConstants.DynamicLibPrefix}coreclr{FileConstants.DynamicLibSuffix}"
            });

            publishDirectory.Should().NotHaveFiles(new[] {
                "Newtonsoft.Json.dll",
            });
        }

        [Theory]
        [InlineData("GenerateDocumentationFile=true", true, true)]
        [InlineData("GenerateDocumentationFile=true;PublishDocumentationFile=false", false, true)]
        [InlineData("GenerateDocumentationFile=true;PublishReferencesDocumentationFiles=false", true, false)]
        [InlineData("GenerateDocumentationFile=true;PublishDocumentationFiles=false", false, false)]
        public void It_publishes_documentation_files(string properties, bool expectAppDocPublished, bool expectLibProjectDocPublished)
        {
            var kitchenSinkAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink", identifier: $"{expectAppDocPublished}_{expectLibProjectDocPublished}")
                .WithSource();
            
            var publishCommand = new PublishCommand(Log, Path.Combine(kitchenSinkAsset.TestRoot, "TestApp"));
            var publishArgs = properties.Split(';').Select(p => $"/p:{p}").ToArray();
            var publishResult = publishCommand.Execute(publishArgs);

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework);

            if (expectAppDocPublished)
            {
                publishDirectory.Should().HaveFile("TestApp.xml");
            }
            else
            {
                publishDirectory.Should().NotHaveFile("TestApp.xml");
            }

            if (expectLibProjectDocPublished)
            {
                publishDirectory.Should().HaveFile("TestLibrary.xml");
            }
            else
            {
                publishDirectory.Should().NotHaveFile("TestLibrary.xml");
            }
        }

        [Theory]
        [InlineData("PublishReferencesDocumentationFiles=false", false)]
        [InlineData("PublishReferencesDocumentationFiles=true", true)]
        public void It_publishes_referenced_assembly_documentation(string property, bool expectAssemblyDocumentationFilePublished)
        {
            var identifier = property.Replace("=", "");

            var libProject = new TestProject
            {
                Name = "NetStdLib",
                TargetFrameworks = "netstandard1.0"
            };

            var libAsset = _testAssetsManager.CreateTestProject(libProject, identifier: identifier);

            var libPublishCommand = new PublishCommand(Log, Path.Combine(libAsset.TestRoot, "NetStdLib"));
            var libPublishResult = libPublishCommand.Execute("/t:Publish", "/p:GenerateDocumentationFile=true");
            libPublishResult.Should().Pass();
            var publishedLibPath = Path.Combine(libPublishCommand.GetOutputDirectory("netstandard1.0").FullName, "NetStdLib.dll");

            var appProject = new TestProject
            {
                Name = "TestApp",
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                References = { publishedLibPath }
            };

            var appAsset = _testAssetsManager.CreateTestProject(appProject, identifier: identifier);
            var appSourcePath  = Path.Combine(appAsset.TestRoot, "TestApp");

            new RestoreCommand(appAsset, "TestApp").Execute().Should().Pass();
            var appPublishCommand = new PublishCommand(Log, appSourcePath);
            var appPublishResult = appPublishCommand.Execute("/p:" + property);
            appPublishResult.Should().Pass();

            var appPublishDirectory = appPublishCommand.GetOutputDirectory(appProject.TargetFrameworks);

            if (expectAssemblyDocumentationFilePublished)
            {
                appPublishDirectory.Should().HaveFile("NetStdLib.xml");
            }
            else
            {
                appPublishDirectory.Should().NotHaveFile("NetStdLib.xml");
            }
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
