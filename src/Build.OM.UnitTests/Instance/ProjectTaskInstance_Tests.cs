// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectTaskInstance
    /// </summary>
    public class ProjectTaskInstance_Tests
    {
        /// <summary>
        /// Test accessors
        /// </summary>
        [Fact]
        public void Accessors()
        {
            var task = GetSampleTaskInstance();

            Assert.Equal("t1", task.Name);
            Assert.Equal("c", task.Condition);
            Assert.Equal("coe", task.ContinueOnError);

            var parameters = task.Parameters;
            Assert.Equal(2, parameters.Count);
            Assert.Equal("a1", parameters["a"]);
            Assert.Equal("b1", parameters["b"]);
        }

        /// <summary>
        /// Generally, empty parameters aren't set on task classes at all, but there is
        /// one exception: if the empty parameter corresponds to a task class property
        /// of array type, an empty array is set on the task class.
        /// Therefore empty task parameters should be returned by the parameter list.
        /// </summary>
        [Fact]
        public void EmptyParameter()
        {
            var task = GetTaskInstance(@"<t1 a=''/>");

            Assert.Single(task.Parameters);
        }

        /// <summary>
        /// Create a TaskInstance with some parameters
        /// </summary>
        private static ProjectTaskInstance GetSampleTaskInstance()
        {
            ProjectTaskInstance task = GetTaskInstance(@"<t1 a='a1' b='b1' ContinueOnError='coe' Condition='c'/>");

            return task;
        }

        /// <summary>
        /// Return a task instance representing the task XML string passed in
        /// </summary>
        private static ProjectTaskInstance GetTaskInstance(string taskXmlString)
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            " + taskXmlString + @"
                        </Target>
                    </Project>
                ";

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(xml);
            ProjectInstance instance = project.CreateProjectInstance();
            ProjectTaskInstance task = (ProjectTaskInstance)(instance.Targets["t"].Children[0]);
            return task;
        }
    }
}
