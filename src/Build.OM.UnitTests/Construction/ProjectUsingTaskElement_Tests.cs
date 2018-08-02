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
    public class ProjectUsingTaskElement_Tests
    {
        /// <summary>
        /// Read project with no usingtasks
        /// </summary>
        [Fact]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.Equal(null, project.UsingTasks.GetEnumerator().Current);
        }

        /// <summary>
        /// Read usingtask with no task name attribute
        /// </summary>
        [Fact]
        public void ReadInvalidMissingTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask AssemblyFile='af'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with empty task name attribute
        /// </summary>
        [Fact]
        public void ReadInvalidEmptyTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='' AssemblyFile='af'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with unexpected attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyFile='af' X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with neither AssemblyFile nor AssemblyName attributes
        /// </summary>
        [Fact]
        public void ReadInvalidMissingAssemblyFileAssemblyName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with only empty AssemblyFile attribute
        /// </summary>
        [Fact]
        public void ReadInvalidEmptyAssemblyFile()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyFile=''/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with empty AssemblyFile attribute but AssemblyName present
        /// </summary>
        [Fact]
        public void ReadInvalidEmptyAssemblyFileAndAssemblyNameNotEmpty()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyFile='' AssemblyName='n'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with only empty AssemblyName attribute but AssemblyFile present
        /// </summary>
        [Fact]
        public void ReadInvalidEmptyAssemblyNameAndAssemblyFileNotEmpty()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyName='' AssemblyFile='f'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with both AssemblyName and AssemblyFile attributes
        /// </summary>
        [Fact]
        public void ReadInvalidBothAssemblyFileAssemblyName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyName='an' AssemblyFile='af'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with both AssemblyName and AssemblyFile attributes but both are empty
        /// </summary>
        [Fact]
        public void ReadInvalidBothEmptyAssemblyFileEmptyAssemblyNameBoth()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyName='' AssemblyFile=''/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read usingtask with assembly file
        /// </summary>
        [Fact]
        public void ReadBasicUsingTaskAssemblyFile()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();

            Assert.Equal("t1", usingTask.TaskName);
            Assert.Equal("af", usingTask.AssemblyFile);
            Assert.Equal(String.Empty, usingTask.AssemblyName);
            Assert.Equal(String.Empty, usingTask.Condition);
        }

        /// <summary>
        /// Read usingtask with assembly name
        /// </summary>
        [Fact]
        public void ReadBasicUsingTaskAssemblyName()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();

            Assert.Equal("t2", usingTask.TaskName);
            Assert.Equal(String.Empty, usingTask.AssemblyFile);
            Assert.Equal("an", usingTask.AssemblyName);
            Assert.Equal("c", usingTask.Condition);
        }

        /// <summary>
        /// Read usingtask with task factory, required runtime and required platform
        /// </summary>
        [Fact]
        public void ReadBasicUsingTaskFactoryRuntimeAndPlatform()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskFactoryRuntimeAndPlatform();

            Assert.Equal("t2", usingTask.TaskName);
            Assert.Equal(String.Empty, usingTask.AssemblyFile);
            Assert.Equal("an", usingTask.AssemblyName);
            Assert.Equal("c", usingTask.Condition);
            Assert.Equal("AssemblyFactory", usingTask.TaskFactory);
        }

        /// <summary>
        /// Verify that passing in string.empty or null for TaskFactory will remove the element from the xml.
        /// </summary>
        [Fact]
        public void RemoveUsingTaskFactoryRuntimeAndPlatform()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskFactoryRuntimeAndPlatform();

            string value = null;
            VerifyAttributesRemoved(usingTask, value);

            usingTask = GetUsingTaskFactoryRuntimeAndPlatform();
            value = String.Empty;
            VerifyAttributesRemoved(usingTask, value);
        }

        /// <summary>
        /// Set assembly file on a usingtask that already has assembly file
        /// </summary>
        [Fact]
        public void SetUsingTaskAssemblyFileOnUsingTaskAssemblyFile()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.AssemblyFile = "afb";
            Assert.Equal("afb", usingTask.AssemblyFile);
            Assert.Equal(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set assembly name on a usingtask that already has assembly name
        /// </summary>
        [Fact]
        public void SetUsingTaskAssemblyNameOnUsingTaskAssemblyName()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.AssemblyName = "anb";
            Assert.Equal("anb", usingTask.AssemblyName);
            Assert.Equal(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set assembly file on a usingtask that already has assembly name
        /// </summary>
        [Fact]
        public void SetUsingTaskAssemblyFileOnUsingTaskAssemblyName()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();

                usingTask.AssemblyFile = "afb";
            }
           );
        }
        /// <summary>
        /// Set assembly name on a usingtask that already has assembly file
        /// </summary>
        [Fact]
        public void SetUsingTaskAssemblyNameOnUsingTaskAssemblyFile()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();

                usingTask.AssemblyName = "anb";
            }
           );
        }
        /// <summary>
        /// Set task name
        /// </summary>
        [Fact]
        public void SetTaskName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.TaskName = "tt";
            Assert.Equal("tt", usingTask.TaskName);
            Assert.Equal(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.Condition = "c";
            Assert.Equal("c", usingTask.Condition);
            Assert.Equal(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set task factory
        /// </summary>
        [Fact]
        public void SetTaskFactory()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.TaskFactory = "AssemblyFactory";
            Assert.Equal("AssemblyFactory", usingTask.TaskFactory);
            Assert.Equal(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Make sure there is an exception when there are multiple parameter groups in the using task tag.
        /// </summary>
        [Fact]
        public void DuplicateParameterGroup()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <ParameterGroup/>
                            <ParameterGroup/>
                        </UsingTask>
                    </Project>
                ";
                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Make sure there is an exception when there are multiple task groups in the using task tag.
        /// </summary>
        [Fact]
        public void DuplicateTaskGroup()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <Task/>
                            <Task/>
                        </UsingTask>
                    </Project>
                ";
                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Make sure there is an exception when there is an unknown child
        /// </summary>
        [Fact]
        public void UnknownChild()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <IAMUNKNOWN/>
                        </UsingTask>
                    </Project>
                ";
                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Make sure there is an no exception when there are children in the using task
        /// </summary>
        [Fact]
        public void WorksWithChildren()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <ParameterGroup>
                               <MyParameter/>
                            </ParameterGroup>
                            <Task>
                                RANDOM GOO
                            </Task>
                        </UsingTask>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            Assert.NotNull(usingTask);
            Assert.Equal(2, usingTask.Count);
        }

        /// <summary>
        /// Make sure there is an exception when a parameter group is added but no task factory attribute is on the using task
        /// </summary>
        [Fact]
        public void ExceptionWhenNoTaskFactoryAndHavePG()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'>
                            <ParameterGroup>
                               <MyParameter/>
                            </ParameterGroup>
                        </UsingTask>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Make sure there is an exception when a parameter group is added but no task factory attribute is on the using task
        /// </summary>
        [Fact]
        public void ExceptionWhenNoTaskFactoryAndHaveTask()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'>
                            <Task/>
                        </UsingTask>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
                Assert.True(false);
            }
           );
        }
        /// <summary>
        /// Helper to get a ProjectUsingTaskElement with a task factory, required runtime and required platform
        /// </summary>
        private static ProjectUsingTaskElement GetUsingTaskFactoryRuntimeAndPlatform()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory' />
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask;
        }

        /// <summary>
        /// Helper to get a ProjectUsingTaskElement with an assembly file set
        /// </summary>
        private static ProjectUsingTaskElement GetUsingTaskAssemblyFile()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t1' AssemblyFile='af' />
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask;
        }

        /// <summary>
        /// Helper to get a ProjectUsingTaskElement with an assembly name set
        /// </summary>
        private static ProjectUsingTaskElement GetUsingTaskAssemblyName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask;
        }

        /// <summary>
        /// Verify the attributes are removed from the xml when string.empty and null are passed in
        /// </summary>
        private static void VerifyAttributesRemoved(ProjectUsingTaskElement usingTask, string value)
        {
            Assert.True(usingTask.ContainingProject.RawXml.Contains("TaskFactory"));
            usingTask.TaskFactory = value;
            Assert.False(usingTask.ContainingProject.RawXml.Contains("TaskFactory"));
        }
    }
}
