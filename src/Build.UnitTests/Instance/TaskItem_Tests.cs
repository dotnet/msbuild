// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
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
            Assert.Equal(item.GetMetadata(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath), deserializedItem.GetMetadata(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath));
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

            CopyOnWritePropertyDictionary<ProjectMetadataInstance> metadata = item.MetadataCollection;
            metadata.Count.ShouldBe(2);
            metadata["a"].EvaluatedValue.ShouldBe("override");
            metadata["b"].EvaluatedValue.ShouldBe("base");

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

                CopyOnWritePropertyDictionary<ProjectMetadataInstance> directMetadata = new();
                if (metadata is not null)
                {
                    foreach ((string name, string value) in metadata)
                    {
                        directMetadata.Set(new(name, value));
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

            ProjectRootElement xml = ProjectRootElement.Create(XmlTextReader.Create(new StringReader(content)));

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
#if RUNTIME_TYPE_NETCORE || MONO
        [Fact(Skip = "FEATURE: TASKHOST")]
#else
        [Fact]
#endif
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

            ProjectRootElement xml = ProjectRootElement.Create(XmlTextReader.Create(new StringReader(content)));

            Project project = new Project(xml);
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
#if RUNTIME_TYPE_NETCORE || MONO
        [Fact(Skip = "FEATURE: TASKHOST")]
#else
        [Fact]
#endif
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

            ProjectRootElement xml = ProjectRootElement.Create(XmlTextReader.Create(new StringReader(content)));

            Project project = new Project(xml);
            MockLogger logger = new MockLogger();
            project.Build("Build", new ILogger[] { logger }).ShouldBeTrue();

            logger.AssertLogContains("i1%2ai2");
        }
    }
}
