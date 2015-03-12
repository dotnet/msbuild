//-----------------------------------------------------------------------
// <copyright file="ProjectItemDefinitionElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectItemDefinitionElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectItemDefinitionElement class
    /// </summary>
    [TestClass]
    public class ProjectItemDefinitionElement_Tests
    {
        /// <summary>
        /// Read item definition with no children
        /// </summary>
        [TestMethod]
        public void ReadNoChildren()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i/>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemDefinitionElement itemDefinition = Helpers.GetFirst(itemDefinitionGroup.ItemDefinitions);

            Assert.AreEqual(0, Helpers.Count(itemDefinition.Metadata));
        }

        /// <summary>
        /// Read an item definition with a child
        /// </summary>
        [TestMethod]
        public void ReadBasic()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i>
                                <m1>v1</m1>
                            </i>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemDefinitionElement definition = Helpers.GetFirst(itemDefinitionGroup.ItemDefinitions);

            Assert.AreEqual("i", definition.ItemType);
            Assert.AreEqual(1, Helpers.Count(definition.Metadata));
            Assert.AreEqual("m1", Helpers.GetFirst(definition.Metadata).Name);
            Assert.AreEqual("v1", Helpers.GetFirst(definition.Metadata).Value);
        }

        /// <summary>
        /// Read item with reserved element name
        /// </summary>
        /// <remarks>
        /// Orcas inadvertently did not check for reserved item types (like "Choose") in item definitions,
        /// as we do for item types in item groups. So we do not fail here.
        /// </remarks>
        [TestMethod]
        public void ReadBuiltInElementName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <PropertyGroup/>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an item definition with several metadata
        /// </summary>
        [TestMethod]
        public void ReadMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i1>
                                <m1>v1</m1>
                                <m2 Condition='c'>v2</m2>
                                <m1>v3</m1>
                            </i1>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemDefinitionElement itemDefinition = Helpers.GetFirst(itemDefinitionGroup.ItemDefinitions);

            var metadata = Helpers.MakeList(itemDefinition.Metadata);

            Assert.AreEqual(3, metadata.Count);
            Assert.AreEqual("m1", metadata[0].Name);
            Assert.AreEqual("v1", metadata[0].Value);
            Assert.AreEqual("m2", metadata[1].Name);
            Assert.AreEqual("v2", metadata[1].Value);
            Assert.AreEqual("c", metadata[1].Condition);
            Assert.AreEqual("m1", metadata[2].Name);
            Assert.AreEqual("v3", metadata[2].Value);
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemDefinitionElement itemDefinition = project.AddItemDefinitionGroup().AddItemDefinition("i");
            Helpers.ClearDirtyFlag(project);

            itemDefinition.Condition = "c";

            Assert.AreEqual("c", itemDefinition.Condition);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }
    }
}
