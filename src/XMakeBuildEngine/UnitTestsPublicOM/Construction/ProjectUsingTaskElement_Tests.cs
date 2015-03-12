//-----------------------------------------------------------------------
// <copyright file="ProjectUsingTaskElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectUsingTaskElement class.</summary>
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
    public class ProjectUsingTaskElement_Tests
    {
        /// <summary>
        /// Read project with no usingtasks
        /// </summary>
        [TestMethod]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.AreEqual(null, project.UsingTasks.GetEnumerator().Current);
        }

        /// <summary>
        /// Read usingtask with no task name attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidMissingTaskName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask AssemblyFile='af'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with empty task name attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidEmptyTaskName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='' AssemblyFile='af'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with unexpected attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyFile='af' X='Y'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with neither AssemblyFile nor AssemblyName attributes
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidMissingAssemblyFileAssemblyName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with only empty AssemblyFile attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidEmptyAssemblyFile()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyFile=''/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with empty AssemblyFile attribute but AssemblyName present
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidEmptyAssemblyFileAndAssemblyNameNotEmpty()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyFile='' AssemblyName='n'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with only empty AssemblyName attribute but AssemblyFile present
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidEmptyAssemblyNameAndAssemblyFileNotEmpty()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyName='' AssemblyFile='f'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with both AssemblyName and AssemblyFile attributes
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBothAssemblyFileAssemblyName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyName='an' AssemblyFile='af'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with both AssemblyName and AssemblyFile attributes but both are empty
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBothEmptyAssemblyFileEmptyAssemblyNameBoth()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t' AssemblyName='' AssemblyFile=''/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read usingtask with assembly file
        /// </summary>
        [TestMethod]
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
        [TestMethod]
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
        [TestMethod]
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
        [TestMethod]
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
        [TestMethod]
        public void SetUsingTaskAssemblyFileOnUsingTaskAssemblyFile()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.AssemblyFile = "afb";
            Assert.AreEqual("afb", usingTask.AssemblyFile);
            Assert.AreEqual(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set assembly name on a usingtask that already has assembly name
        /// </summary>
        [TestMethod]
        public void SetUsingTaskAssemblyNameOnUsingTaskAssemblyName()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.AssemblyName = "anb";
            Assert.AreEqual("anb", usingTask.AssemblyName);
            Assert.AreEqual(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set assembly file on a usingtask that already has assembly name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetUsingTaskAssemblyFileOnUsingTaskAssemblyName()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyName();

            usingTask.AssemblyFile = "afb";
        }

        /// <summary>
        /// Set assembly name on a usingtask that already has assembly file
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetUsingTaskAssemblyNameOnUsingTaskAssemblyFile()
        {
            ProjectUsingTaskElement usingTask = GetUsingTaskAssemblyFile();

            usingTask.AssemblyName = "anb";
        }

        /// <summary>
        /// Set task name
        /// </summary>
        [TestMethod]
        public void SetTaskName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.TaskName = "tt";
            Assert.AreEqual("tt", usingTask.TaskName);
            Assert.AreEqual(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.Condition = "c";
            Assert.AreEqual("c", usingTask.Condition);
            Assert.AreEqual(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set task factory
        /// </summary>
        [TestMethod]
        public void SetTaskFactory()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectUsingTaskElement usingTask = project.AddUsingTask("t", "af", null);
            Helpers.ClearDirtyFlag(usingTask.ContainingProject);

            usingTask.TaskFactory = "AssemblyFactory";
            Assert.AreEqual("AssemblyFactory", usingTask.TaskFactory);
            Assert.AreEqual(true, usingTask.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Make sure there is an exception when there are multiple parameter groups in the using task tag.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void DuplicateParameterGroup()
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
            Assert.Fail();
        }

        /// <summary>
        /// Make sure there is an exception when there are multiple task groups in the using task tag.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void DuplicateTaskGroup()
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
            Assert.Fail();
        }

        /// <summary>
        /// Make sure there is an exception when there is an unknown child
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void UnknownChild()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='t2' AssemblyName='an' Condition='c' TaskFactory='AssemblyFactory'>
                            <IAMUNKNOWN/>
                        </UsingTask>
                    </Project>
                ";
            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Assert.Fail();
        }

        /// <summary>
        /// Make sure there is an no exception when there are children in the using task
        /// </summary>
        [TestMethod]
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
            Assert.IsNotNull(usingTask);
            Assert.AreEqual(2, usingTask.Count);
        }

        /// <summary>
        /// Make sure there is an exception when a parameter group is added but no task factory attribute is on the using task
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExceptionWhenNoTaskFactoryAndHavePG()
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
            Assert.Fail();
        }

        /// <summary>
        /// Make sure there is an exception when a parameter group is added but no task factory attribute is on the using task
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExceptionWhenNoTaskFactoryAndHaveTask()
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
            Assert.Fail();
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
            Assert.IsTrue(usingTask.ContainingProject.RawXml.Contains("TaskFactory"));
            usingTask.TaskFactory = value;
            Assert.IsTrue(!usingTask.ContainingProject.RawXml.Contains("TaskFactory"));
        }
    }
}
