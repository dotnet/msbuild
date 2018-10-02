// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectUsingTaskElement class
    /// </summary>
    public class UsingTaskBodyElement_Tests
    {
        /// <summary>
        /// Read simple task body
        /// </summary>
        [Fact]
        public void ReadBody()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();

            Assert.True(bool.FalseString.Equals(body.Evaluate, StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Contents", body.TaskBody);
        }

        /// <summary>
        /// Read task body with an invalid attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask AssemblyFile='af' TaskFactory='AssemblyFactory'>
                            <Task NotValidAttribute='OHI'/>
                       </UsingTask>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Create a task body outside of a using task
        /// </summary>
        [Fact]
        public void CreateBodyOutsideUsingTask()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Task>
                            Contents
                        </Task>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Set body value
        /// </summary>
        [Fact]
        public void SetValue()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Helpers.ClearDirtyFlag(body.ContainingProject);

            body.TaskBody = "MoreContents";
            Assert.Equal("MoreContents", body.TaskBody);
            Assert.Equal(true, body.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set body value to empty
        /// </summary>
        [Fact]
        public void SetEmptyValue()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Helpers.ClearDirtyFlag(body.ContainingProject);

            body.TaskBody = String.Empty;
            Assert.Equal(String.Empty, body.TaskBody);
            Assert.Equal(true, body.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set body value to null
        /// </summary>
        [Fact]
        public void SetInvalidNullValue()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectUsingTaskBodyElement body = GetBodyXml();
                body.TaskBody = null;
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Verify setting the value of evaluate to null will wipe out the element and then the property will return true by default.
        /// </summary>
        [Fact]
        public void SetEvaluateAttributeToNull()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Assert.True(body.ContainingProject.RawXml.Contains("Evaluate"));
            body.Evaluate = null;
            Assert.False(body.ContainingProject.RawXml.Contains("Evaluate"));
            Assert.Equal(bool.TrueString, body.Evaluate);
        }

        /// <summary>
        /// Helper to get a ProjectUsingTaskBodyElement for a simple task
        /// </summary>
        private static ProjectUsingTaskBodyElement GetBodyXml()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                            <Task Evaluate='false'>Contents</Task>
                       </UsingTask>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            ProjectUsingTaskBodyElement body = usingTask.TaskBody;
            return body;
        }
    }
}
