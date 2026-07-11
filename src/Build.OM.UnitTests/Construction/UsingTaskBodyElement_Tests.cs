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
    /// Tests for the ProjectUsingTaskElement class
    /// </summary>
    [TestClass]
    public class UsingTaskBodyElement_Tests
    {
        /// <summary>
        /// Read simple task body
        /// </summary>
        [MSBuildTestMethod]
        public void ReadBody()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();

            Assert.AreEqual(body.Evaluate, bool.FalseString, true);
            Assert.AreEqual("Contents", body.TaskBody);
        }

        /// <summary>
        /// Read task body with an invalid attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask AssemblyFile='af' TaskFactory='AssemblyFactory'>
                            <Task NotValidAttribute='OHI'/>
                       </UsingTask>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.Fail();
            });
        }
        /// <summary>
        /// Create a task body outside of a using task
        /// </summary>
        [MSBuildTestMethod]
        public void CreateBodyOutsideUsingTask()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
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
        [MSBuildTestMethod]
        public void SetValue()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Helpers.ClearDirtyFlag(body.ContainingProject);

            body.TaskBody = "MoreContents";
            Assert.AreEqual("MoreContents", body.TaskBody);
            Assert.IsTrue(body.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set body value to empty
        /// </summary>
        [MSBuildTestMethod]
        public void SetEmptyValue()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Helpers.ClearDirtyFlag(body.ContainingProject);

            body.TaskBody = String.Empty;
            Assert.AreEqual(String.Empty, body.TaskBody);
            Assert.IsTrue(body.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set body value to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullValue()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectUsingTaskBodyElement body = GetBodyXml();
                body.TaskBody = null;
                Assert.Fail();
            });
        }
        /// <summary>
        /// Verify setting the value of evaluate to null will wipe out the element and then the property will return true by default.
        /// </summary>
        [MSBuildTestMethod]
        public void SetEvaluateAttributeToNull()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Assert.Contains("Evaluate", body.ContainingProject.RawXml);
            body.Evaluate = null;
            Assert.DoesNotContain("Evaluate", body.ContainingProject.RawXml);
            Assert.AreEqual(bool.TrueString, body.Evaluate);
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

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            ProjectUsingTaskBodyElement body = usingTask.TaskBody;
            return body;
        }
    }
}
