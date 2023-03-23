// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishDepsFilePathTests : SdkTest
    {
        public PublishDepsFilePathTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void PublishDepsFilePathIsSetAsExpectedForNormalApps()
        {
            var testProject = SetupProject(singleFile: false);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectPath = Path.Combine(testAsset.Path, testProject.Name);
            var targetFramework = testProject.TargetFrameworks;

            var publishDir = GetPropertyValue(projectPath, targetFramework, "PublishDir");
            var projectDepsFileName = GetPropertyValue(projectPath, targetFramework, "ProjectDepsFileName");
            var publishDepsFilePath = GetPropertyValue(projectPath, targetFramework, "PublishDepsFilePath");
            var expectedDepsFilePath = $"{publishDir}{projectDepsFileName}";

            publishDepsFilePath.Should().Be(expectedDepsFilePath);
        }

        [Fact]
        public void PublishDepsFilePathIsEmptyForSingleFileApps()
        {
            var testProject = SetupProject(singleFile: true);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var projectPath = Path.Combine(testAsset.Path, testProject.Name);
            var targetFramework = testProject.TargetFrameworks;
            var publishDepsFilePath = GetPropertyValue(projectPath, targetFramework, "PublishDepsFilePath");

            String.IsNullOrEmpty(publishDepsFilePath).Should().BeTrue();
        }

        string GetPropertyValue(string projectPath, string targetFramework, string property)
        {
            var getValuesCommand = new GetValuesCommand(Log, projectPath, targetFramework, property)
            {
                DependsOnTargets = "GeneratePublishDependencyFile"
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();

            return values.Any() ? values.Single() : null;  
        }

        private TestProject SetupProject(bool singleFile)
        {
            var testProject = new TestProject()
            {
                Name = "TestsPublishDepsFilePath",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = "win-x64";
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            if (singleFile)
            {
                testProject.AdditionalProperties["PublishSingleFile"] = "true";
            }

            return testProject;
        }
    }
}
