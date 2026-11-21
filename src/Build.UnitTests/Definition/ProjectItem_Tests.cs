// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;
using ProjectItemFactory = Microsoft.Build.Evaluation.ProjectItem.ProjectItemFactory;

#nullable disable

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Class containing tests for the ProjectItem and related functionality.
    /// </summary>
    public class ProjectItem_Tests
    {
        /// <summary>
        /// Make sure the CopyFrom actually does a clone.
        /// </summary>
        [Fact]
        public void CopyFromClonesMetadata()
        {
            ProjectItem item1 = GetOneItemFromFragment(@"<i Include='i1'><m>m1</m></i>");
            ProjectItemFactory factory = new ProjectItemFactory(item1.Project, item1.Xml);
            ProjectItem item2 = factory.CreateItem(item1, item1.Project.FullPath);

            item1.SetMetadataValue("m", "m2");
            item1.SetMetadataValue("n", "n1");

            Assert.Single(Helpers.MakeList(item2.Metadata));
            Assert.Equal(String.Empty, item2.GetMetadataValue("n"));
            Assert.Equal(1 + 15 /* built-in metadata */, item2.MetadataCount);

            // Should still point at the same XML items
            Assert.True(Object.ReferenceEquals(item1.DirectMetadata.First().Xml, item2.DirectMetadata.First().Xml));
        }

        /// <summary>
        /// Direct metadata lookup, enumeration and count work for small and large numbers of metadata.
        /// Lookup is case-insensitive and enumeration follows declaration order.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(12)]
        public void DirectMetadataLookupAndEnumeration(int metadataCount)
        {
            ProjectItem item = GetOneItemFromFragment(CreateItemWithMetadata(metadataCount));

            item.DirectMetadataCount.ShouldBe(metadataCount);
            item.DirectMetadata.Select(m => m.Name).ShouldBe(Enumerable.Range(0, metadataCount).Select(i => "m" + i));
            item.Metadata.Count.ShouldBe(metadataCount);

            for (int i = 0; i < metadataCount; i++)
            {
                ProjectMetadata metadatum = item.GetMetadata("M" + i);
                metadatum.ShouldNotBeNull();
                metadatum.Name.ShouldBe("m" + i);
                item.GetMetadataValue("M" + i).ShouldBe("v" + i);
                item.HasMetadata("M" + i).ShouldBeTrue();
            }

            item.GetMetadata("missing").ShouldBeNull();
            item.HasMetadata("missing").ShouldBeFalse();
            item.GetMetadataValue("missing").ShouldBe(string.Empty);
        }

        /// <summary>
        /// Setting and removing direct metadata with a different casing updates the existing metadatum.
        /// </summary>
        [Fact]
        public void DirectMetadataCaseInsensitiveOverwriteAndRemove()
        {
            ProjectItem item = GetOneItemFromFragment("<i Include='i1'><Link>a</Link><SubType>Code</SubType></i>");

            ProjectMetadata link = item.GetMetadata("Link");
            item.SetMetadataValue("LINK", "b").ShouldBeSameAs(link);
            item.DirectMetadataCount.ShouldBe(2);
            item.GetMetadataValue("link").ShouldBe("b");
            item.Xml.Metadata.Count.ShouldBe(2);

            item.SetMetadataValue("CopyToOutputDirectory", "Always");
            item.DirectMetadata.Select(m => m.Name).ShouldBe(["Link", "SubType", "CopyToOutputDirectory"]);

            item.RemoveMetadata("subtype").ShouldBeTrue();
            item.DirectMetadata.Select(m => m.Name).ShouldBe(["Link", "CopyToOutputDirectory"]);
            item.RemoveMetadata("SUBTYPE").ShouldBeFalse();

            item.RemoveMetadata("link").ShouldBeTrue();
            item.RemoveMetadata("copytooutputdirectory").ShouldBeTrue();
            item.DirectMetadataCount.ShouldBe(0);
            item.DirectMetadata.ShouldBeEmpty();
            item.HasMetadata("Link").ShouldBeFalse();
            item.Xml.Metadata.Count.ShouldBe(0);

            item.SetMetadataValue("Link", "c");
            item.DirectMetadataCount.ShouldBe(1);
            item.GetMetadataValue("LINK").ShouldBe("c");
        }

        /// <summary>
        /// A DirectMetadata view is live, but each enumerator sees a stable snapshot.
        /// </summary>
        [Fact]
        public void DirectMetadataViewIsLiveAndEnumeratesSnapshot()
        {
            ProjectItem item = GetOneItemFromFragment(CreateItemWithMetadata(2));

            ICollection<ProjectMetadata> collection = item.DirectMetadata.ShouldBeAssignableTo<ICollection<ProjectMetadata>>();
            collection.IsReadOnly.ShouldBeTrue();
            Should.Throw<InvalidOperationException>(() => collection.Add(item.GetMetadata("m0")));

            using IEnumerator<ProjectMetadata> enumerator = collection.GetEnumerator();
            item.SetMetadataValue("added", "x");
            item.RemoveMetadata("m1").ShouldBeTrue();
            collection.Count.ShouldBe(2);
            collection.Select(m => m.Name).ShouldBe(["m0", "added"]);

            int enumerated = 0;
            while (enumerator.MoveNext())
            {
                enumerator.Current.Name.ShouldBe("m" + enumerated);
                enumerated++;
            }

            enumerated.ShouldBe(2);
            item.RemoveMetadata("m0").ShouldBeTrue();
            item.RemoveMetadata("added").ShouldBeTrue();
            collection.Count.ShouldBe(0);
            item.SetMetadataValue("new", "y");
            collection.Select(m => m.Name).ShouldBe(["new"]);
        }

        [Fact]
        public void DirectMetadataViewFollowsPromotionAndRemoval()
        {
            ProjectItem item = GetOneItemFromFragment(CreateItemWithMetadata(8));
            ICollection<ProjectMetadata> direct = item.DirectMetadata.ShouldBeAssignableTo<ICollection<ProjectMetadata>>();

            item.SetMetadataValue("M8", "v8");
            direct.Count.ShouldBe(9);
            item.GetMetadataValue("m8").ShouldBe("v8");
            ProjectMetadata existing = item.GetMetadata("m0");
            item.SetMetadataValue("M0", "updated").ShouldBeSameAs(existing);
            existing.Name.ShouldBe("m0");
            item.GetMetadataValue("M0").ShouldBe("updated");

            using IEnumerator<ProjectMetadata> enumerator = direct.GetEnumerator();
            item.RemoveMetadata("M1").ShouldBeTrue();
            item.RemoveMetadata("m3").ShouldBeTrue();
            item.GetMetadata("m1").ShouldBeNull();
            direct.Count.ShouldBe(7);
            item.DirectMetadataCount.ShouldBe(7);
            item.SetMetadataValue("newA", "a");
            item.SetMetadataValue("newB", "b");
            item.SetMetadataValue("m9", "v9");
            item.DirectMetadataCount.ShouldBe(10);
            direct.Select(m => m.Name).ShouldBe(["m0", "newB", "m2", "newA", "m4", "m5", "m6", "m7", "M8", "m9"]);

            List<string> originalNames = [];
            while (enumerator.MoveNext())
            {
                originalNames.Add(enumerator.Current.Name);
            }

            originalNames.ShouldBe(["m0", "m1", "m2", "m3", "m4", "m5", "m6", "m7", "M8"]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(8)]
        [InlineData(9)]
        public void DirectMetadataXmlRenameRequiresReevaluation(int metadataCount)
        {
            ProjectItem item = GetOneItemFromFragment(CreateItemWithMetadata(metadataCount));
            ProjectMetadata metadata = item.GetMetadata("m0");
            metadata.Xml.Name = "Renamed";

            item.GetMetadata("m0").ShouldBeNull();
            item.GetMetadata("Renamed").ShouldBeNull();
            item.RemoveMetadata("Renamed").ShouldBeFalse();
            item.DirectMetadata.First().ShouldBeSameAs(metadata);
            item.DirectMetadata.First().Name.ShouldBe("Renamed");
            item.DirectMetadataCount.ShouldBe(metadataCount);

            if (metadataCount == 8)
            {
                item.SetMetadataValue("extra", "value");
                item.GetMetadata("Renamed").ShouldBeNull();
                item.DirectMetadataCount.ShouldBe(9);
            }

            item.Project.ReevaluateIfNecessary();
            ProjectItem reevaluated = item.Project.GetItems("i").Single();
            reevaluated.GetMetadataValue("Renamed").ShouldBe("v0");
        }

        /// <summary>
        /// Removing direct metadata keeps the enumeration order of the hash set that was used previously:
        /// a removed metadatum leaves a hole, and new metadata fill the most recently freed hole first.
        /// Cloning an item, or removing all metadata, makes the order compact again.
        /// </summary>
        [Fact]
        public void DirectMetadataOrderAfterRemoveMatchesPreviousOrder()
        {
            ProjectItem item = GetOneItemFromFragment("<i Include='i1'><a>1</a><b>2</b><c>3</c></i>");

            item.RemoveMetadata("a").ShouldBeTrue();
            item.RemoveMetadata("c").ShouldBeTrue();
            item.DirectMetadata.Select(m => m.Name).ShouldBe(["b"]);
            item.DirectMetadataCount.ShouldBe(1);

            item.SetMetadataValue("d", "4");
            item.SetMetadataValue("e", "5");
            item.DirectMetadata.Select(m => m.Name).ShouldBe(["e", "b", "d"]);

            item.SetMetadataValue("f", "6");
            item.RemoveMetadata("b").ShouldBeTrue();
            item.SetMetadataValue("B", "7");
            item.DirectMetadata.Select(m => m.Name).ShouldBe(["e", "B", "d", "f"]);
            item.GetMetadataValue("b").ShouldBe("7");

            item.RemoveMetadata("e").ShouldBeTrue();
            item.Metadata.Select(m => m.Name).ShouldBe(["B", "d", "f"]);

            ProjectItem clone = new ProjectItemFactory(item.Project, item.Xml).CreateItem(item, item.Project.FullPath);
            clone.DirectMetadata.Select(m => m.Name).ShouldBe(["B", "d", "f"]);
            clone.SetMetadataValue("g", "8");
            clone.DirectMetadata.Select(m => m.Name).ShouldBe(["B", "d", "f", "g"]);
        }

        /// <summary>
        /// Random sequences of set and remove operations give the same direct metadata, in the same
        /// order, as the <see cref="PropertyDictionary{T}"/> that was used previously.
        /// </summary>
        [Fact]
        public void DirectMetadataMatchesPropertyDictionaryBehavior()
        {
            string[] names = ["Link", "SubType", "Generator", "CopyToOutputDirectory", "DependentUpon", "Visible"];
            Random random = new Random(12345);
            ProjectItem item = GetOneItemFromFragment("<i Include='i1'/>");
            PropertyDictionary<ProjectMetadata> expected = new PropertyDictionary<ProjectMetadata>();

            for (int i = 0; i < 2000; i++)
            {
                string name = names[random.Next(names.Length)];
                name = random.Next(2) == 0 ? name : name.ToUpperInvariant();

                if (random.Next(3) == 0)
                {
                    item.RemoveMetadata(name).ShouldBe(expected.Remove(name));
                }
                else
                {
                    ProjectMetadata metadatum = item.SetMetadataValue(name, i.ToString());
                    expected.Set(metadatum);
                }

                item.DirectMetadata.ShouldBe(expected.ToList<ProjectMetadata>());
                item.DirectMetadataCount.ShouldBe(expected.Count);
            }
        }

        /// <summary>
        /// Creating an item from another item deep-clones all direct metadata.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(12)]
        public void CreateItemFromSourceClonesDirectMetadata(int metadataCount)
        {
            ProjectItem source = GetOneItemFromFragment(CreateItemWithMetadata(metadataCount));
            ProjectItemFactory factory = new ProjectItemFactory(source.Project, source.Xml);

            ProjectItem clone = factory.CreateItem(source, source.Project.FullPath);

            clone.DirectMetadataCount.ShouldBe(metadataCount);
            ProjectMetadata[] sourceMetadata = source.DirectMetadata.ToArray();
            ProjectMetadata[] cloneMetadata = clone.DirectMetadata.ToArray();
            for (int i = 0; i < metadataCount; i++)
            {
                cloneMetadata[i].ShouldNotBeSameAs(sourceMetadata[i]);
                cloneMetadata[i].Xml.ShouldBeSameAs(sourceMetadata[i].Xml);
                cloneMetadata[i].Name.ShouldBe(sourceMetadata[i].Name);
                cloneMetadata[i].EvaluatedValue.ShouldBe(sourceMetadata[i].EvaluatedValue);
            }

            source.SetMetadataValue("M0", "changed");
            clone.GetMetadataValue("m0").ShouldBe("v0");
        }

        /// <summary>
        /// Readers on other threads always see a consistent set of direct metadata
        /// while one thread adds and removes metadata.
        /// </summary>
        [Fact]
        public void DirectMetadataConcurrentReadsDuringWrites()
        {
            ProjectItem item = GetOneItemFromFragment("<i Include='i1'><Link>l</Link><SubType>Code</SubType></i>");
            using CancellationTokenSource cts = new();
            using CountdownEvent readersReady = new(2);
            using ManualResetEventSlim writesStarted = new();
            using ManualResetEventSlim readCompleted = new();

            Task[] readers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            {
                readersReady.Signal();
                writesStarted.Wait();
                while (!cts.IsCancellationRequested)
                {
                    item.GetMetadataValue("LINK").ShouldBe("l");
                    item.HasMetadata("subtype").ShouldBeTrue();
                    item.GetMetadata("Link").ShouldNotBeNull();

                    int count = 0;
                    foreach (ProjectMetadata metadatum in item.DirectMetadata)
                    {
                        metadatum.ShouldNotBeNull();
                        count++;
                    }

                    count.ShouldBeInRange(2, 3);
                    readCompleted.Set();
                }
            })).ToArray();

            try
            {
                readersReady.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
                item.SetMetadataValue("Extra", "0");
                writesStarted.Set();
                readCompleted.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
                item.RemoveMetadata("Extra").ShouldBeTrue();
                for (int i = 1; i < 2000; i++)
                {
                    item.SetMetadataValue("Extra", i.ToString());
                    item.RemoveMetadata("Extra").ShouldBeTrue();
                }
            }
            finally
            {
                cts.Cancel();
                writesStarted.Set();
                Task.WaitAll(readers);
            }

            item.DirectMetadataCount.ShouldBe(2);
        }

#if NET
        /// <summary>
        /// Items with one or two direct metadata must not pay for a full dictionary and lock per item.
        /// Before the compact representation, cloning an item with one metadatum allocated more than
        /// 344 bytes of dictionary and lock scaffolding alone.
        /// </summary>
        [Fact]
        public void CreateItemWithFewMetadataAllocatesLittle()
        {
            ProjectItem source = GetOneItemFromFragment("<i Include='i1'><Link>l</Link></i>");
            ProjectItemFactory factory = new ProjectItemFactory(source.Project, source.Xml);
            const int iterations = 50_000;
            ProjectItem[] items = new ProjectItem[iterations];

            factory.CreateItem(source, source.Project.FullPath);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
            {
                items[i] = factory.CreateItem(source, source.Project.FullPath);
            }

            long perItem = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
            items[iterations - 1].GetMetadataValue("Link").ShouldBe("l");
            perItem.ShouldBeLessThan(344);
        }
#endif

        private static string CreateItemWithMetadata(int metadataCount)
            => "<i Include='i1'>" + string.Concat(Enumerable.Range(0, metadataCount).Select(i => $"<m{i}>v{i}</m{i}>")) + "</i>";

        /// <summary>
        /// Get items of item type "i" with using the item xml fragment passed in
        /// </summary>
        private static IList<ProjectItem> GetItemsFromFragment(string fragment)
        {
            string content = String.Format(
                ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <ItemGroup>
                            {0}
                        </ItemGroup>
                    </Project>
                "),
                 fragment);

            IList<ProjectItem> items = GetItems(content);
            return items;
        }

        /// <summary>
        /// Get the item of type "i" using the item Xml fragment provided.
        /// If there is more than one, fail.
        /// </summary>
        private static ProjectItem GetOneItemFromFragment(string fragment)
        {
            IList<ProjectItem> items = GetItemsFromFragment(fragment);

            Assert.Single(items);
            return items[0];
        }

        /// <summary>
        /// Get the items of type "i" in the project provided
        /// </summary>
        private static IList<ProjectItem> GetItems(string content)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement projectXml = projectRootElementFromString.Project;

            Project project = new Project(projectXml);
            IList<ProjectItem> item = Helpers.MakeList(project.GetItems("i"));

            return item;
        }
    }
}
