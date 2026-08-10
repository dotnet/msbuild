// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class ParserIgnoreConfiguration_Tests : IDisposable
    {
        private readonly string _testDir;

        public ParserIgnoreConfiguration_Tests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "MSBuildTest_UnknownElements_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }

        [Fact]
        public void ParsesValidConfigFile()
        {
            string configPath = Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig>
  <IgnoreAttributes>
    <Ignore Element=""Target"" Name=""Foo"" />
    <Ignore Element=""PropertyGroup"" Name=""Bar"" />
  </IgnoreAttributes>
  <IgnoreChildren>
    <Ignore Element=""Project"" Name=""CustomThing"" />
  </IgnoreChildren>
</ParseConfig>");

            var config = ParserIgnoreConfiguration.LoadFromFile(configPath);

            config.LoadedConfigFiles.Count.ShouldBe(1);
            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            config.CheckSkipElement("Project", "CustomThing").ShouldBeTrue();
            config.CheckSkipAttribute("PropertyGroup", "Bar").ShouldBeTrue();
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            string configPath = Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            var config = ParserIgnoreConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("target", "foo").ShouldBeTrue();
            config.CheckSkipAttribute("TARGET", "FOO").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
        }

        [Fact]
        public void IgnoresInvalidEntries()
        {
            string configPath = Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig>
  <IgnoreAttributes>
    <Ignore Element="""" Name=""Foo"" />
    <Ignore Element=""Target"" Name="""" />
    <Ignore Name=""Bar"" />
    <Ignore Element=""Target"" />
    <NotIgnore Element=""Target"" Name=""Nope"" />
    <Ignore Element=""Target"" Name=""ValidOne"" />
  </IgnoreAttributes>
  <BogusSection>
    <Ignore Element=""Target"" Name=""Nope2"" />
  </BogusSection>
</ParseConfig>");

            var config = ParserIgnoreConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "ValidOne").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
            config.CheckSkipAttribute("Target", "Bar").ShouldBeFalse();
            config.CheckSkipAttribute("Target", "Nope").ShouldBeFalse();
            config.CheckSkipAttribute("Target", "Nope2").ShouldBeFalse();
        }

        [Fact]
        public void ReturnsEmptyWhenNoConfigExists()
        {
            var config = ParserIgnoreConfiguration.LoadFromFile(Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName));

            config.LoadedConfigFiles.Count.ShouldBe(0);
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void MergeCombinesEntriesAndDeduplicatesFiles()
        {
            string configPath = Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            string extraConfigPath = Path.Combine(_testDir, "extra.config");
            File.WriteAllText(extraConfigPath, @"<ParseConfig><IgnoreChildren><Ignore Element=""Project"" Name=""CustomThing"" /></IgnoreChildren></ParseConfig>");

            var merged = ParserIgnoreConfiguration.Merge(
                ParserIgnoreConfiguration.LoadFromFile(configPath),
                ParserIgnoreConfiguration.LoadFromFile(extraConfigPath));
            merged = ParserIgnoreConfiguration.Merge(merged, ParserIgnoreConfiguration.LoadFromFile(configPath));

            merged.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            merged.CheckSkipElement("Project", "CustomThing").ShouldBeTrue();
            merged.LoadedConfigFiles.Count.ShouldBe(2);
        }

        [Fact]
        public void LoadGlobalConfigLoadsEnvironmentVariablePaths()
        {
            string envConfigPath = Path.Combine(_testDir, "env.config");
            File.WriteAllText(envConfigPath, @"<ParseConfig><IgnoreChildren><Ignore Element=""Project"" Name=""CustomThing"" /></IgnoreChildren></ParseConfig>");

            string oldEnv = Environment.GetEnvironmentVariable(ParserIgnoreConfiguration.EnvironmentVariableName);
            try
            {
                Environment.SetEnvironmentVariable(ParserIgnoreConfiguration.EnvironmentVariableName, envConfigPath);
                var config = ParserIgnoreConfiguration.LoadGlobalConfig();

                config.CheckSkipElement("Project", "CustomThing").ShouldBeTrue();
            }
            finally
            {
                Environment.SetEnvironmentVariable(ParserIgnoreConfiguration.EnvironmentVariableName, oldEnv);
            }
        }

        [Fact]
        public void RecordsSkippedItems()
        {
            string configPath = Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            var config = ParserIgnoreConfiguration.LoadFromFile(configPath);

            config.GetSkippedSummaryMessage().ShouldBeNull();

            config.CheckSkipAttribute("Target", "Foo");
            config.CheckSkipAttribute("Target", "Foo");

            string summary = config.GetSkippedSummaryMessage();
            summary.ShouldNotBeNull();
            summary.ShouldContain("Attribute:Target:Foo");
            summary.ShouldContain("2 occurrences");
        }

        [Theory]
        [MemberData(nameof(AttributeSkipCases))]
        public void AllowedAttributeIsSkipped(string configElement, string configName, string projectXml)
        {
            string configPath = Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName);
            File.WriteAllText(configPath, $@"<ParseConfig><IgnoreAttributes><Ignore Element=""{configElement}"" Name=""{configName}"" /></IgnoreAttributes></ParseConfig>");
            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectXml);

            using (var pc = CreateProjectCollection(ParserIgnoreConfiguration.LoadFromFile(configPath)))
            {
                var project = new Project(projectFile, null, null, pc, ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports);
                project.ShouldNotBeNull();
            }
        }

        [Theory]
        [MemberData(nameof(AttributeSkipCases))]
        public void NonAllowedAttributeThrows(string configElement, string configName, string projectXml)
        {
            _ = configElement;
            _ = configName;
            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectXml);

            using (var pc = CreateProjectCollection(ParserIgnoreConfiguration.Empty))
            {
                Should.Throw<InvalidProjectFileException>(() => new Project(projectFile, null, null, pc, ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports));
            }
        }

        [Theory]
        [MemberData(nameof(ChildSkipCases))]
        public void AllowedChildIsSkipped(string configElement, string configName, string projectXml)
        {
            string configPath = Path.Combine(_testDir, ParserIgnoreConfiguration.ConfigFileName);
            File.WriteAllText(configPath, $@"<ParseConfig><IgnoreChildren><Ignore Element=""{configElement}"" Name=""{configName}"" /></IgnoreChildren></ParseConfig>");
            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectXml);

            using (var pc = CreateProjectCollection(ParserIgnoreConfiguration.LoadFromFile(configPath)))
            {
                var project = new Project(projectFile, null, null, pc, ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports);
                project.ShouldNotBeNull();
            }
        }

        [Theory]
        [MemberData(nameof(ChildSkipCases))]
        public void NonAllowedChildThrows(string configElement, string configName, string projectXml)
        {
            _ = configElement;
            _ = configName;
            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectXml);

            using (var pc = CreateProjectCollection(ParserIgnoreConfiguration.Empty))
            {
                Should.Throw<InvalidProjectFileException>(() => new Project(projectFile, null, null, pc, ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports));
            }
        }

        public static IEnumerable<object[]> AttributeSkipCases => new[]
        {
            new object[] { "Target", "X", @"<Project><Target Name=""T"" X=""1"" /></Project>" },
            new object[] { "PropertyGroup", "X", @"<Project><PropertyGroup X=""1""><A>1</A></PropertyGroup><Target Name=""T"" /></Project>" },
            new object[] { "Property", "X", @"<Project><PropertyGroup><A X=""1"">1</A></PropertyGroup><Target Name=""T"" /></Project>" },
            new object[] { "ItemGroup", "X", @"<Project><ItemGroup X=""1""><Compile Include=""a.cs"" /></ItemGroup><Target Name=""T"" /></Project>" },
            new object[] { "Item", "_X", @"<Project><ItemGroup><Compile Include=""a.cs"" _X=""1"" /></ItemGroup><Target Name=""T"" /></Project>" },
            new object[] { "Import", "X", @"<Project><Import Project=""nonexistent.props"" X=""1"" /><Target Name=""T"" /></Project>" },
            new object[] { "ImportGroup", "X", @"<Project><ImportGroup X=""1""><Import Project=""nonexistent.props"" /></ImportGroup><Target Name=""T"" /></Project>" },
            new object[] { "UsingTask", "X", @"<Project><UsingTask TaskName=""Foo"" AssemblyName=""Bar"" X=""1"" /><Target Name=""T"" /></Project>" },
            new object[] { "OnError", "X", @"<Project><Target Name=""T""><OnError ExecuteTargets=""T"" X=""1"" /></Target></Project>" },
            new object[] { "Output", "X", @"<Project><Target Name=""T""><Message Text=""hi""><Output TaskParameter=""Text"" PropertyName=""P"" X=""1"" /></Message></Target></Project>" },
            new object[] { "Choose", "X", @"<Project><Choose X=""1""><When Condition=""true""><PropertyGroup><A>1</A></PropertyGroup></When></Choose><Target Name=""T"" /></Project>" },
            new object[] { "Otherwise", "X", @"<Project><Choose><When Condition=""true""><PropertyGroup><A>1</A></PropertyGroup></When><Otherwise X=""1""><PropertyGroup><B>2</B></PropertyGroup></Otherwise></Choose><Target Name=""T"" /></Project>" },
            new object[] { "ItemDefinition", "_X", @"<Project><ItemDefinitionGroup><Compile _X=""1"" /></ItemDefinitionGroup><Target Name=""T"" /></Project>" },
            new object[] { "Metadata", "X", @"<Project><ItemGroup><Compile Include=""a.cs""><MyMeta X=""1"">val</MyMeta></Compile></ItemGroup><Target Name=""T"" /></Project>" },
            new object[] { "UsingTaskBody", "X", @"<Project><UsingTask TaskName=""Foo"" AssemblyName=""Bar"" TaskFactory=""CodeTaskFactory""><Task X=""1"" /></UsingTask><Target Name=""T"" /></Project>" },
            new object[] { "Parameter", "X", @"<Project><UsingTask TaskName=""Foo"" AssemblyName=""Bar"" TaskFactory=""CodeTaskFactory""><ParameterGroup><MyParam X=""1"" /></ParameterGroup></UsingTask><Target Name=""T"" /></Project>" },
            new object[] { "ProjectExtensions", "X", @"<Project><ProjectExtensions X=""1""><Foo>bar</Foo></ProjectExtensions><Target Name=""T"" /></Project>" },
        };

        public static IEnumerable<object[]> ChildSkipCases => new[]
        {
            new object[] { "Project", "Custom", @"<Project><Custom /><Target Name=""T"" /></Project>" },
            new object[] { "UsingTask", "Custom", @"<Project><UsingTask TaskName=""Foo"" AssemblyName=""Bar"" TaskFactory=""CodeTaskFactory""><Custom /></UsingTask><Target Name=""T"" /></Project>" },
            new object[] { "ImportGroup", "Custom", @"<Project><ImportGroup><Custom /></ImportGroup><Target Name=""T"" /></Project>" },
            new object[] { "Choose", "Custom", @"<Project><Choose><When Condition=""true""><PropertyGroup><A>1</A></PropertyGroup></When><Custom /></Choose><Target Name=""T"" /></Project>" },
            new object[] { "When", "Custom", @"<Project><Choose><When Condition=""true""><PropertyGroup><A>1</A></PropertyGroup><Custom /></When></Choose><Target Name=""T"" /></Project>" },
            new object[] { "Otherwise", "Custom", @"<Project><Choose><When Condition=""true""><PropertyGroup><A>1</A></PropertyGroup></When><Otherwise><PropertyGroup><B>2</B></PropertyGroup><Custom /></Otherwise></Choose><Target Name=""T"" /></Project>" },
            new object[] { "Task", "Custom", @"<Project><Target Name=""T""><Message Text=""hi""><Custom /></Message></Target></Project>" },
        };

        private static ProjectCollection CreateProjectCollection(ParserIgnoreConfiguration config)
        {
            var projectCollection = new ProjectCollection();
            projectCollection.ParserIgnoreConfiguration = config;
            return projectCollection;
        }
    }
}
