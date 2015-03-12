//-----------------------------------------------------------------------
// <copyright file="ProjectPropertyElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test the ProjectPropertyElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

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
        [TestMethod]
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
        [TestMethod]
        public void ReadPropertyWithChildren()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <p>A<B>C<D/></B>E</p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectPropertyGroupElement propertyGroup = (ProjectPropertyGroupElement)Helpers.GetFirst(project.Children);
            ProjectPropertyElement property = Helpers.GetFirst(propertyGroup.Properties);

            Assert.AreEqual("p", property.Name);
            Assert.AreEqual(@"A<B xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">C<D /></B>E", property.Value);
        }

        /// <summary>
        /// Read property with invalid name (but legal xml)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <" + "\u03A3" + @"/>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read property with invalid reserved name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidReservedName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <PropertyGroup/>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read property with invalid built in name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBuiltInName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <MSBuildProjectFile/>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read property with invalid attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <p XX='YY'/>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read property with child element
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidChildElement()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                            <p>
                                <X/>
                            <p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Set property value
        /// </summary>
        [TestMethod]
        public void SetValue()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = "vb";
            Assert.AreEqual("vb", property.Value);
            Assert.AreEqual(true, property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set property value to the same value it was before.
        /// This should not dirty the project.
        /// </summary>
        [TestMethod]
        public void SetSameValue()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property = project.AddProperty("p", "v1");
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = "v1";
            Assert.AreEqual("v1", property.Value);
            Assert.AreEqual(false, property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename
        /// </summary>
        [TestMethod]
        public void SetName()
        {
            ProjectPropertyElement property = GetPropertyXml();

            property.Name = "p2";
            Assert.AreEqual("p2", property.Name);
            Assert.AreEqual(true, property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to same value should not mark dirty
        /// </summary>
        [TestMethod]
        public void SetNameSame()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Name = "p";
            Assert.AreEqual("p", property.Name);
            Assert.AreEqual(false, property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to illegal name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SetNameIllegal()
        {
            ProjectPropertyElement property = GetPropertyXml();

            property.Name = "ImportGroup";
        }

        /// <summary>
        /// Set property value to empty
        /// </summary>
        [TestMethod]
        public void SetEmptyValue()
        {
            ProjectPropertyElement property = GetPropertyXml();
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Value = String.Empty;
            Assert.AreEqual(String.Empty, property.Value);
            Assert.AreEqual(true, property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set property value to null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullValue()
        {
            ProjectPropertyElement property = GetPropertyXml();

            property.Value = null;
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property = project.AddProperty("p", "v1");
            Helpers.ClearDirtyFlag(property.ContainingProject);

            property.Condition = "c";
            Assert.AreEqual("c", property.Condition);
            Assert.AreEqual(true, property.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Helper to get a ProjectPropertyElement for a simple property
        /// </summary>
        private static ProjectPropertyElement GetPropertyXml()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
