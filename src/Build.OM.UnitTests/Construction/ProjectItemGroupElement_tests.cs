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

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
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

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
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
    }
}
