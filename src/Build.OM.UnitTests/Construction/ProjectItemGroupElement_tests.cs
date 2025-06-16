// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectItemGroupElement class
    /// </summary>
    public class ProjectItemGroupElement_tests
    {
        /// <summary>
        /// Read item groups in an empty project
        /// </summary>
        [Fact]
        public void ReadNoItemGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.Equal(0, Helpers.Count(project.Children));
            Assert.Empty(project.ItemGroups);
        }

        /// <summary>
        /// Read an empty item group
        /// </summary>
        [Fact]
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

            Assert.Equal(0, Helpers.Count(group.Items));
        }

        /// <summary>
        /// Read an item group with two item children
        /// </summary>
        [Fact]
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

            Assert.Equal(2, items.Count);
            Assert.Equal("i1", items[0].Include);
            Assert.Equal("i2", items[1].Include);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemGroupElement itemGroup = Helpers.GetFirst(project.ItemGroups);
            itemGroup.Condition = "c";

            Assert.Equal("c", itemGroup.Condition);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the Label value
        /// </summary>
        [Fact]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemGroupElement itemGroup = Helpers.GetFirst(project.ItemGroups);
            itemGroup.Label = "c";

            Assert.Equal("c", itemGroup.Label);
            Assert.True(project.HasUnsavedChanges);
        }

        [Fact]
        public void AddPackageReference_PreservesTrailingCommentOnSameLine()
        {
            string content =
            """
               <Project>
                   <ItemGroup>
                       <PackageReference Include="Newtonsoft.Json" Version="13.0.1" /><!-- some comment -->
                   </ItemGroup>
               </Project>
            """;
            using ProjectRootElementFromString projectRootElementFromString = new(content, ProjectCollection.GlobalProjectCollection, true);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectItemGroupElement group = project.ItemGroups.First();

            // Add a new PackageReference
            group.AddItem("PackageReference", "Serilog").AddMetadata("Version", "4.3.0", expressAsAttribute: true);

            string expectedContent =
            """
               <Project>
                   <ItemGroup>
                       <PackageReference Include="Newtonsoft.Json" Version="13.0.1" /><!-- some comment -->
                       <PackageReference Include="Serilog" Version="4.3.0" />
                   </ItemGroup>
               </Project>
            """;

            Helpers.VerifyAssertLineByLine(expectedContent, project.RawXml);
        }

        private void AddItem_PreservesComments_Helper(string content, string expectedContent)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(content, ProjectCollection.GlobalProjectCollection, true);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectItemGroupElement group = project.ItemGroups.First();

            // Insert a new item between the two existing items
            var items = group.Items.ToList();
            var newItem = project.CreateItemElement("PackageReference");
            newItem.Include = "Inserted";
            group.InsertBeforeChild(newItem, items[1]);
            newItem.AddMetadata("Version", "1.5.0", true);

            Helpers.VerifyAssertLineByLine(expectedContent, project.RawXml);
        }

        [Fact]
        public void AddItem_PreservesComments_VariousCases()
        {
            // Multi-line comment scenario
            string content1 =
            """
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
            """;
            string expectedContent1 =
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
            """;
            AddItem_PreservesComments_Helper(content1, expectedContent1);

            // Single-line comment scenario
            string content2 =
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="A" Version="1.0.0" />
                <!-- comment before B -->
                <PackageReference Include="B" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;
            string expectedContent2 =
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="A" Version="1.0.0" />
                <PackageReference Include="Inserted" Version="1.5.0" />
                <!-- comment before B -->
                <PackageReference Include="B" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;
            AddItem_PreservesComments_Helper(content2, expectedContent2);
        }
    }
}
