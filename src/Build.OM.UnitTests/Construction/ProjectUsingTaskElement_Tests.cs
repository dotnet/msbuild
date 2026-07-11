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
    public class ProjectUsingTaskElement_Tests
    {
        /// <summary>
        /// Read project with no usingtasks
        /// </summary>
        [MSBuildTestMethod]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.IsEmpty(project.UsingTasks);
        }

        /// <summary>
        /// Read usingtask with no task name attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidMissingTaskName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask AssemblyFile='af'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with empty task name attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidEmptyTaskName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='' AssemblyFile='af'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with unexpected attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t' AssemblyFile='af' X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with neither AssemblyFile nor AssemblyName attributes
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidMissingAssemblyFileAssemblyName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with only empty AssemblyFile attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidEmptyAssemblyFile()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t' AssemblyFile=''/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with empty AssemblyFile attribute but AssemblyName present
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidEmptyAssemblyFileAndAssemblyNameNotEmpty()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t' AssemblyFile='' AssemblyName='n'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with only empty AssemblyName attribute but AssemblyFile present
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidEmptyAssemblyNameAndAssemblyFileNotEmpty()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t' AssemblyName='' AssemblyFile='f'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with both AssemblyName and AssemblyFile attributes
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidBothAssemblyFileAssemblyName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t' AssemblyName='an' AssemblyFile='af'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with both AssemblyName and AssemblyFile attributes but both are empty
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidBothEmptyAssemblyFileEmptyAssemblyNameBoth()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t' AssemblyName='' AssemblyFile=''/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read usingtask with assembly file
        /// </summary>
        [MSBuildTestMethod]
        public void ReadBasicUsingTaskAssemblyFile()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();

            Assert.AreEqual("t1", usingTask.TaskName);
            Assert.AreEqual("af", usingTask.AssemblyFile);
            Assert.AreEqual(String.Empty, usingTask.AssemblyName);
            Assert.AreEqual(String.Empty, usingTask.Condition);
        }

        /// <summary>
        /// Read usingtask with assembly name
        /// </summary>
        [MSBuildTestMethod]
        public void ReadBasicUsingTaskAssemblyName()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();

            Assert.AreEqual("t2", usingTask.TaskName);
            Assert.AreEqual(String.Empty, usingTask.AssemblyFile);
            Assert.AreEqual("an", usingTask.AssemblyName);
            Assert.AreEqual("c", usingTask.Condition);
        }

        /// <summary>
        /// Read usingtask with task factory, required runtime and required platform
        /// </summary>
        [MSBuildTestMethod]
        public void ReadBasicUsingTaskFactoryRuntimeAndPlatform()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskFactoryRuntimeAndPlatform();

            Assert.AreEqual("t2", usingTask.TaskName);
            Assert.AreEqual(String.Empty, usingTask.AssemblyFile);
            Assert.AreEqual("an", usingTask.AssemblyName);
            Assert.AreEqual("c", usingTask.Condition);
            Assert.AreEqual("AssemblyFactory", usingTask.TaskFactory);
        }

        /// <summary>
        /// Verify that passing in string.empty or null for TaskFactory will remove the element from the xml.
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void SetUsingTaskAssemblyFileOnUsingTaskAssemblyFile()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.AssemblyFile = "afb";
            Assert.AreEqual("afb", usingTask.AssemblyFile);
            Assert.IsTrue(usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set assembly name on a usingtask that already has assembly name
        /// </summary>
        [MSBuildTestMethod]
        public void SetUsingTaskAssemblyNameOnUsingTaskAssemblyName()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.AssemblyName = "anb";
            Assert.AreEqual("anb", usingTask.AssemblyName);
            Assert.IsTrue(usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set assembly file on a usingtask that already has assembly name
        /// </summary>
        [MSBuildTestMethod]
        public void SetUsingTaskAssemblyFileOnUsingTaskAssemblyName()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();

                usingTask.AssemblyFile = "afb";
            });
        }
        /// <summary>
        /// Set assembly name on a usingtask that already has assembly file
        /// </summary>
        [MSBuildTestMethod]
        public void SetUsingTaskAssemblyNameOnUsingTaskAssemblyFile()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();

                usingTask.AssemblyName = "anb";
            });
        }
        /// <summary>
        /// Set task name
        /// </summary>
        [MSBuildTestMethod]
        public void SetTaskName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.TaskName = "tt";
            Assert.AreEqual("tt", usingTask.TaskName);
            Assert.IsTrue(usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.Condition = "c";
            Assert.AreEqual("c", usingTask.Condition);
            Assert.IsTrue(usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set task factory
        /// </summary>
        [MSBuildTestMethod]
        public void SetTaskFactory()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.TaskFactory = "AssemblyFactory";
            Assert.AreEqual("AssemblyFactory", usingTask.TaskFactory);
            Assert.IsTrue(usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Make sure there is an exception when there are multiple parameter groups in the using task tag.
        /// </summary>
        [MSBuildTestMethod]
        public void DuplicateParameterGroup()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <ParameterGroup/>
                            <ParameterGroup/>
                        </UsingTask>
                    </Project>
                ";
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.Fail();
            });
        }
        /// <summary>
        /// Make sure there is an exception when there are multiple task groups in the using task tag.
        /// </summary>
        [MSBuildTestMethod]
        public void DuplicateTaskGroup()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <Task/>
                            <Task/>
                        </UsingTask>
                    </Project>
                ";
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.Fail();
            });
        }
        /// <summary>
        /// Make sure there is an exception when there is an unknown child
        /// </summary>
        [MSBuildTestMethod]
        public void UnknownChild()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <IAMUNKNOWN/>
                        </UsingTask>
                    </Project>
                ";
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.Fail();
            });
        }
        /// <summary>
        /// Make sure there is an no exception when there are children in the using task
        /// </summary>
        [MSBuildTestMethod]
        public void WorksWithChildren()
        {
            string content = @"
                    <Project>
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

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            Assert.IsNotNull(usingTask);
            Assert.AreEqual(2, usingTask.Count);
        }

        /// <summary>
        /// Make sure there is an exception when a parameter group is added but no task factory attribute is on the using task
        /// </summary>
        [MSBuildTestMethod]
        public void ExceptionWhenNoTaskFactoryAndHavePG()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'>
                            <ParameterGroup>
                               <MyParameter/>
                            </ParameterGroup>
                        </UsingTask>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Helpers.GetFirst(project.Children);
                Assert.Fail();
            });
        }
        /// <summary>
        /// Make sure there is an exception when a parameter group is added but no task factory attribute is on the using task
        /// </summary>
        [MSBuildTestMethod]
        public void ExceptionWhenNoTaskFactoryAndHaveTask()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'>
                            <Task/>
                        </UsingTask>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Helpers.GetFirst(project.Children);
                Assert.Fail();
            });
        }
        /// <summary>
        /// Helper to get a ProjectUsingTaskElement with a task factory, required runtime and required platform
        /// </summary>
        private static ProjectUsingTaskElement GetUsingTaskFactoryRuntimeAndPlatform()
        {
            string content = @"
                    <Project>
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory' />
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask;
        }

        /// <summary>
        /// Helper to get a ProjectUsingTaskElement with an assembly file set
        /// </summary>
        private static ProjectUsingTaskElement GetUsingTaskAssemblyFile()
        {
            string content = @"
                    <Project>
                        <UsingTask TaskName='t1' AssemblyFile='af' />
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'/>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask;
        }

        /// <summary>
        /// Helper to get a ProjectUsingTaskElement with an assembly name set
        /// </summary>
        private static ProjectUsingTaskElement GetUsingTaskAssemblyName()
        {
            string content = @"
                    <Project>
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c'/>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask;
        }

        /// <summary>
        /// Verify the attributes are removed from the xml when string.empty and null are passed in
        /// </summary>
        private static void VerifyAttributesRemoved(ProjectUsingTaskElement usingTask, string value)
        {
            Assert.Contains("TaskFactory", usingTask.ContainingProject.RawXml);
            usingTask.TaskFactory = value;
            Assert.DoesNotContain("TaskFactory", usingTask.ContainingProject.RawXml);
        }
    }
}
