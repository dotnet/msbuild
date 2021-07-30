// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatICareAboutVBApps : SdkTest
    {
        public GivenThatICareAboutVBApps(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ICanRemoveSpecificImportsInProjectFile()
        {
            var testProject = new TestProject
            {
                IsExe = true,
                TargetFrameworks = "netcoreapp3.0",
                ProjectSdk = "Microsoft.NET.Sdk"
            };
            testProject.SourceFiles["Program.vb"] = @"
Module Program
    Sub Main(args As String())
        Console.WriteLine(""Hello World!"")
    End Sub
End Module
";
            testProject.AdditionalItems["Import"] = new Dictionary<string, string> { ["Remove"] = "System" };
            var testInstance = _testAssetsManager.CreateTestProject(testProject);

            new BuildCommand(testInstance)
                .Execute()
                .Should().Fail();
        }

        [Fact]
        public void ICanBuildVBApps()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ICanRunVBApps()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("run")
                .Should().Pass();
        }

        [Fact]
        public void ICanPublicAndRunVBApps()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            new PublishCommand(Log, testInstance.Path)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(
                testInstance.Path,
                "bin",
                configuration,
                "netcoreapp3.1",
                "publish",
                "VBTestApp.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }
    }
}
