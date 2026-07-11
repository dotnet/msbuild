// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectTargetInstance
    /// </summary>
    [TestClass]
    public class ProjectTargetInstance_Tests
    {
        /// <summary>
        /// Test accessors
        /// </summary>
        [MSBuildTestMethod]
        public void Accessors()
        {
            ProjectTargetInstance target = GetSampleTargetInstance();

            Assert.AreEqual("t", target.Name);
            Assert.AreEqual("c", target.Condition);
            Assert.AreEqual("i", target.Inputs);
            Assert.AreEqual("o", target.Outputs);
            Assert.AreEqual("d", target.DependsOnTargets);
            Assert.AreEqual("b", target.BeforeTargets);
            Assert.AreEqual("a", target.AfterTargets);
            Assert.AreEqual("k", target.KeepDuplicateOutputs);
            Assert.AreEqual("r", target.Returns);
            Assert.AreEqual("t1", ((ProjectTaskInstance)target.Children[0]).Name);

            IList<ProjectTaskInstance> tasks = Helpers.MakeList(target.Tasks);
            Assert.ContainsSingle(tasks);
            Assert.AreEqual("t1", tasks[0].Name);
        }

        /// <summary>
        /// Evaluation of a project with more than one target with the same name
        /// should skip all but the last one.
        /// </summary>
        [MSBuildTestMethod]
        public void TargetOverride()
        {
            ProjectRootElement projectXml = ProjectRootElement.Create();
            projectXml.AddTarget("t").Inputs = "i1";
            projectXml.AddTarget("t").Inputs = "i2";

            Project project = new Project(projectXml);
            ProjectInstance instance = project.CreateProjectInstance();

            ProjectTargetInstance target = instance.Targets["t"];

            Assert.AreEqual("i2", target.Inputs);
        }

        /// <summary>
        /// Evaluation of a project with more than one target with the same name
        /// should skip all but the last one.  This is true even if the targets
        /// involved only have the same unescaped name (Orcas compat)
        /// </summary>
        [MSBuildTestMethod]
        public void TargetOverride_Escaped()
        {
            ProjectRootElement projectXml = ProjectRootElement.Create();
            projectXml.AddTarget("t%3b").Inputs = "i1";
            projectXml.AddTarget("t;").Inputs = "i2";

            Project project = new Project(projectXml);
            ProjectInstance instance = project.CreateProjectInstance();

            ProjectTargetInstance target = instance.Targets["t;"];

            Assert.AreEqual("i2", target.Inputs);
        }

        /// <summary>
        /// Evaluation of a project with more than one target with the same name
        /// should skip all but the last one.  This is true even if the targets
        /// involved only have the same unescaped name (Orcas compat)
        /// </summary>
        [MSBuildTestMethod]
        public void TargetOverride_Escaped2()
        {
            ProjectRootElement projectXml = ProjectRootElement.Create();
            projectXml.AddTarget("t;").Inputs = "i1";
            projectXml.AddTarget("t%3b").Inputs = "i2";

            Project project = new Project(projectXml);
            ProjectInstance instance = project.CreateProjectInstance();

            ProjectTargetInstance target = instance.Targets["t;"];

            Assert.AreEqual("i2", target.Inputs);
        }

        /// <summary>
        /// Verify that targets from a saved, but subsequently edited, project
        /// provide the correct full path.
        /// </summary>
        [MSBuildTestMethod]
        public void FileLocationAvailableEvenAfterEdits()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFileName();
                ProjectRootElement projectXml = ProjectRootElement.Create(path);
                projectXml.Save();

                projectXml.AddTarget("t");

                Project project = new Project(projectXml);
                ProjectTargetInstance target = project.Targets["t"];

                Assert.AreEqual(project.FullPath, target.FullPath);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Create a ProjectTargetInstance with some parameters
        /// </summary>
        private static ProjectTargetInstance GetSampleTargetInstance()
        {
            string content = @"
                    <Project>
                        <Target Name='t' Inputs='i' Outputs='o' Condition='c' DependsOnTargets='d' BeforeTargets='b' AfterTargets='a' KeepDuplicateOutputs='k' Returns='r'>
                            <t1/>
                        </Target>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement xml = projectRootElementFromString.Project;
            Project project = new Project(xml);
            ProjectInstance instance = project.CreateProjectInstance();
            ProjectTargetInstance target = instance.Targets["t"];

            return target;
        }
    }
}
