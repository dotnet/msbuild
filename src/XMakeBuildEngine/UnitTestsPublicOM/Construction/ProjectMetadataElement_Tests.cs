// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Test the ProjectMetadataElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectMetadataElement class
    /// </summary>
    public class ProjectMetadataElement_Tests
    {
        /// <summary>
        /// Read simple metadatum
        /// </summary>
        [Fact]
        public void ReadMetadata()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            Assert.Equal("m", metadatum.Name);
            Assert.Equal("m1", metadatum.Value);
            Assert.Equal("c", metadatum.Condition);
        }

        /// <summary>
        /// Read metadatum with invalid attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m Condition='c' XX='YY'/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read metadatum with invalid name characters (but legal xml)
        /// </summary>
        [Fact]
        public void ReadInvalidName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <" + "\u03A3" + @"/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read metadatum with invalid built-in metadata name
        /// </summary>
        [Fact]
        public void ReadInvalidBuiltInName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <Filename/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read metadatum with invalid built-in element name
        /// </summary>
        [Fact]
        public void ReadInvalidBuiltInElementName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <PropertyGroup/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Set metadatum value
        /// </summary>
        [Fact]
        public void SetValue()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Value = "m1b";
            Assert.Equal("m1b", metadatum.Value);
        }

        /// <summary>
        /// Rename
        /// </summary>
        [Fact]
        public void SetName()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Name = "m2";
            Assert.Equal("m2", metadatum.Name);
            Assert.Equal(true, metadatum.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to same value should not mark dirty
        /// </summary>
        [Fact]
        public void SetNameSame()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();
            Helpers.ClearDirtyFlag(metadatum.ContainingProject);

            metadatum.Name = "m";
            Assert.Equal("m", metadatum.Name);
            Assert.Equal(false, metadatum.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to illegal name
        /// </summary>
        [Fact]
        public void SetNameIllegal()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectMetadataElement metadatum = GetMetadataXml();

                metadatum.Name = "ImportGroup";
            }
           );
        }
        /// <summary>
        /// Set metadatum value to empty
        /// </summary>
        [Fact]
        public void SetEmptyValue()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Value = String.Empty;
            Assert.Equal(String.Empty, metadatum.Value);
        }

        /// <summary>
        /// Set metadatum value to null
        /// </summary>
        [Fact]
        public void SetInvalidNullValue()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectMetadataElement metadatum = GetMetadataXml();

                metadatum.Value = null;
            }
           );
        }
        /// <summary>
        /// Read a metadatum containing an expression like @(..) but whose parent is an ItemDefinitionGroup
        /// </summary>
        [Fact]
        public void ReadInvalidItemExpressionInMetadata()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i>
                                <m1>@(x)</m1>
                            </i>
                        </ItemDefinitionGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read a metadatum containing an expression like @(..) but whose parent is NOT an ItemDefinitionGroup
        /// </summary>
        [Fact]
        public void ReadValidItemExpressionInMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>@(x)</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            // Should not throw
            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Helper to get a ProjectMetadataElement for a simple metadatum
        /// </summary>
        private static ProjectMetadataElement GetMetadataXml()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m Condition='c'>m1</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);
            ProjectMetadataElement metadata = Helpers.GetFirst(item.Metadata);
            return metadata;
        }
    }
}
