// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;
using Evaluator = Microsoft.Build.Evaluation.LazyItemEvaluator<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance, Microsoft.Build.Execution.ProjectMetadataInstance, Microsoft.Build.Execution.ProjectItemDefinitionInstance>;

namespace Microsoft.Build.UnitTests.Evaluation;

public class OrderedItemDataCollection_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const int Boundary = Evaluator.OrderedItemDataCollection.ItemDataChunkList.ChunkSize;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(31)]
    public void SmallListsAndRepeatedSnapshotsUseOnlyTheOriginalTree(int count)
    {
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        builder.ReserveAppend(count);
        for (int i = 0; i < count; i++)
        {
            builder.Add(Data(i));
        }
        ChunkBuilder(builder).ShouldBeNull();
        for (int repeat = 0; repeat < 5; repeat++)
        {
            var snapshot = builder.ToImmutable();
            ChunkList(snapshot).ShouldBeNull();
            Field(snapshot, "_list").ShouldBeOfType<ImmutableList<Evaluator.ItemData>>();
            snapshot.ToBuilder().Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, count));
        }
    }

    [Fact]
    public void IncrementalAndKnownBatchPromotionPreserveOldVersionBranches()
    {
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        for (int i = 0; i < Boundary - 1; i++)
        {
            builder.Add(Data(i));
        }
        var small = builder.ToImmutable();
        builder.Add(Data(Boundary - 1));
        ChunkBuilder(builder).ShouldNotBeNull();
        for (int i = Boundary; i < Boundary * 2 + 2; i++)
        {
            builder.Add(Data(i));
        }
        var large = builder.ToImmutable();
        builder[Boundary] = Data(-1);
        var oldBranch = small.ToBuilder();
        oldBranch.ReserveAppend(Boundary + 5);
        ChunkBuilder(oldBranch).ShouldNotBeNull();
        for (int i = 0; i < Boundary + 5; i++)
        {
            oldBranch.Add(Data(1000 + i));
        }
        small.ToBuilder().Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, Boundary - 1));
        large.ToBuilder().Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, Boundary * 2 + 2));
        oldBranch.Select(i => i.ElementOrder).ShouldBe(
            Enumerable.Range(0, Boundary - 1).Concat(Enumerable.Range(1000, Boundary + 5)));
        builder.Clear();
        ChunkBuilder(builder).ShouldBeNull();
        builder.ReserveAppend(2);
        builder.Add(Data(100));
        builder.Add(Data(100));
        ChunkBuilder(builder).ShouldBeNull();
        builder.Select(i => i.ElementOrder).ShouldBe([100, 100]);
        large.ToBuilder().Count.ShouldBe(Boundary * 2 + 2);
    }

    [Fact]
    public void KnownLargeBatchPromotesBeforeBuildingThePrefix()
    {
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        builder.ReserveAppend(Boundary);
        ChunkBuilder(builder).ShouldNotBeNull();
        Field(builder, "_listBuilder").ShouldBeNull();
        builder.Count.ShouldBe(0);
        for (int i = 0; i < Boundary; i++)
        {
            builder.Add(Data(i));
        }
        builder.Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, Boundary));
        var chunks = Chunks(builder.ToImmutable());
        chunks.ShouldHaveSingleItem();
        chunks[0].Items.Length.ShouldBe(Boundary);
    }

    [Fact]
    public void PromotionAndClearInvalidateExistingBuilderEnumerators()
    {
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        for (int i = 0; i < Boundary - 1; i++)
        {
            builder.Add(Data(i));
        }
        using IEnumerator<Evaluator.ItemData> treeEnumerator = ((IEnumerable<Evaluator.ItemData>)builder).GetEnumerator();
        treeEnumerator.MoveNext().ShouldBeTrue();
        builder.Add(Data(Boundary - 1));
        Should.Throw<InvalidOperationException>(() => treeEnumerator.MoveNext());
        using IEnumerator<Evaluator.ItemData> chunkEnumerator = ((IEnumerable<Evaluator.ItemData>)builder).GetEnumerator();
        chunkEnumerator.MoveNext().ShouldBeTrue();
        builder.Clear();
        Should.Throw<InvalidOperationException>(() => chunkEnumerator.MoveNext());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChunkEditsInvalidateEnumeratorsEvenBeforeFirstMoveNext(bool startEnumeration)
    {
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        builder.ReserveAppend(Boundary);
        for (int i = 0; i < Boundary; i++)
        {
            builder.Add(Data(i));
        }
        using IEnumerator<Evaluator.ItemData> enumerator = ((IEnumerable<Evaluator.ItemData>)builder).GetEnumerator();
        if (startEnumeration)
        {
            enumerator.MoveNext().ShouldBeTrue();
        }
        builder[Boundary - 1] = Data(-1);
        Should.Throw<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void DeletionHeavyIndexedAndIdentityRemovalAgreeWithListOracle(int percent)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ProjectCollection collection = new();
        ProjectInstance project = new(ProjectRootElement.Create(collection));
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        List<Evaluator.ItemData> original = [];
        const int count = 1024;
        builder.ReserveAppend(count);
        for (int i = 0; i < count; i++)
        {
            string key = $"item{i / 2}";
            ProjectItemInstance item = project.AddItem("I", key);
            var data = new Evaluator.ItemData(item, null!, i, i % 7 != 0, key);
            builder.Add(data);
            original.Add(data);
        }
        var before = builder.ToImmutable();
        HashSet<string> remove = new(original.Where(i => i.ElementOrder / 2 % 100 < percent)
            .Select(i => i.NormalizedItemValue));
        List<Evaluator.ItemData> expected = original.Where(i => !remove.Contains(i.NormalizedItemValue)).ToList();
        builder.RemoveAll(remove);
        builder.Select(i => (i.ElementOrder, i.ConditionResult)).ShouldBe(expected.Select(i => (i.ElementOrder, i.ConditionResult)));
        ChunkBuilder(builder).ShouldNotBeNull();
        var deleted = builder.ToImmutable();
        before.ToBuilder().Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, count));
        VerifyClearedTail(deleted);
        var branch = deleted.ToBuilder();
        if (expected.Count > 0)
        {
            Evaluator.ItemData value = expected[0];
            var replacement = new Evaluator.ItemData(project.AddItem("I", value.Item.EvaluatedInclude),
                null!, -1, false, value.NormalizedItemValue);
            branch[0] = replacement;
            bool found = false;
            foreach (ProjectItemInstance item in branch.Dictionary[value.NormalizedItemValue])
            {
                found |= ReferenceEquals(item, replacement.Item);
            }
            found.ShouldBeTrue();
        }
        branch.RemoveAll(new HashSet<ProjectItemInstance>(branch.Select(i => i.Item)));
        branch.Count.ShouldBe(0);
        branch.Add(new Evaluator.ItemData(project.AddItem("I", "tail"), null!, 5000, false, "tail"));
        branch.Add(new Evaluator.ItemData(project.AddItem("I", "tail"), null!, 5001, true, "tail"));
        branch.Select(i => i.ElementOrder).ShouldBe([5000, 5001]);
        int tailCount = 0;
        foreach (ProjectItemInstance item in branch.Dictionary["tail"])
        {
            item.EvaluatedInclude.ShouldBe("tail");
            tailCount++;
        }
        tailCount.ShouldBe(2);
        deleted.ToBuilder().Select(i => i.ElementOrder).ShouldBe(expected.Select(i => i.ElementOrder));
        before.ToBuilder().Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IntermediateCopiesPreserveMetadataAfterUpdateRemoveAndAppend(bool instance)
    {
        string includes = string.Join(";", Enumerable.Range(0, 150).Select(i => $"item{i}"));
        string xml = $$"""
            <Project><ItemGroup>
              <I Include="{{includes}}"><M>before</M></I>
              <Before Include="@(I)" />
              <I Update="item0;item31;item32;item64;item149"><M>after</M></I>
              <Updated Include="@(I)" />
              <I Remove="item0;item32;item149" />
              <I Include="tail;tail"><M>tail</M></I>
              <Final Include="@(I)" />
              <I Include="false" Condition="'a' == 'b'" />
            </ItemGroup></Project>
            """;
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ProjectCollection collection = new();
        using XmlReader reader = XmlReader.Create(new StringReader(xml));
        ProjectRootElement root = ProjectRootElement.Create(reader, collection);
        string[] itemTypes = ["Before", "Updated", "Final"];
        Dictionary<string, (string Include, string M)[]> evaluated;
        if (instance)
        {
            ProjectInstance project = new(root);
            evaluated = itemTypes.ToDictionary(t => t,
                t => project.GetItems(t).Select(i => (i.EvaluatedInclude, i.GetMetadataValue("M"))).ToArray());
            project.GetItems("I").ShouldNotContain(i => i.EvaluatedInclude == "false");
        }
        else
        {
            Project project = new(root, null, null, collection);
            evaluated = itemTypes.ToDictionary(t => t,
                t => project.GetItems(t).Select(i => (i.EvaluatedInclude, i.GetMetadataValue("M"))).ToArray());
            project.ItemsIgnoringCondition.ShouldContain(i => i.EvaluatedInclude == "false");
        }
        evaluated["Before"].Length.ShouldBe(150);
        evaluated["Before"].ShouldAllBe(i => i.M == "before");
        evaluated["Updated"].Where(i => i.M == "after").Select(i => i.Include)
            .ShouldBe(["item0", "item31", "item32", "item64", "item149"]);
        evaluated["Final"].Select(i => i.Include)
            .ShouldBe(Enumerable.Range(0, 150).Where(i => i != 0 && i != 32 && i != 149).Select(i => $"item{i}").Concat(["tail", "tail"]));
        evaluated["Final"].Where(i => i.Include is "item31" or "item64").ShouldAllBe(i => i.M == "after");
        evaluated["Final"].Where(i => i.Include == "tail").ShouldAllBe(i => i.M == "tail");
    }

    [Fact]
    public void SnapshotsShareUntouchedChunksAndIsolateBranchEdits()
    {
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        builder.ReserveAppend(96);
        for (int i = 0; i < 96; i++)
        {
            builder.Add(Data(i));
        }
        var before = builder.ToImmutable();
        builder.Add(Data(96));
        builder[33] = Data(-33);
        var after = builder.ToImmutable();
        var branch = before.ToBuilder();
        branch[65] = Data(-65);
        branch.Clear();
        before.ToBuilder().Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, 96));
        after.ToBuilder().Select(i => i.ElementOrder).ShouldBe(Enumerable.Range(0, 97).Select(i => i == 33 ? -33 : i));
        var beforeChunks = Chunks(before);
        var afterChunks = Chunks(after);
        afterChunks[0].Items.ShouldBeSameAs(beforeChunks[0].Items);
        afterChunks[1].Items.ShouldNotBeSameAs(beforeChunks[1].Items);
        afterChunks[2].Items.ShouldBeSameAs(beforeChunks[2].Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(96)]
    public void RandomizedRemovalIndexAndSnapshotsAgreeWithList(int initialCount)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ProjectCollection collection = new();
        ProjectInstance project = new(ProjectRootElement.Create(collection));
        var builder = Evaluator.OrderedItemDataCollection.CreateBuilder();
        List<Evaluator.ItemData> expected = [];
        List<(Evaluator.OrderedItemDataCollection Snapshot, int[] Expected)> snapshots = [];
        Random random = new(1234);
        int sequence = 0;
        builder.ReserveAppend(initialCount);
        for (int i = 0; i < initialCount; i++)
        {
            string include = $"item{random.Next(25)}";
            var data = new Evaluator.ItemData(project.AddItem("I", include), null!, sequence++, true, include);
            builder.Add(data);
            expected.Add(data);
        }
        for (int operation = 0; operation < 500; operation++)
        {
            int choice = random.Next(5);
            if (choice <= 1 || expected.Count == 0)
            {
                string include = $"item{random.Next(25)}";
                ProjectItemInstance item = project.AddItem("I", include);
                var data = new Evaluator.ItemData(item, null!, sequence++, true, include);
                builder.Add(data);
                expected.Add(data);
            }
            else if (choice == 2)
            {
                string path = $"item{random.Next(25)}";
                builder.RemoveAll(new HashSet<string> { path });
                expected.RemoveAll(i => i.NormalizedItemValue == path);
            }
            else if (choice == 3)
            {
                int index = random.Next(expected.Count);
                var old = expected[index];
                var replacement = new Evaluator.ItemData(
                    project.AddItem("I", old.Item.EvaluatedInclude), null!, sequence++, true, old.NormalizedItemValue);
                builder[index] = replacement;
                expected[index] = replacement;
            }
            else
            {
                snapshots.Add((builder.ToImmutable(), expected.Select(i => i.ElementOrder).ToArray()));
                builder = snapshots[^1].Snapshot.ToBuilder();
            }
            builder.Select(i => i.ElementOrder).ShouldBe(expected.Select(i => i.ElementOrder));
        }
        foreach (var snapshot in snapshots)
        {
            snapshot.Snapshot.ToBuilder().Select(i => i.ElementOrder).ShouldBe(snapshot.Expected);
        }
        builder.RemoveAll(new HashSet<ProjectItemInstance>(expected.Select(i => i.Item)));
        builder.Count.ShouldBe(0);
        builder.Add(new Evaluator.ItemData(project.AddItem("I", "after"), null!, sequence, false, "after"));
        builder.Count.ShouldBe(1);
    }

    private static void VerifyClearedTail(Evaluator.OrderedItemDataCollection snapshot)
    {
        var chunks = Chunks(snapshot);
        int count = snapshot.ToBuilder().Count;
        if (count == 0)
        {
            chunks.ShouldBeEmpty();
        }
        else if (count % Boundary != 0)
        {
            foreach (var item in chunks[^1].Items.Skip(count % Boundary))
            {
                item.Item.ShouldBeNull();
                item.OriginatingItemElement.ShouldBeNull();
            }
        }
    }

    private static Evaluator.OrderedItemDataCollection.ItemDataChunkList.Chunk[] Chunks(Evaluator.OrderedItemDataCollection snapshot)
    {
        object list = ChunkList(snapshot).ShouldNotBeNull();
        return ((IEnumerable<Evaluator.OrderedItemDataCollection.ItemDataChunkList.Chunk>)Field(list, "_chunks")!).ToArray();
    }

    private static Evaluator.ItemData Data(int order) => new(null!, null!, order, true);
    private static object? Field(object receiver, string name) => receiver.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(receiver);
    private static object? ChunkBuilder(object builder) => Field(builder, "_chunkBuilder");
    private static object? ChunkList(object snapshot) => Field(snapshot, "_chunkList");
}
