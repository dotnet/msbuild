// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Publish.Tests
{
    public class GivenThatWeWantToPublishAHelloWorldProject
    {
        private TestAssetsManager _testAssetsManager;

        public GivenThatWeWantToPublishAHelloWorldProject()
        {
            _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;
        }

        [Fact]
        public void It_publishes_portable_apps_to_the_publish_folder_and_the_app_should_run()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore();

            var publishCommand = new PublishCommand(Stage0MSBuild, helloWorldAsset.TestRoot);
            // Temporarily pass in the TFM to publish until https://github.com/dotnet/sdk/issues/175 is addressed
            var publishResult = publishCommand.Execute("/p:TargetFramework=netcoreapp1.0");

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

        // https://github.com/dotnet/sdk/issues/116 - need to support self-contained apps in msbuild /t:restore
        //[Fact]
        public void It_publishes_self_contained_apps_to_the_publish_folder_and_the_app_should_run()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                //.AsSelfContained()
                .Restore();

            var publishCommand = new PublishCommand(Stage0MSBuild, helloWorldAsset.TestRoot);
            var publishResult = publishCommand.Execute($"/p:RuntimeIdentifier={RuntimeEnvironment.GetRuntimeIdentifier()}");

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(selfContained: true);
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            var libPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";

            publishDirectory.Should().HaveFiles(new[] {
                selfContainedExecutable,
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json",
                $"{libPrefix}coreclr{Constants.DynamicLibSuffix}",
                $"{libPrefix}hostfxr{Constants.DynamicLibSuffix}",
                $"{libPrefix}hostpolicy{Constants.DynamicLibSuffix}",
                $"mscorlib.dll",
                $"System.Private.CoreLib.dll",
            });

            Command.Create(Path.Combine(publishDirectory.FullName, selfContainedExecutable), new string[] { })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }
    }
}