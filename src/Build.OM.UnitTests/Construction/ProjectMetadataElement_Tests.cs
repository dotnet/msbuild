// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;
using System.Linq;
using Microsoft.Build.Evaluation;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectMetadataElement class
    /// </summary>
    public class ProjectMetadataElement_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public ProjectMetadataElement_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

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

        [Fact]
        public void ReadInvalidNameAsAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' " + "\u03A3" + @"='v1' />
                        </ItemGroup>
                    </Project>
                ";

            Assert.Throws<InvalidProjectFileException>(() =>
            {
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

        [Fact]
        public void ReadInvalidBuiltInNameAsAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' Filename='v1'/>
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
        /// Read metadatum with invalid built-in element name
        /// </summary>
        [Fact]
        public void ReadInvalidBuiltInElementNameAsAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' PropertyGroup='v1' />
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

        [Fact]
        public void SetNameIllegalAsAttribute()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();
            metadatum.ExpressedAsAttribute = true;

            Assert.Throws<InvalidProjectFileException>(() =>
            {                
                metadatum.Name = "Include";
            }
           );
        }


        [Fact]
        public void SetExpressedAsAttributeIllegalName()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();
            metadatum.Name = "Include";

            Assert.Throws<InvalidProjectFileException>(() =>
            {
                metadatum.ExpressedAsAttribute = true;
            }
           );
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i1 Include='i' />
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void AddMetadataAsAttributeIllegalName(string project)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);

            var item = items.First();

            Assert.Throws<InvalidProjectFileException>(() =>
            {
                item.AddMetadata("Include", "v1", true);
            });
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

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i1 Include='i' m1='v1' />
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' m1='v1' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadMetadataAsAttribute(string project)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(1, items[0].Metadata.Count);

            var metadata = items[0].Metadata.First();
            Assert.Equal("m1", metadata.Name);
            Assert.Equal("v1", metadata.Value);
            Assert.True(metadata.ExpressedAsAttribute);
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i1 Include='i' m1='&lt;&amp;>""' />
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' m1='&lt;&amp;>""' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadMetadataAsAttributeWithSpecialCharacters(string project)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(1, items[0].Metadata.Count);

            var metadata = items[0].Metadata.First();
            Assert.Equal("m1", metadata.Name);
            Assert.Equal(@"<&>""", metadata.Value);
            Assert.True(metadata.ExpressedAsAttribute);
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>",
                @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v2` />
                        </ItemGroup>
                    </Project>")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>",
                @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v2` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void UpdateMetadataValueAsAttribute(string projectContents, string updatedProject)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(
                new StringReader(ObjectModelHelpers.CleanupFileContents(projectContents))),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(1, items[0].Metadata.Count);

            var metadata = items[0].Metadata.First();
            Assert.Equal("m1", metadata.Name);
            Assert.Equal("v1", metadata.Value);
            Assert.True(metadata.ExpressedAsAttribute);

            metadata.Value = "v2";

            Assert.True(project.IsDirty);
            Assert.True(metadata.ExpressedAsAttribute);

            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        //  NOTE: When https://github.com/Microsoft/msbuild/issues/362 is fixed, then the expected value in XML may be:
        //      &lt;&amp;>"
        //  instead of:
        //      &lt;&amp;&gt;&quot;
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>",
                @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <ItemGroup>
                            <i1 Include=`i` m1=`&lt;&amp;&gt;&quot;` />
                        </ItemGroup>
                    </Project>")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>",
                @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`&lt;&amp;&gt;&quot;` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void UpdateMetadataValueAsAttributeWithSpecialCharacters(string projectContents, string updatedProject)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(
                new StringReader(ObjectModelHelpers.CleanupFileContents(projectContents))),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(1, items[0].Metadata.Count);

            var metadata = items[0].Metadata.First();
            Assert.Equal("m1", metadata.Name);
            Assert.Equal("v1", metadata.Value);
            Assert.True(metadata.ExpressedAsAttribute);

            metadata.Value = @"<&>""";

            Assert.True(project.IsDirty);
            Assert.True(metadata.ExpressedAsAttribute);

            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <i1 Include='i'>
                              <m1>v1</m1>
                            </i1>
                        </ItemGroup>
                    </Project>",
                @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <i1 Include='i'><m1>v1</m1></i1>
                        </ItemGroup>
                    </Project>",
                @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i'>
                                  <m1>v1</m1>
                                </i1>
                            </ItemGroup>
                        </Target>
                    </Project>",
                @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void ChangeMetadataToAttribute(string projectContents, string updatedProject)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectContents)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);
            
            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(1, items[0].Metadata.Count);

            var metadata = items[0].Metadata.First();
            Assert.Equal("m1", metadata.Name);
            Assert.Equal("v1", metadata.Value);
            Assert.False(metadata.ExpressedAsAttribute);

            metadata.ExpressedAsAttribute = true;

            Assert.True(project.IsDirty);
            Assert.True(metadata.ExpressedAsAttribute);

            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <i1 Include='i' m1='v1' />
                        </ItemGroup>
                    </Project>",
                    @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <ItemGroup>
                            <i1 Include=`i`>
                              <m1>v1</m1>
                            </i1>
                        </ItemGroup>
                    </Project>")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' m1='v1' />
                            </ItemGroup>
                        </Target>
                    </Project>",
                    @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i`>
                                  <m1>v1</m1>
                                </i1>
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void ChangeAttributeToMetadata(string projectContents, string updatedProject)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectContents)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(1, items[0].Metadata.Count);

            var metadata = items[0].Metadata.First();
            Assert.Equal("m1", metadata.Name);
            Assert.Equal("v1", metadata.Value);
            Assert.True(metadata.ExpressedAsAttribute);

            metadata.ExpressedAsAttribute = false;

            Assert.False(metadata.ExpressedAsAttribute);
            Assert.True(project.IsDirty);

            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <i1 Include='i' />
                        </ItemGroup>
                    </Project>",
        @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' />
                            </ItemGroup>
                        </Target>
                    </Project>",
        @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void AddMetadataAsAttribute(string projectContents, string updatedProject)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectContents)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(0, items[0].Metadata.Count);

            var metadata = items[0].AddMetadata("m1", "v1", true);

            Assert.Equal("m1", metadata.Name);
            Assert.Equal("v1", metadata.Value);
            Assert.True(metadata.ExpressedAsAttribute);
            Assert.True(project.IsDirty);

            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <i1 Include='i' />
                        </ItemGroup>
                    </Project>",
        @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1`>
                              <m2>v2</m2>
                            </i1>
                        </ItemGroup>
                    </Project>")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' />
                            </ItemGroup>
                        </Target>
                    </Project>",
        @"
                    <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1`>
                                  <m2>v2</m2>
                                </i1>
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void AddMetadataAsAttributeAndAsElement(string projectContents, string updatedProject)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectContents)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal(1, items.Count);
            Assert.Equal(0, items[0].Metadata.Count);

            var metadata = items[0].AddMetadata("m1", "v1", true);

            Assert.Equal("m1", metadata.Name);
            Assert.Equal("v1", metadata.Value);
            Assert.True(metadata.ExpressedAsAttribute);
            Assert.True(project.IsDirty);

            metadata = items[0].AddMetadata("m2", "v2", false);

            Assert.Equal("m2", metadata.Name);
            Assert.Equal("v2", metadata.Value);
            Assert.False(metadata.ExpressedAsAttribute);
            Assert.True(project.IsDirty);

            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
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

        void VerifyAssertLineByLine(string expected, string actual)
        {
            Helpers.VerifyAssertLineByLine(expected, actual, false, _testOutput);
        }
    }
}
