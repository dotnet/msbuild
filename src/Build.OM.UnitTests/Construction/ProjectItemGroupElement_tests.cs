// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectItemGroupElement class
    /// </summary>
    [TestClass]
    public class ProjectItemGroupElement_tests
    {
        /// <summary>
        /// Read item groups in an empty project
        /// </summary>
        [MSBuildTestMethod]
        public void ReadNoItemGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.AreEqual(0, Helpers.Count(project.Children));
            Assert.IsEmpty(project.ItemGroups);
        }

        /// <summary>
        /// Read an empty item group
        /// </summary>
        [MSBuildTestMethod]
        public void ReadEmptyItemGroup()
        {
            string content = @"
                    <Project>
                        <ItemGroup/>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectItemGroupElement group = (ProjectItemGroupElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(0, Helpers.Count(group.Items));
        }

        /// <summary>
        /// Read an item group with two item children
        /// </summary>
        [MSBuildTestMethod]
        public void ReadItemGroupTwoItems()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'/>
                            <i Include='i2'/>
                        </ItemGroup>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectItemGroupElement group = (ProjectItemGroupElement)Helpers.GetFirst(project.Children);

            var items = Helpers.MakeList(group.Items);

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("i1", items[0].Include);
            Assert.AreEqual("i2", items[1].Include);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemGroupElement itemGroup = Helpers.GetFirst(project.ItemGroups);
            itemGroup.Condition = "c";

            Assert.AreEqual("c", itemGroup.Condition);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the Label value
        /// </summary>
        [MSBuildTestMethod]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemGroupElement itemGroup = Helpers.GetFirst(project.ItemGroups);
            itemGroup.Label = "c";

            Assert.AreEqual("c", itemGroup.Label);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        [MSBuildTestMethod]
        [DataRow("""
            <Project>
                <ItemGroup>
                    <PackageReference Include="A" Version="1.0.0" /><!-- some comment -->
                </ItemGroup>
            </Project>
            """,
            """
            <Project>
                <ItemGroup>
                    <PackageReference Include="A" Version="1.0.0" /><!-- some comment -->
                    <PackageReference Include="Inserted" Version="1.5.0" />
                </ItemGroup>
            </Project>
            """,
            true)] // use trailing single comment scenario
        [DataRow("""
            <Project>
              <ItemGroup>
                <PackageReference Include="A" Version="1.0.0" /><!--
                    This is a multi-line
                    comment across lines
                    -->
              </ItemGroup>
            </Project>
            """,
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="A" Version="1.0.0" /><!--
                    This is a multi-line
                    comment across lines
                    -->
                <PackageReference Include="Inserted" Version="1.5.0" />
              </ItemGroup>
            </Project>
            """,// use multi-line trailing comment scenario
            true)]
        [DataRow("""
            <Project>
               <ItemGroup>
                    <PackageReference Include="A" Version="1.0.0" />
                    <!--
                    This is a multi-line
                    comment before the next item
                    -->
                    <PackageReference Include="B" Version="2.0.0" />
                </ItemGroup>
            </Project>
            """,
            """
            <Project>
               <ItemGroup>
                    <PackageReference Include="A" Version="1.0.0" />
                    <PackageReference Include="Inserted" Version="1.5.0" />
                    <!--
                    This is a multi-line
                    comment before the next item
                    -->
                    <PackageReference Include="B" Version="2.0.0" />
                </ItemGroup>
            </Project>
            """,
            false)] // use multi-line comment scenario
        [DataRow(
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="A" Version="1.0.0" />    <!-- comment A -->
                <!-- comment before B -->
                <PackageReference Include="B" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """,
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="A" Version="1.0.0" />    <!-- comment A -->
                <PackageReference Include="Inserted" Version="1.5.0" />
                <!-- comment before B -->
                <PackageReference Include="B" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """,
            false)] // use comment before next item scenario
        public void AddItem_PreservesComments(string content, string expectedContent, bool trailingComment)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(content, ProjectCollection.GlobalProjectCollection, true);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectItemGroupElement group = project.ItemGroups.First();
            if (trailingComment)
            {

                group.AddItem("PackageReference", "Inserted").AddMetadata("Version", "1.5.0", expressAsAttribute: true);
            }
            else
            {
                // Insert a new item between the two existing items
                var items = group.Items.ToList();
                var newItem = project.CreateItemElement("PackageReference");
                newItem.Include = "Inserted";
                group.InsertAfterChild(newItem, items[0]);
                newItem.AddMetadata("Version", "1.5.0", true);
            }
            Helpers.VerifyAssertLineByLine(expectedContent, project.RawXml);
        }
    }
}
