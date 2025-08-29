// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Xunit;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectPropertyElement class
    /// </summary>
    public class ProjectPropertyElement_Tests
    {
        /// <summary>
        /// Read simple property
        /// </summary>
        [Fact]
        public void ReadProperty()
        {
            ProjectPropertyElement property = GetPropertyXml();

            Assert.Equal("p", property.Name);
            Assert.Equal("v", property.Value);
            Assert.Equal("c", property.Condition);
        }

        /// <summary>
        /// Read property with children - they are merely part of its value
        /// </summary>
        [Fact]
        public void ReadPropertyWithChildren()
        {
            string content = @"
                    <Project>
                        <PropertyGroup>
                            <p>A<B>C<D/></B>E</p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectPropertyGroupElement propertyGroup = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);
            ProjectPropertyElement property = Helpers.GetFirst(propertyGroup.Properties);

            Assert.Equal("p", property.Name);
            Assert.Equal(@"A<B>C<D /></B>E", property.Value);
        }

        /// <summary>
        /// Read property with invalid name (but legal xml)
        /// </summary>
        [Fact]
        public void ReadInvalidName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <PropertyGroup>
                            <" + "\u03A3" + @"/>
                        </PropertyGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read property with invalid reserved name
        /// </summary>
        [Fact]
        public void ReadInvalidReservedName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <PropertyGroup>
                            <PropertyGroup/>
                        </PropertyGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read property with invalid built in name
        /// </summary>
        [Fact]
        public void ReadInvalidBuiltInName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <PropertyGroup>
                            <MSBuildProjectFile/>
                        </PropertyGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read property with invalid attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <PropertyGroup>
                            <p XX='YY'/>
                        </PropertyGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read property with child element
        /// </summary>
        [Fact]
        public void ReadInvalidChildElement()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <PropertyGroup>
                            <p>
                                <X/>
                            <p>
                        </PropertyGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Set property value
        /// </summary>
        [Fact]
        public void SetValue()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = "vb";
            Assert.Equal("vb", property.Value);
            Assert.True(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set property value to the same value it was before.
        /// This should not dirty the project.
        /// </summary>
        [Fact]
        public void SetSameValue()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property = project.AddProperty("p", "v1");
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = "v1";
            Assert.Equal("v1", property.Value);
            Assert.False(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename
        /// </summary>
        [Fact]
        public void SetName()
        {
            ProjectPropertyElement property = GetPropertyXml();

            property.Name = "p2";
            Assert.Equal("p2", property.Name);
            Assert.True(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to same value should not mark dirty
        /// </summary>
        [Fact]
        public void SetNameSame()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Name = "p";
            Assert.Equal("p", property.Name);
            Assert.False(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to illegal name
        /// </summary>
        [Fact]
        public void SetNameIllegal()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectPropertyElement property = GetPropertyXml();

                property.Name = "ImportGroup";
            });
        }
        /// <summary>
        /// Set property value to empty
        /// </summary>
        [Fact]
        public void SetEmptyValue()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = String.Empty;
            Assert.Equal(String.Empty, property.Value);
            Assert.True(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set property value to null
        /// </summary>
        [Fact]
        public void SetInvalidNullValue()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectPropertyElement property = GetPropertyXml();

                property.Value = null;
            });
        }
        /// <summary>
        /// Set condition
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property = project.AddProperty("p", "v1");
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Condition = "c";
            Assert.Equal("c", property.Condition);
            Assert.True(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Helper to get a ProjectPropertyElement for a simple property
        /// </summary>
        private static ProjectPropertyElement GetPropertyXml()
        {
            string content = @"
                    <Project>
                        <PropertyGroup>
                            <p Condition='c'>v</p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectPropertyGroupElement propertyGroup = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);
            ProjectPropertyElement property = Helpers.GetFirst(propertyGroup.Properties);
            return property;
        }
    }
}
