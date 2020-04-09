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
    public class GivenThatWeWantACustomTFM : SdkTest
    {
        public GivenThatWeWantACustomTFM(ITestOutputHelper log) : base(log)
        {}

        [Fact]
        public void It_imports_custom_parsing_targets()
        {
            TestProject testProject = new TestProject()
            {
                Name = "CustomTFMProject",
                IsSdkProject = true, 
                IsExe = true, 
                TargetFrameworks = "netcoreapp3.0"
            };

            testProject.AdditionalProperties["CustomTargetFrameworkParsingTargets"] = @"$(MSBuildProjectDirectory)\CustomTargetFramework.targets";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var propertyName = "CustomTargetFrameworkProp";
            File.WriteAllText(Path.Combine(testAsset.TestRoot, testProject.Name, "CustomTargetFramework.targets"), $@"
<Project ToolsVersion=`14.0` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
  <PropertyGroup>
    <{ propertyName }>TargetFramework: $(TargetFramework), TargetFrameworkVersion: $(TargetFrameworkVersion)</{ propertyName }>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name),
                    testProject.TargetFrameworks, propertyName, GetValuesCommand.ValueType.Property)
            {
                Configuration = "Debug"
            };
            getValuesCommand
                .Execute("/bl:C:/code/binlogs/binlog.binlog")
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();
            values.Count.Should().Be(1);
            values[0].Trim().Should().Be("TargetFramework: netcoreapp3.0, TargetFrameworkVersion:");
        }
    }
}
