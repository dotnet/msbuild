// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectItemDefinitionElement class
    /// </summary>
    public class ProjectItemDefinitionElement_Tests
    {
        /// <summary>
        /// Read item definition with no children
        /// </summary>
        [Fact]
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

            Assert.Equal(0, Helpers.Count(itemDefinition.Metadata));
        }

        /// <summary>
        /// Read an item definition with a child
        /// </summary>
        [Fact]
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

            Assert.Equal("i", definition.ItemType);
            Assert.Equal(1, Helpers.Count(definition.Metadata));
            Assert.Equal("m1", Helpers.GetFirst(definition.Metadata).Name);
            Assert.Equal("v1", Helpers.GetFirst(definition.Metadata).Value);
        }

        /// <summary>
        /// Read item with reserved element name
        /// </summary>
        /// <remarks>
        /// Orcas inadvertently did not check for reserved item types (like "Choose") in item definitions,
        /// as we do for item types in item groups. So we do not fail here.
        /// </remarks>
        [Fact]
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
        [Fact]
        public void ReadMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i1 m1='v1'>
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

            Assert.Equal(3, metadata.Count);
            Assert.Equal("m1", metadata[0].Name);
            Assert.Equal("v1", metadata[0].Value);
            Assert.Equal("m2", metadata[1].Name);
            Assert.Equal("v2", metadata[1].Value);
            Assert.Equal("c", metadata[1].Condition);
            Assert.Equal("m1", metadata[2].Name);
            Assert.Equal("v3", metadata[2].Value);
        }

        /// <summary>
        /// Reads metadata as attributes that wouldn't be
        /// metadata on items
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i Include='inc' />
                        </ItemDefinitionGroup> 
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i Update='upd' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i Remove='rem' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i Exclude='excl' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i KeepMetadata='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i RemoveMetadata='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i KeepDuplicates='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i cOndiTion='true' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i LabeL='text' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        public void DoNotReadInvalidMetadataAttributesOrAttributesValidOnItems(string content)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Set the condition value
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemDefinitionElement itemDefinition = project.AddItemDefinitionGroup().AddItemDefinition("i");
            Helpers.ClearDirtyFlag(project);

            itemDefinition.Condition = "c";

            Assert.Equal("c", itemDefinition.Condition);
            Assert.True(project.HasUnsavedChanges);
        }
    }
}
