// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="ProjectExtensionsElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectProjectExtensions class.</summary>
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
    // <summary>Tests for the ProjectExtensionsElement class.</summary>
    /// Tests for the  class
    /// </summary>
    [TestFixture]
    public class ProjectExtensionsElement_Tests
    {
        /// <summary>
        /// Read ProjectExtensions with some child
        /// </summary>
        [Test]
        public void Read()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions>
                     <a/>
                   </ProjectExtensions>
                 </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectExtensionsElement extensions = (ProjectExtensionsElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(@"<a xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />", extensions.Content);
        }

        /// <summary>
        /// Read ProjectExtensions with invalid Condition attribute
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidCondition()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions Condition='c'/>
                 </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read project with more than one ProjectExtensions
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidDuplicate()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions/>
                   <Target Name='t'/>
                   <ProjectExtensions   />
                 </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Set valid content
        /// </summary>
        [Test]
        public void SetValid()
        {
            ProjectExtensionsElement extensions = GetEmptyProjectExtensions();
            Helpers.ClearDirtyFlag(extensions.ContainingProject);

            extensions.Content = "a<b/>c";

            Assert.AreEqual(@"a<b xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />c", extensions.Content);
            Assert.AreEqual(true, extensions.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set null content
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNull()
        {
            ProjectExtensionsElement extensions = GetEmptyProjectExtensions();

            extensions.Content = null;
        }

        /// <summary>
        /// Delete by ID 
        /// </summary>
        [Test]
        public void DeleteById()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions>
                     <a>x</a>
                     <b>y</b>
                   </ProjectExtensions>
                 </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectExtensionsElement extensions = (ProjectExtensionsElement)Helpers.GetFirst(project.Children);
            extensions["a"] = String.Empty;
            content = extensions["a"];
            Assert.AreEqual(String.Empty, content);
            extensions["a"] = String.Empty; // make sure it doesn't die or something

            Assert.AreEqual("y", extensions["b"]);
        }

        /// <summary>
        /// Get by ID 
        /// </summary>
        [Test]
        public void GetById()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions>
                     <a>x</a>
                     <b>y</b>
                   </ProjectExtensions>
                 </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectExtensionsElement extensions = (ProjectExtensionsElement)Helpers.GetFirst(project.Children);

            content = extensions["b"];
            Assert.AreEqual("y", content);

            content = extensions["nonexistent"];
            Assert.AreEqual(String.Empty, content);
        }

        /// <summary>
        /// Set by ID on not existing ID
        /// </summary>
        [Test]
        public void SetById()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions>
                     <a>x</a>
                     <b>y</b>
                   </ProjectExtensions>
                 </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectExtensionsElement extensions = (ProjectExtensionsElement)Helpers.GetFirst(project.Children);

            extensions["c"] = "z";
            Assert.AreEqual("z", extensions["c"]);
        }

        /// <summary>
        /// Set by ID on existing ID
        /// </summary>
        [Test]
        public void SetByIdWhereItAlreadyExists()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions>
                     <a>x</a>
                     <b>y</b>
                   </ProjectExtensions>
                 </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectExtensionsElement extensions = (ProjectExtensionsElement)Helpers.GetFirst(project.Children);

            extensions["b"] = "y2";
            Assert.AreEqual("y2", extensions["b"]);
        }

        /// <summary>
        /// Helper to get an empty ProjectExtensionsElement object
        /// </summary>
        private static ProjectExtensionsElement GetEmptyProjectExtensions()
        {
            string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions/>
                 </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectExtensionsElement extensions = (ProjectExtensionsElement)Helpers.GetFirst(project.Children);
            return extensions;
        }
    }
}
