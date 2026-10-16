// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public sealed class ImmutablePieceList_Tests
{
    [Fact]
    public void EmptySnapshotHasNoPiecesOrItems()
    {
        ImmutablePieceList<int> empty = ImmutablePieceList<int>.Empty;
        empty.Count.ShouldBe(0);
        empty.PieceCount.ShouldBe(0);
        empty.Depth.ShouldBe(0);
        empty.ShouldBeEmpty();
        var editor = empty.ToEditor();
        editor.AddRange([]);
        editor.RemoveRange(0, 0);
        editor.RemoveAll(static _ => true).ShouldBe(0);
        editor.UpdateAll(static _ => true, static _ => throw new InvalidOperationException()).ShouldBe(0);
        editor.Clear();
        editor.ToImmutable().ShouldBeSameAs(empty);
        ((IEnumerable)empty).GetEnumerator().MoveNext().ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(127)]
    [InlineData(512)]
    [InlineData(4097)]
    public void AppendsPreserveOrderAcrossChunks(int count)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        int[] expected = new int[count];
        for (int i = 0; i < count; i++)
        {
            editor.Add(i);
            expected[i] = i;
        }

        AssertContents(editor, expected);
        ImmutablePieceList<int> snapshot = editor.ToImmutable();
        AssertContents(snapshot, expected);
        snapshot.PieceCount.ShouldBeInRange(1, count);
        editor.ToImmutable().ShouldBeSameAs(snapshot);
    }

    [Fact]
    public void AddRangeCopiesCallerStorage()
    {
        int[] values = [1, 2, 3];
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange(values);
        ImmutablePieceList<int> snapshot = editor.ToImmutable();
        values[0] = 99;

        AssertContents(editor, [1, 2, 3]);
        AssertContents(snapshot, [1, 2, 3]);
    }

    [Fact]
    public void LookupHandlesTheFirstPieceAndExclusiveEndBoundaries()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2, 3, 4, 5, 6, 7]);
        ImmutablePieceList<int> snapshot = editor.ToImmutable();
        snapshot.PieceCount.ShouldBe(2);
        int[] indices = [0, 3, 4, 7];
        foreach (int index in indices)
        {
            snapshot[index].ShouldBe(index);
            snapshot.ItemRef(index).ShouldBe(index);
            editor[index].ShouldBe(index);
        }

        editor[6] = 60;
        AssertContents(editor, [0, 1, 2, 3, 4, 5, 60, 7]);
        AssertContents(snapshot, [0, 1, 2, 3, 4, 5, 6, 7]);
    }

    [Fact]
    public void AppendsCoalesceNewStorageWithoutExpandingTheBaseSnapshot()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.Add(1);
        ImmutablePieceList<int> first = editor.ToImmutable();
        editor.AddRange([2, 3]);
        ImmutablePieceList<int> second = editor.ToImmutable();

        first.PieceCount.ShouldBe(1);
        second.PieceCount.ShouldBe(2);
        second.Depth.ShouldBe(1);
        AssertContents(first, [1]);
        AssertContents(second, [1, 2, 3]);
        SharesElementStorage(first, 0, second, 0).ShouldBeTrue();
    }

    [Fact]
    public void ReplacementsSplitPiecesAndShareUnchangedStorage()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([10, 20, 30]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor[1] = 200;
        ImmutablePieceList<int> changed = editor.ToImmutable();

        AssertContents(original, [10, 20, 30]);
        AssertContents(changed, [10, 200, 30]);
        changed.PieceCount.ShouldBe(3);
        changed.Depth.ShouldBe(1);
        SharesElementStorage(original, 0, changed, 0).ShouldBeTrue();
        SharesElementStorage(original, 2, changed, 2).ShouldBeTrue();
        SharesElementStorage(original, 1, changed, 1).ShouldBeFalse();
    }

    [Fact]
    public void RemovingTheMiddleItemPublishesTwoRangesOfThePreviousSnapshot()
    {
        var editor = ImmutablePieceList<string>.Empty.ToEditor();
        editor.AddRange(["a", "b", "c"]);
        ImmutablePieceList<string> original = editor.ToImmutable();
        editor.PieceCount.ShouldBe(1);
        editor.RemoveAt(1);
        ImmutablePieceList<string> removed = editor.ToImmutable();

        removed.PieceCount.ShouldBe(2);
        removed.Depth.ShouldBe(1);
        AssertContents(original, ["a", "b", "c"]);
        AssertContents(removed, ["a", "c"]);
        SharesElementStorage(original, 0, removed, 0).ShouldBeTrue();
        SharesElementStorage(original, 2, removed, 1).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADeltaOverAFragmentedSnapshotDoesNotCopyItsPieceIndex(bool removeByPredicate)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        for (int i = 0; i < 1024; i++)
        {
            editor.Add(i);
        }

        editor.RemoveAll(static value => value % 2 == 0);
        ImmutablePieceList<int> fragmented = editor.ToImmutable();
        fragmented.PieceCount.ShouldBe(512);
        editor.PieceCount.ShouldBe(1);
        fragmented.ToEditor().PieceCount.ShouldBe(1);

        if (removeByPredicate)
        {
            editor.RemoveAll(static value => value == 513).ShouldBe(1);
        }
        else
        {
            editor.RemoveAt(256);
        }

        ImmutablePieceList<int> removed = editor.ToImmutable();
        removed.PieceCount.ShouldBe(2);
        removed.Depth.ShouldBe(fragmented.Depth + 1);
        removed.Count.ShouldBe(511);
        for (int i = 0; i < removed.Count; i++)
        {
            int sourceIndex = i < 256 ? i : i + 1;
            removed[i].ShouldBe((sourceIndex * 2) + 1);
            SharesElementStorage(fragmented, sourceIndex, removed, i).ShouldBeTrue();
        }
    }

    [Fact]
    public void EachPublishedDeltaBecomesTheNextBaseSnapshot()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        original.Depth.ShouldBe(0);
        editor.ToImmutable().ShouldBeSameAs(original);

        editor.RemoveAt(1);
        ImmutablePieceList<int> removed = editor.ToImmutable();
        removed.PieceCount.ShouldBe(2);
        removed.Depth.ShouldBe(1);
        editor.PieceCount.ShouldBe(1);
        editor.ToImmutable().ShouldBeSameAs(removed);

        editor.Add(3);
        ImmutablePieceList<int> added = editor.ToImmutable();
        added.PieceCount.ShouldBe(2);
        added.Depth.ShouldBe(2);
        AssertContents(original, [0, 1, 2]);
        AssertContents(removed, [0, 2]);
        AssertContents(added, [0, 2, 3]);
    }

    [Fact]
    public void NestedSnapshotRangesClipBothEndsAcrossAncestorPieces()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.RemoveRange(2, 3);
        ImmutablePieceList<int> first = editor.ToImmutable();
        editor.RemoveRange(1, 3);
        ImmutablePieceList<int> second = editor.ToImmutable();
        editor[1] = 70;
        ImmutablePieceList<int> third = editor.ToImmutable();
        editor.RemoveAt(0);
        ImmutablePieceList<int> fourth = editor.ToImmutable();

        AssertContents(original, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
        AssertContents(first, [0, 1, 5, 6, 7, 8, 9]);
        AssertContents(second, [0, 7, 8, 9]);
        AssertContents(third, [0, 70, 8, 9]);
        AssertContents(fourth, [70, 8, 9]);
        fourth.Depth.ShouldBe(4);
        fourth.PieceCount.ShouldBe(1);
        SharesElementStorage(original, 8, fourth, 1).ShouldBeTrue();
        SharesElementStorage(original, 9, fourth, 2).ShouldBeTrue();
    }

    [Fact]
    public void ConsecutiveReplacementsCoalesceIntoRuns()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        for (int i = 0; i < editor.Count; i++)
        {
            editor[i] = i + 10;
        }

        ImmutablePieceList<int> changed = editor.ToImmutable();
        changed.PieceCount.ShouldBe(1);
        changed.Depth.ShouldBe(0);
        AssertContents(changed, [10, 11, 12, 13]);
        AssertContents(original, [0, 1, 2, 3]);
    }

    [Fact]
    public void RepeatedReplacementsKeepEveryPublishedValue()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, -1]);
        List<ImmutablePieceList<int>> versions = [editor.ToImmutable()];
        for (int value = 1; value <= 100; value++)
        {
            editor[0] = value;
            versions.Add(editor.ToImmutable());
        }

        for (int value = 0; value < versions.Count; value++)
        {
            AssertContents(versions[value], [value, -1]);
            SharesElementStorage(versions[0], 1, versions[value], 1).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void RemoveAtPreservesSurvivorOrderAndStorage(int index)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.RemoveAt(index);
        ImmutablePieceList<int> changed = editor.ToImmutable();

        List<int> expected = [0, 1, 2, 3];
        expected.RemoveAt(index);
        AssertContents(changed, [.. expected]);
        AssertContents(original, [0, 1, 2, 3]);
        for (int i = 0; i < changed.Count; i++)
        {
            SharesElementStorage(original, i < index ? i : i + 1, changed, i).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 0)]
    [InlineData(0, 3)]
    [InlineData(2, 4)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 4)]
    [InlineData(0, 8)]
    public void RemoveRangeHandlesEmptyRangesAndChunkBoundaries(int index, int count)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2, 3, 4, 5, 6, 7]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.RemoveRange(index, count);
        ImmutablePieceList<int> changed = editor.ToImmutable();

        List<int> expected = [0, 1, 2, 3, 4, 5, 6, 7];
        expected.RemoveRange(index, count);
        AssertContents(changed, [.. expected]);
        AssertContents(original, [0, 1, 2, 3, 4, 5, 6, 7]);
        for (int i = 0; i < changed.Count; i++)
        {
            SharesElementStorage(original, i < index ? i : i + count, changed, i).ShouldBeTrue();
        }

        if (count == 0)
        {
            changed.ShouldBeSameAs(original);
        }
    }

    [Fact]
    public void RemovalAcrossReplacementPiecesPreservesEveryVersion()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2, 3, 4, 5, 6, 7]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor[2] = 20;
        editor[5] = 50;
        ImmutablePieceList<int> replaced = editor.ToImmutable();
        editor.RemoveRange(1, 6);
        ImmutablePieceList<int> removed = editor.ToImmutable();

        AssertContents(original, [0, 1, 2, 3, 4, 5, 6, 7]);
        AssertContents(replaced, [0, 1, 20, 3, 4, 50, 6, 7]);
        AssertContents(removed, [0, 7]);
        SharesElementStorage(original, 0, removed, 0).ShouldBeTrue();
        SharesElementStorage(original, 7, removed, 1).ShouldBeTrue();
    }

    [Fact]
    public void BulkUpdatesTraverseOnceAndShareUnchangedRanges()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([0, 1, 2, 3, 4, 5, 6, 7]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        List<int> visited = [];
        List<int> updated = [];

        editor.UpdateAll(
            value =>
            {
                visited.Add(value);
                return value is 2 or 3;
            },
            value =>
            {
                updated.Add(value);
                return value + 100;
            }).ShouldBe(2);

        ImmutablePieceList<int> changed = editor.ToImmutable();
        visited.ShouldBe([0, 1, 2, 3, 4, 5, 6, 7]);
        updated.ShouldBe([2, 3]);
        changed.PieceCount.ShouldBe(3);
        changed.Depth.ShouldBe(1);
        AssertContents(original, [0, 1, 2, 3, 4, 5, 6, 7]);
        AssertContents(changed, [0, 1, 102, 103, 4, 5, 6, 7]);
        SharesElementStorage(original, 0, changed, 0).ShouldBeTrue();
        SharesElementStorage(original, 7, changed, 7).ShouldBeTrue();
        SharesElementStorage(original, 2, changed, 2).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void DenseBulkUpdatesCoalesceAndCountEveryMatch(int increment)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3, 4]);
        ImmutablePieceList<int> original = editor.ToImmutable();

        editor.UpdateAll(static _ => true, value => value + increment).ShouldBe(4);

        ImmutablePieceList<int> changed = editor.ToImmutable();
        changed.ShouldNotBeSameAs(original);
        changed.PieceCount.ShouldBe(1);
        changed.Depth.ShouldBe(0);
        AssertContents(original, [1, 2, 3, 4]);
        AssertContents(changed, [1 + increment, 2 + increment, 3 + increment, 4 + increment]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingBulkUpdateCallbacksDoNotCommitReplacements(bool throwInUpdate)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();

        Should.Throw<FormatException>(() => editor.UpdateAll(
            value =>
            {
                if (!throwInUpdate && value == 3)
                {
                    throw new FormatException();
                }

                return true;
            },
            value =>
            {
                if (throwInUpdate && value == 3)
                {
                    throw new FormatException();
                }

                return value + 10;
            }));

        editor.ToImmutable().ShouldBeSameAs(original);
        AssertContents(editor, [1, 2, 3]);
        editor.Add(4);
        AssertContents(editor, [1, 2, 3, 4]);
        AssertContents(original, [1, 2, 3]);
    }

    [Fact]
    public void BulkUpdateCallbacksCannotOverwriteReentrantEdits()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();

        Should.Throw<InvalidOperationException>(() => editor.UpdateAll(static _ => true, value =>
        {
            editor.Add(4);
            return value + 10;
        }));

        AssertContents(editor, [1, 2, 3, 4]);
        AssertContents(original, [1, 2, 3]);
    }

    [Fact]
    public void BulkUpdatesCanPublishAndReadThePreUpdateSequence()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> published = ImmutablePieceList<int>.Empty;

        editor.UpdateAll(static _ => true, value =>
        {
            editor[0].ShouldBe(1);
            published = editor.ToImmutable();
            return value + 10;
        }).ShouldBe(3);

        AssertContents(published, [1, 2, 3]);
        AssertContents(editor, [11, 12, 13]);
    }

    [Fact]
    public void RemoveAllSharesSurvivingRunsAndRemovesDuplicates()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 2, 3, 2, 4, 5, 6]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.RemoveAll(static value => value % 2 == 0).ShouldBe(5);
        ImmutablePieceList<int> removed = editor.ToImmutable();

        AssertContents(original, [1, 2, 2, 3, 2, 4, 5, 6]);
        AssertContents(removed, [1, 3, 5]);
        SharesElementStorage(original, 0, removed, 0).ShouldBeTrue();
        SharesElementStorage(original, 3, removed, 1).ShouldBeTrue();
        SharesElementStorage(original, 6, removed, 2).ShouldBeTrue();
    }

    [Fact]
    public void DenseReplacementsCoalesceAHighlyFragmentedSequence()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        int[] values = new int[1024];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = i;
        }

        editor.AddRange(values);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.RemoveAll(static value => value % 2 == 0).ShouldBe(512);
        ImmutablePieceList<int> fragmented = editor.ToImmutable();
        fragmented.PieceCount.ShouldBe(fragmented.Count);

        int[] survivors = new int[512];
        int[] replacements = new int[512];
        for (int i = 0; i < editor.Count; i++)
        {
            survivors[i] = (i * 2) + 1;
            replacements[i] = survivors[i] + 10000;
            editor[i] = replacements[i];
        }

        ImmutablePieceList<int> changed = editor.ToImmutable();
        changed.PieceCount.ShouldBeLessThan(fragmented.PieceCount);
        AssertContents(original, values);
        AssertContents(fragmented, survivors);
        AssertContents(changed, replacements);
    }

    [Fact]
    public void RemovingAllElementsReturnsTheSharedEmptySnapshot()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.RemoveAll(static _ => true).ShouldBe(3);
        editor.Count.ShouldBe(0);
        editor.PieceCount.ShouldBe(0);
        editor.ToImmutable().ShouldBeSameAs(ImmutablePieceList<int>.Empty);
        editor.Add(4);

        AssertContents(editor, [4]);
        AssertContents(original, [1, 2, 3]);
    }

    [Fact]
    public void ThrowingPredicatesDoNotCommitPartialRemovals()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();

        Should.Throw<FormatException>(() => editor.RemoveAll(value => value == 3 ? throw new FormatException() : value == 1));

        editor.ToImmutable().ShouldBeSameAs(original);
        AssertContents(editor, [1, 2, 3]);
    }

    [Fact]
    public void PredicatesCannotSilentlyOverwriteReentrantEdits()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();

        Should.Throw<InvalidOperationException>(() => editor.RemoveAll(_ =>
        {
            editor.Add(4);
            return true;
        }));

        AssertContents(editor, [1, 2, 3, 4]);
        AssertContents(original, [1, 2, 3]);
    }

    [Fact]
    public void ClearReleasesTheEditorStateWithoutChangingSnapshots()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.Clear();
        editor.ToImmutable().ShouldBeSameAs(ImmutablePieceList<int>.Empty);
        editor.PieceCount.ShouldBe(0);
        editor.AddRange([10, 20]);

        AssertContents(original, [1, 2, 3]);
        AssertContents(editor, [10, 20]);
    }

    [Fact]
    public void IndependentEditorsCanBranchWhileTheOriginalEditorContinuesAppending()
    {
        var originalEditor = ImmutablePieceList<int>.Empty.ToEditor();
        originalEditor.AddRange([1, 2]);
        ImmutablePieceList<int> original = originalEditor.ToImmutable();
        var left = original.ToEditor();
        var right = original.ToEditor();
        left.ToImmutable().ShouldBeSameAs(original);
        right.ToImmutable().ShouldBeSameAs(original);

        originalEditor.Add(3);
        left.Add(10);
        right.Add(20);
        left[0] = 100;
        right.RemoveAt(1);

        AssertContents(original, [1, 2]);
        AssertContents(originalEditor, [1, 2, 3]);
        AssertContents(left, [100, 2, 10]);
        AssertContents(right, [1, 20]);
        SharesElementStorage(original, 1, left.ToImmutable(), 1).ShouldBeTrue();
        SharesElementStorage(original, 0, right.ToImmutable(), 0).ShouldBeTrue();
    }

    [Fact]
    public void EmptyEditsAndReadsReuseTheSnapshot()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.AddRange([]);
        editor.RemoveRange(1, 0);
        editor.RemoveAll(static _ => false).ShouldBe(0);
        using IEnumerator<int> enumerator = editor.GetEnumerator();
        enumerator.MoveNext().ShouldBeTrue();
        editor.UpdateAll(static _ => false, static _ => throw new InvalidOperationException()).ShouldBe(0);
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(2);
        editor[1].ShouldBe(2);
        AssertContents(editor, [1, 2, 3]);

        editor.ToImmutable().ShouldBeSameAs(original);
    }

    [Fact]
    public void NullableReferencesAreStoredWithoutDeepCopying()
    {
        var editor = ImmutablePieceList<object?>.Empty.ToEditor();
        object first = new();
        object second = new();
        editor.AddRange([first, null, second]);
        ImmutablePieceList<object?> original = editor.ToImmutable();
        editor[0] = null;
        editor.RemoveAt(1);
        ImmutablePieceList<object?> changed = editor.ToImmutable();

        original[0].ShouldBeSameAs(first);
        original[1].ShouldBeNull();
        original[2].ShouldBeSameAs(second);
        changed.Count.ShouldBe(2);
        changed[0].ShouldBeNull();
        changed[1].ShouldBeSameAs(second);
    }

    [Fact]
    public void SnapshotEnumeratorsSupportResetAndBothEnumerableInterfaces()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        editor[1] = 20;
        ImmutablePieceList<int> snapshot = editor.ToImmutable();
        var enumerator = snapshot.GetEnumerator();
        Should.Throw<InvalidOperationException>(() => { _ = enumerator.Current; });
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(1);
        enumerator.Reset();
        Should.Throw<InvalidOperationException>(() => { _ = enumerator.Current; });

        List<int> values = [];
        while (enumerator.MoveNext())
        {
            values.Add(enumerator.Current);
        }

        values.ShouldBe([1, 20, 3]);
        enumerator.MoveNext().ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => { _ = enumerator.Current; });
        enumerator.Dispose();

        IEnumerable<int> generic = snapshot;
        int[] genericItems = [.. generic];
        genericItems.ShouldBe([1, 20, 3]);
        List<int> nongenericItems = [];
        foreach (int item in (IEnumerable)snapshot)
        {
            nongenericItems.Add(item);
        }

        nongenericItems.ShouldBe([1, 20, 3]);
    }

    [Fact]
    public void DefaultEnumeratorIsEmptyAndCanBeReset()
    {
        ImmutablePieceList<int>.Enumerator enumerator = default;
        enumerator.MoveNext().ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => { _ = enumerator.Current; });
        enumerator.Reset();
        enumerator.MoveNext().ShouldBeFalse();
        enumerator.Dispose();
        enumerator.MoveNext().ShouldBeFalse();
    }

    [Fact]
    public void EmptyEnumeratorCanBeReset()
    {
        using var enumerator = ImmutablePieceList<int>.Empty.GetEnumerator();
        enumerator.MoveNext().ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => { _ = enumerator.Current; });
        enumerator.Reset();
        enumerator.MoveNext().ShouldBeFalse();
        enumerator.Dispose();
    }

    [Fact]
    public void DeepSnapshotHistoriesUseIterativeIndexingAndEnumeration()
    {
        const int versionCount = 20000;
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.Add(-1);
        ImmutablePieceList<int> first = editor.ToImmutable();
        ImmutablePieceList<int> last = first;
        for (int version = 1; version <= versionCount; version++)
        {
            editor.Add(version);
            last = editor.ToImmutable();
        }

        last.Depth.ShouldBe(versionCount);
        last.PieceCount.ShouldBe(2);
        last.Count.ShouldBe(versionCount + 1);
        last[0].ShouldBe(-1);
        last[versionCount].ShouldBe(versionCount);
        SharesElementStorage(first, 0, last, 0).ShouldBeTrue();

        using var enumerator = last.GetEnumerator();
        for (int pass = 0; pass < 2; pass++)
        {
            int index = 0;
            while (enumerator.MoveNext())
            {
                enumerator.Current.ShouldBe(index == 0 ? -1 : index);
                index++;
            }

            index.ShouldBe(versionCount + 1);
            enumerator.Reset();
        }

        editor.UpdateAll(static value => value == versionCount / 2, static value => -value).ShouldBe(1);
        ImmutablePieceList<int> changed = editor.ToImmutable();
        changed.PieceCount.ShouldBe(3);
        changed[versionCount / 2].ShouldBe(-(versionCount / 2));
        last[versionCount / 2].ShouldBe(versionCount / 2);
        SharesElementStorage(first, 0, changed, 0).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditorEnumerationSurvivesPublishingItsDelta(bool startEnumeration)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        using IEnumerator<int> enumerator = editor.GetEnumerator();
        List<int> values = [];
        if (startEnumeration)
        {
            enumerator.MoveNext().ShouldBeTrue();
            values.Add(enumerator.Current);
        }

        ImmutablePieceList<int> published = editor.ToImmutable();
        while (enumerator.MoveNext())
        {
            values.Add(enumerator.Current);
        }

        values.ShouldBe([1, 2, 3]);
        editor.ToImmutable().ShouldBeSameAs(published);
        editor.Add(4);
        AssertContents(published, [1, 2, 3]);
    }

    [Fact]
    public void ARemovalPredicateCanPublishWithoutChangingTheTraversal()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3, 4]);
        ImmutablePieceList<int> published = ImmutablePieceList<int>.Empty;
        editor.RemoveAll(value =>
        {
            published = editor.ToImmutable();
            return value % 2 == 0;
        }).ShouldBe(2);

        AssertContents(published, [1, 2, 3, 4]);
        AssertContents(editor, [1, 3]);
    }

    [Fact]
    public void SnapshotEnumerationSurvivesEditorChanges()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> snapshot = editor.ToImmutable();
        var enumerator = snapshot.GetEnumerator();
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(1);

        editor[1] = 20;
        editor.Add(4);
        editor.RemoveAt(0);
        editor.Clear();

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(2);
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(3);
        enumerator.MoveNext().ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditorEnumerationDetectsChangesBeforeAndDuringTraversal(bool startEnumeration)
    {
        var sourceEditor = ImmutablePieceList<int>.Empty.ToEditor();
        sourceEditor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> source = sourceEditor.ToImmutable();
        Action<ImmutablePieceList<int>.Editor>[] edits =
        [
            static editor => editor.Add(4),
            static editor => editor.AddRange([4, 5]),
            static editor => editor[0] = 10,
            static editor => editor.RemoveAt(0),
            static editor => editor.RemoveRange(0, 2),
            static editor => editor.RemoveAll(static item => item == 2),
            static editor => editor.UpdateAll(static item => item == 2, static item => item + 10),
            static editor => editor.Clear(),
        ];

        foreach (var edit in edits)
        {
            var editor = source.ToEditor();
            using IEnumerator<int> enumerator = editor.GetEnumerator();
            if (startEnumeration)
            {
                enumerator.MoveNext().ShouldBeTrue();
                enumerator.Current.ShouldBe(1);
            }

            edit(editor);
            Should.Throw<InvalidOperationException>(() => enumerator.MoveNext());
        }

        AssertContents(source, [1, 2, 3]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void EmptySequencesValidateIndicesBeforeSearchingForPieces(int index)
    {
        ImmutablePieceList<int> empty = ImmutablePieceList<int>.Empty;
        var editor = empty.ToEditor();

        Should.Throw<ArgumentOutOfRangeException>(() => { _ = empty[index]; }).ParamName.ShouldBe(nameof(index));
        Should.Throw<ArgumentOutOfRangeException>(() => { _ = empty.ItemRef(index); }).ParamName.ShouldBe(nameof(index));
        Should.Throw<ArgumentOutOfRangeException>(() => { _ = editor[index]; }).ParamName.ShouldBe(nameof(index));
        Should.Throw<ArgumentOutOfRangeException>(() => editor[index] = 99).ParamName.ShouldBe(nameof(index));
        Should.Throw<ArgumentOutOfRangeException>(() => editor.RemoveAt(index)).ParamName.ShouldBe(nameof(index));

        editor.ToImmutable().ShouldBeSameAs(empty);
    }

    [Fact]
    public void EditedSequencesValidateAgainstTheirCurrentCount()
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();
        editor.RemoveAt(2);

        Should.Throw<ArgumentOutOfRangeException>(() => { _ = editor[2]; });
        Should.Throw<ArgumentOutOfRangeException>(() => editor[2] = 99);
        editor.Add(4);
        AssertContents(editor, [1, 2, 4]);
        AssertContents(original, [1, 2, 3]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void InvalidElementIndicesLeaveTheEditorUnchanged(int index)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3]);
        ImmutablePieceList<int> original = editor.ToImmutable();

        Should.Throw<ArgumentOutOfRangeException>(() => { _ = original[index]; });
        Should.Throw<ArgumentOutOfRangeException>(() => { _ = original.ItemRef(index); });
        Should.Throw<ArgumentOutOfRangeException>(() => { _ = editor[index]; });
        Should.Throw<ArgumentOutOfRangeException>(() => editor[index] = 99);
        Should.Throw<ArgumentOutOfRangeException>(() => editor.RemoveAt(index));

        editor.ToImmutable().ShouldBeSameAs(original);
        AssertContents(editor, [1, 2, 3]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4, 1)]
    [InlineData(5, 0)]
    [InlineData(3, 2)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1, int.MaxValue)]
    public void InvalidRemovalRangesLeaveTheEditorUnchanged(int index, int count)
    {
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        editor.AddRange([1, 2, 3, 4]);
        ImmutablePieceList<int> original = editor.ToImmutable();

        Should.Throw<ArgumentOutOfRangeException>(() => editor.RemoveRange(index, count));

        editor.ToImmutable().ShouldBeSameAs(original);
        AssertContents(editor, [1, 2, 3, 4]);
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        Should.Throw<ArgumentNullException>(() => new ImmutablePieceList<int>.Editor(null!));
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        Should.Throw<ArgumentNullException>(() => editor.RemoveAll(null!));
        Should.Throw<ArgumentNullException>(() => editor.UpdateAll(null!, static item => item));
        Should.Throw<ArgumentNullException>(() => editor.UpdateAll(static _ => true, null!));
    }

    [Theory]
    [InlineData(42)]
    [InlineData(1729)]
    [InlineData(8675309)]
    public void RandomizedEditsAndBranchesMatchListSnapshots(int seed)
    {
        Random random = new(seed);
        var editor = ImmutablePieceList<int>.Empty.ToEditor();
        List<int> expected = [];
        for (int i = 0; i < 128; i++)
        {
            editor.Add(i);
            expected.Add(i);
        }

        List<(ImmutablePieceList<int> Snapshot, int[] Items)> versions = [(editor.ToImmutable(), [.. expected])];
        for (int step = 0; step < 300; step++)
        {
            switch (random.Next(13))
            {
                case 0:
                case 1:
                    int value = random.Next(10000);
                    editor.Add(value);
                    expected.Add(value);
                    break;

                case 2:
                case 3:
                    int[] added = new int[random.Next(33)];
                    for (int i = 0; i < added.Length; i++)
                    {
                        added[i] = random.Next(10000);
                    }

                    editor.AddRange(added);
                    expected.AddRange(added);
                    break;

                case 4:
                    if (expected.Count > 0)
                    {
                        int index = random.Next(expected.Count);
                        int replacement = random.Next(10000);
                        editor[index] = replacement;
                        expected[index] = replacement;
                    }

                    break;

                case 5:
                    if (expected.Count > 0)
                    {
                        int index = random.Next(expected.Count);
                        editor.RemoveAt(index);
                        expected.RemoveAt(index);
                    }

                    break;

                case 6:
                    int start = random.Next(expected.Count + 1);
                    int count = random.Next(Math.Min(8, expected.Count - start) + 1);
                    editor.RemoveRange(start, count);
                    expected.RemoveRange(start, count);
                    break;

                case 7:
                    int remainder = random.Next(31);
                    editor.RemoveAll(item => item % 31 == remainder).ShouldBe(expected.RemoveAll(item => item % 31 == remainder));
                    break;

                case 8:
                    versions.Add((editor.ToImmutable(), [.. expected]));
                    break;

                case 9:
                    var version = versions[random.Next(versions.Count)];
                    editor = version.Snapshot.ToEditor();
                    expected = [.. version.Items];
                    break;

                case 10:
                    ImmutablePieceList<int> snapshot = editor.ToImmutable();
                    editor.ToImmutable().ShouldBeSameAs(snapshot);
                    AssertContents(snapshot, [.. expected]);
                    break;

                case 11:
                    if (step % 31 == 0)
                    {
                        editor.Clear();
                        expected.Clear();
                    }

                    break;

                case 12:
                    int updateRemainder = random.Next(17);
                    int updatedCount = 0;
                    for (int i = 0; i < expected.Count; i++)
                    {
                        if (expected[i] % 17 == updateRemainder)
                        {
                            expected[i] += 10000;
                            updatedCount++;
                        }
                    }

                    editor.UpdateAll(item => item % 17 == updateRemainder, static item => item + 10000).ShouldBe(updatedCount);
                    break;
            }

            AssertContents(editor, [.. expected]);
            if (step % 3 == 0)
            {
                ImmutablePieceList<int> snapshot = editor.ToImmutable();
                AssertContents(snapshot, [.. expected]);
                editor.ToImmutable().ShouldBeSameAs(snapshot);
            }
        }

        foreach (var version in versions)
        {
            AssertContents(version.Snapshot, version.Items);
        }
    }

    private static bool SharesElementStorage<T>(ImmutablePieceList<T> left, int leftIndex, ImmutablePieceList<T> right, int rightIndex)
        => Unsafe.AreSame(ref Unsafe.AsRef(in left.ItemRef(leftIndex)), ref Unsafe.AsRef(in right.ItemRef(rightIndex)));

    private static void AssertContents<T>(IReadOnlyList<T> actual, T[] expected)
    {
        actual.Count.ShouldBe(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].ShouldBe(expected[i]);
        }

        T[] enumerated = [.. actual];
        enumerated.ShouldBe(expected);
        if (actual is ImmutablePieceList<T> snapshot)
        {
            snapshot.PieceCount.ShouldBeInRange(expected.Length == 0 ? 0 : 1, expected.Length);
        }
    }
}
