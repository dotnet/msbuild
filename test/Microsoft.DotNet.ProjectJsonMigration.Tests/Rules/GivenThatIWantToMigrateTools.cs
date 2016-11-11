// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using System.Linq;
using Xunit;
using FluentAssertions;
using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateTools : PackageDependenciesTestBase
    {
        [Theory]
        [InlineData("Microsoft.EntityFrameworkCore.Tools", "Microsoft.EntityFrameworkCore.Tools", ConstantPackageVersions.AspNetToolsVersion)]
        [InlineData("Microsoft.VisualStudio.Web.CodeGenerators.Mvc", "Microsoft.VisualStudio.Web.CodeGeneration.Design", ConstantPackageVersions.AspNetToolsVersion)]
        public void It_migrates_project_dependencies_to_a_new_name_and_version(
            string sourceToolName,
            string targetToolName,
            string targetVersion)
        {
            const string anyVersion = "1.0.0-preview2-final";
            var mockProj = RunPackageDependenciesRuleOnPj("{ \"dependencies\": { \"" + sourceToolName + "\" : { \"version\": \"" + anyVersion + "\", \"type\": \"build\" } } }");
            
            var packageRef = mockProj.Items.First(i => i.Include == targetToolName && i.ItemType == "PackageReference");

            packageRef.GetMetadataWithName("Version").Value.Should().Be(ConstantPackageVersions.AspNetToolsVersion);

            packageRef.GetMetadataWithName("PrivateAssets").Value.Should().NotBeNull().And.Be("All");
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.Razor.Tools")]
        [InlineData("Microsoft.AspNetCore.Razor.Design")]
        [InlineData("Microsoft.VisualStudio.Web.CodeGeneration.Tools")]
        [InlineData("dotnet-test-xunit")]
        [InlineData("dotnet-test-mstest")]
        public void It_does_not_migrate_project_tool_dependency_that_is_no_longer_needed(string dependencyName)
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""dependencies"": {
                        """ + dependencyName + @""" : {
                            ""version"": ""1.0.0-preview2-final"",
                            ""type"": ""build""
                        }
                    }
                }");

            var packageRef = mockProj.Items.Where(i =>
                i.Include != "Microsoft.NET.Sdk" &&
                i.Include != "NETStandard.Library" &&
                i.ItemType == "PackageReference").Should().BeEmpty();
        }

        [Theory]
        [InlineData("Microsoft.EntityFrameworkCore.Tools", "Microsoft.EntityFrameworkCore.Tools.DotNet", ConstantPackageVersions.AspNetToolsVersion)]
        [InlineData("Microsoft.VisualStudio.Web.CodeGeneration.Tools", "Microsoft.VisualStudio.Web.CodeGeneration.Tools", ConstantPackageVersions.AspNetToolsVersion)]
        [InlineData("Microsoft.DotNet.Watcher.Tools", "Microsoft.DotNet.Watcher.Tools", ConstantPackageVersions.AspNetToolsVersion)]
        [InlineData("Microsoft.Extensions.SecretManager.Tools", "Microsoft.Extensions.SecretManager.Tools", ConstantPackageVersions.AspNetToolsVersion)]
        [InlineData("BundlerMinifier.Core", "BundlerMinifier.Core", ConstantPackageVersions.BundleMinifierToolVersion)]
        public void It_migrates_asp_project_tools_to_a_new_name_and_version(
            string sourceToolName,
            string targetToolName,
            string targetVersion)
        {
            const string anyVersion = "1.0.0-preview2-final";
            var mockProj = RunPackageDependenciesRuleOnPj("{ \"tools\": { \"" + sourceToolName + "\": \"" + anyVersion + "\" } }");
            
            EmitsToolReferences(mockProj, Tuple.Create(targetToolName, targetVersion));
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.Razor.Tools")]
        [InlineData("Microsoft.AspNetCore.Server.IISIntegration.Tools")]
        public void It_does_not_migrate_asp_project_tool(string toolName)
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""tools"": {
                        """ + toolName + @""": ""1.0.0-preview2-final""
                    }
                }");

            var packageRef = mockProj.Items.Where(i => i.ItemType == "DotNetCliToolReference").Should().BeEmpty();
        }
    }
}