// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

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
            Assert.Equal(null, project.ItemDefinitionGroups.GetEnumerator().Current);
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
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read itemdefinitiongroup with no children
        /// </summary>
        [Fact]
        public void ReadNoChildren()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
            Assert.Equal(true, project.HasUnsavedChanges);
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
            Assert.Equal(true, project.HasUnsavedChanges);
        }
    }
}
