// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    // <summary>Tests for the ProjectExtensionsElement class.</summary>
    /// Tests for the  class
    /// </summary>
    public class ProjectExtensionsElement_Tests
    {
        /// <summary>
        /// Read ProjectExtensions with some child
        /// </summary>
        [Fact]
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

            Assert.Equal(@"<a xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />", extensions.Content);
        }

        /// <summary>
        /// Read ProjectExtensions with invalid Condition attribute
        /// </summary>
        [Fact]
        public void ReadInvalidCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                 <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <ProjectExtensions Condition='c'/>
                 </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read project with more than one ProjectExtensions
        /// </summary>
        [Fact]
        public void ReadInvalidDuplicate()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Set valid content
        /// </summary>
        [Fact]
        public void SetValid()
        {
            ProjectExtensionsElement extensions = GetEmptyProjectExtensions();
            Helpers.ClearDirtyFlag(extensions.ContainingProject);

            extensions.Content = "a<b/>c";

            Assert.Equal(@"a<b xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />c", extensions.Content);
            Assert.Equal(true, extensions.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set null content
        /// </summary>
        [Fact]
        public void SetInvalidNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectExtensionsElement extensions = GetEmptyProjectExtensions();

                extensions.Content = null;
            }
           );
        }
        /// <summary>
        /// Delete by ID 
        /// </summary>
        [Fact]
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
            Assert.Equal(String.Empty, content);
            extensions["a"] = String.Empty; // make sure it doesn't die or something

            Assert.Equal("y", extensions["b"]);
        }

        /// <summary>
        /// Get by ID 
        /// </summary>
        [Fact]
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
            Assert.Equal("y", content);

            content = extensions["nonexistent"];
            Assert.Equal(String.Empty, content);
        }

        /// <summary>
        /// Set by ID on not existing ID
        /// </summary>
        [Fact]
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
            Assert.Equal("z", extensions["c"]);
        }

        /// <summary>
        /// Set by ID on existing ID
        /// </summary>
        [Fact]
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
            Assert.Equal("y2", extensions["b"]);
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
