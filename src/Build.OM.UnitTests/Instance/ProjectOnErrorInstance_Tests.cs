// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for the ProjectOnErrorInstance class.
    /// </summary>
    public class ProjectOnErrorInstance_Tests
    {
        /// <summary>
        /// Test accessors
        /// </summary>
        [Fact]
        public void Accessors()
        {
            var onError = GetSampleOnErrorInstance();

            Assert.Equal("et", onError.ExecuteTargets);
            Assert.Equal("c", onError.Condition);
        }

        /// <summary>
        /// Create a TaskInstance with some parameters
        /// </summary>
        private static ProjectOnErrorInstance GetSampleOnErrorInstance()
        {
            string content = @"
                    <Project>
                       <Target Name='t'>
                            <OnError ExecuteTargets='et' Condition='c'/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(xml);
            ProjectInstance instance = project.CreateProjectInstance();
            ProjectOnErrorInstance onError = (ProjectOnErrorInstance)instance.Targets["t"].OnErrorChildren[0];

            return onError;
        }
    }
}
