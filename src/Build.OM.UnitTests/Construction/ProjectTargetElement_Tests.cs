// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Shouldly;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

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
        [MSBuildTestMethod]
        public void AddTargetInvalidName()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                project.CreateTargetElement("@#$invalid@#$");
            });
        }
        /// <summary>
        /// Read targets in an empty project
        /// </summary>
        [MSBuildTestMethod]
        public void ReadNoTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.IsEmpty(project.Targets);
        }

        /// <summary>
        /// Read an empty target
        /// </summary>
        [MSBuildTestMethod]
        public void ReadEmptyTarget()
        {
            string content = @"
                    <Project>
                        <Target Name='t'/>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(0, Helpers.Count(target.Children));
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Read attributes on the target element
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void SetInvalidNullInputs()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.Inputs = null;
            });
        }
        /// <summary>
        /// Set null outputs on the target element
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullOutputs()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.Outputs = null;
            });
        }
        /// <summary>
        /// Set null dependsOnTargets on the target element
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullDependsOnTargets()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.DependsOnTargets = null;
            });
        }
        /// <summary>
        /// Set null dependsOnTargets on the target element
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullKeepDuplicateOutputs()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.KeepDuplicateOutputs = null;
            });
        }
        /// <summary>
        /// Set null condition on the target element
        /// </summary>
        [MSBuildTestMethod]
        public void SetNullCondition()
        {
            ProjectTargetElement target = GetTargetXml();
            target.Condition = null;

            Assert.AreEqual(String.Empty, target.Condition);
        }

        /// <summary>
        /// Read a target with a missing name
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidMissingName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read a target with an invalid attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target XX='YY'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an target with two task children
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void SetName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Name = "t2";

            Assert.AreEqual("t2", target.Name);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set inputs
        /// </summary>
        [MSBuildTestMethod]
        public void SetInputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Inputs = "in";

            Assert.AreEqual("in", target.Inputs);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set outputs
        /// </summary>
        [MSBuildTestMethod]
        public void SetOutputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Outputs = "out";

            Assert.AreEqual("out", target.Outputs);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set dependsontargets
        /// </summary>
        [MSBuildTestMethod]
        public void SetDependsOnTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.DependsOnTargets = "dot";

            Assert.AreEqual("dot", target.DependsOnTargets);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Condition = "c";

            Assert.AreEqual("c", target.Condition);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set KeepDuplicateOutputs attribute
        /// </summary>
        [MSBuildTestMethod]
        public void SetKeepDuplicateOutputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.KeepDuplicateOutputs = "true";

            Assert.AreEqual("true", target.KeepDuplicateOutputs);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set return value.  Verify that setting to the empty string and null are
        /// both allowed and have distinct behaviour.
        /// </summary>
        [MSBuildTestMethod]
        public void SetReturns()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Returns = "@(a)";

            Assert.AreEqual("@(a)", target.Returns);
            Assert.IsTrue(project.HasUnsavedChanges);

            Helpers.ClearDirtyFlag(project);

            target.Returns = String.Empty;

            Assert.AreEqual(String.Empty, target.Returns);
            Assert.IsTrue(project.HasUnsavedChanges);

            Helpers.ClearDirtyFlag(project);

            target.Returns = null;

            Assert.IsNull(target.Returns);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Parse invalid property under target
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidPropertyUnderTarget()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                ChangeWaves.ResetStateForTests();

                string projectFile = @"
                    <Project>
                        <Target Name='t'>
                            <test>m</test>
                        </Target>
                    </Project>";
                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);
                using ProjectCollection collection = new ProjectCollection();
                var error = Assert.ThrowsExactly<InvalidProjectFileException>(() =>
                {
                    collection.LoadProject(file.Path).Build().ShouldBeTrue();
                });

                error.ErrorCode.ShouldMatch("MSB4067");
                var expectedString = "<PropertyGroup>";
                error.Message.ShouldMatch(expectedString);
            }
        }

        /// <summary>
        /// Helper to get an empty ProjectTargetElement with various attributes and two tasks
        /// </summary>
        private static ProjectTargetElement GetTargetXml()
        {
            string content = @"
                    <Project>
                        <Target Name='t' Inputs='i' Outputs='o' DependsOnTargets='d' Condition='c'>
                            <t1/>
                            <t2/>
                        </Target>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            return target;
        }
    }
}
