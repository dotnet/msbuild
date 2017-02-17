// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using System.IO;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASelfContainedAppWithLibrariesAndRid : SdkTest
    {
        [Fact]
        public void It_builds_a_RID_specific_runnable_output()
        {
            if (UsingFullFrameworkMSBuild)
            {
                //  Disable this test on full framework, as the current build won't have access to 
                //  https://github.com/Microsoft/msbuild/pull/1674
                //  See https://github.com/dotnet/sdk/issues/877
                return;
            }

            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid")
                .WithSource();
            
            var projectPath = Path.Combine(testAsset.TestRoot, "App");

            var restoreCommand = new RestoreCommand(Stage0MSBuild, projectPath, "App.csproj"); 
            restoreCommand 
                .Execute($"/p:TestRuntimeIdentifier={runtimeIdentifier}") 
                .Should() 
                .Pass(); 

            var buildCommand = new BuildCommand(Stage0MSBuild, projectPath);

            buildCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}", $"/p:TestRuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.0", runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"App{Constants.ExeSuffix}";

            Command.Create(Path.Combine(outputDirectory.FullName, selfContainedExecutable), new string[] { })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World");
        }
    }
}
