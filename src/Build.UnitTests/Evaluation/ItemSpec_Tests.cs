// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using ProjectInstanceExpander =
    Microsoft.Build.Evaluation.Expander<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;
using ProjectInstanceItemSpec =
    Microsoft.Build.Evaluation.ItemSpec<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class ItemSpec_Tests
    {
        [Fact]
        public void EachFragmentTypeShouldContributeToItemSpecGlob()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { { "foo", new[] { "d", "e" } } }));

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.True(itemSpecGlob.IsMatch("a"));
            Assert.True(itemSpecGlob.IsMatch("bar"));
            Assert.True(itemSpecGlob.IsMatch("car"));
            Assert.True(itemSpecGlob.IsMatch("d"));
            Assert.True(itemSpecGlob.IsMatch("e"));
        }

        [Fact]
        public void AbsolutePathsShouldMatch()
        {
            var absoluteRootPath = NativeMethodsShared.IsWindows
                ? @"c:\a\b"
                : "/a/b";

            var projectFile = Path.Combine(absoluteRootPath, "build.proj");
            var absoluteSpec = Path.Combine(absoluteRootPath, "s.cs");

            var itemSpecFromAbsolute = CreateItemSpecFrom(absoluteSpec, CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));
            var itemSpecFromRelative = CreateItemSpecFrom("s.cs", CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));

            itemSpecFromRelative.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            itemSpecFromRelative.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();

            itemSpecFromAbsolute.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            itemSpecFromAbsolute.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();
        }

        [Fact]
        public void FragmentGlobsWorkAfterStateIsPartiallyInitializedByOtherOperations()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { { "foo", new[] { "d", "e" } } }));

            int matches;
            // cause partial Lazy state to initialize in the ItemExpressionFragment
            itemSpec.FragmentsMatchingItem("e", out matches);

            Assert.Equal(1, matches);

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.True(itemSpecGlob.IsMatch("a"));
            Assert.True(itemSpecGlob.IsMatch("bar"));
            Assert.True(itemSpecGlob.IsMatch("car"));
            Assert.True(itemSpecGlob.IsMatch("d"));
            Assert.True(itemSpecGlob.IsMatch("e"));
        }

        [Fact]
        public void MetadataTriePreservesWholeTuplesAcrossSharedBranchesAndDuplicateSources()
        {
            var itemSpec = CreateItemSpecFrom("@(Lookup)", CreateExpander(new Dictionary<string, string[]>
            {
                ["Lookup"] = ["first", "second", "third", "duplicate"],
            }));
            var fragment = (ProjectInstanceItemSpec.ItemExpressionFragment)itemSpec.Fragments[0];
            (string First, string Second)[] sourceValues =
            [
                ("shared", "left"), ("shared", "right"), ("other", "third"), ("shared", "left"),
            ];
            for (int index = 0; index < sourceValues.Length; index++)
            {
                ProjectItemInstance source = fragment.ReferencedItems[index].Item;
                source.SetMetadata("First", sourceValues[index].First);
                source.SetMetadata("Second", sourceValues[index].Second);
            }

            ImmutableArray<string> names = ["First", "Second"];
            var trie = MetadataTrie<ProjectPropertyInstance, ProjectItemInstance>.Create(MatchOnMetadataOptions.CaseSensitive, names, itemSpec);
            HashSet<string> nameSet = [.. names];
            var hashSetTrie = MetadataTrie<ProjectPropertyInstance, ProjectItemInstance>.Create(MatchOnMetadataOptions.CaseSensitive, nameSet, itemSpec);
            ImmutableArray<string> hashSetOrder = [.. nameSet];

            // Reuse an item after construction to check that the trie captured values, not live item state.
            ProjectItemInstance candidate = fragment.ReferencedItems[0].Item;
            (string First, string Second, bool Expected)[] candidates =
            [
                ("shared", "left", true),
                ("shared", "right", true),
                ("other", "third", true),
                ("other", "right", false),
                ("missing", "left", false),
                ("shared", string.Empty, false),
            ];
            foreach (var values in candidates)
            {
                candidate.SetMetadata("First", values.First);
                candidate.SetMetadata("Second", values.Second);
                trie.Contains(candidate, names).ShouldBe(values.Expected);
                trie.Contains([values.First, values.Second]).ShouldBe(values.Expected);
                hashSetTrie.Contains(candidate, hashSetOrder).ShouldBe(values.Expected);
            }
        }

        [Fact]
        public void MetadataTrieMatchesSingleMetadataValuesThroughEitherLookup()
        {
            var itemSpec = CreateItemSpecFrom("@(Lookup)", CreateExpander(new Dictionary<string, string[]>
            {
                ["Lookup"] = ["first", "second"],
            }));
            var fragment = (ProjectInstanceItemSpec.ItemExpressionFragment)itemSpec.Fragments[0];
            fragment.ReferencedItems[0].Item.SetMetadata("Value", "one");
            fragment.ReferencedItems[1].Item.SetMetadata("Value", "two");
            ImmutableArray<string> names = ["Value"];
            var trie = MetadataTrie<ProjectPropertyInstance, ProjectItemInstance>.Create(MatchOnMetadataOptions.CaseSensitive, names, itemSpec);
            HashSet<string> nameSet = [.. names];
            var hashSetTrie = MetadataTrie<ProjectPropertyInstance, ProjectItemInstance>.Create(MatchOnMetadataOptions.CaseSensitive, nameSet, itemSpec);
            ProjectItemInstance candidate = fragment.ReferencedItems[0].Item;
            string[] candidateValues = ["one", "two", "missing"];

            foreach (string value in candidateValues)
            {
                candidate.SetMetadata("Value", value);
                bool expected = value != "missing";
                trie.Contains(candidate, names).ShouldBe(expected);
                trie.Contains([value]).ShouldBe(expected);
                hashSetTrie.Contains(candidate, names).ShouldBe(expected);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void MetadataTrieRejectsMissingAndEmptyCandidateMetadata(int sourceCount)
        {
            string[] sources = sourceCount == 0 ? [] : ["source"];
            var itemSpec = CreateItemSpecFrom("@(Lookup)", CreateExpander(new Dictionary<string, string[]>
            {
                ["Lookup"] = sources,
            }));
            ImmutableArray<string> names = ["Missing"];
            var trie = MetadataTrie<ProjectPropertyInstance, ProjectItemInstance>.Create(MatchOnMetadataOptions.CaseSensitive, names, itemSpec);
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ProjectItemInstance candidate = new(project, "Candidate", "candidate", project.FullPath);

            trie.Contains(candidate, names).ShouldBeFalse();
            trie.Contains([string.Empty]).ShouldBeFalse();
            candidate.SetMetadata("Missing", string.Empty);
            trie.Contains(candidate, names).ShouldBeFalse();
            candidate.SetMetadata("Missing", "value");
            trie.Contains(candidate, names).ShouldBeFalse();
            trie.Contains(["value"]).ShouldBeFalse();
        }

        private ProjectInstanceItemSpec CreateItemSpecFrom(string itemSpec, ProjectInstanceExpander expander, IElementLocation location = null)
        {
            location ??= MockElementLocation.Instance;

            return new ProjectInstanceItemSpec(itemSpec, expander, location, Path.GetDirectoryName(location.File));
        }

        private ProjectInstanceExpander CreateExpander(Dictionary<string, string[]> items)
        {
            var itemDictionary = ToItemDictionary(items);

            return new ProjectInstanceExpander(
                new PropertyDictionary<ProjectPropertyInstance>(),
                itemDictionary,
                (IFileSystem)FileSystems.Default,
                new TestLoggingContext(null!, new BuildEventContext(1, 2, 3, 4)));
        }

        private static ItemDictionary<ProjectItemInstance> ToItemDictionary(Dictionary<string, string[]> itemTypes)
        {
            var itemDictionary = new ItemDictionary<ProjectItemInstance>();

            var dummyProject = ProjectHelpers.CreateEmptyProjectInstance();

            foreach (var itemType in itemTypes)
            {
                foreach (var item in itemType.Value)
                {
                    itemDictionary.Add(new ProjectItemInstance(dummyProject, itemType.Key, item, dummyProject.FullPath));
                }
            }

            return itemDictionary;
        }
    }
}
