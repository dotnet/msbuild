// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Engine.UnitTests.InstanceFromRemote;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Tests mainly for project evaluation
    /// </summary>
    public class ItemEvaluation_Tests : IDisposable
    {
        /// <summary>
        /// Cleanup
        /// </summary>
        public ItemEvaluation_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        [Fact]
        public void IncludeShouldPreserveIntermediaryReferences()
        {
            var content = @"
                            <i2 Include='a;b;c'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i2>

                            <i Include='@(i2)'/>

                            <i2 Include='d;e;f;@(i2)'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            var mI2_1 = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"},
            };

            var itemsForI = items.Where(i => i.ItemType == "i").ToList();
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, itemsForI, mI2_1);

            var mI2_2 = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
            };

            var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(
                new[] { "a", "b", "c", "d", "e", "f", "a", "b", "c" },
                itemsForI2,
                new[] { mI2_1, mI2_1, mI2_1, mI2_2, mI2_2, mI2_2, mI2_2, mI2_2, mI2_2 });
        }

        [Theory]
        // remove the items by referencing each one
        [InlineData(
            @"
            <i2 Include='a;b;c'>
                <m1>m1_contents</m1>
                <m2>m2_contents</m2>
            </i2>

            <i Include='@(i2)'/>

            <i2 Remove='a;b;c'/>")]
        // remove the items via a glob
        [InlineData(
            @"
            <i2 Include='a;b;c'>
                <m1>m1_contents</m1>
                <m2>m2_contents</m2>
            </i2>

            <i Include='@(i2)'/>

            <i2 Remove='*'/>")]
        public void RemoveShouldPreserveIntermediaryReferences(string content)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            var expectedMetadata = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"}
            };

            var itemsForI = items.Where(i => i.ItemType == "i").ToList();
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, itemsForI, expectedMetadata);

            var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(Array.Empty<string>(), itemsForI2);
        }

        [Fact]
        public void RemoveRespectsItemTransform()
        {
            var content = @"
                            <i Include='a;b;c' />

                            <i Remove='@(i->WithMetadataValue(`Identity`, `b`))' />
                            <i Remove='@(i->`%(Extension)`)' /> <!-- should do nothing -->";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            ObjectModelHelpers.AssertItems(new[] { "a", "c" }, items);
        }

        [Fact]
        public void UpdateRespectsItemTransform()
        {
            var content = @"
                            <i Include='a;b;c' />

                            <i Update='@(i->WithMetadataValue(`Identity`, `b`))'>
                                <m1>m1_updated</m1>
                            </i>
                            <i Update=`@(i->'%(Extension)')`> <!-- should do nothing -->
                                <m2>m2_updated</m2>
                            </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, items,
                new[] {
                    new Dictionary<string, string>(),
                    new Dictionary<string, string> { ["m1"] = "m1_updated" },
                    new Dictionary<string, string>(),
                });
        }

        [Fact]
        public void UpdateShouldPreserveIntermediaryReferences()
        {
            var content = @"
                            <i2 Include='a;b;c'>
                                <m1>m1_contents</m1>
                                <m2>%(Identity)</m2>
                            </i2>

                            <i Include='@(i2)'>
                                <m3>@(i2 -> '%(m2)')</m3>
                                <m4 Condition=""'@(i2 -> '%(m2)')' == 'a;b;c'"">m4_contents</m4>
                            </i>

                            <i2 Update='a;b;c'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                                <m3>m3_updated</m3>
                                <m4>m4_updated</m4>
                            </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);


            var a = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "a"},
                {"m3", "a;b;c"},
                {"m4", "m4_contents"},
            };

            var b = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "b"},
                {"m3", "a;b;c"},
                {"m4", "m4_contents"}
            };

            var c = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "c"},
                {"m3", "a;b;c"},
                {"m4", "m4_contents"},
            };

            var itemsForI = items.Where(i => i.ItemType == "i").ToList();
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, itemsForI, new[] { a, b, c });

            var metadataForI2 = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
                {"m3", "m3_updated"},
                {"m4", "m4_updated"}
            };

            var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, itemsForI2, metadataForI2);
        }

        public static IEnumerable<object[]> IndirectItemReferencesTestData
        {
            get
            {
                // indirect item reference via properties in metadata
                yield return new object[]
                {
                    @"<Project>
                      <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                      </ItemGroup>
                      <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                      </PropertyGroup>

                      <ItemGroup>
                        <i Include=`val`>
                          <m1>$(P1)</m1>
                          <m2>$(P2)</m2>
                        </i>
                      </ItemGroup>

                    </Project>",
                    new []{"val"},
                    new Dictionary<string, string>
                    {
                        {"m1", "1;2"},
                        {"m2", "1;2"}
                    }
                };

                // indirect item reference via properties in metadata condition
                yield return new object[]
                {
                    @"<Project>
                      <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                      </ItemGroup>
                      <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                      </PropertyGroup>

                      <ItemGroup>
                        <i Include=`val`>
                          <m1 Condition=`'$(P1)' == '1;2'`>val1</m1>
                          <m2 Condition=`'$(P2)' == '1;2'`>val2</m2>
                        </i>
                      </ItemGroup>

                    </Project>",
                    new []{"val"},
                    new Dictionary<string, string>
                    {
                        {"m1", "val1"},
                        {"m2", "val2"}
                    }
                };

                // indirect item reference via properties in include
                yield return new object[]
                {
                    @"<Project>
                      <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                      </ItemGroup>
                      <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                      </PropertyGroup>

                      <ItemGroup>
                        <i Include=`$(P1)`/>
                        <i Include=`$(P2)`/>
                      </ItemGroup>

                    </Project>",
                    new []{"1", "2", "1", "2"},
                    new Dictionary<string, string>()
                };

                // indirect item reference via properties in condition
                yield return new object[]
                {
                    @"<Project>
                      <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                      </ItemGroup>
                      <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                      </PropertyGroup>

                      <ItemGroup>
                        <i Condition=`'$(P1)' == '1;2'` Include=`val1`/>
                        <i Condition=`'$(P2)' == '1;2'` Include=`val2`/>
                      </ItemGroup>

                    </Project>",
                    new []{"val1", "val2"},
                    new Dictionary<string, string>()
                };

                // indirect item reference via metadata reference in conditions and metadata
                yield return new object[]
                {
                    @"<Project>
                      <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                      </ItemGroup>
                      <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                      </PropertyGroup>

                      <ItemGroup>
                        <i Include=`val`>
                          <m1 Condition=`'$(P2)' == '1;2'`>$(P2)</m1>
                          <m2 Condition=`'%(m1)' == '1;2'`>%(m1)</m2>
                        </i>
                      </ItemGroup>

                    </Project>",
                    new []{"val"},
                    new Dictionary<string, string>
                    {
                        {"m1", "1;2"},
                        {"m2", "1;2"}
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(IndirectItemReferencesTestData))]
        public void ItemOperationsShouldExpandIndirectItemReferences(string projectContent, string[] expectedItemValues, Dictionary<string, string> expectedItemMetadata)
        {
            var items = ObjectModelHelpers.GetItems(projectContent);

            ObjectModelHelpers.AssertItems(expectedItemValues, items, expectedItemMetadata);
        }

        [Fact]
        public void ExactMetadataCapturePreservesEmptyAttributeAndElementMetadataInBothObjectModels()
        {
            const string projectContent = """
                <Project>
                  <ItemGroup>
                    <Empty Include="empty" />
                    <Mixed Include="include" AttributeMetadata="attribute">
                      <ElementMetadata>element</ElementMetadata>
                    </Mixed>
                    <Mixed Update="include" AttributeUpdate="updated">
                      <ElementUpdate>updated element</ElementUpdate>
                    </Mixed>
                  </ItemGroup>
                </Project>
                """;

            using XmlReader projectReader = XmlReader.Create(new StringReader(projectContent));
            var project = new Project(projectReader);
            ProjectItem empty = project.GetItems("Empty").ShouldHaveSingleItem();
            empty.DirectMetadata.ShouldBeEmpty();
            ProjectItem mixed = project.GetItems("Mixed").ShouldHaveSingleItem();
            mixed.GetMetadataValue("AttributeMetadata").ShouldBe("attribute");
            mixed.GetMetadataValue("ElementMetadata").ShouldBe("element");
            mixed.GetMetadataValue("AttributeUpdate").ShouldBe("updated");
            mixed.GetMetadataValue("ElementUpdate").ShouldBe("updated element");

            using XmlReader instanceReader = XmlReader.Create(new StringReader(projectContent));
            ProjectRootElement root = ProjectRootElement.Create(instanceReader);
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(root, new ProjectOptions());
            ProjectItemInstance emptyInstance = instance.GetItems("Empty").ShouldHaveSingleItem();
            emptyInstance.GetMetadataValue("AttributeMetadata").ShouldBeEmpty();
            emptyInstance.GetMetadataValue("ElementMetadata").ShouldBeEmpty();
            ProjectItemInstance mixedInstance = instance.GetItems("Mixed").ShouldHaveSingleItem();
            mixedInstance.GetMetadataValue("AttributeMetadata").ShouldBe("attribute");
            mixedInstance.GetMetadataValue("ElementMetadata").ShouldBe("element");
            mixedInstance.GetMetadataValue("AttributeUpdate").ShouldBe("updated");
            mixedInstance.GetMetadataValue("ElementUpdate").ShouldBe("updated element");
        }

        [Fact]
        public void ExactMetadataCaptureUsesLocalCountWithoutReadingLinkedCount()
        {
            ProjectRootElement root = ProjectRootElement.Create();
            ProjectItemElement localItem = root.AddItemGroup().AddItem("Local", "item");
            localItem.AddMetadata("First", "one");
            localItem.AddMetadata("Second", "two");

            LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>
                .GetInitialMetadataCapacity(localItem)
                .ShouldBe(2);

            var linkedItem = new ProjectItemElement(new FakeProjectItemElementLink("Linked", "linked.proj"));
            LazyItemEvaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>
                .GetInitialMetadataCapacity(linkedItem)
                .ShouldBe(0);
        }

        [Theory]
        [InlineData(MetadataMutationKind.Grow, 3, 6)]
        [InlineData(MetadataMutationKind.Underfill, 2, 4)]
        public void ExactMetadataCaptureHandlesXmlMutationDuringPropertyExpansion(
            MetadataMutationKind mutationKind,
            int expectedMetadataCount,
            int expectedPropertyFunctionCalls)
        {
            const string enableAllPropertyFunctionsSwitch = "Microsoft.Build.EnableAllPropertyFunctions";
            bool switchWasSet = AppContext.TryGetSwitch(enableAllPropertyFunctionsSwitch, out bool enableAllPropertyFunctions);
            AppContext.SetSwitch(enableAllPropertyFunctionsSwitch, true);

            try
            {
                ProjectRootElement root = ProjectRootElement.Create();
                ProjectItemElement item = root.AddItemGroup().AddItem("I", "item");
                string propertyFunction = $"$([{typeof(MetadataMutationPropertyFunction).AssemblyQualifiedName}]::Mutate())";
                item.AddMetadata("First", propertyFunction);
                item.AddMetadata("Second", propertyFunction);

                if (mutationKind == MetadataMutationKind.Underfill)
                {
                    item.AddMetadata("Third", propertyFunction);
                }

                using MetadataMutationPropertyFunction.Scope mutation =
                    MetadataMutationPropertyFunction.Begin(item, mutationKind, propertyFunction);
                ProjectInstance project = ProjectInstance.FromProjectRootElement(root, new ProjectOptions());
                ProjectItemInstance evaluatedItem = project.GetItems("I").ShouldHaveSingleItem();

                mutation.CallCount.ShouldBe(expectedPropertyFunctionCalls);
                evaluatedItem.GetMetadataValue("First").ShouldBe("value");
                evaluatedItem.GetMetadataValue("Second").ShouldBe("value");
                (evaluatedItem.GetMetadataValue("Added").Length > 0 ? 3 : 2).ShouldBe(expectedMetadataCount);
                evaluatedItem.GetMetadataValue("Third").ShouldBeEmpty();
            }
            finally
            {
                MetadataMutationPropertyFunction.End();
                RestoreAppContextSwitch(enableAllPropertyFunctionsSwitch, switchWasSet, enableAllPropertyFunctions);
            }
        }

        private static void RestoreAppContextSwitch(string switchName, bool wasSet, bool value)
        {
            if (wasSet)
            {
                AppContext.SetSwitch(switchName, value);
                return;
            }

            foreach (System.Reflection.FieldInfo field in typeof(AppContext).GetFields(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
            {
                if (field.GetValue(null) is System.Collections.IDictionary switches)
                {
                    lock (switches)
                    {
                        if (switches.Contains(switchName))
                        {
                            switches.Remove(switchName);
                            return;
                        }
                    }
                }
            }
        }

        [Fact]
        public void OnlyPropertyReferencesGetExpandedInPropertyFunctionArgumentsInsideIncludeAttributes()
        {
            var projectContent =
@"<Project>
        <ItemGroup>
            <A Include=`1`/>
            <B Include=`$([System.String]::new('@(A)'))`/>
            <C Include=`$([System.String]::new('$(P)'))`/>
        </ItemGroup>

        <PropertyGroup>
            <P>@(A)</P>
        </PropertyGroup>
</Project>";

            var items = ObjectModelHelpers.GetItems(projectContent, allItems: true);

            ObjectModelHelpers.AssertItems(new[] { "1", "@(A)", "@(A)" }, items);
        }

        [Fact]
        public void MetadataAndPropertyReferencesGetExpandedInPropertyFunctionArgumentsInsideMetadataElements()
        {
            var projectContent =
@"<Project>
        <ItemGroup>
            <A Include=`1` />
            <B Include=`B`>
               <M>$([System.String]::new(`%(Identity)`))</M>
               <M2>$([System.String]::new(`%(M)`))</M2>
            </B>
            <C Include=`C`>
               <M>$([System.String]::new(`$(P)`))</M>
               <M2>$([System.String]::new(`%(M)`))</M2>
            </C>
            <D Include=`D`>
               <M>$([System.String]::new(`@(A)`))</M>
               <M2>$([System.String]::new(`%(M)`))</M2>
            </D>
        </ItemGroup>

        <PropertyGroup>
            <P>@(A)</P>
        </PropertyGroup>
</Project>";

            var items = ObjectModelHelpers.GetItems(projectContent, allItems: true);

            var expectedMetadata = new[]
            {
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    {"M", "B"},
                    {"M2", "B"}
                },
                new Dictionary<string, string>
                {
                    {"M", "@(A)"},
                    {"M2", "@(A)"}
                },
                new Dictionary<string, string>
                {
                    {"M", "@(A)"},
                    {"M2", "@(A)"}
                }
            };

            ObjectModelHelpers.AssertItems(new[] { "1", "B", "C", "D" }, items, expectedMetadata);
        }

        [Fact]
        public void ExcludeSeesIntermediaryState()
        {
            var projectContent =
@"<Project>
  <ItemGroup>
    <a Include=`1` />
    <i Include=`1;2` Exclude=`@(a)` />
    <a Include=`2` />
    <a Condition=`'@(a)' == '1;2'` Include=`3` />
  </ItemGroup>
  <Target Name=`Build`>
    <Message Text=`Done!` />
  </Target>
</Project>";

            var items = ObjectModelHelpers.GetItems(projectContent);

            ObjectModelHelpers.AssertItems(new[] { "2" }, items);
        }

        [Fact]
        public void MultipleInterItemDependenciesOnSameItemOperation()
        {
            var content = @"
                            <i1 Include='i1_1;i1_2;i1_3;i1_4;i1_5'/>
                            <i1 Update='*'>
                                <m>i1</m>
                            </i1>
                            <i1 Remove='*i1_5'/>

                            <i_cond Condition='@(i1->Count()) == 4' Include='i1 has 4 items'/>

                            <i2 Include='@(i1);i2_4'/>
                            <i2 Remove='i?_4'/>
                            <i2 Update='i?_1'>
                               <m>i2</m>
                            </i2>

                            <i3 Include='@(i1);i3_3'/>
                            <i3 Remove='*i?_3'/>

                            <i1 Remove='*i1_2'/>
                            <i1 Include='i1_6'/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            var i1BaseMetadata = new Dictionary<string, string>
            {
                {"m", "i1"}
            };

            // i1 items: i1_1; i1_3; i1_4; i1_6
            var i1Metadata = new Dictionary<string, string>[]
            {
                i1BaseMetadata,
                i1BaseMetadata,
                i1BaseMetadata,
                new Dictionary<string, string>()
            };

            var i1Items = items.Where(i => i.ItemType == "i1").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1_1", "i1_3", "i1_4", "i1_6" }, i1Items, i1Metadata);

            // i2 items: i1_1; i1_2; i1_3
            var i2Metadata = new Dictionary<string, string>[]
            {
                new Dictionary<string, string>
                {
                    {"m", "i2"}
                },
                i1BaseMetadata,
                i1BaseMetadata
            };

            var i2Items = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1_1", "i1_2", "i1_3" }, i2Items, i2Metadata);

            // i3 items: i1_1; i1_2; i1_4
            var i3Items = items.Where(i => i.ItemType == "i3").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1_1", "i1_2", "i1_4" }, i3Items, i1BaseMetadata);

            var i_condItems = items.Where(i => i.ItemType == "i_cond").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1 has 4 items" }, i_condItems);
        }

        [Fact]
        public void LongIncludeChain()
        {
            const int INCLUDE_COUNT = 10000;

            // This was about the minimum count needed to repro a StackOverflowException
            // const int INCLUDE_COUNT = 4000;

            StringBuilder content = new StringBuilder();
            for (int i = 0; i < INCLUDE_COUNT; i++)
            {
                content.Append("<i Include='ItemValue").Append(i).AppendLine("' />");
            }

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content.ToString());

            Assert.Equal(INCLUDE_COUNT, items.Count);
        }

        // see https://github.com/dotnet/msbuild/issues/2069
        [Fact]
        public void ImmutableListBuilderBug()
        {
            var content = @"<i Include=""0;x1;x2;x3;x4;x5;6;7;8;9""/>
                            <i Remove=""x*""/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            Assert.Equal("0;6;7;8;9", String.Join(";", items.Select(i => i.EvaluatedInclude)));
        }

        [Fact]
        public void LazyWildcardExpansionDoesNotEvaluateWildCardsIfNotReferenced()
        {
            var content = @"
<Project>
   <Import Project=`foo/*.props`/>
   <ItemGroup>
      <i Include=`**/foo/**/*.cs` />
      <i2 Include=`**/bar/**/*.cs` />
      <i3 Include=`**/yyy/**/*.cs` Exclude=`mock-value` />
   </ItemGroup>

   <ItemGroup>
      <ItemReference Include=`@(i)`/>
      <FullPath Include=`@(i->'%(FullPath)')`/>
      <Identity Include=`@(i->'%(Identity)')`/>
      <RecursiveDir Include=`@(i->'%(RecursiveDir)')`/>
   </ItemGroup>
</Project>
".Cleanup();

            var import = @"
<Project>
   <PropertyGroup>
      <FromImport>true</FromImport>
   </PropertyGroup>
</Project>
".Cleanup();
            using (var env = TestEnvironment.Create())
            {
                var projectFiles = env.CreateTestProjectWithFiles(content, new[] { "foo/extra.props", "foo/a.cs", "foo/b.cs", "bar/c.cs", "bar/d.cs", "yyy/d.cs" });

                File.WriteAllText(projectFiles.CreatedFiles[0], import);

                env.SetEnvironmentVariable("MsBuildSkipEagerWildCardEvaluationRegexes", ".*foo.*;.*yyy*.");

                EngineFileUtilities.CaptureLazyWildcardRegexes();

                var project = new Project(projectFiles.ProjectFile);

                Assert.Equal("true", project.GetPropertyValue("FromImport"));
                Assert.Equal("**/foo/**/*.cs", project.GetConcatenatedItemsOfType("i"));
                Assert.Equal("**/yyy/**/*.cs", project.GetConcatenatedItemsOfType("i3"));

                var expectedItems = "bar\\c.cs;bar\\d.cs";

                if (!NativeMethodsShared.IsWindows)
                {
                    expectedItems = expectedItems.ToSlash();
                }

                Assert.Equal(expectedItems, project.GetConcatenatedItemsOfType("i2"));

                var fullPathItems = project.GetConcatenatedItemsOfType("FullPath");
                Assert.Contains("a.cs", fullPathItems);
                Assert.Contains("b.cs", fullPathItems);

                var identityItems = project.GetConcatenatedItemsOfType("Identity");
                Assert.Contains("a.cs", identityItems);
                Assert.Contains("b.cs", identityItems);

                // direct item references do not expand the lazy wildcard
                Assert.Equal("**/foo/**/*.cs", project.GetConcatenatedItemsOfType("ItemReference"));

                // recursive dir does not work with lazy wildcards
                Assert.Equal(string.Empty, project.GetConcatenatedItemsOfType("RecursiveDir"));
            }
        }

        [Fact]
        public void DoesNotCrashWhenUnEvaluatedWildCardLooksLikeUNC()
        {
            var content = """
                <Project>

                  <PropertyGroup>
                    <A>$(B)\</A>
                  </PropertyGroup>

                  <ItemGroup>
                    <None Include="$(A)\csc.*" />
                    <None Update="2.txt">
                      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                    </None>
                  </ItemGroup>

                  <Target Name="a" />

                </Project>
                """.Cleanup();

            using var env = TestEnvironment.Create();

            var projectFiles = env.CreateTestProjectWithFiles(content);

            env.SetEnvironmentVariable("MsBuildSkipEagerWildCardEvaluationRegexes", ".*");

            EngineFileUtilities.CaptureLazyWildcardRegexes();

            Project project = Should.NotThrow(() => new Project(projectFiles.ProjectFile));

            project.GetConcatenatedItemsOfType("None").ShouldContain("csc.*");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DifferentExcludesOnSameWildcardProduceDifferentResults(bool cacheFileEnumerations)
        {
            var projectContents = @"
<Project>
   <ItemGroup>
      <i Include=`**/*.cs`/>
      <i Include=`**/*.cs` Exclude=`*a.cs`/>
      <i Include=`**/*.cs` Exclude=`a.cs;c.cs`/>
   </ItemGroup>
</Project>
".Cleanup();

            try
            {
                using (var env = TestEnvironment.Create())
                {
                    if (cacheFileEnumerations)
                    {
                        env.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
                    }

                    ObjectModelHelpers.AssertItemEvaluationFromProject(
                        projectContents,
                        inputFiles: new[] { "a.cs", "b.cs", "c.cs" },
                        expectedInclude: new[] { "a.cs", "b.cs", "c.cs", "b.cs", "c.cs", "b.cs" });
                }
            }
            finally
            {
                FileMatcher.ClearCaches();
            }
        }

        // see https://github.com/dotnet/msbuild/issues/3460
        [Fact]
        public void MetadataPropertyFunctionBug()
        {
            const string prefix = "SomeLongPrefix-"; // Needs to be longer than "%(FileName)"
            var projectContent = $@"
<Project>
  <ItemGroup>
    <Bar Include=`{prefix}foo`>
      <Baz>$([System.String]::new(%(FileName)).Substring({prefix.Length}))</Baz>
    </Bar>
  </ItemGroup>
</Project>
".Cleanup();

            var items = ObjectModelHelpers.GetItems(projectContent, allItems: true);

            var expectedMetadata = new[]
            {
                new Dictionary<string, string>
                {
                    {"Baz", "foo"},
                },
            };

            ObjectModelHelpers.AssertItems(new[] { $"{prefix}foo" }, items, expectedMetadata);
        }
    }

    public enum MetadataMutationKind
    {
        Grow,
        Underfill,
    }

    public static class MetadataMutationPropertyFunction
    {
        private static readonly AsyncLocal<Scope> s_scope = new();

        internal static Scope Begin(ProjectItemElement item, MetadataMutationKind mutationKind, string propertyFunction)
        {
            var scope = new Scope(item, mutationKind, propertyFunction);
            s_scope.Value = scope;
            return scope;
        }

        public static string Mutate()
        {
            Scope scope = s_scope.Value ?? throw new InvalidOperationException("No metadata mutation scope is active.");
            scope.Mutate();
            return "value";
        }

        internal static void End()
        {
            s_scope.Value = null;
        }

        internal sealed class Scope : IDisposable
        {
            private readonly ProjectItemElement _item;
            private readonly MetadataMutationKind _mutationKind;
            private readonly string _propertyFunction;
            private bool _mutated;

            internal Scope(ProjectItemElement item, MetadataMutationKind mutationKind, string propertyFunction)
            {
                _item = item;
                _mutationKind = mutationKind;
                _propertyFunction = propertyFunction;
            }

            internal int CallCount { get; private set; }

            internal void Mutate()
            {
                CallCount++;

                if (_mutated)
                {
                    return;
                }

                _mutated = true;
                if (_mutationKind == MetadataMutationKind.Grow)
                {
                    _item.AddMetadata("Added", _propertyFunction);
                }
                else
                {
                    _item.RemoveChild(_item.LastChild);
                }
            }

            public void Dispose()
            {
                End();
            }
        }
    }
}
