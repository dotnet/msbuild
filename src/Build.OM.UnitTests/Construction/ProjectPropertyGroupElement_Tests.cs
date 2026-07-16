// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectPropertyGroupElement class
    /// </summary>
    [TestClass]
    public class ProjectPropertyGroupElement_Tests
    {
        /// <summary>
        /// Read property groups in an empty project
        /// </summary>
        [MSBuildTestMethod]
        public void ReadNoPropertyGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.AreEqual(0, Helpers.Count(project.Children));
            Assert.IsEmpty(project.PropertyGroups);
        }

        /// <summary>
        /// Read an empty property group
        /// </summary>
        [MSBuildTestMethod]
        public void ReadEmptyPropertyGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <PropertyGroup/>
                    </Project>
                ");

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectPropertyGroupElement group = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(0, Helpers.Count(group.Properties));
        }

        /// <summary>
        /// Read an property group with two property children
        /// </summary>
        [MSBuildTestMethod]
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

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectPropertyGroupElement group = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);

            var properties = Helpers.MakeList(group.Properties);
            Assert.AreEqual(2, properties.Count);
            Assert.AreEqual("p1", properties[0].Name);
            Assert.AreEqual("p2", properties[1].Name);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectPropertyGroupElement propertyGroup = Helpers.GetFirst(project.PropertyGroups);
            propertyGroup.Condition = "c";

            Assert.AreEqual("c", propertyGroup.Condition);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the Label value
        /// </summary>
        [MSBuildTestMethod]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectPropertyGroupElement propertyGroup = Helpers.GetFirst(project.PropertyGroups);
            propertyGroup.Label = "c";

            Assert.AreEqual("c", propertyGroup.Label);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Add a property through the convenience method on a property group
        /// </summary>
        [MSBuildTestMethod]
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
