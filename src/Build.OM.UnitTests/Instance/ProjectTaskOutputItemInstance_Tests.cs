// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for the ProjectTaskOutputItemInstance class.
    /// </summary>
    [TestClass]
    public class ProjectTaskOutputItemInstance_Tests
    {
        /// <summary>
        /// Test accessors
        /// </summary>
        [MSBuildTestMethod]
        public void Accessors()
        {
            var output = GetSampleTaskOutputInstance();

            Assert.AreEqual("p", output.TaskParameter);
            Assert.AreEqual("c", output.Condition);
            Assert.AreEqual("i", output.ItemType);
        }

        /// <summary>
        /// Create a TaskInstance with some parameters
        /// </summary>
        private static ProjectTaskOutputItemInstance GetSampleTaskOutputInstance()
        {
            string content = @"
                    <Project>
                       <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p' Condition='c' ItemName='i'/>
                            </t1>
                        </Target>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement xml = projectRootElementFromString.Project;
            Project project = new Project(xml);
            ProjectInstance instance = project.CreateProjectInstance();
            ProjectTaskInstance task = (ProjectTaskInstance)instance.Targets["t"].Children[0];
            ProjectTaskOutputItemInstance output = (ProjectTaskOutputItemInstance)task.Outputs[0];

            return output;
        }
    }
}
