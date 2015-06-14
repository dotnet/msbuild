// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="ProjectItemDefinitionGroupElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for of ProjectItemDefinitionGroupElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using NUnit.Framework;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectItemDefinitionGroupElement class
    /// </summary>
    [TestFixture]
    public class ProjectItemDefinitionGroupElement_Tests
    {
        /// <summary>
        /// Read project with no item definition groups
        /// </summary>
        [Test]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.AreEqual(0, Helpers.Count(project.Children));
            Assert.AreEqual(null, project.ItemDefinitionGroups.GetEnumerator().Current);
        }

        /// <summary>
        /// Read itemdefinitiongroup with unexpected attribute
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup X='Y'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read itemdefinitiongroup with no children
        /// </summary>
        [Test]
        public void ReadNoChildren()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(0, Helpers.Count(itemDefinitionGroup.ItemDefinitions));
        }

        /// <summary>
        /// Read basic valid set of itemdefinitiongroups
        /// </summary>
        [Test]
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
            Assert.AreEqual(2, itemDefinitionGroups.Count);

            Assert.AreEqual(1, Helpers.Count(itemDefinitionGroups[0].ItemDefinitions));
            Assert.AreEqual(2, Helpers.Count(itemDefinitionGroups[1].ItemDefinitions));
            Assert.AreEqual("c", itemDefinitionGroups[0].Condition);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [Test]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemDefinitionGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemDefinitionGroupElement itemDefinitionGroup = Helpers.GetFirst(project.ItemDefinitionGroups);
            itemDefinitionGroup.Condition = "c";

            Assert.AreEqual("c", itemDefinitionGroup.Condition);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the Label value
        /// </summary>
        [Test]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemDefinitionGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectItemDefinitionGroupElement itemDefinitionGroup = Helpers.GetFirst(project.ItemDefinitionGroups);
            itemDefinitionGroup.Label = "c";

            Assert.AreEqual("c", itemDefinitionGroup.Label);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }
    }
}
