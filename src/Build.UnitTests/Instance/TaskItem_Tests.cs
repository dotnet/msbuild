// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
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
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance internal members
    /// </summary>
    [TestClass]
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
        [MSBuildTestMethod]
        public void Serialization()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            item.SetMetadata("a", "b");

            TranslationHelpers.GetWriteTranslator().Translate(ref item, TaskItem.FactoryForDeserialization);
            TaskItem deserializedItem = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedItem, TaskItem.FactoryForDeserialization);

            Assert.AreEqual(item.ItemSpec, deserializedItem.ItemSpec);
            Assert.AreEqual(item.MetadataCount, deserializedItem.MetadataCount);
            Assert.AreEqual(item.GetMetadata("a"), deserializedItem.GetMetadata("a"));
            Assert.AreEqual(item.GetMetadata(ItemSpecModifiers.DefiningProjectFullPath), deserializedItem.GetMetadata(ItemSpecModifiers.DefiningProjectFullPath));
        }

        /// <summary>
        /// Ensure an item is equivalent to itself.
        /// </summary>
        [MSBuildTestMethod]
        public void TestEquivalenceIdentity()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");

            Assert.IsTrue(left.Equals(left));
        }

        /// <summary>
        /// Ensure two items with the same item spec and no metadata are equivalent
        /// </summary>
        [MSBuildTestMethod]
        public void TestEquivalence()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            TaskItem right = new TaskItem("foo", "bar.proj");

            Assert.AreEqual(left, right);
            Assert.AreEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with the same custom metadata are equivalent
        /// </summary>
        [MSBuildTestMethod]
        public void TestEquivalenceWithCustomMetadata()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "b");

            Assert.AreEqual(left, right);
            Assert.AreEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with different custom metadata values are not equivalent
        /// </summary>
        [MSBuildTestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataValues()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "c");

            Assert.AreNotEqual(left, right);
            Assert.AreNotEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with different custom metadata keys are not equivalent
        /// </summary>
        [MSBuildTestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataKeys()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("b", "b");

            Assert.AreNotEqual(left, right);
            Assert.AreNotEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with different numbers of custom metadata are not equivalent
        /// </summary>
        [MSBuildTestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataCount()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");

            Assert.AreNotEqual(left, right);
            Assert.AreNotEqual(right, left);
        }

        /// <summary>
        /// Ensure two items with different numbers of custom metadata are not equivalent
        /// </summary>
        [MSBuildTestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataCount2()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "b");
            right.SetMetadata("c", "d");

            Assert.AreNotEqual(left, right);
            Assert.AreNotEqual(right, left);
        }

        /// <summary>
        /// Ensure when cloning an Item that the clone is equivalent to the parent item and that they are not the same object.
        /// </summary>
        [MSBuildTestMethod]
        public void TestDeepClone()
        {
            TaskItem parent = new TaskItem("foo", "bar.proj");
            parent.SetMetadata("a", "b");
            parent.SetMetadata("c", "d");

            TaskItem clone = parent.DeepClone();
            Assert.IsTrue(parent.Equals(clone)); // "The parent and the clone should be equal"
            Assert.IsTrue(clone.Equals(parent)); // "The parent and the clone should be equal"
            Assert.IsFalse(object.ReferenceEquals(parent, clone)); // "The parent and the child should not be the same object"
        }

        /// <summary>
        /// Validate the presentation of metadata on a TaskItem, both of direct values and those inherited from
        /// item definitions.
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
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
