// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class UnknownElementsConfiguration_Tests : IDisposable
    {
        private readonly string _testDir;

        public UnknownElementsConfiguration_Tests()
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
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig>
  <IgnoreAttributes>
    <Ignore Element=""Target"" Name=""Foo"" />
    <Ignore Element=""PropertyGroup"" Name=""Bar"" />
  </IgnoreAttributes>
  <IgnoreChildren>
    <Ignore Element=""Project"" Name=""CustomThing"" />
  </IgnoreChildren>
</ParseConfig>");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.LoadedConfigFiles.Count.ShouldBe(1);
            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            config.CheckSkipElement("Project", "CustomThing").ShouldBeTrue();
            config.CheckSkipAttribute("PropertyGroup", "Bar").ShouldBeTrue();
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("target", "foo").ShouldBeTrue();
            config.CheckSkipAttribute("TARGET", "FOO").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
        }

        [Fact]
        public void RejectsNonAllowedItems()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "Bar").ShouldBeFalse();
            config.CheckSkipElement("Target", "Foo").ShouldBeFalse();
            config.CheckSkipAttribute("ItemGroup", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void IgnoresInvalidEntries()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
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

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "ValidOne").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
            config.CheckSkipAttribute("Target", "Bar").ShouldBeFalse();
            config.CheckSkipAttribute("Target", "Nope").ShouldBeFalse();
            config.CheckSkipAttribute("Target", "Nope2").ShouldBeFalse();
        }

        [Fact]
        public void ReturnsEmptyWhenNoConfigExists()
        {
            var config = UnknownElementsConfiguration.LoadFromFile(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName));

            config.LoadedConfigFiles.Count.ShouldBe(0);
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void MergeCombinesEntriesAndDeduplicatesFiles()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            string extraConfigPath = Path.Combine(_testDir, "extra.config");
            File.WriteAllText(extraConfigPath, @"<ParseConfig><IgnoreChildren><Ignore Element=""Project"" Name=""CustomThing"" /></IgnoreChildren></ParseConfig>");

            var merged = UnknownElementsConfiguration.Merge(
                UnknownElementsConfiguration.LoadFromFile(configPath),
                UnknownElementsConfiguration.LoadFromFile(extraConfigPath));
            merged = UnknownElementsConfiguration.Merge(merged, UnknownElementsConfiguration.LoadFromFile(configPath));

            merged.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            merged.CheckSkipElement("Project", "CustomThing").ShouldBeTrue();
            merged.LoadedConfigFiles.Count.ShouldBe(2);
        }

        [Fact]
        public void LoadGlobalConfigLoadsEnvironmentVariablePaths()
        {
            string envConfigPath = Path.Combine(_testDir, "env.config");
            File.WriteAllText(envConfigPath, @"<ParseConfig><IgnoreChildren><Ignore Element=""Project"" Name=""CustomThing"" /></IgnoreChildren></ParseConfig>");

            string oldEnv = Environment.GetEnvironmentVariable(UnknownElementsConfiguration.EnvironmentVariableName);
            try
            {
                Environment.SetEnvironmentVariable(UnknownElementsConfiguration.EnvironmentVariableName, envConfigPath);
                var config = UnknownElementsConfiguration.LoadGlobalConfig();

                config.CheckSkipElement("Project", "CustomThing").ShouldBeTrue();
            }
            finally
            {
                Environment.SetEnvironmentVariable(UnknownElementsConfiguration.EnvironmentVariableName, oldEnv);
            }
        }

        [Fact]
        public void RecordsSkippedItems()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.GetSkippedSummaryMessage().ShouldBeNull();

            config.CheckSkipAttribute("Target", "Foo");
            config.CheckSkipAttribute("Target", "Foo");

            string summary = config.GetSkippedSummaryMessage();
            summary.ShouldNotBeNull();
            summary.ShouldContain("Attribute:Target:Foo");
            summary.ShouldContain("2 occurrences");
        }

        [Fact]
        public void GetLoadedConfigsMessageReturnsNullWhenEmpty()
        {
            var config = UnknownElementsConfiguration.LoadFromFile(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName));
            config.GetLoadedConfigsMessage().ShouldBeNull();
        }

        [Fact]
        public void GetLoadedConfigsMessageListsFiles()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""Foo"" /></IgnoreAttributes></ParseConfig>");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            string message = config.GetLoadedConfigsMessage();
            message.ShouldNotBeNull();
            message.ShouldContain(_testDir);
        }

        [Fact]
        public void AllowedAttributeDoesNotThrowDuringParsing()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreAttributes><Ignore Element=""Target"" Name=""CustomAttr"" /></IgnoreAttributes></ParseConfig>");

            string projectContent = @"
<Project>
  <Target Name=""Build"" CustomAttr=""hello"">
  </Target>
</Project>";

            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectContent);

            using (var projectCollection = CreateProjectCollection(UnknownElementsConfiguration.LoadFromFile(configPath)))
            {
                var project = new Project(projectFile, null, null, projectCollection);
                project.ShouldNotBeNull();
            }
        }

        [Fact]
        public void NonAllowedAttributeStillThrows()
        {
            string projectContent = @"
<Project>
  <Target Name=""Build"" BogusAttr=""hello"">
  </Target>
</Project>";

            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectContent);

            using (var projectCollection = CreateProjectCollection(UnknownElementsConfiguration.LoadFromFile(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName))))
            {
                Should.Throw<InvalidProjectFileException>(() => new Project(projectFile, null, null, projectCollection));
            }
        }

        [Fact]
        public void AllowedChildElementDoesNotThrowDuringParsing()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><IgnoreChildren><Ignore Element=""Project"" Name=""CustomElement"" /></IgnoreChildren></ParseConfig>");

            string projectContent = @"
<Project>
  <CustomElement />
  <Target Name=""Build"">
  </Target>
</Project>";

            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectContent);

            using (var projectCollection = CreateProjectCollection(UnknownElementsConfiguration.LoadFromFile(configPath)))
            {
                var project = new Project(projectFile, null, null, projectCollection);
                project.ShouldNotBeNull();
            }
        }

        [Fact]
        public void NonAllowedChildElementStillThrows()
        {
            string projectContent = @"
<Project>
  <BogusElement />
  <Target Name=""Build"">
  </Target>
</Project>";

            string projectFile = Path.Combine(_testDir, "test.proj");
            File.WriteAllText(projectFile, projectContent);

            using (var projectCollection = CreateProjectCollection(UnknownElementsConfiguration.LoadFromFile(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName))))
            {
                Should.Throw<InvalidProjectFileException>(() => new Project(projectFile, null, null, projectCollection));
            }
        }

        private static ProjectCollection CreateProjectCollection(UnknownElementsConfiguration config)
        {
            var projectCollection = new ProjectCollection();
            projectCollection.UnknownElementsConfiguration = config;
            return projectCollection;
        }
    }
}
