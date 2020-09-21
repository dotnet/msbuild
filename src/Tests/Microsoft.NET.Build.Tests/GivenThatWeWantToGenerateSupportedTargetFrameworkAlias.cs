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
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToGenerateSupportedTargetFrameworkAlias : SdkTest
    {
        public GivenThatWeWantToGenerateSupportedTargetFrameworkAlias(ITestOutputHelper log) : base(log)
        {}

        [Theory]
        [InlineData("netcoreapp3.1")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        [InlineData("netstandard2.1")]
        [InlineData("net48")]
        public void It_generates_supported_target_framework_alias_items(string currentTargetFramework)
        {
            TestTargetFrameworkAlias(currentTargetFramework, propertySetToTrue: null, new[]
                {
                    "netcoreapp3.0",
                    "netcoreapp3.1",
                    "net5.0",
                    "net6.0",
                    "netstandard2.0",
                    "netstandard2.1",
                    "net471",
                    "net48"
                });
        }

        [WindowsOnlyTheory]
        [InlineData("net5.0-windows")]
        [InlineData("net6.0-windows")]
        public void It_generates_supported_target_framework_alias_items_when_targeting_windows(string currentTargetFramework)
        {
            TestTargetFrameworkAlias(currentTargetFramework, propertySetToTrue: null, new[]
                {
                    "netcoreapp3.0",
                    "netcoreapp3.1",
                    "net5.0-windows7.0",
                    "net6.0-windows7.0",
                    "netstandard2.0",
                    "netstandard2.1",
                    "net471",
                    "net48"
                });
        }

        [WindowsOnlyTheory]
        [InlineData("net5.0", "UseWpf")]
        [InlineData("net5.0", "UseWindowsForms")]
        [InlineData("net5.0-windows", "UseWpf")]
        [InlineData("net5.0-windows", "UseWindowsForms")]
        [InlineData("netcoreapp3.1", "UseWpf")]
        [InlineData("netcoreapp3.1", "UseWindowsForms")]
        public void It_generates_supported_target_framework_alias_items_when_using_wpf_or_winforms(string currentTargetFramework, string propertyName)
        {
            TestTargetFrameworkAlias(currentTargetFramework, propertySetToTrue: propertyName, new[]
                {
                    "netcoreapp3.0",
                    "netcoreapp3.1",
                    "net5.0-windows",
                    "net6.0-windows",
                    "netstandard2.0",
                    "netstandard2.1",
                    "net471",
                    "net48"
                });
        }

        private void TestTargetFrameworkAlias(string targetFramework, string propertySetToTrue, string[] expectedSupportedTargetFrameworkAliases)
        {
            TestProject testProject = new TestProject()
            {
                Name = "MockTargetFrameworkAliasItemGroup",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["NETCoreAppMaximumVersion"] = "6.0";

            if (!string.IsNullOrEmpty(propertySetToTrue))
            {
                testProject.AdditionalProperties[propertySetToTrue] = "true";
            }

            var mockSupportedTargetFramework = new List<(string targetFrameworkMoniker, string displayName)>()
            {
                ( ".NETCoreApp,Version=v3.0", ".NET Core 3.0"),
                ( ".NETCoreApp,Version=v3.1", ".NET Core 3.1"),
                ( ".NETCoreApp,Version=v5.0", ".NET 5"),
                ( ".NETCoreApp,Version=v6.0", ".NET 6"),
                ( ".NETStandard,Version=v2.0", ".NET Standard 2.0"),
                ( ".NETStandard,Version=v2.1", ".NET Standard 2.1"),
                ( ".NETFramework,Version=v4.7.1", ".NET Framework 4.7.1"),
                ( ".NETFramework,Version=v4.8", ".NET Framework 4.8"),
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
                                        new XAttribute("Include", tfm.targetFrameworkMoniker),
                                        new XAttribute("DisplayName", tfm.displayName));
                    itemGroup.Add(mockTfm);
                }
            });

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework, "SupportedTargetFrameworkAlias", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "GenerateSupportedTargetFrameworkAlias"
            };
            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();
            var valuesForTargetFrameworkIdentifier =
                values.Where(v => NuGetFramework.Parse(v).Framework == NuGetFramework.Parse(targetFramework).Framework).ToList();
            values.ShouldBeEquivalentTo(expectedSupportedTargetFrameworkAliases);
        }
    }
}
