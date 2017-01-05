// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExe : SdkTest
    {
        //[Fact]
        public void It_fails_to_build_if_no_rid_is_set()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopMinusRid")
                .WithSource()
                .Restore();

            var buildCommand = new BuildCommand(Stage0MSBuild, testAsset.TestRoot);
            buildCommand
                .MSBuild
                .CreateCommandForTarget("build", buildCommand.FullPathProjectFile)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("RuntimeIdentifier must be set");
        }

        //[Theory]
        //[InlineData("win7-x86", "x86")]
        //[InlineData("win8-x86-aot", "x86")]
        //[InlineData("win7-x64", "x64")]
        //[InlineData("win8-x64-aot", "x64")]
        //[InlineData("win10-arm", "arm")]
        //[InlineData("win10-arm-aot", "arm")]
        ////PlatformTarget=arm64 is not supported and never inferred
        //[InlineData("win10-arm64", "AnyCPU")]
        //[InlineData("win10-arm64-aot", "AnyCPU")]
        //// cpu architecture is never expected at the front
        //[InlineData("x86-something", "AnyCPU")]
        //[InlineData("x64-something", "AnyCPU")]
        //[InlineData("arm-something", "AnyCPU")]
        public void It_builds_with_inferred_platform_target(string runtimeIdentifier, string expectedPlatformTarget)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopMinusRid", identifier: Path.DirectorySeparatorChar + runtimeIdentifier)
                .WithSource()
                .Restore("", $"/p:RuntimeIdentifier={runtimeIdentifier}");

            var getValuesCommand = new GetValuesCommand(Stage0MSBuild, testAsset.TestRoot,
                "net46", "PlatformTarget", GetValuesCommand.ValueType.Property);

            getValuesCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo(expectedPlatformTarget);
        }

        //[Fact]
        public void It_respects_explicit_platform_target()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopMinusRid")
                .WithSource()
                .Restore("", $"/p:RuntimeIdentifier=win7-x86");

            var getValuesCommand = new GetValuesCommand(Stage0MSBuild, testAsset.TestRoot,
                "net46", "PlatformTarget", GetValuesCommand.ValueType.Property);

            getValuesCommand
                .Execute($"/p:RuntimeIdentifier=win7-x86", "/p:PlatformTarget=x64")
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo("x64");
        }

        //[Fact]
        public void It_includes_default_framework_references()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "DefaultReferences",
                //  TODO: Add net35 to the TargetFrameworks list once https://github.com/Microsoft/msbuild/issues/1333 is fixed
                TargetFrameworks = "net40;net45;net461",
                IsSdkProject = true,
                IsExe = true
            };

            string sourceFile =
@"using System;

namespace DefaultReferences
{
    public class TestClass
    {
        public static void Main(string [] args)
        {
            var uri = new System.Uri(""http://github.com/dotnet/corefx"");
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        }
    }
}";
            testProject.SourceFiles.Add("TestClass.cs", sourceFile);

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore("DefaultReferences");

            var buildCommand = new BuildCommand(Stage0MSBuild, Path.Combine(testAsset.TestRoot, "DefaultReferences"));

            buildCommand
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Could not resolve this reference", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        }

        //[Fact]
        public void It_generates_binding_redirects_if_needed()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource()
                .Restore();

            var buildCommand = new BuildCommand(Stage0MSBuild, testAsset.TestRoot);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("net452");

            outputDirectory.Should().HaveFiles(new[] {
                "DesktopNeedsBindingRedirects.exe",
                "DesktopNeedsBindingRedirects.exe.config"
            });
        }
    }
}
