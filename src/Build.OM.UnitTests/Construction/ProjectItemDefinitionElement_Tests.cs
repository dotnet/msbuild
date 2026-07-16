// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;

#nullable disable

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
        [MSBuildTestMethod]
        public void ReadNoChildren()
        {
            string content = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i/>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemDefinitionElement itemDefinition = Helpers.GetFirst(itemDefinitionGroup.ItemDefinitions);

            Assert.AreEqual(0, Helpers.Count(itemDefinition.Metadata));
        }

        /// <summary>
        /// Read an item definition with a child
        /// </summary>
        [MSBuildTestMethod]
        public void ReadBasic()
        {
            string content = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i>
                                <m1>v1</m1>
                            </i>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
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
        [MSBuildTestMethod]
        public void ReadBuiltInElementName()
        {
            string content = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <PropertyGroup/>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            using ProjectFromString projectFromString = new(content);
        }

        /// <summary>
        /// Read an item definition with several metadata
        /// </summary>
        [MSBuildTestMethod]
        public void ReadMetadata()
        {
            string content = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1='v1'>
                                <m2 Condition='c'>v2</m2>
                                <m1>v3</m1>
                            </i1>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
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
        /// Reads metadata as attributes that wouldn't be
        /// metadata on items
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i Include='inc' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i Update='upd' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i Remove='rem' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i Exclude='excl' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i KeepMetadata='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i RemoveMetadata='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i KeepDuplicates='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i cOndiTion='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i LabeL='text' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        public void DoNotReadInvalidMetadataAttributesOrAttributesValidOnItems(string content)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemDefinitionElement itemDefinition = project.AddItemDefinitionGroup().AddItemDefinition("i");
            Helpers.ClearDirtyFlag(project);

            itemDefinition.Condition = "c";

            Assert.AreEqual("c", itemDefinition.Condition);
            Assert.IsTrue(project.HasUnsavedChanges);
        }
    }
}
