//-----------------------------------------------------------------------
// <copyright file="ProjectTargetInstance_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectTargetInstanceTests class.</summary>
//-----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        [TestMethod]
        public void Accessors()
        {
            ProjectTargetInstance target = GetSampleTargetInstance();

            Assert.AreEqual("t", target.Name);
            Assert.AreEqual("c", target.Condition);
            Assert.AreEqual("i", target.Inputs);
            Assert.AreEqual("o", target.Outputs);
            Assert.AreEqual("d", target.DependsOnTargets);
            Assert.AreEqual("k", target.KeepDuplicateOutputs);
            Assert.AreEqual("r", target.Returns);
            Assert.AreEqual("t1", ((ProjectTaskInstance)target.Children[0]).Name);

            IList<ProjectTaskInstance> tasks = Helpers.MakeList(target.Tasks);
            Assert.AreEqual(1, tasks.Count);
            Assert.AreEqual("t1", tasks[0].Name);
        }

        /// <summary>
        /// Evaluation of a project with more than one target with the same name
        /// should skip all but the last one.
        /// </summary>
        [TestMethod]
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
        [TestMethod]
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
        [TestMethod]
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
        [TestMethod]
        public void FileLocationAvailableEvenAfterEdits()
        {
            string path = null;

            try
            {
                path = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
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
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t' Inputs='i' Outputs='o' Condition='c' DependsOnTargets='d' KeepDuplicateOutputs='k' Returns='r'>
                            <t1/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(xml);
            ProjectInstance instance = project.CreateProjectInstance();
            ProjectTargetInstance target = instance.Targets["t"];

            return target;
        }
    }
}
