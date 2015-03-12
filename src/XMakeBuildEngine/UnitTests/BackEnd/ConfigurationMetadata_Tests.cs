// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the ConfigurationMetadata class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.BackEnd;
using System.IO;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for ConfigurationMetadata
    /// </summary>
    [TestClass]
    public class ConfigurationMetadata_Tests
    {
        /// <summary>
        /// Prepares to run the test
        /// </summary>
        [TestInitialize]
        public void SetUp()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Tears down after the test.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
        }

        /// <summary>
        /// Verify that a null config throws an ArgumentNullException.
        /// </summary>
        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestConstructorNullConfiguration()
        {
            BuildRequestConfiguration config = null;
            ConfigurationMetadata metadata = new ConfigurationMetadata(config);
        }

        /// <summary>
        /// Verify that a null project thrown an ArgumentNullException
        /// </summary>
        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestConstructorNullProject()
        {
            Project project = null;
            ConfigurationMetadata metadata = new ConfigurationMetadata(project);
        }

        /// <summary>
        /// Verify that we get the project path and tools version from the configuration
        /// </summary>
        [TestMethod]
        public void TestValidConfiguration()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            ConfigurationMetadata metadata = new ConfigurationMetadata(config);
            Assert.AreEqual(data.ProjectFullPath, metadata.ProjectFullPath);
            Assert.AreEqual(data.ExplicitlySpecifiedToolsVersion, metadata.ToolsVersion);
        }

        /// <summary>
        /// Verify that we get the project path and tools version from the project.
        /// </summary>
        [TestMethod]
        public void TestValidProject()
        {
            Project project = CreateProject();

            ConfigurationMetadata metadata = new ConfigurationMetadata(project);
            Assert.AreEqual(project.FullPath, metadata.ProjectFullPath);
            Assert.AreEqual(project.ToolsVersion, metadata.ToolsVersion);
        }

        /// <summary>
        /// Verify that we get the same hash code from equivalent metadatas even if they come from different sources.
        /// </summary>
        [TestMethod]
        public void TestGetHashCode()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Project project = CreateProject();

            ConfigurationMetadata metadata1 = new ConfigurationMetadata(config);
            ConfigurationMetadata metadata2 = new ConfigurationMetadata(project);
            Assert.AreEqual(metadata1.GetHashCode(), metadata2.GetHashCode());
        }

        /// <summary>
        /// Verify that the Equals method works correctly.
        /// </summary>
        [TestMethod]
        public void TestEquals()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Project project = CreateProject();

            ConfigurationMetadata metadata1 = new ConfigurationMetadata(config);
            ConfigurationMetadata metadata2 = new ConfigurationMetadata(project);
            Assert.IsTrue(metadata1.Equals(metadata2));

            data = new BuildRequestData("file2", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[0], null);
            BuildRequestConfiguration config2 = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            ConfigurationMetadata metadata3 = new ConfigurationMetadata(config2);
            Assert.IsFalse(metadata1.Equals(metadata3));

            data = new BuildRequestData("file", new Dictionary<string, string>(), "3.0", new string[0], null);
            BuildRequestConfiguration config3 = new BuildRequestConfiguration(1, data, "3.0");
            ConfigurationMetadata metadata4 = new ConfigurationMetadata(config3);
            Assert.IsFalse(metadata1.Equals(metadata4));
        }

        /// <summary>
        /// Creates a test project.
        /// </summary>
        private Project CreateProject()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<Target Name='Build'>
</Target>
</Project>");

            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Project project = new Project(XmlReader.Create(new StringReader(projectBody)), globalProperties, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            project.FullPath = "file";

            return project;
        }
    }
}
