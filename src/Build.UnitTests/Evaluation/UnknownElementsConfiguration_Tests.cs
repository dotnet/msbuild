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

        private string WriteConfig(string directory, string body)
        {
            string configPath = Path.Combine(directory, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, $"<ParseConfig>{body}</ParseConfig>");
            return configPath;
        }

        [Fact]
        public void ParsesValidConfigFile()
        {
            string configPath = WriteConfig(_testDir, @"
  <!-- a comment -->
  <AllowAttribute Element=""Target"" Name=""Foo"" />
  <AllowElement Parent=""Project"" Name=""CustomThing"" />
  <AllowAttribute Element=""PropertyGroup"" Name=""Bar"" />
");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.LoadedConfigFiles.Count.ShouldBe(1);
            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            config.CheckSkipElement("Project", "CustomThing").ShouldBeTrue();
            config.CheckSkipAttribute("PropertyGroup", "Bar").ShouldBeTrue();
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            string configPath = WriteConfig(_testDir, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("target", "foo").ShouldBeTrue();
            config.CheckSkipAttribute("TARGET", "FOO").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
        }

        [Fact]
        public void RejectsNonAllowedItems()
        {
            string configPath = WriteConfig(_testDir, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "Bar").ShouldBeFalse();
            config.CheckSkipElement("Target", "Foo").ShouldBeFalse();
            config.CheckSkipAttribute("ItemGroup", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void UnrecognizedDirectivesAreIgnoredButReported()
        {
            // Forward compatibility: a directive a newer MSBuild defines must not fail this engine,
            // but it is reported so that a typo remains diagnosable.
            string configPath = WriteConfig(_testDir, @"
  <AllowMetadata Item=""Compile"" Name=""Future"" />
  <AllowAttribute Element=""Target"" Name=""ValidOne"" />
");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "ValidOne").ShouldBeTrue();
            config.GetMalformedEntriesMessage().ShouldContain("AllowMetadata");
        }

        [Fact]
        public void DirectivesMissingRequiredAttributesAreReported()
        {
            string configPath = WriteConfig(_testDir, @"
  <AllowAttribute Element=""Target"" />
  <AllowElement Name=""Orphan"" />
  <AllowAttribute Element=""Target"" Name=""ValidOne"" />
");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "ValidOne").ShouldBeTrue();
            config.GetMalformedEntriesMessage().ShouldNotBeNull();
        }

        [Fact]
        public void MalformedXmlIsReportedRatherThanThrowing()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, "<ParseConfig><AllowAttribute Element=\"Target\" Name=\"Foo\" ></ParseConfig>");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.IsEmpty.ShouldBeTrue();
            config.GetMalformedEntriesMessage().ShouldNotBeNull();
        }

        [Fact]
        public void ReturnsEmptyWhenNoConfigExists()
        {
            var config = UnknownElementsConfiguration.LoadFromFile(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName));

            config.LoadedConfigFiles.Count.ShouldBe(0);
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void NearestConfigWinsEntirelyWithNoLayering()
        {
            // Discovery is first-found-wins, matching Directory.Build.props / .rsp. A nearer file replaces a
            // farther one outright; it does not add to it.
            string subDir = Path.Combine(_testDir, "sub");
            Directory.CreateDirectory(subDir);

            WriteConfig(_testDir, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");
            WriteConfig(subDir, @"<AllowAttribute Element=""Target"" Name=""Bar"" />");

            var config = UnknownElementsConfiguration.Resolve(subDir);

            config.CheckSkipAttribute("Target", "Bar").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
            config.LoadedConfigFiles.Count.ShouldBe(1);
        }

        [Fact]
        public void ResolveFindsConfigInAnAncestorDirectory()
        {
            string subDir = Path.Combine(_testDir, "a", "b", "c");
            Directory.CreateDirectory(subDir);

            WriteConfig(_testDir, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");

            var config = UnknownElementsConfiguration.Resolve(subDir);

            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
        }
        [Fact]
        public void IdentityIsContentBasedNotPathBased()
        {
            string dirA = Path.Combine(_testDir, "a");
            string dirB = Path.Combine(_testDir, "b");
            string dirC = Path.Combine(_testDir, "c");
            Directory.CreateDirectory(dirA);
            Directory.CreateDirectory(dirB);
            Directory.CreateDirectory(dirC);

            WriteConfig(dirA, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");
            WriteConfig(dirB, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");
            WriteConfig(dirC, @"<AllowAttribute Element=""Target"" Name=""Other"" />");

            var a = UnknownElementsConfiguration.Resolve(dirA);
            var b = UnknownElementsConfiguration.Resolve(dirB);
            var c = UnknownElementsConfiguration.Resolve(dirC);

            // Same rules from different files are interchangeable, so a cache can be shared between them.
            a.Equals(b).ShouldBeTrue();
            a.Identity.ShouldBe(b.Identity);

            // Different rules must never share a cache.
            a.Equals(c).ShouldBeFalse();
        }

        [Fact]
        public void EmptyConfigurationsShareTheCanonicalIdentity()
        {
            UnknownElementsConfiguration.Resolve(_testDir).Equals(UnknownElementsConfiguration.Empty).ShouldBeTrue();
            UnknownElementsConfiguration.Empty.IsEmpty.ShouldBeTrue();
        }

        [Fact]
        public void MalformedEntriesAreReportedRatherThanSilentlyDropped()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"<ParseConfig><AllowAttribute Element=""Target"" Name=""Foo"" /><Attribuet Element=""Target"" Name=""Typo"" /></ParseConfig>");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            config.GetMalformedEntriesMessage().ShouldNotBeNull();
            config.GetMalformedEntriesMessage().ShouldContain("Attribuet");
        }

        [Fact]
        public void RecordsSkippedItems()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            WriteConfig(_testDir, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");

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
            WriteConfig(_testDir, @"<AllowAttribute Element=""Target"" Name=""Foo"" />");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            string message = config.GetLoadedConfigsMessage();
            message.ShouldNotBeNull();
            message.ShouldContain(_testDir);
        }

        [Fact]
        public void AllowedAttributeDoesNotThrowDuringParsing()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            WriteConfig(_testDir, @"<AllowAttribute Element=""Target"" Name=""CustomAttr"" />");

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
            WriteConfig(_testDir, @"<AllowElement Parent=""Project"" Name=""CustomElement"" />");

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
