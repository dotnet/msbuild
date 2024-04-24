// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectPropertyGroupElement class
    /// </summary>
    public class ProjectPropertyGroupElement_Tests
    {
        /// <summary>
        /// Read property groups in an empty project
        /// </summary>
        [Fact]
        public void ReadNoPropertyGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.Equal(0, Helpers.Count(project.Children));
            Assert.Empty(project.PropertyGroups);
        }

        /// <summary>
        /// Read an empty property group
        /// </summary>
        [Fact]
        public void ReadEmptyPropertyGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <PropertyGroup/>
                    </Project>
                ");

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectPropertyGroupElement group = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);

            Assert.Equal(0, Helpers.Count(group.Properties));
        }

        /// <summary>
        /// Read an property group with two property children
        /// </summary>
        [Fact]
        public void ReadPropertyGroupTwoProperties()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <PropertyGroup>
                            <p1/>
                            <p2>v1</p2>
                        </PropertyGroup>
                    </Project>
                ");

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectPropertyGroupElement group = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);

            var properties = Helpers.MakeList(group.Properties);
            Assert.Equal(2, properties.Count);
            Assert.Equal("p1", properties[0].Name);
            Assert.Equal("p2", properties[1].Name);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectPropertyGroupElement propertyGroup = Helpers.GetFirst(project.PropertyGroups);
            propertyGroup.Condition = "c";

            Assert.Equal("c", propertyGroup.Condition);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the Label value
        /// </summary>
        [Fact]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectPropertyGroupElement propertyGroup = Helpers.GetFirst(project.PropertyGroups);
            propertyGroup.Label = "c";

            Assert.Equal("c", propertyGroup.Label);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Add a property through the convenience method on a property group
        /// </summary>
        [Fact]
        public void AddProperty_ExistingPropertySameName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyGroupElement propertyGroup = project.AddPropertyGroup();
            propertyGroup.AddProperty("p", "v1");
            propertyGroup.AddProperty("p", "v2");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
    <p>v2</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }
    }
}
