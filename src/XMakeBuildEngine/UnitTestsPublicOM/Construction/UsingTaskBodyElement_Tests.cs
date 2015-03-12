//-----------------------------------------------------------------------
// <copyright file="UsingTaskBodyElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectUsingTaskBodyElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

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
        [TestMethod]
        public void ReadBody()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();

            Assert.IsTrue(bool.FalseString.Equals(body.Evaluate, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("Contents", body.TaskBody);
        }

        /// <summary>
        /// Read task body with an invalid attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask AssemblyFile='af' TaskFactory='AssemblyFactory'>
                            <Task NotValidAttribute='OHI'/>
                       </UsingTask>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Assert.Fail();
        }

        /// <summary>
        /// Create a task body outside of a using task
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CreateBodyOutsideUsingTask()
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

        /// <summary>
        /// Set body value
        /// </summary>
        [TestMethod]
        public void SetValue()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Helpers.ClearDirtyFlag(body.ContainingProject);

            body.TaskBody = "MoreContents";
            Assert.AreEqual("MoreContents", body.TaskBody);
            Assert.AreEqual(true, body.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set body value to empty
        /// </summary>
        [TestMethod]
        public void SetEmptyValue()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Helpers.ClearDirtyFlag(body.ContainingProject);

            body.TaskBody = String.Empty;
            Assert.AreEqual(String.Empty, body.TaskBody);
            Assert.AreEqual(true, body.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set body value to null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullValue()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            body.TaskBody = null;
            Assert.Fail();
        }

        /// <summary>
        /// Verify setting the value of evaluate to null will wipe out the element and then the property will return true by default.
        /// </summary>
        [TestMethod]
        public void SetEvaluateAttributeToNull()
        {
            ProjectUsingTaskBodyElement body = GetBodyXml();
            Assert.IsTrue(body.ContainingProject.RawXml.Contains("Evaluate"));
            body.Evaluate = null;
            Assert.IsTrue(!body.ContainingProject.RawXml.Contains("Evaluate"));
            Assert.AreEqual(bool.TrueString, body.Evaluate);
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
