// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToSetPropertiesInDirectoryBuildProps : SdkTest
    {
        public GivenThatWeWantToSetPropertiesInDirectoryBuildProps(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void The_default_configuration_can_be_set_to_release()
        {
            TestProject project = new TestProject()
            {
                Name = "DirectoryBuildPropsTest",
                TargetFrameworks = "netstandard1.4",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(project);

            string directoryBuildPropsPath = Path.Combine(testAsset.Path, "Directory.Build.props");
            
            var directoryBuildPropsContent = @"
<Project>
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Release</Configuration>
  </PropertyGroup>
</Project>
";

            File.WriteAllText(directoryBuildPropsPath, directoryBuildPropsContent);

            var restoreCommand = testAsset.GetRestoreCommand(Log, project.Name);

            restoreCommand
                .Execute()
                .Should()
                .Pass();

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string GetPropertyValue(string propertyName)
            {
                var getValuesCommand = new GetValuesCommand(Log, projectFolder,
                    project.TargetFrameworks, propertyName, GetValuesCommand.ValueType.Property)
                {
                    Configuration = "Release"
                };

                getValuesCommand
                    .Execute()
                    .Should()
                    .Pass();

                var values = getValuesCommand.GetValues();
                values.Count.Should().Be(1);
                return values[0];
            }

            GetPropertyValue("Configuration").Should().Be("Release");
            GetPropertyValue("Optimize").Should().Be("true");
        }

    }
}
