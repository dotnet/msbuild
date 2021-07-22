// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Collections.Generic;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToProduceReferenceAssembly : SdkTest
    {
        public GivenThatWeWantToProduceReferenceAssembly(ITestOutputHelper log) : base(log)
        {}

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.1", false, false)]
        [InlineData("net5.0", true, false)]
        [InlineData("net5.0", true, true)]
        public void It_produces_ref_assembly_for_appropriate_frameworks(string targetFramework, bool expectedExists, bool expectedInBinDir)
        {
            TestProject testProject = new TestProject()
            {
                Name = "ProduceRefAssembly",
                IsExe = true, 
                TargetFrameworks = targetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + expectedExists + expectedInBinDir);

            var buildCommand = new BuildCommand(testAsset);
            var buildArgument = expectedInBinDir ? "/p:ProduceReferenceAssemblyInOutDir=true" : string.Empty;
            buildCommand.Execute(buildArgument)
                .Should()
                .Pass();

            // TODO: Remove after Framework MSBuild picks up https://github.com/dotnet/msbuild/pull/6560
            if (UsingFullFrameworkMSBuild)
            {
                var filePath = Path.Combine(testAsset.Path, testProject.Name, "obj", "Debug", targetFramework, "ref", $"{testProject.Name}.dll");
                File.Exists(filePath).Should().Be(expectedExists);
            }
            else
            {
                var filePath = Path.Combine(testAsset.Path, testProject.Name, "obj", "Debug", targetFramework, "ref", $"{testProject.Name}.dll");
                File.Exists(filePath).Should().Be(expectedExists && !expectedInBinDir);

                var intFilePath = Path.Combine(testAsset.Path, testProject.Name, "obj", "Debug", targetFramework, "refint", $"{testProject.Name}.dll");
                File.Exists(intFilePath).Should().Be(expectedExists);

                var binFilePath = Path.Combine(testAsset.Path, testProject.Name, "bin", "Debug", targetFramework, "ref", $"{testProject.Name}.dll");
                File.Exists(binFilePath).Should().Be(expectedExists && expectedInBinDir);
            }
        }
    }
}
