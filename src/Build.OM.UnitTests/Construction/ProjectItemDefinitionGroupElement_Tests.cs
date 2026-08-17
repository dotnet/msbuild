// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Xunit;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectItemDefinitionGroupElement class
    /// </summary>
    public class ProjectItemDefinitionGroupElement_Tests
    {
        /// <summary>
        /// Read project with no item definition groups
        /// </summary>
        [Fact]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.Equal(0, Helpers.Count(project.Children));
            Assert.Empty(project.ItemDefinitionGroups);
        }

        /// <summary>
        /// Read itemdefinitiongroup with unexpected attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemDefinitionGroup X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read itemdefinitiongroup with no children
        /// </summary>
        [Fact]
        public void ReadNoChildren()
        {
            string content = @"
                    <Project>
                        <ItemDefinitionGroup/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)Helpers.GetFirst(project.Children);

            Assert.Equal(0, Helpers.Count(itemDefinitionGroup.ItemDefinitions));
        }

        /// <summary>
        /// Read basic valid set of itemdefinitiongroups
        /// </summary>
        [Fact]
        public void ReadBasic()
        {
            string content = @"
                    <Project>
                        <ItemDefinitionGroup Condition='c'>
                            <i1/>
                        </ItemDefinitionGroup>
                        <ItemDefinitionGroup>
                            <i2/>
                            <i3/>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            var itemDefinitionGroups = Helpers.MakeList(project.ItemDefinitionGroups);
            Assert.Equal(2, itemDefinitionGroups.Count);

            Assert.Equal(1, Helpers.Count(itemDefinitionGroups[0].ItemDefinitions));
            Assert.Equal(2, Helpers.Count(itemDefinitionGroups[1].ItemDefinitions));
            Assert.Equal("c", itemDefinitionGroups[0].Condition);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemDefinitionGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemDefinitionGroupElement itemDefinitionGroup = Helpers.GetFirst(project.ItemDefinitionGroups);
            itemDefinitionGroup.Condition = "c";

            Assert.Equal("c", itemDefinitionGroup.Condition);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the Label value
        /// </summary>
        [Fact]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemDefinitionGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemDefinitionGroupElement itemDefinitionGroup = Helpers.GetFirst(project.ItemDefinitionGroups);
            itemDefinitionGroup.Label = "c";

            Assert.Equal("c", itemDefinitionGroup.Label);
            Assert.True(project.HasUnsavedChanges);
        }
    }
}
