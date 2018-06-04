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
    /// Tests for the ProjectTaskElement class
    /// </summary>
    public class ProjectTaskElement_Tests
    {
        /// <summary>
        /// Read task with no parameters
        /// </summary>
        [Fact]
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
            Assert.Equal("t1", task.Name);
            Assert.Equal(0, parameters.Count);
            Assert.Equal(0, Helpers.Count(task.Outputs));
            Assert.Equal(String.Empty, task.ContinueOnError);
        }

        /// <summary>
        /// Read task with continue on error
        /// </summary>
        [Fact]
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

            Assert.Equal("coe", task.ContinueOnError);
        }

        /// <summary>
        /// Read task with condition
        /// </summary>
        [Fact]
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

            Assert.Equal("c", task.Condition);
        }

        /// <summary>
        /// Read task with invalid child
        /// </summary>
        [Fact]
        public void ReadInvalidChild()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read task with empty parameter.
        /// Although MSBuild does not set these on tasks, they 
        /// are visible in the XML objects for editing purposes.
        /// </summary>
        [Fact]
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

            Assert.Equal(1, parameters.Count);
        }

        /// <summary>
        /// Read task with parameters
        /// </summary>
        [Fact]
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

            Assert.Equal(2, parameters.Count);
            Assert.Equal("v1", parameters["p1"]);
            Assert.Equal("v2", parameters["p2"]);

            Assert.Equal("v1", task.GetParameter("p1"));
            Assert.Equal(String.Empty, task.GetParameter("xxxx"));
        }

        /// <summary>
        /// Change a parameter value on the task
        /// </summary>
        [Fact]
        public void SetParameterValue()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.SetParameter("p1", "v1b");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.Equal("v1b", parameters["p1"]);
            Assert.Equal(true, task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set a parameter to null
        /// </summary>
        [Fact]
        public void SetInvalidNullParameterValue()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter("p1", null);
            }
           );
        }
        /// <summary>
        /// Set a parameter with the reserved name 'continueonerror'
        /// </summary>
        [Fact]
        public void SetInvalidParameterNameContinueOnError()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter("ContinueOnError", "v");
            }
           );
        }
        /// <summary>
        /// Set a parameter with the reserved name 'condition'
        /// </summary>
        [Fact]
        public void SetInvalidParameterNameCondition()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter("Condition", "c");
            }
           );
        }
        /// <summary>
        /// Set a parameter using a null name
        /// </summary>
        [Fact]
        public void SetInvalidNullParameterName()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectTaskElement task = GetBasicTask();

                task.SetParameter(null, "v1");
            }
           );
        }
        /// <summary>
        /// Add a parameter to the task
        /// </summary>
        [Fact]
        public void SetNotExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.SetParameter("p2", "v2");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.Equal("v2", parameters["p2"]);
            Assert.Equal(true, task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Remove a parameter from the task
        /// </summary>
        [Fact]
        public void RemoveExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.RemoveParameter("p1");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.Equal(0, parameters.Count);
            Assert.Equal(true, task.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Remove a parameter that is not on the task
        /// </summary>
        /// <remarks>
        /// This should not throw.
        /// </remarks>
        [Fact]
        public void RemoveNonExistingParameter()
        {
            ProjectTaskElement task = GetBasicTask();

            task.RemoveParameter("XX");

            var parameters = Helpers.MakeDictionary(task.Parameters);
            Assert.Equal(1, parameters.Count);
        }

        /// <summary>
        /// Set continue on error
        /// </summary>
        [Fact]
        public void SetContinueOnError()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTaskElement task = project.AddTarget("t").AddTask("tt");
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.ContinueOnError = "true";
            Assert.Equal("true", task.ContinueOnError);
            Assert.Equal(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTaskElement task = project.AddTarget("t").AddTask("tt");
            Helpers.ClearDirtyFlag(task.ContainingProject);

            task.Condition = "c";
            Assert.Equal("c", task.Condition);
            Assert.Equal(true, project.HasUnsavedChanges);
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
