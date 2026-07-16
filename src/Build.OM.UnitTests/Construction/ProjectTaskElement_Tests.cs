// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Construction;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

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
        [MSBuildTestMethod]
        public void ReadNoParameters()
        {
            string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1/>
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);
            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual("t1", task.Name);
            Assert.IsEmpty(parameters);
            Assert.AreEqual(0, Helpers.Count(task.Outputs));
            Assert.AreEqual(String.Empty, task.ContinueOnError);
        }

        /// <summary>
        /// Read task with continue on error
        /// </summary>
        [MSBuildTestMethod]
        public void ReadContinueOnError()
        {
            string content = @"
                    <Project>
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
        [MSBuildTestMethod]
        public void ReadCondition()
        {
            string content = @"
                    <Project>
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
        [MSBuildTestMethod]
        public void ReadInvalidChild()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <X/>
                            </t1>
                        </Target>
                    </Project>
                ";

                GetTaskFromContent(content);
            });
        }
        /// <summary>
        /// Read task with empty parameter.
        /// Although MSBuild does not set these on tasks, they
        /// are visible in the XML objects for editing purposes.
        /// </summary>
        [MSBuildTestMethod]
        public void ReadEmptyParameter()
        {
            string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1 p1='' />
                        </Target>
                    </Project>
                ";

            ProjectTaskElement task = GetTaskFromContent(content);

            var parameters = Helpers.MakeDictionary(task.Parameters);

            Assert.ContainsSingle(parameters);
        }

        /// <summary>
        /// Read task with parameters
        /// </summary>
        [MSBuildTestMethod]
        public void ReadParameters()
        {
            string content = @"
                    <Project>
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
        [MSBuildTestMethod]
        public void SetParameterValue()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.SetParameter("p1", "v1b");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual("v1b", parameters["p1"]);
            Assert.IsTrue(task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set a parameter to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullParameterValue()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter("p1", null);
            });
        }
        /// <summary>
        /// Set a parameter with the reserved name 'continueonerror'
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidParameterNameContinueOnError()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter("ContinueOnError", "v");
            });
        }
        /// <summary>
        /// Set a parameter with the reserved name 'condition'
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidParameterNameCondition()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter("Condition", "c");
            });
        }
        /// <summary>
        /// Set a parameter using a null name
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullParameterName()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter(null, "v1");
            });
        }
        /// <summary>
        /// Add a parameter to the task
        /// </summary>
        [MSBuildTestMethod]
        public void SetNotExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.SetParameter("p2", "v2");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.AreEqual("v2", parameters["p2"]);
            Assert.IsTrue(task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Remove a parameter from the task
        /// </summary>
        [MSBuildTestMethod]
        public void RemoveExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.RemoveParameter("p1");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.IsEmpty(parameters);
            Assert.IsTrue(task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Remove a parameter that is not on the task
        /// </summary>
        /// <remarks>
        /// This should not throw.
        /// </remarks>
        [MSBuildTestMethod]
        public void RemoveNonExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();

            task.RemoveParameter("XX");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.ContainsSingle(parameters);
        }

        /// <summary>
        /// Set continue on error
        /// </summary>
        [MSBuildTestMethod]
        public void SetContinueOnError()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTaskElement task = project.AddTarget("t").AddTask("tt");
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.ContinueOnError = "true";
            Assert.AreEqual("true", task.ContinueOnError);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTaskElement task = project.AddTarget("t").AddTask("tt");
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.Condition = "c";
            Assert.AreEqual("c", task.Condition);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Helper to return the first ProjectTaskElement from the parsed project content provided
        /// </summary>
        private static ProjectTaskElement GetTaskFromContent(string content)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            return (ProjectTaskElement)Helpers.GetFirst(target.Children);
        }

        /// <summary>
        /// Get a basic ProjectTaskElement with one parameter p1
        /// </summary>
        private static ProjectTaskElement GetBasicTask()
        {
            string content = @"
                    <Project>
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
