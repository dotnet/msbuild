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

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToProduceReferenceAssembly : SdkTest
    {
        public GivenThatWeWantToProduceReferenceAssembly(ITestOutputHelper log) : base(log)
        {}

        [RequiresMSBuildVersionTheory("17.7.0")]
        [InlineData("netcoreapp3.1", ".csproj")]
        [InlineData("net5.0", ".csproj")]
        [InlineData("net5.0", ".fsproj")]
        [InlineData("net6.0", ".csproj")]
        [InlineData("net6.0", ".fsproj")]
        [InlineData("net7.0", ".csproj")]
        [InlineData("net7.0", ".fsproj")]
#pragma warning disable xUnit1025 // InlineData duplicates
        [InlineData(ToolsetInfo.CurrentTargetFramework, ".csproj")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ".fsproj")]
#pragma warning restore xUnit1025 // InlineData duplicates
        public void It_produces_ref_assembly_for_appropriate_frameworks(string targetFramework, string extension)
        {
            TestProject testProject = new()
            {
                Name = "ProduceRefAssembly",
                IsExe = true,
                TargetFrameworks = targetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + extension, targetExtension: extension);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
            var filePath = Path.Combine(testAsset.Path, testProject.Name, "obj", "Debug", targetFramework, "ref", $"{testProject.Name}.dll");
            File.Exists(filePath).Should().BeTrue();
        }
    }
}
