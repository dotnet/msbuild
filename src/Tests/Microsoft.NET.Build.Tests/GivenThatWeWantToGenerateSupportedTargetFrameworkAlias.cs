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
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToGenerateSupportedTargetFrameworkAlias : SdkTest
    {
        public GivenThatWeWantToGenerateSupportedTargetFrameworkAlias(ITestOutputHelper log) : base(log)
        {}

        [Theory]
        [InlineData("", new string[] { ".NETCoreApp,Version=v3.1", ".NETCoreApp,Version=v5.0", ".NETStandard,Version=v2.1", ".NETFramework,Version=v4.7.2" }, new string[] { "netcoreapp3.1", "net5.0", "netstandard2.1", "net472" })] 
        [InlineData("Windows", new string[] { ".NETCoreApp,Version=v3.1", ".NETCoreApp,Version=v5.0" }, new string[] { "netcoreapp3.1", "net5.0-windows7.0" })]
        public void It_generates_supported_target_framework_alias_items(string targetPlatform, string[] mockSupportedTargetFramework, string[] expectedSupportedTargetFrameworkAlias)
        {
            var targetFramework = string.IsNullOrWhiteSpace(targetPlatform)? "net5.0" :  $"net5.0-{ targetPlatform }";
            TestProject testProject = new TestProject()
            {
                Name = "MockTargetFrameworkAliasItemGroup",
                IsSdkProject = true, 
                IsExe = true, 
                TargetFrameworks = targetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject).WithProjectChanges(project =>
            {
                // Replace the default SupportedTargetFramework ItemGroup with our mock items
                var ns = project.Root.Name.Namespace;
                var target = new XElement(ns + "Target",
                    new XAttribute("Name", "OverwriteSupportedTargetFramework"),
                    new XAttribute("BeforeTargets", "GenerateSupportedTargetFrameworkAlias"));

                project.Root.Add(target);

                var itemGroup = new XElement(ns + "ItemGroup");
                target.Add(itemGroup);

                var removeAll = new XElement(ns + "SupportedTargetFramework",
                    new XAttribute("Remove", "@(SupportedTargetFramework)"));
                itemGroup.Add(removeAll);

                foreach (var tfm in mockSupportedTargetFramework)
                {
                    var mockTfm = new XElement(ns + "SupportedTargetFramework",
                                        new XAttribute("Include", tfm));
                    itemGroup.Add(mockTfm);
                }
            });

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework, "SupportedTargetFrameworkAlias", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Build"
            };
            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();
            values.ShouldBeEquivalentTo(expectedSupportedTargetFrameworkAlias);
        }

        [Theory]
        [InlineData("UseWpf")]
        [InlineData("UseWindowsForms")]
        public void It_generates_supported_target_framework_alias_items_with_target_platform(string propertyName)
        {
            var targetFramework = "netcoreapp3.1";
            TestProject testProject = new TestProject()
            {
                Name = "TargetFrameworkAliasItemGroup",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties[propertyName] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework, "SupportedTargetFrameworkAlias", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Build",
                MetadataNames = { "DisplayName" }
            };
            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValuesWithMetadata();
            var net5Value = values.Where(value => value.value.Equals("net5.0-windows7.0"));
            net5Value.Should().NotBeNullOrEmpty();
            net5Value.FirstOrDefault().metadata.GetValueOrDefault("DisplayName").Should().Be(".NET 5.0");

            var net31Value = values.Where(value => value.value.Equals("netcoreapp3.1"));
            net31Value.Should().NotBeNullOrEmpty();
            net31Value.FirstOrDefault().metadata.GetValueOrDefault("DisplayName").Should().Be(".NET Core 3.1");
        }
    }
}
