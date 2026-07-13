// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Shouldly;
using System.Linq;

#nullable disable

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
        public ConfigurationMetadata_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Verify that a null config throws an ArgumentNullException.
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorNullConfiguration()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequestConfiguration config = null;
                ConfigurationMetadata metadata = new ConfigurationMetadata(config);
            });
        }
        /// <summary>
        /// Verify that a null project thrown an ArgumentNullException
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorNullProject()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                Project project = null;
                ConfigurationMetadata metadata = new ConfigurationMetadata(project);
            });
        }
        /// <summary>
        /// Verify that we get the project path and tools version from the configuration
        /// </summary>
        [MSBuildTestMethod]
        public void TestValidConfiguration()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            ConfigurationMetadata metadata = new ConfigurationMetadata(config);
            Assert.AreEqual(data.ProjectFullPath, metadata.ProjectFullPath);
            Assert.AreEqual(data.ExplicitlySpecifiedToolsVersion, metadata.ToolsVersion);
        }

        /// <summary>
        /// Verify that we get the project path and tools version from the project.
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void TestGetHashCode()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Project project = CreateProject();

            ConfigurationMetadata metadata1 = new ConfigurationMetadata(config);
            ConfigurationMetadata metadata2 = new ConfigurationMetadata(project);
            Assert.AreEqual(metadata1.GetHashCode(), metadata2.GetHashCode());
        }

        /// <summary>
        /// Verify that the Equals method works correctly.
        /// </summary>
        [MSBuildTestMethod]
        public void TestEquals()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Project project = CreateProject();

            ConfigurationMetadata metadata1 = new ConfigurationMetadata(config);
            ConfigurationMetadata metadata2 = new ConfigurationMetadata(project);
            Assert.IsTrue(metadata1.Equals(metadata2));

            data = new BuildRequestData("file2", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, Array.Empty<string>(), null);
            BuildRequestConfiguration config2 = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            ConfigurationMetadata metadata3 = new ConfigurationMetadata(config2);
            Assert.IsFalse(metadata1.Equals(metadata3));

            data = new BuildRequestData("file", new Dictionary<string, string>(), "3.0", Array.Empty<string>(), null);
            BuildRequestConfiguration config3 = new BuildRequestConfiguration(1, data, "3.0");
            ConfigurationMetadata metadata4 = new ConfigurationMetadata(config3);
            Assert.IsFalse(metadata1.Equals(metadata4));
        }

        [MSBuildTestMethod]
        public void TestTranslation()
        {
            var globalProperties = new PropertyDictionary<ProjectPropertyInstance>();
            globalProperties["a"] = ProjectPropertyInstance.Create("a", "b");

            var initial = new ConfigurationMetadata("path", globalProperties);

            initial.Translate(TranslationHelpers.GetWriteTranslator());
            var copy = ConfigurationMetadata.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            copy.ProjectFullPath.ShouldBe(initial.ProjectFullPath);
            copy.ToolsVersion.ShouldBe(initial.ToolsVersion);

            Assert.IsTrue(copy.GlobalProperties.SequenceEqual(initial.GlobalProperties, EqualityComparer<ProjectPropertyInstance>.Default));
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
            using ProjectFromString projectFromString = new(projectBody, globalProperties, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            Project project = projectFromString.Project;
            project.FullPath = "file";

            return project;
        }
    }
}
