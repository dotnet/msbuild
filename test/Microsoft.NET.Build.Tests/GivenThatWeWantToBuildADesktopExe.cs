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

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExe
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
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

        [Theory]
        [InlineData("win7-x86", "x86")]
        [InlineData("win8-x86-aot", "x86")]
        [InlineData("win7-x64", "x64")]
        [InlineData("win8-x64-aot", "x64")]
        [InlineData("win10-arm", "arm")]
        [InlineData("win10-arm-aot", "arm")]
        //PlatformTarget=arm64 is not supported and never inferred
        [InlineData("win10-arm64", "AnyCPU")]
        [InlineData("win10-arm64-aot", "AnyCPU")]
        // cpu architecture is never expected at the front
        [InlineData("x86-something", "AnyCPU")]
        [InlineData("x64-something", "AnyCPU")]
        [InlineData("arm-something", "AnyCPU")]
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

            var buildCommand = new BuildCommand(Stage0MSBuild, testAsset.TestRoot);
            buildCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("net46").FullName, "DesktopMinusRid.exe");
            var assemblyInfo = AssemblyInfo.Get(assemblyPath);
            assemblyInfo
                .Should()
                .Contain(
                    "AssemblyDescriptionAttribute",
                    $"PlatformTarget={expectedPlatformTarget}");
        }

        [Fact]
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

            var buildCommand = new BuildCommand(Stage0MSBuild, testAsset.TestRoot);
            buildCommand
                .Execute($"/p:RuntimeIdentifier=win7-x86", "/p:PlatformTarget=x64")
                .Should()
                .Pass();

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("net46").FullName, "DesktopMinusRid.exe");
            var assemblyInfo = AssemblyInfo.Get(assemblyPath);
            assemblyInfo
                .Should()
                .Contain(
                    "AssemblyDescriptionAttribute",
                    $"PlatformTarget=x64");
        }
    }
}
