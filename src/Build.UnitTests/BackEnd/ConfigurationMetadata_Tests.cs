// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.BackEnd;
using System.IO;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for ConfigurationMetadata
    /// </summary>
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
        [Fact]
        public void TestConstructorNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequestConfiguration config = null;
                ConfigurationMetadata metadata = new ConfigurationMetadata(config);
            }
           );
        }
        /// <summary>
        /// Verify that a null project thrown an ArgumentNullException
        /// </summary>
        [Fact]
        public void TestConstructorNullProject()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Project project = null;
                ConfigurationMetadata metadata = new ConfigurationMetadata(project);
            }
           );
        }
        /// <summary>
        /// Verify that we get the project path and tools version from the configuration
        /// </summary>
        [Fact]
        public void TestValidConfiguration()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            ConfigurationMetadata metadata = new ConfigurationMetadata(config);
            Assert.Equal(data.ProjectFullPath, metadata.ProjectFullPath);
            Assert.Equal(data.ExplicitlySpecifiedToolsVersion, metadata.ToolsVersion);
        }

        /// <summary>
        /// Verify that we get the project path and tools version from the project.
        /// </summary>
        [Fact]
        public void TestValidProject()
        {
            Project project = CreateProject();

            ConfigurationMetadata metadata = new ConfigurationMetadata(project);
            Assert.Equal(project.FullPath, metadata.ProjectFullPath);
            Assert.Equal(project.ToolsVersion, metadata.ToolsVersion);
        }

        /// <summary>
        /// Verify that we get the same hash code from equivalent metadatas even if they come from different sources.
        /// </summary>
        [Fact]
        public void TestGetHashCode()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Project project = CreateProject();

            ConfigurationMetadata metadata1 = new ConfigurationMetadata(config);
            ConfigurationMetadata metadata2 = new ConfigurationMetadata(project);
            Assert.Equal(metadata1.GetHashCode(), metadata2.GetHashCode());
        }

        /// <summary>
        /// Verify that the Equals method works correctly.
        /// </summary>
        [Fact]
        public void TestEquals()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Project project = CreateProject();

            ConfigurationMetadata metadata1 = new ConfigurationMetadata(config);
            ConfigurationMetadata metadata2 = new ConfigurationMetadata(project);
            Assert.True(metadata1.Equals(metadata2));

            data = new BuildRequestData("file2", new Dictionary<string, string>(), ObjectModelHelpers.MSBuildDefaultToolsVersion, new string[0], null);
            BuildRequestConfiguration config2 = new BuildRequestConfiguration(1, data, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            ConfigurationMetadata metadata3 = new ConfigurationMetadata(config2);
            Assert.False(metadata1.Equals(metadata3));

            data = new BuildRequestData("file", new Dictionary<string, string>(), "3.0", new string[0], null);
            BuildRequestConfiguration config3 = new BuildRequestConfiguration(1, data, "3.0");
            ConfigurationMetadata metadata4 = new ConfigurationMetadata(config3);
            Assert.False(metadata1.Equals(metadata4));
        }

        [Fact]
        public void TestTranslation()
        {
            var globalProperties = new PropertyDictionary<ProjectPropertyInstance>();
            globalProperties["a"] = ProjectPropertyInstance.Create("a", "b");

            var initial = new ConfigurationMetadata("path", globalProperties);

            initial.Translate(TranslationHelpers.GetWriteTranslator());
            var copy = ConfigurationMetadata.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            copy.ProjectFullPath.ShouldBe(initial.ProjectFullPath);
            copy.ToolsVersion.ShouldBe(initial.ToolsVersion);

            Assert.Equal(copy.GlobalProperties.GetCopyOnReadEnumerable(), initial.GlobalProperties.GetCopyOnReadEnumerable(), EqualityComparer<ProjectPropertyInstance>.Default);
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
