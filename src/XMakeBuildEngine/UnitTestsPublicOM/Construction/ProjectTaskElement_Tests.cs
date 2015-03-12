//-----------------------------------------------------------------------
// <copyright file="ProjectTaskElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectTaskElement class.</summary>
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
    /// Tests for the ProjectTaskElement class
    /// </summary>
    [TestClass]
    public class ProjectTaskElement_Tests
    {
        /// <summary>
        /// Read task with no parameters
        /// </summary>
        [TestMethod]
        public void ReadNoParameters()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1/>
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);
            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual("t1", task.Name);
            Assert.AreEqual(0, parameters.Count);
            Assert.AreEqual(0, Helpers.Count(task.Outputs));
            Assert.AreEqual(String.Empty, task.ContinueOnError);
        }

        /// <summary>
        /// Read task with continue on error
        /// </summary>
        [TestMethod]
        public void ReadContinueOnError()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1 ContinueOnError='coe'/>
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);

            Assert.AreEqual("coe", task.ContinueOnError);
        }

        /// <summary>
        /// Read task with condition
        /// </summary>
        [TestMethod]
        public void ReadCondition()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1 Condition='c'/>
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);

            Assert.AreEqual("c", task.Condition);
        }
        
        /// <summary>
        /// Read task with invalid child
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidChild()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <X/>
                            </t1>
                        </Target>
                    </Project>
                ";

            GetTaskFromContent(content);
        }

        /// <summary>
        /// Read task with empty parameter.
        /// Although MSBuild does not set these on tasks, they 
        /// are visible in the XML objects for editing purposes.
        /// </summary>
        [TestMethod]
        public void ReadEmptyParameter()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1 p1='' />
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);

            var parameters = Helpers.MakeDictionary(task.Parameters);

            Assert.AreEqual(1, parameters.Count);
        }

        /// <summary>
        /// Read task with parameters
        /// </summary>
        [TestMethod]
        public void ReadParameters()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1 p1='v1' p2='v2' />
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);

            var parameters = Helpers.MakeDictionary(task.Parameters);

            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual("v1", parameters["p1"]);
            Assert.AreEqual("v2", parameters["p2"]);

            Assert.AreEqual("v1", task.GetParameter("p1"));
            Assert.AreEqual(String.Empty, task.GetParameter("xxxx"));
        }

        /// <summary>
        /// Change a parameter value on the task
        /// </summary>
        [TestMethod]
        public void SetParameterValue()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.SetParameter("p1", "v1b");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual("v1b", parameters["p1"]);
            Assert.AreEqual(true, task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set a parameter to null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullParameterValue()
        {
            ProjectTaskElement task = GetBasicTask();

            task.SetParameter("p1", null);
        }

        /// <summary>
        /// Set a parameter with the reserved name 'continueonerror'
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SetInvalidParameterNameContinueOnError()
        {
            ProjectTaskElement task = GetBasicTask();

            task.SetParameter("ContinueOnError", "v");
        }

        /// <summary>
        /// Set a parameter with the reserved name 'condition'
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SetInvalidParameterNameCondition()
        {
            ProjectTaskElement task = GetBasicTask();

            task.SetParameter("Condition", "c");
        }

        /// <summary>
        /// Set a parameter using a null name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullParameterName()
        {
            ProjectTaskElement task = GetBasicTask();

            task.SetParameter(null, "v1");
        }

        /// <summary>
        /// Add a parameter to the task
        /// </summary>
        [TestMethod]
        public void SetNotExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.SetParameter("p2", "v2");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual("v2", parameters["p2"]);
            Assert.AreEqual(true, task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Remove a parameter from the task
        /// </summary>
        [TestMethod]
        public void RemoveExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.RemoveParameter("p1");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual(0, parameters.Count);
            Assert.AreEqual(true, task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Remove a parameter that is not on the task
        /// </summary>
        /// <remarks>
        /// This should not throw.
        /// </remarks>
        [TestMethod]
        public void RemoveNonExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();

            task.RemoveParameter("XX");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual(1, parameters.Count);
        }

        /// <summary>
        /// Set continue on error
        /// </summary>
        [TestMethod]
        public void SetContinueOnError()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTaskElement task = project.AddTarget("t").AddTask("tt");
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.ContinueOnError = "true";
            Assert.AreEqual("true", task.ContinueOnError);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTaskElement task = project.AddTarget("t").AddTask("tt");
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.Condition = "c";
            Assert.AreEqual("c", task.Condition);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Helper to return the first ProjectTaskElement from the parsed project content provided
        /// </summary>
        private static ProjectTaskElement GetTaskFromContent(string content)
        {
            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            return (ProjectTaskElement)Helpers.GetFirst(target.Children);
        }

        /// <summary>
        /// Get a basic ProjectTaskElement with one parameter p1
        /// </summary>
        private static ProjectTaskElement GetBasicTask()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1 p1='v1' />
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);
            return task;
        }
    }
}
