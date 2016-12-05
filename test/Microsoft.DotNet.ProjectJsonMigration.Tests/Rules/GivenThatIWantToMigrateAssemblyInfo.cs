// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using System.Linq;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateAssemblyInfo : TestBase
    {
        private ProjectRootElement _mockProject;

        public GivenThatIWantToMigrateAssemblyInfo()
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance("AppWithAssemblyInfo").Path;
            var projectContext =
                ProjectContext.Create(projectDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);
            _mockProject = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(projectDirectory, projectDirectory, _mockProject, null);
            var testInputs = new MigrationRuleInputs(
                new[] {projectContext},
                _mockProject,
                _mockProject.AddItemGroup(),
                _mockProject.AddPropertyGroup());

            new MigrateAssemblyInfoRule().Apply(testSettings, testInputs);
        }

        [Fact]
        public void ItSetsGenerateAssemblyCompanyAttributeToFalseWhenAssemblyCompanyExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyCompanyAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyConfigurationAttributeToFalseWhenAssemblyConfigurationExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyConfigurationAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyCopyrightAttributeToFalseWhenAssemblyCopyrightExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyCopyrightAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyDescriptionAttributeToFalseWhenAssemblyDescriptionExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyDescriptionAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyFileVersionAttributeToFalseWhenAssemblyFileVersionExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyFileVersionAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyInformationalVersionAttributeToFalseWhenAssemblyInformationalVersionExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyInformationalVersionAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyProductAttributeToFalseWhenAssemblyProductExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyProductAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyTitleAttributeToFalseWhenAssemblyTitleExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyTitleAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateAssemblyVersionAttributeToFalseWhenAssemblyVersionExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateAssemblyVersionAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }

        [Fact]
        public void ItSetsGenerateNeutralResourcesLanguageAttributeToFalseWhenNeutralResourcesLanguageExists()
        {
            var generateAssemblyAttributes =
                _mockProject.Properties.Where(p => p.Name.Equals("GenerateNeutralResourcesLanguageAttribute", StringComparison.Ordinal));
            generateAssemblyAttributes.Count().Should().Be(1);
            generateAssemblyAttributes.First().Value.Should().Be("false");
        }
    }
}