//-----------------------------------------------------------------------
// <copyright file="ProjectPropertyGroupElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test the ProjectPropertyGroupElement class.</summary>
//-----------------------------------------------------------------------

using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        [TestMethod]
        public void ReadNoPropertyGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.AreEqual(0, Helpers.Count(project.Children));
            Assert.AreEqual(null, project.PropertyGroups.GetEnumerator().Current);
        }

        /// <summary>
        /// Read an empty property group
        /// </summary>
        [TestMethod]
        public void ReadEmptyPropertyGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <PropertyGroup/>
                    </Project>
                ");

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectPropertyGroupElement group = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(0, Helpers.Count(group.Properties));
        }

        /// <summary>
        /// Read an property group with two property children
        /// </summary>
        [TestMethod]
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
            Assert.AreEqual(2, properties.Count);
            Assert.AreEqual("p1", properties[0].Name);
            Assert.AreEqual("p2", properties[1].Name);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectPropertyGroupElement propertyGroup = Helpers.GetFirst(project.PropertyGroups);
            propertyGroup.Condition = "c";

            Assert.AreEqual("c", propertyGroup.Condition);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the Label value
        /// </summary>
        [TestMethod]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectPropertyGroupElement propertyGroup = Helpers.GetFirst(project.PropertyGroups);
            propertyGroup.Label = "c";

            Assert.AreEqual("c", propertyGroup.Label);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Add a property through the convenience method on a property group
        /// </summary>
        [TestMethod]
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
