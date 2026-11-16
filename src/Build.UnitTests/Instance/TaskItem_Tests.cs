// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance internal members
    /// </summary>
    public class TaskItem_Tests
    {
        internal static readonly string[] s_builtInMetadataNames =
        {
            "FullPath",
            "RootDir",
            "Filename",
            "Extension",
            "RelativeDir",
            "Directory",
            "RecursiveDir",
            "Identity",
            "ModifiedTime",
            "CreatedTime",
            "AccessedTime",
            "DefiningProjectFullPath",
            "DefiningProjectDirectory",
            "DefiningProjectName",
            "DefiningProjectExtension"
        };

        /// <summary>
        /// Test serialization
        /// </summary>
        [Fact]
        public void Serialization()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            item.SetMetadata("a", "b");

            TranslationHelpers.GetWriteTranslator().Translate(ref item, TaskItem.FactoryForDeserialization);
            TaskItem deserializedItem = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedItem, TaskItem.FactoryForDeserialization);

            Assert.Equal(item.ItemSpec, deserializedItem.ItemSpec);
            Assert.Equal(item.MetadataCount, deserializedItem.MetadataCount);
            Assert.Equal(item.GetMetadata("a"), deserializedItem.GetMetadata("a"));
            Assert.Equal(item.GetMetadata(ItemSpecModifiers.DefiningProjectFullPath), deserializedItem.GetMetadata(ItemSpecModifiers.DefiningProjectFullPath));
        }

        /// <summary>
        /// Ensure an item is equivalent to itself.
        /// </summary>
        [Fact]
        public void TestEquivalenceIdentity()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");

            Assert.True(left.Equals(left));
        }

        /// <summary>
        /// Ensure two items with the same item spec and no metadata are equivalent
        /// </summary>
        [Fact]
        public void TestEquivalence()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            TaskItem right = new TaskItem("foo", "bar.proj");

            Assert.Equal(left, right);
            Assert.Equal(right, left);
        }

        /// <summary>
        /// Ensure two items with the same custom metadata are equivalent
        /// </summary>
        [Fact]
        public void TestEquivalenceWithCustomMetadata()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "b");

            Assert.Equal(left, right);
            Assert.Equal(right, left);
        }

        /// <summary>
        /// Ensure two items with different custom metadata values are not equivalent
        /// </summary>
        [Fact]
        public void TestInequivalenceWithDifferentCustomMetadataValues()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "c");

            Assert.NotEqual(left, right);
            Assert.NotEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with different custom metadata keys are not equivalent
        /// </summary>
        [Fact]
        public void TestInequivalenceWithDifferentCustomMetadataKeys()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("b", "b");

            Assert.NotEqual(left, right);
            Assert.NotEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with different numbers of custom metadata are not equivalent
        /// </summary>
        [Fact]
        public void TestInequivalenceWithDifferentCustomMetadataCount()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");

            Assert.NotEqual(left, right);
            Assert.NotEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with different numbers of custom metadata are not equivalent
        /// </summary>
        [Fact]
        public void TestInequivalenceWithDifferentCustomMetadataCount2()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "b");
            right.SetMetadata("c", "d");

            Assert.NotEqual(left, right);
            Assert.NotEqual(right, left);
        }

        /// <summary>
        /// Ensure when cloning an Item that the clone is equivalent to the parent item and that they are not the same object.
        /// </summary>
        [Fact]
        public void TestDeepClone()
        {
            TaskItem parent = new TaskItem("foo", "bar.proj");
            parent.SetMetadata("a", "b");
            parent.SetMetadata("c", "d");

            TaskItem clone = parent.DeepClone();
            Assert.True(parent.Equals(clone)); // "The parent and the clone should be equal"
            Assert.True(clone.Equals(parent)); // "The parent and the clone should be equal"
            Assert.False(object.ReferenceEquals(parent, clone)); // "The parent and the child should not be the same object"
        }

        /// <summary>
        /// Metadata of a project item and a target output, as name=value pairs with escaped values. Null means that an
        /// item has no metadata dictionary, and an empty array means that its dictionary is empty.
        /// </summary>
        public static TheoryData<string[], string[]> TargetOutputMetadataCases => new()
        {
            { ["A=projectA", "B=projectB"], ["B=outputB", "C=outputC"] },
            { ["A=projectA", "B="], ["A=", "B=", "OriginalItemSpec=", "MSBuildSourceProjectFile=", "MSBuildSourceTargetName="] },
            { ["OriginalItemSpec=projectOriginal", "MSBuildSourceProjectFile=projectSource.proj", "MSBuildSourceTargetName=ProjectTarget"], ["C=outputC"] },
            { ["MSBuildSourceProjectFile="], ["OriginalItemSpec=outputOriginal", "MSBuildSourceProjectFile=outputSource.proj", "MSBuildSourceTargetName=OutputTarget"] },
            { ["MSBuildSourceProjectFile="], ["C=outputC"] },
            { ["Escaped=a%3Bb%25c"], ["Escaped=", "Other=x%3By"] },
            { ["foo=projectFoo", "originalitemspec=projectOriginal", "msbuildsourcetargetname="], ["FOO=", "Bar=outputBar"] },
            { ["A=projectA"], null },
            { null, null },
            { ["A=projectA"], [] },
            { [], [] },
            { null, ["C=outputC"] },
        };

        /// <summary>
        /// Adding the MSBuild task's metadata to a target output in one step gives the same metadata as adding it one
        /// metadatum at a time, as the MSBuild task does for the outputs that cannot take the one-step path.
        /// </summary>
        [Theory]
        [MemberData(nameof(TargetOutputMetadataCases))]
        public void TargetOutputMetadataMatchesAddingMetadataOneAtATime(string[] projectMetadata, string[] outputMetadata)
        {
            const string TargetName = "Target";
            TaskItem projectItem = CreateItemWithMetadata("Callee.proj", projectMetadata);
            TaskItem expected = CreateItemWithMetadata("output.dll", outputMetadata);
            TaskItem actual = expected.DeepClone();

            projectItem.CopyMetadataTo(expected);
            if (string.IsNullOrEmpty(expected.GetMetadata("MSBuildSourceProjectFile")))
            {
                expected.SetMetadata("MSBuildSourceProjectFile", projectItem.GetMetadata("FullPath"));
            }

            if (string.IsNullOrEmpty(expected.GetMetadata("MSBuildSourceTargetName")))
            {
                expected.SetMetadata("MSBuildSourceTargetName", TargetName);
            }

            TaskItem.TargetOutputMetadata targetOutputMetadata = TaskItem.TargetOutputMetadata.TryCreate(projectItem, TargetName);
            targetOutputMetadata.ShouldNotBeNull();
            actual.TryAddTargetOutputMetadata(targetOutputMetadata).ShouldBeTrue();

            actual.MetadataCollection.ShouldBe(expected.MetadataCollection, ignoreOrder: true);
            actual.DirectMetadataCount.ShouldBe(expected.DirectMetadataCount);
        }

        /// <summary>
        /// Items whose metadata cannot be updated in one step are left to take the MSBuild task's metadata one at a time.
        /// </summary>
        [Fact]
        public void TargetOutputMetadataIsDeclinedWhenItemsNeedMetadataAddedOneAtATime()
        {
            ProjectItemDefinition projectItemDefinition = new(new Project(), "MyItem");
            projectItemDefinition.SetMetadataValue("A", "definition");
            List<ProjectItemDefinitionInstance> itemDefinitions = [new ProjectItemDefinitionInstance(projectItemDefinition)];
            ImmutableDictionary<string, string> ordinalIgnoreCaseMetadata =
                ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase).Add("A", "a");
            string projectDirectory = Path.GetTempPath();

            TaskItem.TargetOutputMetadata.TryCreate(
                new TaskItem("Callee.proj", "Callee.proj", null, itemDefinitions, projectDirectory, immutable: false, "caller.proj"), "Target").ShouldBeNull();
            TaskItem.TargetOutputMetadata.TryCreate(
                new TaskItem("Callee.proj", "Callee.proj", ordinalIgnoreCaseMetadata, null, projectDirectory, immutable: false, "caller.proj"), "Target").ShouldBeNull();
            TaskItem.TargetOutputMetadata.TryCreate(CreateItemWithMetadata("Callee.proj", ["RecursiveDir=sub"]), "Target").ShouldBeNull();

            TaskItem.TargetOutputMetadata targetOutputMetadata = TaskItem.TargetOutputMetadata.TryCreate(CreateItemWithMetadata("Callee.proj", ["A=a"]), "Target");
            new TaskItem("output.dll", "output.dll", null, itemDefinitions, projectDirectory, immutable: false, "callee.proj")
                .TryAddTargetOutputMetadata(targetOutputMetadata).ShouldBeFalse();
            new TaskItem("output.dll", "output.dll", ordinalIgnoreCaseMetadata, null, projectDirectory, immutable: false, "callee.proj")
                .TryAddTargetOutputMetadata(targetOutputMetadata).ShouldBeFalse();
            new TaskItem("output.dll", "output.dll", null, null, projectDirectory, immutable: true, "callee.proj")
                .TryAddTargetOutputMetadata(targetOutputMetadata).ShouldBeFalse();
        }

        private static TaskItem CreateItemWithMetadata(string itemSpec, string[] metadata)
        {
            TaskItem item = new(itemSpec, itemSpec, null, null, Path.GetTempPath(), immutable: false, "test.proj");

            if (metadata is null)
            {
                return item;
            }

            if (metadata.Length == 0)
            {
                // Leave an empty, rather than missing, metadata dictionary.
                item.SetMetadata("Removed", "value");
                item.RemoveMetadata("Removed");
                return item;
            }

            foreach (string metadatum in metadata)
            {
                int separator = metadatum.IndexOf('=');
                item.SetMetadata(metadatum.Substring(0, separator), metadatum.Substring(separator + 1));
            }

            return item;
        }

        /// <summary>
        /// Validate the presentation of metadata on a TaskItem, both of direct values and those inherited from
        /// item definitions.
        /// </summary>
        [Fact]
        public void Metadata()
        {
            TaskItem item = BuildItem(
                definitions: new[] { ("a", "base"), ("b", "base") },
                metadata: new[] { ("a", "override") });

            item.MetadataNames.Cast<string>().ShouldBeSetEquivalentTo(new[] { "a", "b" }.Concat(s_builtInMetadataNames));
            item.MetadataCount.ShouldBe(s_builtInMetadataNames.Length + 2);
            item.DirectMetadataCount.ShouldBe(1);
            item.HasCustomMetadata.ShouldBeTrue();

            ImmutableDictionary<string, string> metadata = item.MetadataCollection;
            metadata.Count.ShouldBe(2);
            metadata["a"].ShouldBe("override");
            metadata["b"].ShouldBe("base");

            item.EnumerateMetadata().ShouldBeSetEquivalentTo(new KeyValuePair<string, string>[] { new("a", "override"), new("b", "base") });

            ((Dictionary<string, string>)item.CloneCustomMetadata()).ShouldBeSetEquivalentTo(new KeyValuePair<string, string>[] { new("a", "override"), new("b", "base") });

            static TaskItem BuildItem(
                IEnumerable<(string Name, string Value)> definitions = null,
                IEnumerable<(string Name, string Value)> metadata = null)
            {
                List<ProjectItemDefinitionInstance> itemDefinitions = new();
                if (definitions is not null)
                {
                    Project project = new();

                    foreach ((string name, string value) in definitions)
                    {
                        ProjectItemDefinition projectItemDefinition = new ProjectItemDefinition(project, "MyItem");
                        projectItemDefinition.SetMetadataValue(name, value);
                        ProjectItemDefinitionInstance itemDefinition = new(projectItemDefinition);
                        itemDefinitions.Add(itemDefinition);
                    }
                }

                ImmutableDictionary<string, string> directMetadata = ImmutableDictionaryExtensions.EmptyMetadata;
                if (metadata is not null)
                {
                    foreach ((string name, string value) in metadata)
                    {
                        directMetadata = directMetadata.SetItem(name, value);
                    }
                }

                return new TaskItem("foo", "foo", directMetadata, itemDefinitions, "dir", immutable: false, "bar.proj");
            }
        }

        /// <summary>
        /// Flushing an item through a task should not mess up special characters on the metadata.
        /// </summary>
        [Fact]
        public void Escaping1()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
           <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='i1'>
                      <m>i1m1;i1m2</m>
                    </i>
                    <j Include='j1'>
                      <m>j1m1;j1m2</m>
                    </j>
                  </ItemGroup>

                  <Target Name='Build'>
                    <CallTarget Targets='%(i.m)'/>
                    <CreateItem Include='@(j)'>
                      <Output TaskParameter='Include' ItemName='j2'/>
                    </CreateItem>
                    <CallTarget Targets='%(j2.m)'/>
                  </Target>

                  <Target Name='i1m1'>
                    <Warning Text='[i1m1]'/>
                  </Target>
                  <Target Name='i1m2'>
                    <Warning Text='[i1m2]'/>
                  </Target>
                  <Target Name='j1m1'>
                    <Warning Text='[j1m1]'/>
                  </Target>
                  <Target Name='j1m2'>
                    <Warning Text='[j1m2]'/>
                  </Target>
                </Project>
                ");

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement xml = projectRootElementFromString.Project;

            Project project = new Project(xml);
            MockLogger logger = new MockLogger();
            project.Build("Build", new ILogger[] { logger }).ShouldBeTrue();

            logger.AssertLogContains("[i1m1]");
            logger.AssertLogContains("[i1m2]");
            logger.AssertLogContains("[j1m1]");
            logger.AssertLogContains("[j1m2]");
        }

        /// <summary>
        /// Flushing an item through a task run in the task host also should not mess up special characters on the metadata.
        /// </summary>
        [Fact]
        public void Escaping2()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
           <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <UsingTask TaskName='CreateItem' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' TaskFactory='TaskHostFactory' />
                  <ItemGroup>
                    <i Include='i1'>
                      <m>i1m1;i1m2</m>
                    </i>
                    <j Include='j1'>
                      <m>j1m1;j1m2</m>
                    </j>
                  </ItemGroup>

                  <Target Name='Build'>
                    <CallTarget Targets='%(i.m)'/>
                    <CreateItem Include='@(j)'>
                      <Output TaskParameter='Include' ItemName='j2'/>
                    </CreateItem>
                    <CallTarget Targets='%(j2.m)'/>
                  </Target>

                  <Target Name='i1m1'>
                    <Warning Text='[i1m1]'/>
                  </Target>
                  <Target Name='i1m2'>
                    <Warning Text='[i1m2]'/>
                  </Target>
                  <Target Name='j1m1'>
                    <Warning Text='[j1m1]'/>
                  </Target>
                  <Target Name='j1m2'>
                    <Warning Text='[j1m2]'/>
                  </Target>
                </Project>
                ");

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement xml = projectRootElementFromString.Project;

            Project project = new Project(xml);
            project.FullPath = "test.proj";
            MockLogger logger = new MockLogger();
            project.Build("Build", new ILogger[] { logger }).ShouldBeTrue();

            logger.AssertLogContains("[i1m1]");
            logger.AssertLogContains("[i1m2]");
            logger.AssertLogContains("[j1m1]");
            logger.AssertLogContains("[j1m2]");
        }

        /// <summary>
        /// Flushing an item through a task run in the task host also should not mess up the escaping of the itemspec either.
        /// </summary>
        [Fact]
        public void Escaping3()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
           <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <UsingTask TaskName='AssignCulture' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' TaskFactory='TaskHostFactory' />
                  <ItemGroup>
                    <i Include='i1%252ai2' />
                  </ItemGroup>

                  <Target Name='Build'>
                    <AssignCulture Files='@(i)'>
                      <Output TaskParameter='AssignedFiles' ItemName='i1'/>
                    </AssignCulture>
                    <Message Text='@(i1)'/>
                  </Target>
                </Project>
                ");

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement xml = projectRootElementFromString.Project;

            Project project = new Project(xml);
            MockLogger logger = new MockLogger();
            project.Build("Build", new ILogger[] { logger }).ShouldBeTrue();

            logger.AssertLogContains("i1%2ai2");
        }
    }
}
