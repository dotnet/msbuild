// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class BuildRequestConfiguration_Tests : IDisposable
    {
        private TestEnvironment _env;

        public BuildRequestConfiguration_Tests(TestContext testOutput)
        {
            _env = TestEnvironment.Create(testOutput);
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        [MSBuildTestMethod]
        public void TestConstructorNullFile()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequestData config1 = new BuildRequestData(null, new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
            });
        }
        [MSBuildTestMethod]
        public void TestConstructorNullProps()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequestData config1 = new BuildRequestData("file", null, "toolsVersion", Array.Empty<string>(), null);
            });
        }
        [MSBuildTestMethod]
        public void TestConstructor1()
        {
            BuildRequestData config1 = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
        }

        [MSBuildTestMethod]
        public void TestConstructorInvalidConfigId()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
                BuildRequestConfiguration config1 = new BuildRequestConfiguration(1, data, "2.0");
                config1.ShallowCloneWithNewId(0);
            });
        }
        [MSBuildTestMethod]
        public void TestConstructor2PositiveConfigId()
        {
            BuildRequestData config1 = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
            new BuildRequestConfiguration(1, config1, "2.0");
        }

        [MSBuildTestMethod]
        public void TestConstructor2NegativeConfigId()
        {
            BuildRequestData config1 = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
            new BuildRequestConfiguration(-1, config1, "2.0");
        }

        [MSBuildTestMethod]
        public void TestConstructor2NullFile()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequestData config1 = new BuildRequestData(null, new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
            });
        }

        [MSBuildTestMethod]
        public void TestConstructor2NullProps()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                BuildRequestData config1 = new BuildRequestData("file", null, "toolsVersion", Array.Empty<string>(), null);
            });
        }
        [MSBuildTestMethod]
        public void TestWasGeneratedByNode()
        {
            BuildRequestData data1 = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(-1, data1, "2.0");
            Assert.IsTrue(config1.WasGeneratedByNode);

            BuildRequestData data2 = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config2 = new BuildRequestConfiguration(1, data2, "2.0");
            Assert.IsFalse(config2.WasGeneratedByNode);

            BuildRequestData data3 = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config3 = new BuildRequestConfiguration(data3, "2.0");
            Assert.IsFalse(config3.WasGeneratedByNode);
        }

        [MSBuildTestMethod]
        public void TestDefaultConfigurationId()
        {
            BuildRequestData data1 = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(-1, data1, "2.0");
            Assert.AreEqual(-1, config1.ConfigurationId);

            BuildRequestData data2 = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config2 = new BuildRequestConfiguration(1, data2, "2.0");
            Assert.AreEqual(1, config2.ConfigurationId);

            BuildRequestData data3 = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config3 = new BuildRequestConfiguration(0, data3, "2.0");
            Assert.AreEqual(0, config3.ConfigurationId);
        }

        [MSBuildTestMethod]
        public void TestSetConfigurationIdBad()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
                BuildRequestConfiguration config1 = new BuildRequestConfiguration(-1, data, "2.0");
                config1.ConfigurationId = -2;
            });
        }
        [MSBuildTestMethod]
        public void TestSetConfigurationIdGood()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(data, "2.0");
            Assert.AreEqual(0, config1.ConfigurationId);
            config1.ConfigurationId = 1;
            Assert.AreEqual(1, config1.ConfigurationId);
        }

        [MSBuildTestMethod]
        public void TestGetFileName()
        {
            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(data, "2.0");
            Assert.AreEqual(config1.ProjectFullPath, Path.GetFullPath("file"));
        }

        [MSBuildTestMethod]
        public void TestGetToolsVersion()
        {
            BuildRequestData data1 = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(data1, "2.0");
            Assert.AreEqual("toolsVersion", config1.ToolsVersion);
        }

        [MSBuildTestMethod]
        public void TestGetProperties()
        {
            Dictionary<string, string> props = new Dictionary<string, string>();
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(new BuildRequestData("file", props, "toolsVersion", Array.Empty<string>(), null), "2.0");

            Assert.AreEqual(props.Count, Helpers.MakeList((IEnumerable<ProjectPropertyInstance>)(config1.GlobalProperties)).Count);
        }

        [MSBuildTestMethod]
        public void TestSetProjectGood()
        {
            BuildRequestData data1 = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(data1, "2.0");
            Assert.IsNull(config1.Project);
            using ProjectFromString projectFromString = new(ObjectModelHelpers.CleanupFileContents(@"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace' />"));
            Project project = projectFromString.Project;

            ProjectInstance projectInstance = project.CreateProjectInstance();
            config1.Project = projectInstance;
            Assert.AreSame(config1.Project, projectInstance);
        }

        [MSBuildTestMethod]
        public void TestPacketType()
        {
            BuildRequestData data1 = new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(data1, "2.0");
            Assert.AreEqual(NodePacketType.BuildRequestConfiguration, config1.Type);
        }

        [MSBuildTestMethod]
        public void TestGetHashCode()
        {
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null), "2.0");
            BuildRequestConfiguration config2 = new BuildRequestConfiguration(new BuildRequestData("File", new Dictionary<string, string>(), "ToolsVersion", Array.Empty<string>(), null), "2.0");
            BuildRequestConfiguration config3 = new BuildRequestConfiguration(new BuildRequestData("file2", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null), "2.0");
            BuildRequestConfiguration config4 = new BuildRequestConfiguration(new BuildRequestData("file2", new Dictionary<string, string>(), "toolsVersion2", Array.Empty<string>(), null), "2.0");
            BuildRequestConfiguration config5 = new BuildRequestConfiguration(new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion2", Array.Empty<string>(), null), "2.0");

            Assert.AreEqual(config1.GetHashCode(), config2.GetHashCode());
            Assert.AreNotEqual(config1.GetHashCode(), config3.GetHashCode());
            Assert.AreNotEqual(config1.GetHashCode(), config5.GetHashCode());
            Assert.AreNotEqual(config4.GetHashCode(), config5.GetHashCode());
        }

        [MSBuildTestMethod]
        public void TestEquals()
        {
            BuildRequestConfiguration config1 = new BuildRequestConfiguration(new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null), "2.0");
            Assert.IsTrue(config1.Equals(config1));
            BuildRequestConfiguration config2 = new BuildRequestConfiguration(new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null), "2.0");
            Assert.AreEqual(config1, config2);

            BuildRequestConfiguration config3 = new BuildRequestConfiguration(new BuildRequestData("file2", new Dictionary<string, string>(), "toolsVersion", Array.Empty<string>(), null), "2.0");
            Assert.AreNotEqual(config1, config3);

            BuildRequestConfiguration config4 = new BuildRequestConfiguration(new BuildRequestData("file", new Dictionary<string, string>(), "toolsVersion2", Array.Empty<string>(), null), "2.0");
            Assert.AreNotEqual(config1, config4);

            PropertyDictionary<ProjectPropertyInstance> props = new PropertyDictionary<ProjectPropertyInstance>();
            props.Set(ProjectPropertyInstance.Create("prop1", "value1"));
            BuildRequestData data = new BuildRequestData("file", props.ToDictionary(), "toolsVersion", Array.Empty<string>(), null);
            BuildRequestConfiguration config5 = new BuildRequestConfiguration(data, "2.0");
            Assert.AreNotEqual(config1, config5);

            Assert.AreEqual(config1, config2);
            Assert.AreNotEqual(config1, config3);
        }

        [MSBuildTestMethod]
        public void TestTranslation()
        {
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            properties.Set(ProjectPropertyInstance.Create("this", "that"));
            properties.Set(ProjectPropertyInstance.Create("foo", "bar"));

            BuildRequestData data = new BuildRequestData("file", properties.ToDictionary(), "4.0", Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(data, "2.0");

            Assert.AreEqual(NodePacketType.BuildRequestConfiguration, config.Type);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildRequestConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildRequestConfiguration deserializedConfig = packet as BuildRequestConfiguration;

            Assert.AreEqual(config, deserializedConfig);

            // RequestedTargets is excluded from InternalEquals, so assert the empty-list round-trip explicitly.
            deserializedConfig.RequestedTargets.ShouldBeEmpty();
        }

        /// <summary>
        /// Regression test for the solution metaproject bug where building a solution with a
        /// non-standard target (e.g. "Pack") in a multi-node/parallel build fails with MSB4057.
        /// The requested targets must round-trip through translation; otherwise a configuration
        /// that crosses a node boundary loses them and the generated solution metaproject omits
        /// the user-requested targets.
        /// </summary>
        [MSBuildTestMethod]
        public void TestTranslationPreservesRequestedTargets()
        {
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            properties.Set(ProjectPropertyInstance.Create("this", "that"));

            BuildRequestData data = new BuildRequestData("file", properties.ToDictionary(), "4.0", ["Build", "Pack"], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(data, "2.0");

            config.RequestedTargets.ShouldBe(["Build", "Pack"]);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildRequestConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildRequestConfiguration deserializedConfig = packet as BuildRequestConfiguration;

            deserializedConfig.RequestedTargets.ShouldBe(["Build", "Pack"]);
        }

        [MSBuildTestMethod]
        public void TestTranslationWithEntireProjectState()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<PropertyGroup>
    <One>1</One>
    <Two>2</Two>
    <Three>$(ThreeIn)</Three>
</PropertyGroup>
<Target Name='Build'>
    <CallTarget Targets='Foo'/>
</Target>
</Project>");

            Dictionary<string, string> globalProperties = new(StringComparer.OrdinalIgnoreCase);
            globalProperties["ThreeIn"] = "3";

            using var collection = new ProjectCollection();
            using ProjectFromString projectFromString = new(
                projectBody,
                globalProperties,
                ObjectModelHelpers.MSBuildDefaultToolsVersion,
                collection);
            Project project = projectFromString.Project;
            project.FullPath = "foo";
            ProjectInstance instance = project.CreateProjectInstance();

            instance.TranslateEntireState = true;

            BuildRequestConfiguration configuration = new BuildRequestConfiguration(new BuildRequestData(instance, Array.Empty<string>(), null), "2.0");
            configuration.ConfigurationId = 1;

            ((ITranslatable)configuration).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildRequestConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildRequestConfiguration deserializedConfig = packet as BuildRequestConfiguration;

            deserializedConfig.ShouldNotBeNull();
            deserializedConfig.ShouldBe(configuration);
            deserializedConfig.Project.ShouldNotBeNull();

            // Verify that at least some data from 'entire project state' has been deserialized.
            deserializedConfig.Project.Directory.ShouldNotBeEmpty();
            deserializedConfig.Project.Directory.ShouldBe(configuration.Project.Directory);
        }

        [MSBuildTestMethod]
        public void TestProperties()
        {
            BuildRequestConfiguration configuration = new BuildRequestConfiguration(new BuildRequestData("path", new Dictionary<string, string>(), "2.0", Array.Empty<string>(), null), "2.0");
            Assert.IsTrue(configuration.IsCacheable);
            Assert.IsFalse(configuration.IsLoaded);
            Assert.IsFalse(configuration.IsCached);
            Assert.IsFalse(configuration.IsActivelyBuilding);
        }

        [MSBuildTestMethod]
        public void TestCache()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<PropertyGroup>
    <One>1</One>
    <Two>2</Two>
    <Three>$(ThreeIn)</Three>
</PropertyGroup>
<ItemGroup>
    <Foo Include=""*""/>
    <Bar Include=""msbuild.out"">
        <One>1</One>
    </Bar>
    <Baz Include=""$(BazIn)""/>
</ItemGroup>
<Target Name='Build'>
    <CallTarget Targets='Foo;Goo'/>
</Target>

<Target Name='Foo' DependsOnTargets='Foo2'>
    <FooTarget/>
</Target>

<Target Name='Goo'>
    <GooTarget/>
</Target>

<Target Name='Foo2'>
    <Foo2Target/>
</Target>
</Project>");

            Dictionary<string, string> globalProperties =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties["ThreeIn"] = "3";
            globalProperties["BazIn"] = "bazfile";

            using var collection = new ProjectCollection();
            using ProjectFromString projectFromString = new(projectBody,
                globalProperties,
                ObjectModelHelpers.MSBuildDefaultToolsVersion,
                collection);
            Project project = projectFromString.Project;
            project.FullPath = "foo";
            ProjectInstance instance = project.CreateProjectInstance();
            BuildRequestConfiguration configuration = new BuildRequestConfiguration(new BuildRequestData(instance, Array.Empty<string>(), null), "2.0");
            configuration.ConfigurationId = 1;

            string originalValue = Environment.GetEnvironmentVariable("MSBUILDCACHE");
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDCACHE", "1");
                Assert.AreEqual("3", instance.GlobalProperties["ThreeIn"]);
                Assert.AreEqual("bazfile", instance.GlobalProperties["BazIn"]);
                Assert.AreEqual("1", instance.PropertiesToBuildWith["One"].EvaluatedValue);
                Assert.AreEqual("2", instance.PropertiesToBuildWith["Two"].EvaluatedValue);
                Assert.AreEqual("3", instance.PropertiesToBuildWith["Three"].EvaluatedValue);

                int fooCount = instance.ItemsToBuildWith["Foo"].Count;
                Assert.IsTrue(fooCount > 0);
                Assert.ContainsSingle(instance.ItemsToBuildWith["Bar"]);
                Assert.ContainsSingle(instance.ItemsToBuildWith["Baz"]);
                Assert.AreEqual("bazfile", instance.ItemsToBuildWith["Baz"].First().EvaluatedInclude);

                Lookup lookup = configuration.BaseLookup;

                Assert.IsNotNull(lookup);
                Assert.AreEqual(fooCount, lookup.GetItems("Foo").Count);

                // Configuration initialized with a ProjectInstance should not be cacheable by default.
                Assert.IsFalse(configuration.IsCacheable);
                configuration.IsCacheable = true;
                configuration.CacheIfPossible();

                Assert.IsNull(instance.GlobalPropertiesDictionary);
                Assert.IsNull(instance.ItemsToBuildWith);
                Assert.IsNull(instance.PropertiesToBuildWith);

                configuration.RetrieveFromCache();

                Assert.AreEqual("3", instance.GlobalProperties["ThreeIn"]);
                Assert.AreEqual("bazfile", instance.GlobalProperties["BazIn"]);
                Assert.AreEqual("1", instance.PropertiesToBuildWith["One"].EvaluatedValue);
                Assert.AreEqual("2", instance.PropertiesToBuildWith["Two"].EvaluatedValue);
                Assert.AreEqual("3", instance.PropertiesToBuildWith["Three"].EvaluatedValue);
                Assert.AreEqual(fooCount, instance.ItemsToBuildWith["Foo"].Count);
                Assert.ContainsSingle(instance.ItemsToBuildWith["Bar"]);
                Assert.ContainsSingle(instance.ItemsToBuildWith["Baz"]);
                Assert.AreEqual("bazfile", instance.ItemsToBuildWith["Baz"].First().EvaluatedInclude);

                lookup = configuration.BaseLookup;

                Assert.IsNotNull(lookup);
                Assert.AreEqual(fooCount, lookup.GetItems("Foo").Count);
            }
            finally
            {
                configuration.ClearCacheFile();
                Environment.SetEnvironmentVariable("MSBUILDCACHE", originalValue);
            }
        }

        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void WorksCorrectlyWithCurlyBraces()
        {
            string projectBody = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                <PropertyGroup>
                    <One>1</One>
                    <Two>2</Two>
                    <Three>$(ThreeIn)</Three>
                </PropertyGroup>
                <ItemGroup>
                    <Foo Include=""*""/>
                    <Bar Include=""msbuild.out"">
                        <One>1</One>
                    </Bar>
                    <Baz Include=""$(BazIn)""/>
                </ItemGroup>
                <Target Name='Build'>
                    <CallTarget Targets='Foo;Bar'/>
                </Target>

                <Target Name='Foo' DependsOnTargets='Foo'>
                    <FooTarget/>
                </Target>

                <Target Name='Bar'>
                    <BarTarget/>
                </Target>

                <Target Name='Foo'>
                    <FooTarget/>
                </Target>
                </Project>");

            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties["ThreeIn"] = "3";
            globalProperties["BazIn"] = "bazfile";

            using var collection = new ProjectCollection();
            using ProjectFromString projectFromString = new(projectBody, globalProperties, ObjectModelHelpers.MSBuildDefaultToolsVersion, collection);
            Project project = projectFromString.Project;
            project.FullPath = "foo";
            ProjectInstance instance = project.CreateProjectInstance();
            BuildRequestConfiguration configuration = new BuildRequestConfiguration(new BuildRequestData(instance, Array.Empty<string>(), null), "2.0");

            string originalTmp = Environment.GetEnvironmentVariable("TMP");
            string originalTemp = Environment.GetEnvironmentVariable("TEMP");

            try
            {
                // Check if } do not cause it to crash due to usage of String.Format or such on code path
                string problematicTmpPath = Path.Combine(originalTmp, "}", "blabla", "temp");
                Environment.SetEnvironmentVariable("TMP", problematicTmpPath);
                Environment.SetEnvironmentVariable("TEMP", problematicTmpPath);

                FileUtilities.ClearCacheDirectoryPath();
                FileUtilities.ClearTempFileDirectory();
                string cacheFilePath = configuration.GetCacheFile();
                Assert.StartsWith(problematicTmpPath, cacheFilePath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", originalTmp);
                Environment.SetEnvironmentVariable("TEMP", originalTemp);
                FileUtilities.ClearCacheDirectoryPath();
                FileUtilities.ClearTempFileDirectory();
            }
        }

        [MSBuildTestMethod]
        public void SkipIsolationChecksRejectsMissingEvaluation()
        {
            var configWithoutEvaluation = new BuildRequestConfiguration();

            var exception = Assert.ThrowsExactly<InternalErrorException>(
                () =>
                {
                    configWithoutEvaluation.ShouldSkipIsolationConstraintsForReference(Path.GetFullPath("foo"));
                });
        }

        [MSBuildTestMethod]
        public void SkipIsolationChecksRejectsRelativeReferencePaths()
        {
            var exception = Assert.ThrowsExactly<InternalErrorException>(
                () =>
                {
                    TestSkipIsolationConstraints("*", "build.proj", false);
                });

            exception.Message.ShouldContain("Method does not treat path normalization cases");
        }

        [MSBuildTestMethod]
        public void SkipIsolationConstraintsDoesNotSkipWhenItemDoesNotExist()
        {
            TestSkipIsolationConstraints(@"c:\*.csproj", @"c:\foo.csproj", false, "<Project></Project>");
        }

        [MSBuildTestMethod]
        [DataRow("", @"c:\foo", false)]
        [DataRow("*", @"c:\foo.proj", false)] // relative glob is normalized to project directory
        [DataRow("*", @"$(MSBuildProjectDirectory)\foo.proj", true)] // relative glob is normalized to project directory
        [DataRow(@"c:\*.csproj", @"c:\foo.proj", false)]
        [DataRow(@"c:\*.csproj", @"c:\foo.csproj", true)]
        [DataRow(@"c:\*.props;c:\*.csproj", @"c:\foo.csproj", true)]
        [DataRow(@"c:\project\*script*\**\*.proj", @"c:\foo.csproj", false)]
        [DataRow(@"c:\project\*script*\**\*.proj", @"c:\project\scripts\a\b\build.proj", true)]
        [DataRow(@"c:\project\script\Project*.proj", @"c:\project\script\Project.proj", true)]
        [DataRow(@"c:\project\script\Project*.proj", @"c:\project\script\Project1.proj", true)]
        [DataRow(@"c:\project\script\Project*.proj", @"c:\project\script\build.proj", false)]
        public void SkipIsolationCheckShouldFilterReferencesViaMSBuildGlobs(string glob, string referencePath, bool expectedOutput)
        {
            TestSkipIsolationConstraints(glob, referencePath, expectedOutput);
        }

        private void TestSkipIsolationConstraints(string glob, string referencePath, bool expectedOutput, string projectContents = null)
        {
            if (!NativeMethodsShared.IsWindows)
            {
                glob = glob.Replace(@"c:\", "/").ToSlash();
                referencePath = referencePath.Replace(@"c:\", "/").ToSlash();
            }

            glob = $"$([MSBuild]::Escape('{glob}'))";

            projectContents ??= $@"
<Project>
    <ItemGroup>
        <{ItemTypeNames.GraphIsolationExemptReference} Include=`{glob};ShouldNotMatchAnything`/>
    </ItemGroup>
</Project>
".Cleanup();

            var projectCollection = _env.CreateProjectCollection().Collection;
            using var xmlReader = XmlReader.Create(new StringReader(projectContents));
            var project = Project.FromXmlReader(
                xmlReader,
                new ProjectOptions
                {
                    ProjectCollection = projectCollection
                });

            project.FullPath = _env.CreateFolder().Path;

            var projectInstance = project.CreateProjectInstance();

            var configuration = new BuildRequestConfiguration(new BuildRequestData(projectInstance, Array.Empty<string>()), MSBuildConstants.CurrentToolsVersion);

            if (referencePath.Contains("$"))
            {
                referencePath = project.ExpandPropertyValueBestEffortLeaveEscaped(referencePath, ElementLocation.EmptyLocation);
            }

            configuration.ShouldSkipIsolationConstraintsForReference(referencePath).ShouldBe(expectedOutput);
        }

        [MSBuildTestMethod]
        public void TestProjectEvaluationIdPreservedAcrossTranslation()
        {
            string projectBody = """
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                <Target Name='Build' />
            </Project>
            """.Cleanup();

            using var collection = new ProjectCollection();
            using ProjectFromString projectFromString = new(
                projectBody,
                new Dictionary<string, string>(),
                ObjectModelHelpers.MSBuildDefaultToolsVersion,
                collection);
            Project project = projectFromString.Project;
            project.FullPath = "foo";
            ProjectInstance instance = project.CreateProjectInstance();

            BuildRequestConfiguration configuration = new(
                new BuildRequestData(instance, [], null, BuildRequestDataFlags.None, propertiesToTransfer: []), "2.0")
            {
                ConfigurationId = 1,
            };

            // The evaluation ID should be set from the project instance.
            int expectedEvalId = instance.EvaluationId;
            configuration.ProjectEvaluationId.ShouldBe(expectedEvalId);
            expectedEvalId.ShouldNotBe(BuildEventContext.InvalidEvaluationId);

            ((ITranslatable)configuration).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildRequestConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildRequestConfiguration deserializedConfig = packet as BuildRequestConfiguration;
            deserializedConfig.ShouldNotBeNull();
            deserializedConfig.ProjectEvaluationId.ShouldBe(expectedEvalId);
        }

        [MSBuildTestMethod]
        public void TestProjectEvaluationIdPreservedInShallowClone()
        {
            string projectBody = """
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                <Target Name='Build' />
            </Project>
            """.Cleanup();

            using var collection = new ProjectCollection();
            using ProjectFromString projectFromString = new(
                projectBody,
                new Dictionary<string, string>(),
                ObjectModelHelpers.MSBuildDefaultToolsVersion,
                collection);
            Project project = projectFromString.Project;
            project.FullPath = "foo";
            ProjectInstance instance = project.CreateProjectInstance();

            BuildRequestConfiguration original = new(new BuildRequestData(instance, [], null), "2.0")
            {
                ConfigurationId = 1,
            };

            int expectedEvalId = instance.EvaluationId;
            original.ProjectEvaluationId.ShouldBe(expectedEvalId);

            BuildRequestConfiguration clone = original.ShallowCloneWithNewId(2);
            clone.ProjectEvaluationId.ShouldBe(expectedEvalId);
        }


        [MSBuildTestMethod]
        public void TestProjectEvaluationIdPreservedAcrossTranslateForFutureUse()
        {
            string projectBody = """
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='Build' />
                </Project>
                """.Cleanup();

            using var collection = new ProjectCollection();
            using ProjectFromString projectFromString = new(
                projectBody,
                new Dictionary<string, string>(),
                ObjectModelHelpers.MSBuildDefaultToolsVersion,
                collection);
            Project project = projectFromString.Project;
            project.FullPath = "foo";
            ProjectInstance instance = project.CreateProjectInstance();

            BuildRequestConfiguration configuration = new(new BuildRequestData(instance, [], null), "2.0")
            {
                ConfigurationId = 1,
            };

            int expectedEvalId = instance.EvaluationId;
            configuration.ProjectEvaluationId.ShouldBe(expectedEvalId);

            // TranslateForFutureUse uses a different serialization path.
            configuration.TranslateForFutureUse(TranslationHelpers.GetWriteTranslator());
            ITranslator reader = TranslationHelpers.GetReadTranslator();

            BuildRequestConfiguration deserialized = new();
            deserialized.TranslateForFutureUse(reader);

            deserialized.ProjectEvaluationId.ShouldBe(expectedEvalId);
        }
    }
}
