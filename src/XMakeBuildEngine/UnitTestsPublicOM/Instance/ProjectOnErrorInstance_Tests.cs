//-----------------------------------------------------------------------
// <copyright file="ProjectOnErrorInstance_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectOnErrorInstance class.</summary>
//-----------------------------------------------------------------------

using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for the ProjectOnErrorInstance class.
    /// </summary>
    [TestClass]
    public class ProjectOnErrorInstance_Tests
    {
        /// <summary>
        /// Test accessors
        /// </summary>
        [TestMethod]
        public void Accessors()
        {
            var onError = GetSampleOnErrorInstance();

            Assert.AreEqual("et", onError.ExecuteTargets);
            Assert.AreEqual("c", onError.Condition);
        }

        /// <summary>
        /// Create a TaskInstance with some parameters
        /// </summary>
        private static ProjectOnErrorInstance GetSampleOnErrorInstance()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
