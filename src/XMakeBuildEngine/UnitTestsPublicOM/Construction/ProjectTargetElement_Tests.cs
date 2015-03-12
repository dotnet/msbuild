//-----------------------------------------------------------------------
// <copyright file="ProjectTargetElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test the ProjectTargetElement class.</summary>
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
    /// Test the ProjectTargetElement class
    /// </summary>
    [TestClass]
    public class ProjectTargetElement_Tests
    {
        /// <summary>
        /// Create target with invalid name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddTargetInvalidName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.CreateTargetElement("@#$invalid@#$");
        }

        /// <summary>
        /// Read targets in an empty project
        /// </summary>
        [TestMethod]
        public void ReadNoTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.AreEqual(null, project.Targets.GetEnumerator().Current);
        }

        /// <summary>
        /// Read an empty target
        /// </summary>
        [TestMethod]
        public void ReadEmptyTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(0, Helpers.Count(target.Children));
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Read attributes on the target element
        /// </summary>
        [TestMethod]
        public void ReadParameters()
        {
            ProjectTargetElement target = GetTargetXml();

            Assert.AreEqual("i", target.Inputs);
            Assert.AreEqual("o", target.Outputs);
            Assert.AreEqual("d", target.DependsOnTargets);
            Assert.AreEqual("c", target.Condition);
        }

        /// <summary>
        /// Set attributes on the target element
        /// </summary>
        [TestMethod]
        public void SetParameters()
        {
            ProjectTargetElement target = GetTargetXml();

            target.Inputs = "ib";
            target.Outputs = "ob";
            target.DependsOnTargets = "db";
            target.Condition = "cb";

            Assert.AreEqual("ib", target.Inputs);
            Assert.AreEqual("ob", target.Outputs);
            Assert.AreEqual("db", target.DependsOnTargets);
            Assert.AreEqual("cb", target.Condition);
        }

        /// <summary>
        /// Set null inputs on the target element
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullInputs()
        {
            ProjectTargetElement target = GetTargetXml();
            target.Inputs = null;
        }

        /// <summary>
        /// Set null outputs on the target element
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullOutputs()
        {
            ProjectTargetElement target = GetTargetXml();
            target.Outputs = null;
        }

        /// <summary>
        /// Set null dependsOnTargets on the target element
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullDependsOnTargets()
        {
            ProjectTargetElement target = GetTargetXml();
            target.DependsOnTargets = null;
        }

        /// <summary>
        /// Set null dependsOnTargets on the target element
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullKeepDuplicateOutputs()
        {
            ProjectTargetElement target = GetTargetXml();
            target.KeepDuplicateOutputs = null;
        }

        /// <summary>
        /// Set null condition on the target element
        /// </summary>
        [TestMethod]
        public void SetNullCondition()
        {
            ProjectTargetElement target = GetTargetXml();
            target.Condition = null;

            Assert.AreEqual(String.Empty, target.Condition);
        }

        /// <summary>
        /// Read a target with a missing name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidMissingName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read a target with an invalid attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target XX='YY'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an target with two task children
        /// </summary>
        [TestMethod]
        public void ReadTargetTwoTasks()
        {
            ProjectTargetElement target = GetTargetXml();

            var tasks = Helpers.MakeList(target.Children);

            Assert.AreEqual(2, tasks.Count);

            ProjectTaskElement task1 = (ProjectTaskElement)tasks[0];
            ProjectTaskElement task2 = (ProjectTaskElement)tasks[1];

            Assert.AreEqual("t1", task1.Name);
            Assert.AreEqual("t2", task2.Name);
        }

        /// <summary>
        /// Set name
        /// </summary>
        [TestMethod]
        public void SetName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Name = "t2";

            Assert.AreEqual("t2", target.Name);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set inputs
        /// </summary>
        [TestMethod]
        public void SetInputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Inputs = "in";

            Assert.AreEqual("in", target.Inputs);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set outputs
        /// </summary>
        [TestMethod]
        public void SetOutputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Outputs = "out";

            Assert.AreEqual("out", target.Outputs);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set dependsontargets
        /// </summary>
        [TestMethod]
        public void SetDependsOnTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.DependsOnTargets = "dot";

            Assert.AreEqual("dot", target.DependsOnTargets);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Condition = "c";

            Assert.AreEqual("c", target.Condition);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set KeepDuplicateOutputs attribute
        /// </summary>
        [TestMethod]
        public void SetKeepDuplicateOutputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.KeepDuplicateOutputs = "true";

            Assert.AreEqual("true", target.KeepDuplicateOutputs);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set return value.  Verify that setting to the empty string and null are
        /// both allowed and have distinct behaviour. 
        /// </summary>
        [TestMethod]
        public void SetReturns()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Returns = "@(a)";

            Assert.AreEqual("@(a)", target.Returns);
            Assert.AreEqual(true, project.HasUnsavedChanges);

            Helpers.ClearDirtyFlag(project);

            target.Returns = String.Empty;

            Assert.AreEqual(String.Empty, target.Returns);
            Assert.AreEqual(true, project.HasUnsavedChanges);

            Helpers.ClearDirtyFlag(project);

            target.Returns = null;

            Assert.IsNull(target.Returns);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Helper to get an empty ProjectTargetElement with various attributes and two tasks
        /// </summary>
        private static ProjectTargetElement GetTargetXml()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t' Inputs='i' Outputs='o' DependsOnTargets='d' Condition='c'>
                            <t1/>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            return target;
        }
    }
}
