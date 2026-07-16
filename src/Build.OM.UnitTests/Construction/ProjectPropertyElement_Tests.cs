// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectPropertyElement class
    /// </summary>
    [TestClass]
    public class ProjectPropertyElement_Tests
    {
        /// <summary>
        /// Read simple property
        /// </summary>
        [MSBuildTestMethod]
        public void ReadProperty()
        {
            ProjectPropertyElement property = GetPropertyXml();

            Assert.AreEqual("p", property.Name);
            Assert.AreEqual("v", property.Value);
            Assert.AreEqual("c", property.Condition);
        }

        /// <summary>
        /// Read property with children - they are merely part of its value
        /// </summary>
        [MSBuildTestMethod]
        public void ReadPropertyWithChildren()
        {
            string content = @"
                    <Project>
                        <PropertyGroup>
                            <p>A<B>C<D/></B>E</p>
                        </PropertyGroup>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectPropertyGroupElement propertyGroup = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);
            ProjectPropertyElement property = Helpers.GetFirst(propertyGroup.Properties);

            Assert.AreEqual("p", property.Name);
            Assert.AreEqual(@"A<B>C<D /></B>E", property.Value);
        }

        /// <summary>
        /// Read property with invalid name (but legal xml)
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
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
        [MSBuildTestMethod]
        public void ReadInvalidReservedName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
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
        [MSBuildTestMethod]
        public void ReadInvalidBuiltInName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
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
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
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
        [MSBuildTestMethod]
        public void ReadInvalidChildElement()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
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
        [MSBuildTestMethod]
        public void SetValue()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = "vb";
            Assert.AreEqual("vb", property.Value);
            Assert.IsTrue(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set property value to the same value it was before.
        /// This should not dirty the project.
        /// </summary>
        [MSBuildTestMethod]
        public void SetSameValue()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property = project.AddProperty("p", "v1");
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = "v1";
            Assert.AreEqual("v1", property.Value);
            Assert.IsFalse(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename
        /// </summary>
        [MSBuildTestMethod]
        public void SetName()
        {
            ProjectPropertyElement property = GetPropertyXml();

            property.Name = "p2";
            Assert.AreEqual("p2", property.Name);
            Assert.IsTrue(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to same value should not mark dirty
        /// </summary>
        [MSBuildTestMethod]
        public void SetNameSame()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Name = "p";
            Assert.AreEqual("p", property.Name);
            Assert.IsFalse(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to illegal name
        /// </summary>
        [MSBuildTestMethod]
        public void SetNameIllegal()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectPropertyElement property = GetPropertyXml();

                property.Name = "ImportGroup";
            });
        }
        /// <summary>
        /// Set property value to empty
        /// </summary>
        [MSBuildTestMethod]
        public void SetEmptyValue()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = String.Empty;
            Assert.AreEqual(String.Empty, property.Value);
            Assert.IsTrue(property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set property value to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullValue()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectPropertyElement property = GetPropertyXml();

                property.Value = null;
            });
        }
        /// <summary>
        /// Set condition
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property = project.AddProperty("p", "v1");
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Condition = "c";
            Assert.AreEqual("c", property.Condition);
            Assert.IsTrue(property.ContainingProject.HasUnsavedChanges);
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

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectPropertyGroupElement propertyGroup = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);
            ProjectPropertyElement property = Helpers.GetFirst(propertyGroup.Properties);
            return property;
        }
    }
}
