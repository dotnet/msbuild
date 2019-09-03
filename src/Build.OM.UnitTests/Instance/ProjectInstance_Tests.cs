// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

using Microsoft.Build.Framework;
using ForwardingLoggerRecord = Microsoft.Build.Logging.ForwardingLoggerRecord;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectInstance public members
    /// </summary>
    public class ProjectInstance_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public ProjectInstance_Tests(ITestOutputHelper output)
        {
            _testOutput = output;
        }

        /// <summary>
        /// Verify that a cloned off project instance can see environment variables
        /// </summary>
        [Fact]
        public void CreateProjectInstancePassesEnvironment()
        {
            Project p = new Project();
            ProjectInstance i = p.CreateProjectInstance();

            Assert.True(i.GetPropertyValue("username") != null);
        }

        /// <summary>
        /// Read off properties
        /// </summary>
        [Fact]
        public void PropertiesAccessors()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Assert.Equal("v1", p.GetPropertyValue("p1"));
            Assert.Equal("v2X", p.GetPropertyValue("p2"));
        }

        /// <summary>
        /// Read off items
        /// </summary>
        [Fact]
        public void ItemsAccessors()
        {
            ProjectInstance p = GetSampleProjectInstance();

            IList<ProjectItemInstance> items = Helpers.MakeList(p.GetItems("i"));
            Assert.Equal(3, items.Count);
            Assert.Equal("i", items[0].ItemType);
            Assert.Equal("i0", items[0].EvaluatedInclude);
            Assert.Equal(String.Empty, items[0].GetMetadataValue("m"));
            Assert.Null(items[0].GetMetadata("m"));
            Assert.Equal("i1", items[1].EvaluatedInclude);
            Assert.Equal("m1", items[1].GetMetadataValue("m"));
            Assert.Equal("m1", items[1].GetMetadata("m").EvaluatedValue);
            Assert.Equal("v1", items[2].EvaluatedInclude);
        }

        /// <summary>
        /// Add item
        /// </summary>
        [Fact]
        public void AddItemWithoutMetadata()
        {
            ProjectInstance p = GetEmptyProjectInstance();

            ProjectItemInstance returned = p.AddItem("i", "i1");

            Assert.Equal("i", returned.ItemType);
            Assert.Equal("i1", returned.EvaluatedInclude);
            Assert.False(returned.Metadata.GetEnumerator().MoveNext());

            foreach (ProjectItemInstance item in p.Items)
            {
                Assert.Equal("i1", item.EvaluatedInclude);
                Assert.False(item.Metadata.GetEnumerator().MoveNext());
            }
        }

        /// <summary>
        /// Add item
        /// </summary>
        [Fact]
        public void AddItemWithoutMetadata_Escaped()
        {
            ProjectInstance p = GetEmptyProjectInstance();

            ProjectItemInstance returned = p.AddItem("i", "i%3b1");

            Assert.Equal("i", returned.ItemType);
            Assert.Equal("i;1", returned.EvaluatedInclude);
            Assert.False(returned.Metadata.GetEnumerator().MoveNext());

            foreach (ProjectItemInstance item in p.Items)
            {
                Assert.Equal("i;1", item.EvaluatedInclude);
                Assert.False(item.Metadata.GetEnumerator().MoveNext());
            }
        }

        /// <summary>
        /// Add item with metadata
        /// </summary>
        [Fact]
        public void AddItemWithMetadata()
        {
            ProjectInstance p = GetEmptyProjectInstance();

            var metadata = new List<KeyValuePair<string, string>>();
            metadata.Add(new KeyValuePair<string, string>("m", "m1"));
            metadata.Add(new KeyValuePair<string, string>("n", "n1"));
            metadata.Add(new KeyValuePair<string, string>("o", "o%40"));

            ProjectItemInstance returned = p.AddItem("i", "i1", metadata);

            Assert.True(object.ReferenceEquals(returned, Helpers.MakeList(p.GetItems("i"))[0]));

            foreach (ProjectItemInstance item in p.Items)
            {
                Assert.Same(returned, item);
                Assert.Equal("i1", item.EvaluatedInclude);
                var metadataOut = Helpers.MakeList(item.Metadata);
                Assert.Equal(3, metadataOut.Count);
                Assert.Equal("m1", item.GetMetadataValue("m"));
                Assert.Equal("n1", item.GetMetadataValue("n"));
                Assert.Equal("o@", item.GetMetadataValue("o"));
            }
        }

        /// <summary>
        /// Add item null item type
        /// </summary>
        [Fact]
        public void AddItemInvalidNullItemType()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectInstance p = GetEmptyProjectInstance();
                p.AddItem(null, "i1");
            }
           );
        }
        /// <summary>
        /// Add item empty item type
        /// </summary>
        [Fact]
        public void AddItemInvalidEmptyItemType()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectInstance p = GetEmptyProjectInstance();
                p.AddItem(String.Empty, "i1");
            }
           );
        }
        /// <summary>
        /// Add item null include
        /// </summary>
        [Fact]
        public void AddItemInvalidNullInclude()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectInstance p = GetEmptyProjectInstance();
                p.AddItem("i", null);
            }
           );
        }
        /// <summary>
        /// Add item null metadata
        /// </summary>
        [Fact]
        public void AddItemNullMetadata()
        {
            ProjectInstance p = GetEmptyProjectInstance();
            ProjectItemInstance item = p.AddItem("i", "i1", null);

            Assert.False(item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// It's okay to set properties that are also global properties, masking their value
        /// </summary>
        [Fact]
        public void SetGlobalPropertyOnInstance()
        {
            Dictionary<string, string> globals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "p", "p1" } };
            Project p = new Project(ProjectRootElement.Create(), globals, null);
            ProjectInstance instance = p.CreateProjectInstance();

            instance.SetProperty("p", "p2");

            Assert.Equal("p2", instance.GetPropertyValue("p"));

            // And clearing it should not expose the original global property value
            instance.SetProperty("p", "");

            Assert.Equal("", instance.GetPropertyValue("p"));
        }

        /// <summary>
        /// ProjectInstance itself is cloned properly
        /// </summary>
        [Fact]
        public void CloneProjectItself()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Assert.False(Object.ReferenceEquals(first, second));
        }

        /// <summary>
        /// Properties are cloned properly
        /// </summary>
        [Fact]
        public void CloneProperties()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Assert.False(Object.ReferenceEquals(first.GetProperty("p1"), second.GetProperty("p1")));

            ProjectPropertyInstance newProperty = first.SetProperty("p1", "v1b");
            Assert.True(Object.ReferenceEquals(newProperty, first.GetProperty("p1")));
            Assert.Equal("v1b", first.GetPropertyValue("p1"));
            Assert.Equal("v1", second.GetPropertyValue("p1"));
        }

        /// <summary>
        /// Passing an item list into another list should copy the metadata too
        /// </summary>
        [Fact]
        public void ItemEvaluationCopiesMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m>m1</m>
                                <n>n%3b%3b</n>
                            </i>
                            <j Include='@(i)'/>
                        </ItemGroup>
                    </Project>";

            ProjectInstance project = GetProjectInstance(content);

            Assert.Single(Helpers.MakeList(project.GetItems("j")));
            Assert.Equal("i1", Helpers.MakeList(project.GetItems("j"))[0].EvaluatedInclude);
            Assert.Equal("m1", Helpers.MakeList(project.GetItems("j"))[0].GetMetadataValue("m"));
            Assert.Equal("n;;", Helpers.MakeList(project.GetItems("j"))[0].GetMetadataValue("n"));
        }

        /// <summary>
        /// Wildcards are expanded in item groups inside targets, and the evaluatedinclude
        /// is not the wildcard itself!
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void WildcardsInsideTargets()
        {
            string directory = null;
            string file1 = null;
            string file2 = null;
            string file3 = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), "WildcardsInsideTargets");
                Directory.CreateDirectory(directory);
                file1 = Path.Combine(directory, "a.exe");
                file2 = Path.Combine(directory, "b.exe");
                file3 = Path.Combine(directory, "c.bat");
                File.WriteAllText(file1, String.Empty);
                File.WriteAllText(file2, String.Empty);
                File.WriteAllText(file3, String.Empty);

                string path = Path.Combine(directory, "*.exe");

                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                          <ItemGroup>
                            <i Include='" + path + @"'/>
                          </ItemGroup>
                        </Target>
                    </Project>";

                ProjectInstance projectInstance = GetProjectInstance(content);
                projectInstance.Build();

                Assert.Equal(2, Helpers.MakeList(projectInstance.GetItems("i")).Count);
                Assert.Equal(file1, Helpers.MakeList(projectInstance.GetItems("i"))[0].EvaluatedInclude);
                Assert.Equal(file2, Helpers.MakeList(projectInstance.GetItems("i"))[1].EvaluatedInclude);
            }
            finally
            {
                File.Delete(file1);
                File.Delete(file2);
                File.Delete(file3);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Items are cloned properly
        /// </summary>
        [Fact]
        public void CloneItems()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Assert.False(Object.ReferenceEquals(Helpers.MakeList(first.GetItems("i"))[0], Helpers.MakeList(second.GetItems("i"))[0]));

            first.AddItem("i", "i3");
            Assert.Equal(4, Helpers.MakeList(first.GetItems("i")).Count);
            Assert.Equal(3, Helpers.MakeList(second.GetItems("i")).Count);
        }

        /// <summary>
        /// Null target in array should give ArgumentNullException
        /// </summary>
        [Fact]
        public void BuildNullTargetInArray()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectInstance instance = new ProjectInstance(ProjectRootElement.Create());
                instance.Build(new string[] { null }, null);
            }
           );
        }
        /// <summary>
        /// Null logger in array should give ArgumentNullException
        /// </summary>
        [Fact]
        public void BuildNullLoggerInArray()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectInstance instance = new ProjectInstance(ProjectRootElement.Create());
                instance.Build("t", new ILogger[] { null });
            }
           );
        }
        /// <summary>
        /// Null remote logger in array should give ArgumentNullException
        /// </summary>
        [Fact]
        public void BuildNullRemoteLoggerInArray()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectInstance instance = new ProjectInstance(ProjectRootElement.Create());
                instance.Build("t", null, new ForwardingLoggerRecord[] { null });
            }
           );
        }
        /// <summary>
        /// Null target name should imply the default target
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void BuildNullTargetNameIsDefaultTarget()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddTarget("t").AddTask("Message").SetParameter("Text", "[OK]");
            ProjectInstance instance = new ProjectInstance(xml);
            MockLogger logger = new MockLogger();
            string target = null;
            instance.Build(target, new ILogger[] { logger });
            logger.AssertLogContains("[OK]");
        }

        /// <summary>
        /// Build system should correctly reset itself between builds of
        /// project instances.
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void BuildProjectInstancesConsecutively()
        {
            ProjectInstance instance1 = new Project().CreateProjectInstance();

            BuildRequestData buildRequestData1 = new BuildRequestData(instance1, new string[] { });

            BuildManager.DefaultBuildManager.Build(new BuildParameters(), buildRequestData1);

            new Project().CreateProjectInstance();

            BuildRequestData buildRequestData2 = new BuildRequestData(instance1, new string[] { });

            BuildManager.DefaultBuildManager.Build(new BuildParameters(), buildRequestData2);
        }

        /// <summary>
        /// Verifies that the built-in metadata for specialized ProjectInstances is present when items are the simplest (no macros or wildcards).
        /// </summary>
        [Fact]
        public void CreateProjectInstanceWithItemsContainingProjects()
        {
            const string CapturedMetadataName = "DefiningProjectFullPath";
            var pc = new ProjectCollection();
            var projA = ProjectRootElement.Create(pc);
            var projB = ProjectRootElement.Create(pc);
            projA.FullPath = Path.Combine(Path.GetTempPath(), "a.proj");
            projB.FullPath = Path.Combine(Path.GetTempPath(), "b.proj");
            projB.AddImport("a.proj");
            projA.AddItem("Compile", "aItem.cs");
            projB.AddItem("Compile", "bItem.cs");

            var projBEval = new Project(projB, null, null, pc);
            var projBInstance = projBEval.CreateProjectInstance();
            var projBInstanceItem = projBInstance.GetItemsByItemTypeAndEvaluatedInclude("Compile", "bItem.cs").Single();
            var projAInstanceItem = projBInstance.GetItemsByItemTypeAndEvaluatedInclude("Compile", "aItem.cs").Single();
            Assert.Equal(projB.FullPath, projBInstanceItem.GetMetadataValue(CapturedMetadataName));
            Assert.Equal(projA.FullPath, projAInstanceItem.GetMetadataValue(CapturedMetadataName));

            // Although GetMetadataValue returns non-null, GetMetadata returns null...
            Assert.Null(projAInstanceItem.GetMetadata(CapturedMetadataName));

            // .. Just like built-in metadata does: (this segment just demonstrates similar functionality -- it's not meant to test built-in metadata)
            Assert.NotNull(projAInstanceItem.GetMetadataValue("Identity"));
            Assert.Null(projAInstanceItem.GetMetadata("Identity"));

            Assert.True(projAInstanceItem.HasMetadata(CapturedMetadataName));
            Assert.False(projAInstanceItem.Metadata.Any());
            Assert.Contains(CapturedMetadataName, projAInstanceItem.MetadataNames);
            Assert.Equal(projAInstanceItem.MetadataCount, projAInstanceItem.MetadataNames.Count);
        }

        /// <summary>
        /// Verifies that the built-in metadata for specialized ProjectInstances is present when items are based on wildcards in the construction model.
        /// </summary>
        [Fact]
        public void DefiningProjectItemBuiltInMetadataFromWildcards()
        {
            const string CapturedMetadataName = "DefiningProjectFullPath";
            var pc = new ProjectCollection();
            var projA = ProjectRootElement.Create(pc);
            var projB = ProjectRootElement.Create(pc);

            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            File.Create(Path.Combine(tempDir, "aItem.cs")).Dispose();

            projA.FullPath = Path.Combine(tempDir, "a.proj");
            projB.FullPath = Path.Combine(tempDir, "b.proj");
            projB.AddImport("a.proj");
            projA.AddItem("Compile", "*.cs");
            projB.AddItem("CompileB", "@(Compile)");

            var projBEval = new Project(projB, null, null, pc);
            var projBInstance = projBEval.CreateProjectInstance();
            var projAInstanceItem = projBInstance.GetItemsByItemTypeAndEvaluatedInclude("Compile", "aItem.cs").Single();
            var projBInstanceItem = projBInstance.GetItemsByItemTypeAndEvaluatedInclude("CompileB", "aItem.cs").Single();
            Assert.Equal(projA.FullPath, projAInstanceItem.GetMetadataValue(CapturedMetadataName));
            Assert.Equal(projB.FullPath, projBInstanceItem.GetMetadataValue(CapturedMetadataName));

            Assert.True(projAInstanceItem.HasMetadata(CapturedMetadataName));
            Assert.False(projAInstanceItem.Metadata.Any());
            Assert.Contains(CapturedMetadataName, projAInstanceItem.MetadataNames);
            Assert.Equal(projAInstanceItem.MetadataCount, projAInstanceItem.MetadataNames.Count);
        }

        /// <summary>
        /// Validate that the DefiningProject* metadata is set to the correct project based on a variety 
        /// of means of item creation. 
        /// </summary>
        [Fact]
        public void TestDefiningProjectMetadata()
        {
            string projectA = Path.Combine(ObjectModelHelpers.TempProjectDir, "a.proj");
            string projectB = Path.Combine(ObjectModelHelpers.TempProjectDir, "b.proj");

            string includeFileA = Path.Combine(ObjectModelHelpers.TempProjectDir, "aaa4.cs");
            string includeFileB = Path.Combine(ObjectModelHelpers.TempProjectDir, "bbb4.cs");

            string contentsA =
                @"<?xml version=`1.0` encoding=`utf-8`?>
<Project ToolsVersion=`msbuilddefaulttoolsversion` DefaultTargets=`Validate` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <A Include=`aaaa.cs` />
    <A2 Include=`aaa2.cs` />
    <A2 Include=`aaa3.cs`>
      <Foo>Bar</Foo>
    </A2>
  </ItemGroup>

  <Import Project=`b.proj` />

  <ItemGroup>
    <E Include=`@(C)` />
    <F Include=`@(C);@(C2)` />
    <G Include=`@(C->'%(Filename)')` />
    <H Include=`@(C2->WithMetadataValue('Foo', 'Bar'))` />
    <U Include=`*4.cs` />
  </ItemGroup>

  <Target Name=`AddFromMainProject`>
    <ItemGroup>
      <B Include=`bbbb.cs` />
      <I Include=`@(C)` />
      <J Include=`@(C);@(C2)` />
      <K Include=`@(C->'%(Filename)')` />
      <L Include=`@(C2->WithMetadataValue('Foo', 'Bar'))` />
      <V Include=`*4.cs` />
    </ItemGroup>
  </Target>

  <Target Name=`Validate` DependsOnTargets=`AddFromMainProject;AddFromImport`>
    <Warning Text=`A is wrong: EXPECTED: [a] ACTUAL: [%(A.DefiningProjectName)]` Condition=`'%(A.DefiningProjectName)' != 'a'` />    
    <Warning Text=`B is wrong: EXPECTED: [a] ACTUAL: [%(B.DefiningProjectName)]` Condition=`'%(B.DefiningProjectName)' != 'a'` />
    <Warning Text=`C is wrong: EXPECTED: [b] ACTUAL: [%(C.DefiningProjectName)]` Condition=`'%(C.DefiningProjectName)' != 'b'` />
    <Warning Text=`D is wrong: EXPECTED: [b] ACTUAL: [%(D.DefiningProjectName)]` Condition=`'%(D.DefiningProjectName)' != 'b'` />
    <Warning Text=`E is wrong: EXPECTED: [a] ACTUAL: [%(E.DefiningProjectName)]` Condition=`'%(E.DefiningProjectName)' != 'a'` />
    <Warning Text=`F is wrong: EXPECTED: [a] ACTUAL: [%(F.DefiningProjectName)]` Condition=`'%(F.DefiningProjectName)' != 'a'` />
    <Warning Text=`G is wrong: EXPECTED: [a] ACTUAL: [%(G.DefiningProjectName)]` Condition=`'%(G.DefiningProjectName)' != 'a'` />
    <Warning Text=`H is wrong: EXPECTED: [a] ACTUAL: [%(H.DefiningProjectName)]` Condition=`'%(H.DefiningProjectName)' != 'a'` />
    <Warning Text=`I is wrong: EXPECTED: [a] ACTUAL: [%(I.DefiningProjectName)]` Condition=`'%(I.DefiningProjectName)' != 'a'` />
    <Warning Text=`J is wrong: EXPECTED: [a] ACTUAL: [%(J.DefiningProjectName)]` Condition=`'%(J.DefiningProjectName)' != 'a'` />
    <Warning Text=`K is wrong: EXPECTED: [a] ACTUAL: [%(K.DefiningProjectName)]` Condition=`'%(K.DefiningProjectName)' != 'a'` />
    <Warning Text=`L is wrong: EXPECTED: [a] ACTUAL: [%(L.DefiningProjectName)]` Condition=`'%(L.DefiningProjectName)' != 'a'` />
    <Warning Text=`M is wrong: EXPECTED: [b] ACTUAL: [%(M.DefiningProjectName)]` Condition=`'%(M.DefiningProjectName)' != 'b'` />
    <Warning Text=`N is wrong: EXPECTED: [b] ACTUAL: [%(N.DefiningProjectName)]` Condition=`'%(N.DefiningProjectName)' != 'b'` />
    <Warning Text=`O is wrong: EXPECTED: [b] ACTUAL: [%(O.DefiningProjectName)]` Condition=`'%(O.DefiningProjectName)' != 'b'` />
    <Warning Text=`P is wrong: EXPECTED: [b] ACTUAL: [%(P.DefiningProjectName)]` Condition=`'%(P.DefiningProjectName)' != 'b'` />
    <Warning Text=`Q is wrong: EXPECTED: [b] ACTUAL: [%(Q.DefiningProjectName)]` Condition=`'%(Q.DefiningProjectName)' != 'b'` />
    <Warning Text=`R is wrong: EXPECTED: [b] ACTUAL: [%(R.DefiningProjectName)]` Condition=`'%(R.DefiningProjectName)' != 'b'` />
    <Warning Text=`S is wrong: EXPECTED: [b] ACTUAL: [%(S.DefiningProjectName)]` Condition=`'%(S.DefiningProjectName)' != 'b'` />
    <Warning Text=`T is wrong: EXPECTED: [b] ACTUAL: [%(T.DefiningProjectName)]` Condition=`'%(T.DefiningProjectName)' != 'b'` />
    <Warning Text=`U is wrong: EXPECTED: [a] ACTUAL: [%(U.DefiningProjectName)]` Condition=`'%(U.DefiningProjectName)' != 'a'` />
    <Warning Text=`V is wrong: EXPECTED: [a] ACTUAL: [%(V.DefiningProjectName)]` Condition=`'%(V.DefiningProjectName)' != 'a'` />
    <Warning Text=`W is wrong: EXPECTED: [b] ACTUAL: [%(W.DefiningProjectName)]` Condition=`'%(W.DefiningProjectName)' != 'b'` />
    <Warning Text=`X is wrong: EXPECTED: [b] ACTUAL: [%(X.DefiningProjectName)]` Condition=`'%(X.DefiningProjectName)' != 'b'` />
  </Target>

</Project>";

            string contentsB =
                @"<?xml version=`1.0` encoding=`utf-8`?>
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <C Include=`cccc.cs` />
    <C2 Include=`ccc2.cs` />
    <C2 Include=`ccc3.cs`>
      <Foo>Bar</Foo>
    </C2>
    <M Include=`@(A)` />
    <N Include=`@(A);@(A2)` />
    <O Include=`@(A->'%(Filename)')` />
    <P Include=`@(A2->WithMetadataValue('Foo', 'Bar'))` />
    <W Include=`*4.cs` />
  </ItemGroup>


  <Target Name=`AddFromImport`>
    <ItemGroup>
      <D Include=`dddd.cs` />
      <Q Include=`@(A)` />
      <R Include=`@(A);@(A2)` />
      <S Include=`@(A->'%(Filename)')` />
      <T Include=`@(A2->WithMetadataValue('Foo', 'Bar'))` />
      <X Include=`*4.cs` />
    </ItemGroup>
  </Target>
</Project>";

            try
            {
                File.WriteAllText(projectA, ObjectModelHelpers.CleanupFileContents(contentsA));
                File.WriteAllText(projectB, ObjectModelHelpers.CleanupFileContents(contentsB));

                File.WriteAllText(includeFileA, "aaaaaaa");
                File.WriteAllText(includeFileB, "bbbbbbb");

                MockLogger logger = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectSuccess("a.proj", logger);
                logger.AssertNoWarnings();
            }
            finally
            {
                if (File.Exists(projectA))
                {
                    File.Delete(projectA);
                }

                if (File.Exists(projectB))
                {
                    File.Delete(projectB);
                }

                if (File.Exists(includeFileA))
                {
                    File.Delete(includeFileA);
                }

                if (File.Exists(includeFileB))
                {
                    File.Delete(includeFileB);
                }
            }
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetProperty()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.SetProperty("a", "b"); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_RemoveProperty()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.RemoveProperty("p1"); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_RemoveItem()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.RemoveItem(Helpers.GetFirst(instance.Items)); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_AddItem()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.AddItem("a", "b"); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_AddItemWithMetadata()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.AddItem("a", "b", new List<KeyValuePair<string, string>>()); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_Build()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.Build(); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetEvaluatedInclude()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Items).EvaluatedInclude = "x"; });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetEvaluatedIncludeEscaped()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { ((ITaskItem2)Helpers.GetFirst(instance.Items)).EvaluatedIncludeEscaped = "x"; });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetItemSpec()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { ((ITaskItem2)Helpers.GetFirst(instance.Items)).ItemSpec = "x"; });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetMetadataOnItem1()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { ((ITaskItem2)Helpers.GetFirst(instance.Items)).SetMetadataValueLiteral("a", "b"); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetMetadataOnItem2()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Items).SetMetadata(new List<KeyValuePair<string, string>>()); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetMetadataOnItem3()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Items).SetMetadata("a", "b"); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_RemoveMetadataFromItem()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Items).RemoveMetadata("n"); });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetEvaluatedValueOnProperty()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Properties).EvaluatedValue = "v2"; });
        }

        /// <summary>
        /// Test operation fails on immutable project instance
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetEvaluatedValueOnPropertyFromProject()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.GetProperty("p1").EvaluatedValue = "v2"; });
        }

        /// <summary>
        /// Test operation fails on immutable project instance 
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetNewProperty()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.SetProperty("newproperty", "v2"); });
        }

        /// <summary>
        /// Setting global properties should fail if the project is immutable, even though the property
        /// was originally created as mutable
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetGlobalProperty()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.SetProperty("g", "gv2"); });
        }

        /// <summary>
        /// Setting environment originating properties should fail if the project is immutable, even though the property
        /// was originally created as mutable
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_SetEnvironmentProperty()
        {
            var instance = GetSampleProjectInstance(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.SetProperty("username", "someone_else_here"); });
        }

        /// <summary>
        /// Cloning inherits unless otherwise specified
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_CloneMutableFromImmutable()
        {
            var protoInstance = GetSampleProjectInstance(true /* immutable */);
            var instance = protoInstance.DeepCopy(false /* mutable */);

            // These should not throw
            instance.SetProperty("p", "pnew");
            instance.AddItem("i", "ii");
            Helpers.GetFirst(instance.Items).EvaluatedInclude = "new";
            instance.SetProperty("g", "gnew");
            instance.SetProperty("username", "someone_else_here");
        }

        /// <summary>
        /// Cloning inherits unless otherwise specified
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ImmutableProjectInstance_CloneImmutableFromMutable()
        {
            var protoInstance = GetSampleProjectInstance(false /* mutable */);
            var instance = protoInstance.DeepCopy(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.GetProperty("g").EvaluatedValue = "v2"; });
            Helpers.VerifyAssertThrowsInvalidOperation(
                delegate
                {
                    instance.GetProperty(NativeMethodsShared.IsWindows ? "username" : "USER").EvaluatedValue =
                        "someone_else_here";
                });
            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Properties).EvaluatedValue = "v2"; });
            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Items).EvaluatedInclude = "new"; });
        }

        /// <summary>
        /// Cloning inherits unless otherwise specified
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ImmutableProjectInstance_CloneImmutableFromImmutable()
        {
            var protoInstance = GetSampleProjectInstance(true /* immutable */);
            var instance = protoInstance.DeepCopy(/* inherit */);

            // Should not have bothered cloning
            Assert.True(Object.ReferenceEquals(protoInstance, instance));

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.GetProperty("g").EvaluatedValue = "v2"; });
            Helpers.VerifyAssertThrowsInvalidOperation(
                delegate
                {
                    instance.GetProperty(NativeMethodsShared.IsWindows ? "username" : "USER").EvaluatedValue =
                        "someone_else_here";
                });
            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Properties).EvaluatedValue = "v2"; });
            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Items).EvaluatedInclude = "new"; });
        }

        /// <summary>
        /// Cloning inherits unless otherwise specified
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ImmutableProjectInstance_CloneImmutableFromImmutable2()
        {
            var protoInstance = GetSampleProjectInstance(true /* immutable */);
            var instance = protoInstance.DeepCopy(true /* immutable */);

            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { instance.GetProperty("g").EvaluatedValue = "v2"; });
            Helpers.VerifyAssertThrowsInvalidOperation(
                delegate
                {
                    instance.GetProperty(NativeMethodsShared.IsWindows ? "username" : "USER").EvaluatedValue =
                        "someone_else_here";
                });
            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Properties).EvaluatedValue = "v2"; });
            Helpers.VerifyAssertThrowsInvalidOperation(delegate () { Helpers.GetFirst(instance.Items).EvaluatedInclude = "new"; });
        }

        /// <summary>
        /// Cloning inherits unless otherwise specified
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ImmutableProjectInstance_CloneMutableFromMutable()
        {
            var protoInstance = GetSampleProjectInstance(false /* mutable */);
            var instance = protoInstance.DeepCopy(/* inherit */);

            // These should not throw
            instance.SetProperty("p", "pnew");
            instance.AddItem("i", "ii");
            Helpers.GetFirst(instance.Items).EvaluatedInclude = "new";
            instance.SetProperty("g", "gnew");
            instance.SetProperty("username", "someone_else_here");
        }

        /// <summary>
        /// Cloning inherits unless otherwise specified
        /// </summary>
        [Fact]
        public void ImmutableProjectInstance_CloneMutableFromMutable2()
        {
            var protoInstance = GetSampleProjectInstance(false /* mutable */);
            var instance = protoInstance.DeepCopy(false /* mutable */);

            // These should not throw
            instance.SetProperty("p", "pnew");
            instance.AddItem("i", "ii");
            Helpers.GetFirst(instance.Items).EvaluatedInclude = "new";
            instance.SetProperty("g", "gnew");
            instance.SetProperty("username", "someone_else_here");
        }

        /// <summary>
        /// Create a ProjectInstance with some items and properties and targets
        /// </summary>
        private static ProjectInstance GetSampleProjectInstance(bool isImmutable = false)
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i>
                              <n>n1</n>
                            </i>
                        </ItemDefinitionGroup>
                        <PropertyGroup>
                            <p1>v1</p1>
                            <p2>v2</p2>
                            <p2>$(p2)X$(p)</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Include='i0'/>
                            <i Include='i1'>
                                <m>m1</m>
                            </i>
                            <i Include='$(p1)'/>
                        </ItemGroup>
                        <Target Name='t'>
                            <t1 a='a1' b='b1' ContinueOnError='coe' Condition='c'/>
                            <t2/>
                        </Target>
                        <Target Name='tt'/>
                    </Project>
                ";

            ProjectInstance p = GetProjectInstance(content, isImmutable);

            return p;
        }

        /// <summary>
        /// Create a ProjectInstance from provided project content
        /// </summary>
        private static ProjectInstance GetProjectInstance(string content, bool immutable = false)
        {
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties["g"] = "gv";

            Project project = new Project(XmlReader.Create(new StringReader(content)), globalProperties, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            ProjectInstance instance = immutable ? project.CreateProjectInstance(ProjectInstanceSettings.Immutable) : project.CreateProjectInstance();

            return instance;
        }

        /// <summary>
        /// Create a ProjectInstance that's empty
        /// </summary>
        private static ProjectInstance GetEmptyProjectInstance()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            Project project = new Project(xml);
            ProjectInstance instance = project.CreateProjectInstance();

            return instance;
        }
    }
}
