// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using ProjectInstanceExpander =
    Microsoft.Build.Evaluation.Expander<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;
using ProjectInstanceItemSpecMatcher =
    Microsoft.Build.Evaluation.ItemSpecMatcher<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class ItemSpec_Tests
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("literal", false)]
        [InlineData("@(Lookup)", true)]
        [InlineData("@(lookup)", true)]
        [InlineData("@(Other)", false)]
        [InlineData("@(Lookup);@(Lookup)", false)]
        [InlineData("@(Lookup->'%(Filename)')", false)]
        [InlineData("@(Lookup->Count())", false)]
        [InlineData("@(Lookup, ',')", true)] // Preserve current separator behavior; see the predicate's UNDONE.
        public void IsSimpleItemVectorInspectsSyntaxWithoutResolvingItems(string specification, bool expected)
        {
            var expander = CreateExpander(new Dictionary<string, string[]>());
            var location = MockElementLocation.Instance;
            var itemSpec = ItemSpec.Parse(specification, expander, location, Path.GetDirectoryName(location.File));

            itemSpec.IsSimpleItemVector("Lookup").ShouldBe(expected);
        }

        [Fact]
        public void EachFragmentTypeShouldContributeToItemSpecGlob()
        {
            var matcher = CreateMatcherFrom(
                "a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { ["foo"] = ["d", "e"] }));

            var itemSpecGlob = matcher.ToMSBuildGlob();

            itemSpecGlob.IsMatch("a").ShouldBeTrue();
            itemSpecGlob.IsMatch("bar").ShouldBeTrue();
            itemSpecGlob.IsMatch("car").ShouldBeTrue();
            itemSpecGlob.IsMatch("d").ShouldBeTrue();
            itemSpecGlob.IsMatch("e").ShouldBeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("@(Missing)")]
        public void EmptySpecificationAndEmptyVectorProduceNeverMatchingGlobs(string specification)
        {
            var matcher = CreateMatcherFrom(specification, CreateExpander(new Dictionary<string, string[]>()));

            var glob = matcher.ToMSBuildGlob();

            glob.IsMatch("").ShouldBeFalse();
            glob.IsMatch("anything").ShouldBeFalse();
            matcher.ToMSBuildGlob().ShouldBeSameAs(glob);
        }

        [Theory]
        [InlineData("")]
        [InlineData("@(Missing)")]
        public void AddingEmptyFragmentStringsPreservesExistingEntries(string specification)
        {
            var matcher = CreateMatcherFrom(specification, CreateExpander(new Dictionary<string, string[]>()));
            var builder = ImmutableHashSet.CreateBuilder<string>();
            builder.Add("existing");

            matcher.AddFragmentStringsTo(builder);

            builder.ToImmutable().ShouldBe(["existing"], ignoreOrder: true);
        }

        [Fact]
        public void AddingFragmentStringsPreservesEscapingAndCombinesAllFragmentKinds()
        {
            var matcher = CreateMatcherFrom(
                "same.txt;literal%3Bname;g%3Blob*.cs;@(Missing);@(Lookup);@(Lookup);tail",
                CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["vector.txt", "same.txt", "vector.txt"] }));
            var builder = ImmutableHashSet.CreateBuilder<string>();
            builder.Add("existing");

            matcher.AddFragmentStringsTo(builder);

            builder.ToImmutable().ShouldBe(
                ["existing", "same.txt", "literal%3Bname", "g;lob*.cs", "vector.txt", "tail"], ignoreOrder: true);
        }

        [Fact]
        public void AddingFragmentStringsUsesEachDestinationComparer()
        {
            var matcher = CreateMatcherFrom(
                "same.txt;@(Lookup)",
                CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["SAME.txt"] }));
            var caseSensitive = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            var caseInsensitive = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);

            matcher.AddFragmentStringsTo(caseSensitive);
            matcher.AddFragmentStringsTo(caseInsensitive);

            caseSensitive.ToImmutable().ShouldBe(["same.txt", "SAME.txt"], ignoreOrder: true);
            caseInsensitive.ToImmutable().ShouldBe(["same.txt"], ignoreOrder: true);
        }

        [Fact]
        public void AbsolutePathsShouldMatch()
        {
            var absoluteRootPath = NativeMethodsShared.IsWindows
                ? @"c:\a\b"
                : "/a/b";

            var projectFile = Path.Combine(absoluteRootPath, "build.proj");
            var absoluteSpec = Path.Combine(absoluteRootPath, "s.cs");

            var matcherFromAbsolute = CreateMatcherFrom(
                absoluteSpec, CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));
            var matcherFromRelative = CreateMatcherFrom(
                "s.cs", CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));

            matcherFromRelative.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            matcherFromRelative.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();

            matcherFromAbsolute.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            matcherFromAbsolute.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();
        }

        [Fact]
        public void FragmentGlobsWorkAfterStateIsPartiallyInitializedByOtherOperations()
        {
            var matcher = CreateMatcherFrom(
                "a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { ["foo"] = ["d", "e"] }));

            // Resolve the vector through occurrence counting before creating its glob projection.
            matcher.FragmentsMatchingItem("e", out int matches);

            matches.ShouldBe(1);

            var itemSpecGlob = matcher.ToMSBuildGlob();

            itemSpecGlob.IsMatch("a").ShouldBeTrue();
            itemSpecGlob.IsMatch("bar").ShouldBeTrue();
            itemSpecGlob.IsMatch("car").ShouldBeTrue();
            itemSpecGlob.IsMatch("d").ShouldBeTrue();
            itemSpecGlob.IsMatch("e").ShouldBeTrue();
        }

        [Fact]
        public void PropertyExpansionUsesParsingContextAndItemReferencesUseBoundContext()
        {
            var properties = new PropertyDictionary<ProjectPropertyInstance>();
            properties.Set(ProjectPropertyInstance.Create("Spec", "literal;@(Lookup->'%(Filename).obj')"));
            var parsingExpander = new ProjectInstanceExpander(
                properties,
                ToItemDictionary(new Dictionary<string, string[]> { ["Lookup"] = ["wrong.txt"] }),
                (IFileSystem)FileSystems.Default,
                new TestLoggingContext(null!, new BuildEventContext(1, 2, 3, 4)));
            var location = MockElementLocation.Instance;
            var fragments = ItemSpec.ParseFragments("$(Spec)", parsingExpander, location, Path.GetDirectoryName(location.File));

            properties.Set(ProjectPropertyInstance.Create("Spec", "changed"));
            var itemSpec = new ItemSpec("$(Spec)", fragments);
            var matcher = new ProjectInstanceItemSpecMatcher(
                itemSpec, CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["right.txt"] }));

            itemSpec.Text.ShouldBe("$(Spec)");
            itemSpec.Fragments.Length.ShouldBe(2);
            itemSpec.Fragments[0].Text.ShouldBe("literal");
            var itemSpecGlob = matcher.ToMSBuildGlob();
            itemSpecGlob.IsMatch("literal").ShouldBeTrue();
            itemSpecGlob.IsMatch("right.obj").ShouldBeTrue();
            itemSpecGlob.IsMatch("wrong.obj").ShouldBeFalse();
            itemSpecGlob.IsMatch("changed").ShouldBeFalse();
        }

        [Fact]
        public void ConstructingMatcherDoesNotResolveItemVectors()
        {
            var items = ToItemDictionary(new Dictionary<string, string[]> { ["Lookup"] = ["first.txt"] });
            var expander = new ProjectInstanceExpander(
                new PropertyDictionary<ProjectPropertyInstance>(),
                items,
                (IFileSystem)FileSystems.Default,
                new TestLoggingContext(null!, new BuildEventContext(1, 2, 3, 4)));
            var location = MockElementLocation.Instance;
            var itemSpec = ItemSpec.Parse("@(Lookup)", expander, location, Path.GetDirectoryName(location.File));
            var matcher = new ProjectInstanceItemSpecMatcher(itemSpec, expander);

            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ProjectItemInstance secondItem = new(project, "Lookup", "second.txt", project.FullPath);
            items.Add(secondItem);

            matcher.MatchesItem(secondItem).ShouldBeTrue();
            ProjectItemInstance thirdItem = new(project, "Lookup", "third.txt", project.FullPath);
            items.Add(thirdItem);
            matcher.MatchesItem(thirdItem).ShouldBeFalse();
            matcher.ToMSBuildGlob().IsMatch("second.txt").ShouldBeTrue();
            matcher.ToMSBuildGlob().IsMatch("third.txt").ShouldBeFalse();

            var fragmentStrings = ImmutableHashSet.CreateBuilder<string>();
            matcher.AddFragmentStringsTo(fragmentStrings);
            fragmentStrings.ToImmutable().ShouldBe(["first.txt", "second.txt"], ignoreOrder: true);
        }

        [Fact]
        public void MatchersResolveSharedSyntaxAgainstIndependentContexts()
        {
            var parsingExpander = CreateExpander(new Dictionary<string, string[]>());
            var location = MockElementLocation.Instance;
            var itemSpec = ItemSpec.Parse("@(Lookup)", parsingExpander, location, Path.GetDirectoryName(location.File));
            var first = new ProjectInstanceItemSpecMatcher(
                itemSpec, CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["first"] }));
            var second = new ProjectInstanceItemSpecMatcher(
                itemSpec, CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["second"] }));

            first.ToMSBuildGlob().IsMatch("first").ShouldBeTrue();
            first.ToMSBuildGlob().IsMatch("second").ShouldBeFalse();
            second.ToMSBuildGlob().IsMatch("first").ShouldBeFalse();
            second.ToMSBuildGlob().IsMatch("second").ShouldBeTrue();
        }

        [Fact]
        public void MatchingShortCircuitsBeforeResolvingLaterVectors()
        {
            var expander = CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["source"] });
            var matcher = CreateMatcherFrom("literal;@(Lookup->NotAnItemFunction())", expander);
            ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
            ProjectItemInstance literal = new(project, "Candidate", "literal", project.FullPath);
            ProjectItemInstance other = new(project, "Candidate", "other", project.FullPath);

            matcher.MatchesItem(literal).ShouldBeTrue();
            Should.Throw<InvalidProjectFileException>(() => matcher.MatchesItem(other));
        }

        [Fact]
        public void MatcherPreservesVectorOccurrencesAndIndexedMembership()
        {
            var matcher = CreateMatcherFrom(
                "same.txt;@(Lookup)",
                CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["same.txt", "same.txt", "other.txt"] }));

            var matchingFragments = matcher.FragmentsMatchingItem("same.txt", out int occurrences);
            matchingFragments.Length.ShouldBe(2);
            occurrences.ShouldBe(3);

            string directory = matcher.ItemSpec.Fragments[0].ProjectDirectory;
            NormalizedItemPath same = new(FileUtilities.NormalizePathForComparisonNoThrow("same.txt", directory));
            NormalizedItemPath other = new(FileUtilities.NormalizePathForComparisonNoThrow("other.txt", directory));
            HashSet<NormalizedItemPath> indexed = [same, other];
            var matches = matcher.IntersectsWith(indexed);
            matches.Count.ShouldBe(2);
            matches.Contains(same).ShouldBeTrue();
            matches.Contains(other).ShouldBeTrue();
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData(" ; ; ", 0)]
        [InlineData("a;;b;", 2)]
        [InlineData("@(Lookup->'a;b')", 1)]
        [InlineData("literal;@(Lookup->'a;b');*.cs", 3)]
        public void FragmentParsingDiscardsEmptyEntriesAndPreservesQuotedSemicolons(string specification, int expectedCount)
        {
            var expander = CreateExpander(new Dictionary<string, string[]>());
            var location = MockElementLocation.Instance;

            var fragments = ItemSpec.ParseFragments(specification, expander, location, Path.GetDirectoryName(location.File));

            fragments.Length.ShouldBe(expectedCount);
        }

        [Fact]
        public void FragmentParsingPreservesKindsAndEscaping()
        {
            var expander = CreateExpander(new Dictionary<string, string[]>());
            var location = MockElementLocation.Instance;

            var fragments = ItemSpec.ParseFragments("literal;*.cs;%2A;@(Lookup)", expander, location, Path.GetDirectoryName(location.File));

            fragments.Length.ShouldBe(4);
            fragments[0].ShouldBeOfType<ValueFragment>();
            fragments[1].ShouldBeOfType<GlobFragment>();
            fragments[2].ShouldBeOfType<ValueFragment>();
            fragments[2].Text.ShouldBe("%2A");
            fragments[3].ShouldBeOfType<ItemVectorFragment>();
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData(" ; ; ", 0)]
        [InlineData("@(First)", 1)]
        [InlineData("@(First);@(Second)", 2)]
        [InlineData("@(First->'a;b');@(Second)", 2)]
        public void ItemExpressionParsingPreservesEmptyArraysAndVectorOrder(string specification, int expectedCount)
        {
            var expander = CreateExpander(new Dictionary<string, string[]>());
            var location = MockElementLocation.Instance;
            var itemExpressions = ItemSpec.ParseItemVectorFragments(
                specification, expander, location, Path.GetDirectoryName(location.File));

            itemExpressions.Length.ShouldBe(expectedCount);
            for (int i = 0; i < itemExpressions.Length; i++)
            {
                itemExpressions[i].ItemVector.ItemType.ShouldBe(i == 0 ? "First" : "Second");
            }
        }

        [Theory]
        [InlineData("literal")]
        [InlineData("*.cs")]
        [InlineData("@(Lookup);literal")]
        [InlineData("*.cs;@(Lookup)")]
        public void ItemExpressionParsingRejectsNonVectorFragments(string specification)
        {
            var expander = CreateExpander(new Dictionary<string, string[]>());
            var location = MockElementLocation.Instance;

            var exception = Should.Throw<InvalidProjectFileException>(
                () => ItemSpec.ParseItemVectorFragments(
                    specification, expander, location, Path.GetDirectoryName(location.File)));

            exception.HelpKeyword.ShouldBe("MSBuild.OM_MatchOnMetadataIsRestrictedToReferencedItems");
        }

        [Fact]
        public void ItemExpressionParsingExpandsPropertiesWithoutResolvingVectors()
        {
            var properties = new PropertyDictionary<ProjectPropertyInstance>();
            properties.Set(ProjectPropertyInstance.Create("Spec", "@(Lookup->'%(Filename).obj')"));
            var parsingExpander = new ProjectInstanceExpander(
                properties,
                ToItemDictionary(new Dictionary<string, string[]> { ["Lookup"] = ["wrong.txt"] }),
                (IFileSystem)FileSystems.Default,
                new TestLoggingContext(null!, new BuildEventContext(1, 2, 3, 4)));
            var location = MockElementLocation.Instance;

            var fragments = ItemSpec.ParseItemVectorFragments(
                "$(Spec)", parsingExpander, location, Path.GetDirectoryName(location.File));

            fragments.Length.ShouldBe(1);
            var matcher = new ProjectInstanceItemSpecMatcher(
                new ItemSpec("$(Spec)", [fragments[0]]),
                CreateExpander(new Dictionary<string, string[]> { ["Lookup"] = ["right.txt"] }));
            matcher.ToMSBuildGlob().IsMatch("right.obj").ShouldBeTrue();
            matcher.ToMSBuildGlob().IsMatch("wrong.obj").ShouldBeFalse();
        }

        [Fact]
        public void MetadataTriePreservesWholeTuplesAcrossSharedBranchesAndDuplicateSources()
        {
            var items = ToItemDictionary(new Dictionary<string, string[]>
            {
                ["Lookup"] = ["first", "second", "third", "duplicate"],
            });
            var expander = CreateExpander(items);
            var sourceFragments = CreateItemExpressionFragmentsFrom("@(Lookup)", expander);
            List<ProjectItemInstance> sourceItems = [.. items.GetItems("Lookup")];
            (string First, string Second)[] sourceValues =
            [
                ("shared", "left"), ("shared", "right"), ("other", "third"), ("shared", "left"),
            ];
            for (int index = 0; index < sourceValues.Length; index++)
            {
                ProjectItemInstance source = sourceItems[index];
                source.SetMetadata("First", sourceValues[index].First);
                source.SetMetadata("Second", sourceValues[index].Second);
            }

            ImmutableArray<string> names = ["First", "Second"];
            var trie = MetadataTrie.Create(MatchOnMetadataOptions.CaseSensitive, names, sourceFragments, expander);
            HashSet<string> nameSet = [.. names];
            var hashSetTrie = MetadataTrie.Create(MatchOnMetadataOptions.CaseSensitive, nameSet, sourceFragments, expander);
            ImmutableArray<string> hashSetOrder = [.. nameSet];

            // Reuse an item after construction to check that the trie captured values, not live item state.
            ProjectItemInstance candidate = sourceItems[0];
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
            var items = ToItemDictionary(new Dictionary<string, string[]>
            {
                ["Lookup"] = ["first", "second"],
            });
            var expander = CreateExpander(items);
            var sourceFragments = CreateItemExpressionFragmentsFrom("@(Lookup)", expander);
            List<ProjectItemInstance> sourceItems = [.. items.GetItems("Lookup")];
            sourceItems[0].SetMetadata("Value", "one");
            sourceItems[1].SetMetadata("Value", "two");
            ImmutableArray<string> names = ["Value"];
            var trie = MetadataTrie.Create(MatchOnMetadataOptions.CaseSensitive, names, sourceFragments, expander);
            HashSet<string> nameSet = [.. names];
            var hashSetTrie = MetadataTrie.Create(MatchOnMetadataOptions.CaseSensitive, nameSet, sourceFragments, expander);
            ProjectItemInstance candidate = sourceItems[0];
            IItem candidateItem = candidate;
            string[] candidateValues = ["one", "two", "missing"];

            foreach (string value in candidateValues)
            {
                candidate.SetMetadata("Value", value);
                bool expected = value != "missing";
                trie.Contains(candidateItem, names).ShouldBe(expected);
                trie.Contains([value]).ShouldBe(expected);
                hashSetTrie.Contains(candidateItem, names).ShouldBe(expected);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void MetadataTrieRejectsMissingAndEmptyCandidateMetadata(int sourceCount)
        {
            string[] sources = sourceCount == 0 ? [] : ["source"];
            var expander = CreateExpander(new Dictionary<string, string[]>
            {
                ["Lookup"] = sources,
            });
            var sourceFragments = CreateItemExpressionFragmentsFrom("@(Lookup)", expander);
            ImmutableArray<string> names = ["Missing"];
            var trie = MetadataTrie.Create(MatchOnMetadataOptions.CaseSensitive, names, sourceFragments, expander);
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

        [Fact]
        public void MetadataTrieResolvesVectorsForEachConstruction()
        {
            var items = ToItemDictionary(new Dictionary<string, string[]> { ["Lookup"] = ["source"] });
            List<ProjectItemInstance> sources = [.. items.GetItems("Lookup")];
            sources[0].SetMetadata("Group", "include");
            sources[0].SetMetadata("Value", "first");
            var expander = CreateExpander(items);
            var fragments = CreateItemExpressionFragmentsFrom("@(Lookup->WithMetadataValue('Group', 'include'))", expander);
            ImmutableArray<string> names = ["Value"];

            var first = MetadataTrie.Create(MatchOnMetadataOptions.CaseSensitive, names, fragments, expander);
            sources[0].SetMetadata("Group", "exclude");
            sources[0].SetMetadata("Value", "second");
            var second = MetadataTrie.Create(MatchOnMetadataOptions.CaseSensitive, names, fragments, expander);

            first.Contains(["first"]).ShouldBeTrue();
            first.Contains(["second"]).ShouldBeFalse();
            second.Contains(["first"]).ShouldBeFalse();
            second.Contains(["second"]).ShouldBeFalse();
        }

        private static ProjectInstanceItemSpecMatcher CreateMatcherFrom(
            string itemSpec, ProjectInstanceExpander expander, IElementLocation location = null)
        {
            location ??= MockElementLocation.Instance;

            var specification = ItemSpec.Parse(itemSpec, expander, location, Path.GetDirectoryName(location.File));
            return new ProjectInstanceItemSpecMatcher(specification, expander);
        }

        private static ImmutableArray<ItemVectorFragment> CreateItemExpressionFragmentsFrom(
            string specification, ProjectInstanceExpander expander)
        {
            var location = MockElementLocation.Instance;
            return ItemSpec.ParseItemVectorFragments(specification, expander, location, Path.GetDirectoryName(location.File));
        }

        private static ProjectInstanceExpander CreateExpander(Dictionary<string, string[]> items)
            => CreateExpander(ToItemDictionary(items));

        private static ProjectInstanceExpander CreateExpander(ItemDictionary<ProjectItemInstance> itemDictionary)
        {
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
