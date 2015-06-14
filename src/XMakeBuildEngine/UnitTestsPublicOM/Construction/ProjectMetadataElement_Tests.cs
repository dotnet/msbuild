// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="ProjectMetadataElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test the ProjectMetadataElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using NUnit.Framework;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectMetadataElement class
    /// </summary>
    [TestFixture]
    public class ProjectMetadataElement_Tests
    {
        /// <summary>
        /// Read simple metadatum
        /// </summary>
        [Test]
        public void ReadMetadata()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            Assert.AreEqual("m", metadatum.Name);
            Assert.AreEqual("m1", metadatum.Value);
            Assert.AreEqual("c", metadatum.Condition);
        }

        /// <summary>
        /// Read metadatum with invalid attribute
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
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

        /// <summary>
        /// Read metadatum with invalid name characters (but legal xml)
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidName()
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

        /// <summary>
        /// Read metadatum with invalid built-in metadata name
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBuiltInName()
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

        /// <summary>
        /// Read metadatum with invalid built-in element name
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBuiltInElementName()
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

        /// <summary>
        /// Set metadatum value
        /// </summary>
        [Test]
        public void SetValue()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Value = "m1b";
            Assert.AreEqual("m1b", metadatum.Value);
        }

        /// <summary>
        /// Rename
        /// </summary>
        [Test]
        public void SetName()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Name = "m2";
            Assert.AreEqual("m2", metadatum.Name);
            Assert.AreEqual(true, metadatum.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to same value should not mark dirty
        /// </summary>
        [Test]
        public void SetNameSame()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();
            Helpers.ClearDirtyFlag(metadatum.ContainingProject);

            metadatum.Name = "m";
            Assert.AreEqual("m", metadatum.Name);
            Assert.AreEqual(false, metadatum.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to illegal name
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetNameIllegal()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Name = "ImportGroup";
        }

        /// <summary>
        /// Set metadatum value to empty
        /// </summary>
        [Test]
        public void SetEmptyValue()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Value = String.Empty;
            Assert.AreEqual(String.Empty, metadatum.Value);
        }

        /// <summary>
        /// Set metadatum value to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullValue()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Value = null;
        }

        /// <summary>
        /// Read a metadatum containing an expression like @(..) but whose parent is an ItemDefinitionGroup
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidItemExpressionInMetadata()
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

        /// <summary>
        /// Read a metadatum containing an expression like @(..) but whose parent is NOT an ItemDefinitionGroup
        /// </summary>
        [Test]
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
