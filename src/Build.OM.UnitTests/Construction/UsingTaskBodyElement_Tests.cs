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

            Assert.Equal(body.Evaluate, bool.FalseString, true);
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
                    <Project>
                        <UsingTask AssemblyFile='af' TaskFactory='AssemblyFactory'>
                            <Task NotValidAttribute='OHI'/>
                       </UsingTask>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.True(false);
            });
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
                    <Project>
                        <Task>
                            Contents
                        </Task>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
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
            Assert.True(body.ContainingProject.HasUnsavedChanges);
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
            Assert.True(body.ContainingProject.HasUnsavedChanges);
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
            });
        }
        /// <summary>
        /// Verify setting the value of evaluate to null will wipe out the element and then the property will return true by default.
        /// </summary>
        [Fact]
        public void SetEvaluateAttributeToNull()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Assert.Contains("Evaluate", body.ContainingProject.RawXml);
            body.Evaluate = null;
            Assert.DoesNotContain("Evaluate", body.ContainingProject.RawXml);
            Assert.Equal(bool.TrueString, body.Evaluate);
        }

        /// <summary>
        /// Helper to get a ProjectUsingTaskBodyElement for a simple task
        /// </summary>
        private static ProjectUsingTaskBodyElement GetBodyXml()
        {
            string content = @"
                    <Project>
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
