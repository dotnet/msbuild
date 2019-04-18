// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class AppHostTests : SdkTest
    {
        public AppHostTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void It_builds_a_runnable_apphost_by_default(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute(new string[] {
                    "/restore",
                })
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var hostExecutable = $"HelloWorld{Constants.ExeSuffix}";

            outputDirectory.Should().OnlyHaveFiles(new[] {
                hostExecutable,
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.dev.json",
                "HelloWorld.runtimeconfig.json",
            });

            Command.Create(Path.Combine(outputDirectory.FullName, hostExecutable), new string[] { })
                .EnvironmentVariable(
                    Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                    Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath))
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Theory]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp2.2")]
        public void It_does_not_build_with_an_apphost_by_default_before_netcoreapp_3(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute(new string[] { "/restore" })
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.dev.json",
                "HelloWorld.runtimeconfig.json",
            });
        }

        [WindowsOnlyTheory]
        [InlineData("x86")]
        [InlineData("x64")]
        [InlineData("AnyCPU")]
        [InlineData("")]
        public void It_uses_an_apphost_based_on_platform_target(string target)
        {
            var targetFramework = "netcoreapp3.0";

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute(new string[] {
                    "/restore",
                    $"/p:TargetFramework={targetFramework}",
                    $"/p:PlatformTarget={target}",
                    $"/p:NETCoreSdkRuntimeIdentifier={EnvironmentInfo.GetCompatibleRid(targetFramework)}"
                })
                .Should()
                .Pass();

            var apphostPath = Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, "HelloWorld.exe");
            if (target == "x86")
            {
                IsPE32(apphostPath).Should().BeTrue();
            }
            else if (target == "x64")
            {
                IsPE32(apphostPath).Should().BeFalse();
            }
            else
            {
                IsPE32(apphostPath).Should().Be(!Environment.Is64BitProcess);
            }
        }

        [WindowsOnlyFact]
        public void AppHost_contains_resources_from_the_managed_dll()
        {
            var targetFramework = "netcoreapp2.0";
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var version = "5.6.7.8";
            var testProject = new TestProject()
            {
                Name = "ResourceTest",
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsSdkProject = true,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("AssemblyVersion", version);

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            outputDirectory.Should().HaveFiles(new[] { testProject.Name + ".exe" });

            string apphostPath = Path.Combine(outputDirectory.FullName, testProject.Name + ".exe");
            var apphostVersion = FileVersionInfo.GetVersionInfo(apphostPath).FileVersion;
            apphostVersion.Should().Be(version);
        }

        [WindowsOnlyFact]
        public void FSharp_app_can_customize_the_apphost()
        {
            var targetFramework = "netcoreapp3.0";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorldFS")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Element(ns + "TargetFramework").SetValue(targetFramework);
                })
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute("/p:CopyLocalLockFileAssemblies=false")
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.deps.json",
                "TestApp.dll",
                "TestApp.exe",
                "TestApp.pdb",
                "TestApp.runtimeconfig.dev.json",
                "TestApp.runtimeconfig.json",
            });
        }

        [Fact]
        public void If_UseAppHost_is_false_it_does_not_try_to_find_an_AppHost()
        {
            var testProject = new TestProject()
            {
                Name = "NoAppHost",
                TargetFrameworks = "netcoreapp3.0",
                //  Use "any" as RID so that it will fail to find AppHost
                RuntimeIdentifier = "any",
                IsSdkProject = true,
                IsExe = true,
            };
            testProject.AdditionalProperties["SelfContained"] = "false";
            testProject.AdditionalProperties["UseAppHost"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, testProject.Name);

            buildCommand.Execute()
                .Should()
                .Pass();

        }

        private static bool IsPE32(string path)
        {
            using (var reader = new PEReader(File.OpenRead(path)))
            {
                return reader.PEHeaders.PEHeader.Magic == PEMagic.PE32;
            }
        }

    }
}
