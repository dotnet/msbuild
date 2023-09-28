// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using Microsoft.NET.Build.Tasks.UnitTests.Mocks;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAConflictResolver
    {
        [Fact]
        public void ItemsWithDifferentKeysDontConflict()
        {
            var item1 = new MockConflictItem("System.Ben");
            var item2 = new MockConflictItem("System.Immo");

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenOnlyOneItemExistsAWinnerCannotBeDetermined()
        {
            var item1 = new MockConflictItem() { Exists = false };
            var item2 = new MockConflictItem() { Exists = true };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().Equal(item1, item2);
        }

        [Fact]
        public void WhenNeitherItemExistsAWinnerCannotBeDetermined()
        {
            var item1 = new MockConflictItem() { Exists = false, AssemblyVersion = new Version("1.0.0.0") };
            var item2 = new MockConflictItem() { Exists = false, AssemblyVersion = new Version("2.0.0.0") };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().Equal(item1, item2);
        }


        [Fact]
        public void WhenAnItemDoesntExistButDoesNotConflictWithAnythingItIsNotReported()
        {
            var result = GetConflicts(
                new MockConflictItem("System.Ben"),
                new MockConflictItem("System.Immo") { Exists = false },
                new MockConflictItem("System.Dave")
                );

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictAndDontHaveAssemblyVersionsTheFileVersionIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { AssemblyVersion = null, FileVersion = new Version("1.0.0.0") };
            var item2 = new MockConflictItem() { AssemblyVersion = null, FileVersion = new Version("3.0.0.0") };
            var item3 = new MockConflictItem() { AssemblyVersion = null, FileVersion = new Version("2.0.0.0") };

            var result = GetConflicts(item1, item2, item3);

            result.Conflicts.Should().Equal(item1, item3);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictAndOnlyOneHasAnAssemblyVersionAWinnerCannotBeDetermined()
        {
            var item1 = new MockConflictItem() { AssemblyVersion = new Version("1.0.0.0") };
            var item2 = new MockConflictItem() { AssemblyVersion = null };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().Equal(item1, item2);
        }

        [Fact]
        public void WhenItemsConflictAndAssemblyVersionsMatchTheFileVersionIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { FileVersion = new Version("3.0.0.0") };
            var item2 = new MockConflictItem() { FileVersion = new Version("2.0.0.0") };
            var item3 = new MockConflictItem() { FileVersion = new Version("1.0.0.0") };

            var result = GetConflicts(item1, item2, item3);

            result.Conflicts.Should().Equal(item2, item3);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictTheAssemblyVersionIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { AssemblyVersion = new Version("1.0.0.0") };
            var item2 = new MockConflictItem() { AssemblyVersion = new Version("2.0.0.0") };
            var item3 = new MockConflictItem() { AssemblyVersion = new Version("3.0.0.0") };

            var result = GetConflicts(item1, item2, item3);

            result.Conflicts.Should().Equal(item1, item2);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictAndDontHaveFileVersionsThePackageRankIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { FileVersion = null, PackageId = "Package3" };
            var item2 = new MockConflictItem() { FileVersion = null, PackageId = "Package2" };
            var item3 = new MockConflictItem() { FileVersion = null, PackageId = "Package1" };

            var result = GetConflicts(item1, item2, item3);

            result.Conflicts.Should().Equal(item1, item2);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictAndOnlyOneHasAFileVersionAWinnerCannotBeDetermined()
        {
            var item1 = new MockConflictItem() { FileVersion = null };
            var item2 = new MockConflictItem() { FileVersion = new Version("1.0.0.0") };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().Equal(item1, item2);
        }

        [Fact]
        public void WhenItemsConflictAndFileVersionsMatchThePackageRankIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { PackageId = "Package2" };
            var item2 = new MockConflictItem() { PackageId = "Package3" };
            var item3 = new MockConflictItem() { PackageId = "Package1" };

            var result = GetConflicts(item1, item2, item3);

            result.Conflicts.Should().Equal(item2, item1);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictTheFileVersionIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { FileVersion = new Version("2.0.0.0") };
            var item2 = new MockConflictItem() { FileVersion = new Version("1.0.0.0") };
            var item3 = new MockConflictItem() { FileVersion = new Version("3.0.0.0") };

            var result = GetConflicts(item1, item2, item3);

            result.Conflicts.Should().Equal(item2, item1);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictAndDontHaveAPackageRankTheItemTypeIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { PackageId = "Unranked1", ItemType = ConflictItemType.Platform };
            var item2 = new MockConflictItem() { PackageId = "Unranked2", ItemType = ConflictItemType.Reference };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().Equal(item2);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictAndOnlyOneHasAPackageRankItWins()
        {
            var item1 = new MockConflictItem() { PackageId = "Unranked1" };
            var item2 = new MockConflictItem() { PackageId = "Ranked1" };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().Equal(item1);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenItemsConflictAndPackageRanksMatchTheItemTypeIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { PackageId = "Package1", ItemType = ConflictItemType.Reference };
            var item2 = new MockConflictItem() { PackageId = "Package1", ItemType = ConflictItemType.Platform };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().Equal(item1);
            result.UnresolvedConflicts.Should().BeEmpty();
        }


        [Fact]
        public void WhenItemsConflictThePackageRankIsUsedToResolveTheConflict()
        {
            var item1 = new MockConflictItem() { PackageId = "Package1" };
            var item2 = new MockConflictItem() { PackageId = "Package2" };
            var item3 = new MockConflictItem() { PackageId = "Package3" };

            var result = GetConflicts(item1, item2, item3);

            result.Conflicts.Should().Equal(item2, item3);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Theory]
        [InlineData(new[] { 1, 1, 2 }, 2)]
        [InlineData(new[] { 1, 2, 1 }, 1)]
        [InlineData(new[] { 2, 1, 1 }, 0)]
        [InlineData(new[] { 1, 1, 2, 1, 2, 2, 3 }, 6)]
        [InlineData(new[] { 1, 1, 2, 3, 1, 2, 2 }, 3)]
        [InlineData(new[] { 3, 1, 1, 2, 1, 2, 2 }, 0)]
        public void ItemsWithNoWinnerWillCountAsConflictsIfAnotherItemWins(int[] versions, int winnerIndex)
        {
            var items = versions.Select(v => new MockConflictItem() { FileVersion = new Version(v, 0, 0, 0) })
                .ToArray();

            var result = GetConflicts(items);

            result.Conflicts.Should().BeEquivalentTo(items.Except(new[] { items[winnerIndex] }));
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void ItemsWithNoWinnerWillBeUnresolvedIfAnotherItemLoses()
        {
            int[] versions = new[]
            {
                3,
                3,
                2
            };

            var items = versions.Select(v => new MockConflictItem() { FileVersion = new Version(v, 0, 0, 0) })
                .ToArray();

            var result = GetConflicts(items);

            result.Conflicts.Should().BeEquivalentTo(new[] { items[2] });
            result.UnresolvedConflicts.Should().BeEquivalentTo(new[] { items[0], items[1] });
        }

        [Fact]
        public void WhenItemsConflictAndBothArePlatformItemsTheConflictCannotBeResolved()
        {
            var item1 = new MockConflictItem() { ItemType = ConflictItemType.Platform };
            var item2 = new MockConflictItem() { ItemType = ConflictItemType.Platform };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().Equal(item1, item2);
        }

        [Fact]
        public void WhenItemsConflictAndNeitherArePlatformItemsTheConflictCannotBeResolved()
        {
            var item1 = new MockConflictItem() { ItemType = ConflictItemType.Reference };
            var item2 = new MockConflictItem() { ItemType = ConflictItemType.CopyLocal };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().Equal(item1, item2);
        }

        [Fact]
        public void WhenItemsConflictAPlatformItemWins()
        {
            var item1 = new MockConflictItem() { ItemType = ConflictItemType.Reference };
            var item2 = new MockConflictItem() { ItemType = ConflictItemType.Platform };

            var result = GetConflicts(item1, item2);

            result.Conflicts.Should().Equal(item1);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenCommitWinnerIsFalseOnlyTheFirstResolvedConflictIsReported()
        {
            var committedItem = new MockConflictItem() { AssemblyVersion = new Version("2.0.0.0") };

            var uncommittedItem1 = new MockConflictItem() { AssemblyVersion = new Version("3.0.0.0") };
            var uncommittedItem2 = new MockConflictItem() { AssemblyVersion = new Version("1.0.0.0") };
            var uncommittedItem3 = new MockConflictItem() { AssemblyVersion = new Version("2.0.0.0") };

            var result = GetConflicts(new[] { committedItem }, uncommittedItem1, uncommittedItem2, uncommittedItem3);

            result.Conflicts.Should().Equal(committedItem);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenCommitWinnerIsFalseAndThereIsNoWinnerEachUnresolvedConflictIsReported()
        {
            var committedItem = new MockConflictItem();

            var uncommittedItem1 = new MockConflictItem();
            var uncommittedItem2 = new MockConflictItem();
            var uncommittedItem3 = new MockConflictItem();

            var result = GetConflicts(new[] { committedItem }, uncommittedItem1, uncommittedItem2, uncommittedItem3);

            result.Conflicts.Should().BeEmpty();
            result.UnresolvedConflicts.Should().Equal(committedItem, uncommittedItem1, uncommittedItem2, uncommittedItem3);
        }

        [Fact]
        public void WhenCommitWinnerIsFalseMultipleConflictsAreReportedIfTheCommittedItemWins()
        {
            var committedItem = new MockConflictItem() { AssemblyVersion = new Version("4.0.0.0") };

            var uncommittedItem1 = new MockConflictItem() { AssemblyVersion = new Version("3.0.0.0") };
            var uncommittedItem2 = new MockConflictItem() { AssemblyVersion = new Version("1.0.0.0") };
            var uncommittedItem3 = new MockConflictItem() { AssemblyVersion = new Version("2.0.0.0") };

            var result = GetConflicts(new[] { committedItem }, uncommittedItem1, uncommittedItem2, uncommittedItem3);

            result.Conflicts.Should().Equal(uncommittedItem1, uncommittedItem2, uncommittedItem3);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenCommitWinnerIsFalseConflictsWithDifferentKeysAreReported()
        {
            var committedItem1 = new MockConflictItem("System.Ben") { AssemblyVersion = new Version("2.0.0.0") };
            var committedItem2 = new MockConflictItem("System.Immo") { AssemblyVersion = new Version("2.0.0.0") };

            var uncommittedItem1 = new MockConflictItem("System.Ben") { AssemblyVersion = new Version("1.0.0.0") };
            var uncommittedItem2 = new MockConflictItem("System.Immo") { AssemblyVersion = new Version("3.0.0.0") };
            var uncommittedItem3 = new MockConflictItem("System.Dave") { AssemblyVersion = new Version("3.0.0.0") };
            var uncommittedItem4 = new MockConflictItem("System.Ben") { AssemblyVersion = new Version("3.0.0.0") };

            var result = GetConflicts(new[] { committedItem1, committedItem2 }, uncommittedItem1, uncommittedItem2, uncommittedItem3, uncommittedItem4);

            result.Conflicts.Should().Equal(uncommittedItem1, committedItem2, committedItem1);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenPackageOverridesAreSpecifiedTheyAreUsed()
        {
            var systemItem1 = new MockConflictItem("System.Ben") { PackageId = "System.Ben", PackageVersion = new NuGetVersion("4.3.0") };
            var systemItem2 = new MockConflictItem("System.Immo") { PackageId = "System.Immo", PackageVersion = new NuGetVersion("4.2.0") };
            var systemItem3 = new MockConflictItem("System.Dave") { PackageId = "System.Dave", PackageVersion = new NuGetVersion("4.1.0") };

            var platformItem1 = new MockConflictItem("System.Ben") { PackageId = "Platform", PackageVersion = new NuGetVersion("2.0.0") };
            var platformItem2 = new MockConflictItem("System.Immo") { PackageId = "Platform", PackageVersion = new NuGetVersion("2.0.0") };
            var platformItem3 = new MockConflictItem("System.Dave") { PackageId = "Platform", PackageVersion = new NuGetVersion("2.0.0") };

            var result = GetConflicts(
                new[] { systemItem1, systemItem2, systemItem3, platformItem1, platformItem2, platformItem3 },
                Array.Empty<MockConflictItem>(),
                new[] {
                    new MockTaskItem("Platform", new Dictionary<string, string>
                    {
                        { MetadataKeys.OverriddenPackages, "System.Ben|4.3.0;System.Immo|4.3.0;System.Dave|4.3.0" },
                    })
                });

            result.Conflicts.Should().Equal(systemItem1, systemItem2, systemItem3);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenAHigherPackageIsUsedPackageOverrideLoses()
        {
            var platformItem1 = new MockConflictItem("System.Ben") { PackageId = "Platform", PackageVersion = new NuGetVersion("2.0.0") };
            var platformItem2 = new MockConflictItem("System.Immo") { PackageId = "Platform", PackageVersion = new NuGetVersion("2.0.0") };
            var platformItem3 = new MockConflictItem("System.Dave") { PackageId = "Platform", PackageVersion = new NuGetVersion("2.0.0") };

            var systemItem1 = new MockConflictItem("System.Ben") { PackageId = "System.Ben", PackageVersion = new NuGetVersion("4.3.0") };
            var systemItem2 = new MockConflictItem("System.Immo") { PackageId = "System.Immo", PackageVersion = new NuGetVersion("4.2.0") };
            // System.Dave has a higher PackageVersion than the PackageOverride
            var systemItem3 = new MockConflictItem("System.Dave")
            {
                PackageId = "System.Dave",
                PackageVersion = new NuGetVersion("4.4.0"),
                AssemblyVersion = new Version(platformItem3.AssemblyVersion.Major + 1, 0)
            };

            var result = GetConflicts(
                new[] { systemItem1, systemItem2, systemItem3, platformItem1, platformItem2, platformItem3 },
                Array.Empty<MockConflictItem>(),
                new[] {
                    new MockTaskItem("Platform", new Dictionary<string, string>
                    {
                        { MetadataKeys.OverriddenPackages, "System.Ben|4.3.0;System.Immo|4.3.0;System.Dave|4.3.0" },
                    })
                });

            result.Conflicts.Should().Equal(systemItem1, systemItem2, platformItem3);
            result.UnresolvedConflicts.Should().BeEmpty();
        }

        static ConflictResults GetConflicts(params MockConflictItem[] items)
        {
            return GetConflicts(items, Array.Empty<MockConflictItem>());
        }

        static ConflictResults GetConflicts(MockConflictItem[] itemsToCommit, params MockConflictItem[] itemsNotToCommit)
        {
            return GetConflicts(itemsToCommit, itemsNotToCommit, Array.Empty<ITaskItem>());
        }

        static ConflictResults GetConflicts(MockConflictItem[] itemsToCommit, MockConflictItem[] itemsNotToCommit, ITaskItem[] packageOverrides)
        {
            ConflictResults ret = new();

            void ConflictHandler(MockConflictItem winner, MockConflictItem loser)
            {
                ret.Conflicts.Add(loser);
            }

            void UnresolvedConflictHandler(MockConflictItem item)
            {
                ret.UnresolvedConflicts.Add(item);
            }

            string[] packagesForRank = itemsToCommit.Concat(itemsNotToCommit)
                .Select(i => i.PackageId)
                .Where(id => !id.StartsWith("Unranked", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            var overrideResolver = new PackageOverrideResolver<MockConflictItem>(packageOverrides);

            using (var resolver = new ConflictResolver<MockConflictItem>(new PackageRank(packagesForRank), overrideResolver, new MockLog()))
            {
                resolver.UnresolvedConflictHandler = UnresolvedConflictHandler;

                resolver.ResolveConflicts(itemsToCommit, GetItemKey, ConflictHandler);

                resolver.ResolveConflicts(itemsNotToCommit, GetItemKey, ConflictHandler,
                    commitWinner: false);
            }

            return ret;
        }

        static string GetItemKey(MockConflictItem item)
        {
            return item.Key;
        }

        class ConflictResults
        {
            public List<MockConflictItem> Conflicts { get; set; } = new List<MockConflictItem>();
            public List<MockConflictItem> UnresolvedConflicts { get; set; } = new List<MockConflictItem>();
        }

        class MockLog : Logger
        {
            protected override void LogCore(in Message message)
            {
            }
        }
    }
}
