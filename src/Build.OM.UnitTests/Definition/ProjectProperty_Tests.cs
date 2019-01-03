// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectProperty
    /// </summary>
    public class ProjectProperty_Tests
    {
        /// <summary>
        /// Project getter
        /// </summary>
        [Fact]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v");

            Assert.True(Object.ReferenceEquals(project, property.Project));
        }

        /// <summary>
        /// Property with nothing to expand
        /// </summary>
        [Fact]
        public void NoExpansion()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <p>v1</p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectProperty property = GetFirstProperty(content);

            Assert.NotNull(property.Xml);
            Assert.Equal("p", property.Name);
            Assert.Equal("v1", property.EvaluatedValue);
            Assert.Equal("v1", property.UnevaluatedValue);
        }

        /// <summary>
        /// Embedded property
        /// </summary>
        [Fact]
        public void ExpandProperty()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <o>v1</o>
                            <p>$(o)</p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectProperty property = GetFirstProperty(content);

            Assert.NotNull(property.Xml);
            Assert.Equal("p", property.Name);
            Assert.Equal("v1", property.EvaluatedValue);
            Assert.Equal("$(o)", property.UnevaluatedValue);
        }

        /// <summary>
        /// Set the value of a property
        /// </summary>
        [Fact]
        public void SetValue()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v2";

            Assert.Equal("v2", property.EvaluatedValue);
            Assert.Equal("v2", property.UnevaluatedValue);
            Assert.True(project.IsDirty);
        }

        /// <summary>
        /// Set the value of a property
        /// </summary>
        [Fact]
        public void SetValue_Escaped()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v%282%29";

            Assert.Equal("v(2)", property.EvaluatedValue);
            Assert.Equal("v%282%29", property.UnevaluatedValue);
            Assert.True(project.IsDirty);
        }

        /// <summary>
        /// Set the value of a property to the same value.
        /// This should not dirty the project.
        /// </summary>
        [Fact]
        public void SetValueSameValue()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v1";

            Assert.False(project.IsDirty);
        }

        /// <summary>
        /// Attempt to set the value of a built-in property
        /// </summary>
        [Fact]
        public void InvalidSetValueBuiltInProperty()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                ProjectProperty property = project.GetProperty("MSBuildProjectDirectory");

                property.UnevaluatedValue = "v";
            }
           );
        }
        /// <summary>
        /// Set the value of a property originating in the environment.
        /// Should work even though there is no XML behind it.
        /// Also, should persist.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SetValueEnvironmentProperty()
        {
            Project project = new Project();
            string varName = NativeMethodsShared.IsWindows ? "username" : "USER";
            ProjectProperty property = project.GetProperty(varName);

            property.UnevaluatedValue = "v";

            Assert.Equal("v", property.EvaluatedValue);
            Assert.Equal("v", property.UnevaluatedValue);

            project.ReevaluateIfNecessary();

            property = project.GetProperty(varName);
            Assert.Equal("v", property.UnevaluatedValue);
        }

        /// <summary>
        /// Test IsEnvironmentVariable
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void IsEnvironmentVariable()
        {
            Project project = new Project();
            string varName = NativeMethodsShared.IsWindows ? "username" : "USER";

            Assert.True(project.GetProperty(varName).IsEnvironmentProperty);
            Assert.False(project.GetProperty(varName).IsGlobalProperty);
            Assert.False(project.GetProperty(varName).IsReservedProperty);
            Assert.False(project.GetProperty(varName).IsImported);
        }

        /// <summary>
        /// Test IsGlobalProperty
        /// </summary>
        [Fact]
        public void IsGlobalProperty()
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties["g"] = String.Empty;
            Project project = new Project(globalProperties, null, ProjectCollection.GlobalProjectCollection);

            Assert.False(project.GetProperty("g").IsEnvironmentProperty);
            Assert.True(project.GetProperty("g").IsGlobalProperty);
            Assert.False(project.GetProperty("g").IsReservedProperty);
            Assert.False(project.GetProperty("g").IsImported);
        }

        /// <summary>
        /// Test IsReservedProperty
        /// </summary>
        [Fact]
        public void IsReservedProperty()
        {
            Project project = new Project();
            project.FullPath = @"c:\x";
            project.ReevaluateIfNecessary();

            Assert.False(project.GetProperty("MSBuildProjectFile").IsEnvironmentProperty);
            Assert.False(project.GetProperty("MSBuildProjectFile").IsGlobalProperty);
            Assert.True(project.GetProperty("MSBuildProjectFile").IsReservedProperty);
            Assert.False(project.GetProperty("MSBuildProjectFile").IsImported);
        }

        /// <summary>
        /// Verify properties are expanded in new property values
        /// </summary>
        [Fact]
        public void SetPropertyWithPropertyExpression()
        {
            Project project = new Project();
            project.SetProperty("p0", "v0");
            ProjectProperty property = project.SetProperty("p1", "v1");
            property.UnevaluatedValue = "$(p0)";

            Assert.Equal("v0", project.GetPropertyValue("p1"));
            Assert.Equal("v0", property.EvaluatedValue);
            Assert.Equal("$(p0)", property.UnevaluatedValue);
        }

        /// <summary>
        /// Verify item expressions are not expanded in new property values.
        /// NOTE: They aren't expanded to "blank". It just seems like that, because
        /// when you output them, item expansion happens after property expansion, and 
        /// they may evaluate to blank then. (Unless items do exist at that point.)
        /// </summary>
        [Fact]
        public void SetPropertyWithItemAndMetadataExpression()
        {
            Project project = new Project();
            project.SetProperty("p0", "v0");
            ProjectProperty property = project.SetProperty("p1", "v1");
            property.UnevaluatedValue = "@(i)-%(m)";

            Assert.Equal("@(i)-%(m)", project.GetPropertyValue("p1"));
            Assert.Equal("@(i)-%(m)", property.EvaluatedValue);
            Assert.Equal("@(i)-%(m)", property.UnevaluatedValue);
        }

        /// <summary>
        /// Attempt to set value on imported property should fail
        /// </summary>
        [Fact]
        public void SetPropertyImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                    Project import = new Project();
                    import.SetProperty("p", "v0");
                    import.Save(file);

                    ProjectRootElement xml = ProjectRootElement.Create();
                    xml.AddImport(file);
                    Project project = new Project(xml);

                    ProjectProperty property = project.GetProperty("p");
                    property.UnevaluatedValue = "v1";
                }
                finally
                {
                    File.Delete(file);
                }
            }
           );
        }
        /// <summary>
        /// Get the property named "p" in the project provided
        /// </summary>
        private static ProjectProperty GetFirstProperty(string content)
        {
            ProjectRootElement projectXml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(projectXml);
            ProjectProperty property = project.GetProperty("p");

            return property;
        }
    }
}
