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
            File.WriteAllText(configPath, @"
# This is a comment
Attribute:Target:Foo
Element:Project:CustomThing

Attribute:PropertyGroup:Bar
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
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, "Attribute:Target:Foo\n");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("target", "foo").ShouldBeTrue();
            config.CheckSkipAttribute("TARGET", "FOO").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
        }

        [Fact]
        public void RejectsNonAllowedItems()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, "Attribute:Target:Foo\n");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "Bar").ShouldBeFalse();
            config.CheckSkipElement("Target", "Foo").ShouldBeFalse();
            config.CheckSkipAttribute("ItemGroup", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void IgnoresInvalidLines()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, @"
# Comment
InvalidType:Target:Foo
Attribute:Target
Attribute
:Target:Foo
Attribute:Target:Foo:ExtraColon
Attribute:Target:ValidOne
");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "ValidOne").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void ReturnsEmptyWhenNoConfigExists()
        {
            var config = UnknownElementsConfiguration.LoadFromFile(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName));

            config.LoadedConfigFiles.Count.ShouldBe(0);
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
        }

        [Fact]
        public void NearestConfigWinsAndChainIsMerged()
        {
            // repo root permits Foo, subdirectory permits Bar; a project in the subdirectory gets both.
            string subDir = Path.Combine(_testDir, "sub");
            Directory.CreateDirectory(subDir);

            File.WriteAllText(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName), "Attribute:Target:Foo\n");
            File.WriteAllText(Path.Combine(subDir, UnknownElementsConfiguration.ConfigFileName), "Attribute:Target:Bar\n");

            var config = UnknownElementsConfiguration.Resolve(subDir);

            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Bar").ShouldBeTrue();
            config.LoadedConfigFiles.Count.ShouldBe(2);
        }

        [Fact]
        public void RootTrueStopsTheUpwardWalk()
        {
            string subDir = Path.Combine(_testDir, "sub");
            Directory.CreateDirectory(subDir);

            File.WriteAllText(Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName), "Attribute:Target:Foo\n");
            File.WriteAllText(Path.Combine(subDir, UnknownElementsConfiguration.ConfigFileName), "root = true\nAttribute:Target:Bar\n");

            var config = UnknownElementsConfiguration.Resolve(subDir);

            config.CheckSkipAttribute("Target", "Bar").ShouldBeTrue();
            config.CheckSkipAttribute("Target", "Foo").ShouldBeFalse();
            config.LoadedConfigFiles.Count.ShouldBe(1);
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

            File.WriteAllText(Path.Combine(dirA, UnknownElementsConfiguration.ConfigFileName), "root=true\nAttribute:Target:Foo\n");
            File.WriteAllText(Path.Combine(dirB, UnknownElementsConfiguration.ConfigFileName), "root=true\nAttribute:Target:Foo\n");
            File.WriteAllText(Path.Combine(dirC, UnknownElementsConfiguration.ConfigFileName), "root=true\nAttribute:Target:Other\n");

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
            File.WriteAllText(configPath, "Attribute:Target:Foo\nAttribuet:Target:Typo\n");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            config.CheckSkipAttribute("Target", "Foo").ShouldBeTrue();
            config.GetMalformedEntriesMessage().ShouldNotBeNull();
            config.GetMalformedEntriesMessage().ShouldContain("Attribuet:Target:Typo");
        }

        [Fact]
        public void RecordsSkippedItems()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, "Attribute:Target:Foo\n");

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
            File.WriteAllText(configPath, "Attribute:Target:Foo\n");

            var config = UnknownElementsConfiguration.LoadFromFile(configPath);

            string message = config.GetLoadedConfigsMessage();
            message.ShouldNotBeNull();
            message.ShouldContain(_testDir);
        }

        [Fact]
        public void AllowedAttributeDoesNotThrowDuringParsing()
        {
            string configPath = Path.Combine(_testDir, UnknownElementsConfiguration.ConfigFileName);
            File.WriteAllText(configPath, "Attribute:Target:CustomAttr\n");

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
            File.WriteAllText(configPath, "Element:Project:CustomElement\n");

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
