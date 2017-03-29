// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAHelloWorldProject : SdkTest
    {
        [Fact]
        public void It_publishes_portable_apps_to_the_publish_folder_and_the_app_should_run()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore();

            var publishCommand = new PublishCommand(Stage0MSBuild, helloWorldAsset.TestRoot);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json"
            });

            Command.Create(RepoInfo.DotNetHostPath, new[] { Path.Combine(publishDirectory.FullName, "HelloWorld.dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void It_publishes_self_contained_apps_to_the_publish_folder_and_the_app_should_run()
        {
            var targetFramework = "netcoreapp1.1";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "SelfContained")
                .WithSource()
                .Restore(relativePath: "", args: $"/p:RuntimeIdentifiers={rid}");

            var publishCommand = new PublishCommand(Stage0MSBuild, helloWorldAsset.TestRoot);
            var publishResult = publishCommand.Execute($"/p:RuntimeIdentifier={rid}");

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(publishDirectory.FullName, selfContainedExecutable);

            publishDirectory.Should().HaveFiles(new[] {
                selfContainedExecutable,
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json",
                $"{FileConstants.DynamicLibPrefix}coreclr{Constants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{Constants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}",
                $"mscorlib.dll",
                $"System.Private.CoreLib.dll",
            });

            Command.Create(selfContainedExecutableFullPath, new string[] { })
                .EnsureExecutable()
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        //Note: Pre Netcoreapp2.0 stanalone activation uses renamed dotnet.exe
        //      While Post 2.0 we are shifting to using apphost.exe, so both publish needs to be validated
        [Fact]
        public void Publish_standalone_post_netcoreapp2_app_and_it_should_run()
        {
            var targetFramework = "netcoreapp2.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);


            TestProject testProject = new TestProject()
            {
                Name = "Hello",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                RuntimeFrameworkVersion = RepoInfo.NetCoreApp20Version,
                RuntimeIdentifier = rid,
                IsExe = true,
            };
            

            testProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello from a netcoreapp2.0.!"");
    }
}
";
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            testProjectInstance.Restore(testProject.Name);
            var publishCommand = new PublishCommand(Stage0MSBuild, Path.Combine(testProjectInstance.TestRoot, testProject.Name));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);
            var selfContainedExecutable = $"Hello{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(publishDirectory.FullName, selfContainedExecutable);

            publishDirectory.Should().HaveFiles(new[] {
                selfContainedExecutable,
                "Hello.dll",
                "Hello.pdb",
                "Hello.deps.json",
                "Hello.runtimeconfig.json",
                $"{FileConstants.DynamicLibPrefix}coreclr{Constants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{Constants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}",
                $"mscorlib.dll",
                $"System.Private.CoreLib.dll",
            });

            Command.Create(selfContainedExecutableFullPath, new string[] { })
                .EnsureExecutable()
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello from a netcoreapp2.0.!");
        }

        [Fact]
        public void A_deployment_project_can_reference_the_hello_world_project()
        {
            var rid = RuntimeEnvironment.GetRuntimeIdentifier();

            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("DeployProjectReferencingSdkProject")
                .WithSource()
                .Restore(relativePath: "HelloWorld", args: $"/p:RuntimeIdentifiers={rid}");

            var buildCommand = new BuildCommand(Stage0MSBuild, helloWorldAsset.TestRoot, @"DeployProj\Deploy.proj");

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}