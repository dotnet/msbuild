// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class RefArrayBuilder_Tests
{
    [Fact]
    public void Constructor_InitializesWithCapacity()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Count.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithScratchBuffer_UsesScratchBuffer()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[10]);

        builder.Count.ShouldBe(0);
        builder.Add(42);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithScratchBuffer_CanGrowBeyondInitialCapacity()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[2]);

        builder.Add(1);
        builder.Add(2);
        builder.Add(3); // Should trigger growth

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Constructor_WithEmptyScratchBuffer_CanStillAdd()
    {
        using RefArrayBuilder<int> builder = new(scratchBuffer: []);

        builder.Count.ShouldBe(0);
        builder.Add(42); // Should trigger growth

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithScratchBuffer_AddRangeWorks()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[10]);

        builder.AddRange([1, 2, 3]);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Add_SingleItem_IncreasesLength()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(42);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void Add_MultipleItems_MaintainsOrder()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Add_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.Add(4);
        builder.Add(5);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void Add_ScratchBuffer_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[4]);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.Add(4);
        builder.Add(5);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void AddRange_SingleElement_AddsCorrectly()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([42]);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void AddRange_MultipleElements_AddsAllInOrder()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.AddRange([2, 3, 4]);

        builder.Count.ShouldBe(4);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
    }

    [Fact]
    public void AddRange_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.AddRange([2, 3, 4, 5]);

        builder.Count.ShouldBe(5);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void AddRange_ScratchBuffer_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[4]);
        builder.Add(1);
        builder.AddRange([2, 3, 4, 5]);

        builder.Count.ShouldBe(5);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void AddRange_EmptySpan_DoesNothing()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.AddRange([]);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(1);
    }

    [Fact]
    public void Insert_AtBeginning_ShiftsExistingElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(2);
        builder.Add(3);
        builder.Insert(0, 1);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Insert_InMiddle_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(3);
        builder.Insert(1, 2);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Insert_AtEnd_AppendsElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Insert(2, 3);

        builder.Count.ShouldBe(3);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Insert_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(3);
        builder.Add(4);
        builder.Add(5);
        builder.Insert(1, 2);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void Insert_ScratchBuffer_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[4]);
        builder.Add(2);
        builder.Add(3);
        builder.Add(4);
        builder.Add(5);
        builder.Insert(0, 1);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_AtBeginning_ShiftsExistingElements()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Add(4);
        builder.Add(5);
        builder.InsertRange(0, [1, 2, 3]);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_InMiddle_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Add(1);
        builder.Add(5);
        builder.InsertRange(1, [2, 3, 4]);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_AtEnd_AppendsElements()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Add(1);
        builder.Add(2);
        builder.InsertRange(2, [3, 4, 5]);

        builder.Count.ShouldBe(5);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(2);
        builder.Add(1);
        builder.Add(5);
        builder.InsertRange(1, [2, 3, 4]);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_ScratchBuffer_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[2]);
        builder.Add(1);
        builder.Add(5);
        builder.InsertRange(1, [2, 3, 4]);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_AtBeginning_NearCapacity_ShiftsCorrectly()
    {
        // Use a capacity that we know ArrayPool will give us, then fill most of it.
        // The bug is that InsertRange checks (index + source.Length) < capacity
        // instead of (count + source.Length) <= capacity.
        using RefArrayBuilder<int> builder = new(16);
        int capacity = builder.Capacity;

        // Fill all but 2 slots
        int fillCount = capacity - 2;
        for (int i = 1; i <= fillCount; i++)
        {
            builder.Add(i);
        }

        // Insert 5 at index 0: index + 5 could be < capacity, but count + 5 exceeds it.
        builder.InsertRange(0, [91, 92, 93, 94, 95]);

        builder.Count.ShouldBe(fillCount + 5);
        builder[0].ShouldBe(91);
        builder[1].ShouldBe(92);
        builder[2].ShouldBe(93);
        builder[3].ShouldBe(94);
        builder[4].ShouldBe(95);
        builder[5].ShouldBe(1);
        builder[fillCount + 4].ShouldBe(fillCount);
    }

    [Fact]
    public void InsertRange_ScratchBuffer_AtBeginning_NearCapacity_ShiftsCorrectly()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[12]);
        for (int i = 3; i <= 12; i++)
        {
            builder.Add(i);
        }

        builder.InsertRange(0, [22, 33, 44, 55, 66]);

        builder.Count.ShouldBe(15);
        builder[0].ShouldBe(22);
        builder[1].ShouldBe(33);
        builder[2].ShouldBe(44);
        builder[3].ShouldBe(55);
        builder[4].ShouldBe(66);
        builder[5].ShouldBe(3);
        builder[14].ShouldBe(12);
    }

    [Fact]
    public void InsertRange_EmptySpan_DoesNothing()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.InsertRange(1, []);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
    }

    [Fact]
    public void Count_CanBeSet()
    {
        var builder = new RefArrayBuilder<int>(4);
        try
        {
            builder.Add(1);
            builder.Add(2);
            builder.Add(3);
            builder.Count = 2;

            builder.Count.ShouldBe(2);
            builder[0].ShouldBe(1);
            builder[1].ShouldBe(2);
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void Indexer_ReturnsReference()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        ref int value = ref builder[1];
        value = 42;

        builder[1].ShouldBe(42);
    }

    [Fact]
    public void IsEmpty_NewBuilder_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_AfterAdd_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);

        builder.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void IsEmpty_AfterRemovingAllElements_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.RemoveAt(0);

        builder.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_AfterSettingCountToZero_ReturnsTrue()
    {
        var builder = new RefArrayBuilder<int>(4);
        try
        {
            builder.Add(1);
            builder.Add(2);
            builder.Count = 0;

            builder.IsEmpty.ShouldBeTrue();
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSlice()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        ReadOnlySpan<int> span = builder.AsSpan();

        span.Length.ShouldBe(3);
        span[0].ShouldBe(1);
        span[1].ShouldBe(2); span[2].ShouldBe(3);
        span[2].ShouldBe(3);
    }

    [Fact]
    public void AsSpan_EmptyBuilder_ReturnsEmptySpan()
    {
        using RefArrayBuilder<int> builder = new(4);

        ReadOnlySpan<int> span = builder.AsSpan();

        span.Length.ShouldBe(0);
    }

    [Fact]
    public void AsSpan_ReflectsChanges()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        ReadOnlySpan<int> span1 = builder.AsSpan();
        span1.Length.ShouldBe(2);

        builder.Add(3);

        ReadOnlySpan<int> span2 = builder.AsSpan();
        span2.Length.ShouldBe(3);
        span2[2].ShouldBe(3);
    }

    [Fact]
    public void WithReferenceTypes_WorksCorrectly()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("hello");
        builder.Add("world");

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe("hello");
        builder[1].ShouldBe("world");
    }

    [Fact]
    public void WithReferenceTypes_AddRange_WorksCorrectly()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.AddRange(["one", "two", "three"]);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe("one");
        builder[1].ShouldBe("two");
        builder[2].ShouldBe("three");
    }

    [Fact]
    public void WithReferenceTypes_Insert_WorksCorrectly()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("first");
        builder.Add("third");
        builder.Insert(1, "second");

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe("first");
        builder[1].ShouldBe("second");
        builder[2].ShouldBe("third");
    }

    [Fact]
    public void MultipleOperations_ComplexScenario()
    {
        using RefArrayBuilder<int> builder = new(2);

        // Start small and grow
        builder.Add(1);
        builder.Add(2);
        builder.AddRange([3, 4, 5]);
        builder.Insert(0, 0);
        builder.InsertRange(6, [6, 7, 8]);

        builder.Count.ShouldBe(9);

        // Verify sequence
        for (int i = 0; i < 9; i++)
        {
            builder[i].ShouldBe(i);
        }
    }

    [Fact]
    public void LargeCapacity_HandlesGrowthCorrectly()
    {
        using RefArrayBuilder<int> builder = new(2);

        // Add many items to trigger multiple growth operations
        for (int i = 0; i < 100; i++)
        {
            builder.Add(i);
        }

        builder.Count.ShouldBe(100);
        builder[0].ShouldBe(0);
        builder[50].ShouldBe(50);
        builder[99].ShouldBe(99);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        builder.Dispose();
        builder.Dispose(); // Should not throw
    }

    [Fact]
    public void RemoveAt_FirstElement_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(0);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe(2);
        builder[1].ShouldBe(3);
    }

    [Fact]
    public void RemoveAt_MiddleElement_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.Add(4);
        builder.RemoveAt(1);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(3);
        builder[2].ShouldBe(4);
    }

    [Fact]
    public void RemoveAt_LastElement_RemovesCorrectly()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(2);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
    }

    [Fact]
    public void RemoveAt_SingleElement_BecomesEmpty()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(42);
        builder.RemoveAt(0);

        builder.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveAt_WithReferenceTypes_ClearsReference()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("first");
        builder.Add("second");
        builder.Add("third");
        builder.RemoveAt(1);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe("first");
        builder[1].ShouldBe("third");
    }

    [Fact]
    public void RemoveAt_MultipleRemovals_WorksCorrectly()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.AddRange([1, 2, 3, 4, 5]);
        builder.RemoveAt(2); // Remove 3
        builder.RemoveAt(1); // Remove 2

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(4);
        builder[2].ShouldBe(5);
    }

    [Fact]
    public void RemoveAt_RemoveAllElements_LeavesEmpty()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(2);
        builder.RemoveAt(1);
        builder.RemoveAt(0);

        builder.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveAt_AfterGrowth_WorksCorrectly()
    {
        using RefArrayBuilder<int> builder = new(2);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3); // Triggers growth
        builder.Add(4);
        builder.RemoveAt(1);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(3);
        builder[2].ShouldBe(4);
    }

    [Fact]
    public void RemoveAt_CanAddAfterRemoval()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(1);
        builder.Add(4);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(3);
        builder[2].ShouldBe(4);
    }

    [Fact]
    public void RemoveAt_WithScratchBuffer_WorksCorrectly()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[10]);
        builder.AddRange([1, 2, 3, 4, 5]);
        builder.RemoveAt(2);

        builder.Count.ShouldBe(4);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(4);
        builder[3].ShouldBe(5);
    }

    [Fact]
    public void ToImmutable_EmptyBuilder_ReturnsEmptyImmutableArray()
    {
        using RefArrayBuilder<int> builder = new(4);

        var result = builder.ToImmutable();

        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void ToImmutable_WithItems_ReturnsImmutableArrayWithCorrectElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        var result = builder.ToImmutable();

        result.Length.ShouldBe(3);
        result[0].ShouldBe(1);
        result[1].ShouldBe(2);
        result[2].ShouldBe(3);
    }

    [Fact]
    public void ToImmutable_WithReferenceTypes_ReturnsImmutableArrayWithCorrectElements()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("hello");
        builder.Add("world");

        var result = builder.ToImmutable();

        result.Length.ShouldBe(2);
        result[0].ShouldBe("hello");
        result[1].ShouldBe("world");
    }

    [Fact]
    public void ToImmutable_AfterMultipleOperations_ReturnsCorrectImmutableArray()
    {
        using RefArrayBuilder<int> builder = new(2);
        builder.Add(1);
        builder.AddRange([2, 3]);
        builder.Insert(0, 0);
        builder.InsertRange(4, [4, 5]);

        var result = builder.ToImmutable();

        result.Length.ShouldBe(6);
        for (int i = 0; i < 6; i++)
        {
            result[i].ShouldBe(i);
        }
    }

    [Fact]
    public void ToImmutable_DoesNotModifyBuilder()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        var result1 = builder.ToImmutable();
        builder.Add(3);
        var result2 = builder.ToImmutable();

        result1.Length.ShouldBe(2);
        result1[0].ShouldBe(1);
        result1[1].ShouldBe(2);

        result2.Length.ShouldBe(3);
        result2[0].ShouldBe(1);
        result2[1].ShouldBe(2);
        result2[2].ShouldBe(3);
    }

    [Fact]
    public void ToImmutable_AfterCountSet_ReturnsCorrectImmutableArray()
    {
        var builder = new RefArrayBuilder<int>(4);
        try
        {
            builder.Add(1);
            builder.Add(2);
            builder.Add(3);
            builder.Count = 2;

            var result = builder.ToImmutable();

            result.Length.ShouldBe(2);
            result[0].ShouldBe(1);
            result[1].ShouldBe(2);
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void Any_EmptyBuilder_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.Any().ShouldBeFalse();
    }

    [Fact]
    public void Any_WithElements_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);

        builder.Any().ShouldBeTrue();
    }

    [Fact]
    public void Any_WithPredicate_NoMatch_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.Any(x => x > 5).ShouldBeFalse();
    }

    [Fact]
    public void Any_WithPredicate_HasMatch_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.Any(x => x > 3).ShouldBeTrue();
    }

    [Fact]
    public void Any_WithPredicate_EmptyBuilder_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.Any(x => x > 0).ShouldBeFalse();
    }

    [Fact]
    public void Any_WithPredicateAndArg_NoMatch_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.Any(5, (x, threshold) => x > threshold).ShouldBeFalse();
    }

    [Fact]
    public void Any_WithPredicateAndArg_HasMatch_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.Any(3, (x, threshold) => x > threshold).ShouldBeTrue();
    }

    [Fact]
    public void Any_WithNullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.Any(null!);
        });
    }

    [Fact]
    public void Any_WithPredicateAndArg_NullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.Any(5, null!);
        });
    }

    [Fact]
    public void All_EmptyBuilder_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.All(x => x > 0).ShouldBeTrue();
    }

    [Fact]
    public void All_AllElementsMatch_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([2, 4, 6, 8]);

        builder.All(x => x % 2 == 0).ShouldBeTrue();
    }

    [Fact]
    public void All_SomeElementsDontMatch_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([2, 3, 4, 6]);

        builder.All(x => x % 2 == 0).ShouldBeFalse();
    }

    [Fact]
    public void All_NoElementsMatch_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 3, 5, 7]);

        builder.All(x => x % 2 == 0).ShouldBeFalse();
    }

    [Fact]
    public void All_WithPredicateAndArg_AllMatch_ReturnsTrue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([5, 10, 15, 20]);

        builder.All(5, (x, divisor) => x % divisor == 0).ShouldBeTrue();
    }

    [Fact]
    public void All_WithPredicateAndArg_SomeDontMatch_ReturnsFalse()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([5, 10, 12, 20]);

        builder.All(5, (x, divisor) => x % divisor == 0).ShouldBeFalse();
    }

    [Fact]
    public void All_WithNullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.All(null!);
        });
    }

    [Fact]
    public void All_WithPredicateAndArg_NullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.All(5, null!);
        });
    }

    [Fact]
    public void First_EmptyBuilder_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);

            return builder.First();
        });
    }

    [Fact]
    public void First_WithElements_ReturnsFirstElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.First().ShouldBe(1);
    }

    [Fact]
    public void First_SingleElement_ReturnsThatElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(42);

        builder.First().ShouldBe(42);
    }

    [Fact]
    public void First_WithPredicate_NoMatch_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.AddRange([1, 2, 3]);

            return builder.First(x => x > 5);
        });
    }

    [Fact]
    public void First_WithPredicate_HasMatch_ReturnsFirstMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.First(x => x > 2).ShouldBe(3);
    }

    [Fact]
    public void First_WithPredicate_EmptyBuilder_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);

            return builder.First(x => x > 0);
        });
    }

    [Fact]
    public void First_WithPredicateAndArg_NoMatch_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.AddRange([1, 2, 3]);

            return builder.First(5, (x, threshold) => x > threshold);
        });
    }

    [Fact]
    public void First_WithPredicateAndArg_HasMatch_ReturnsFirstMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.First(2, (x, threshold) => x > threshold).ShouldBe(3);
    }

    [Fact]
    public void First_WithNullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.First(null!);
        });
    }

    [Fact]
    public void First_WithPredicateAndArg_NullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.First(5, null!);
        });
    }

    [Fact]
    public void FirstOrDefault_EmptyBuilder_ReturnsDefault()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.FirstOrDefault().ShouldBe(0);
    }

    [Fact]
    public void FirstOrDefault_WithElements_ReturnsFirstElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.FirstOrDefault().ShouldBe(1);
    }

    [Fact]
    public void FirstOrDefault_WithDefaultValue_EmptyBuilder_ReturnsDefaultValue()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.FirstOrDefault(99).ShouldBe(99);
    }

    [Fact]
    public void FirstOrDefault_WithDefaultValue_WithElements_ReturnsFirstElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.FirstOrDefault(99).ShouldBe(1);
    }

    [Fact]
    public void FirstOrDefault_WithPredicate_NoMatch_ReturnsDefault()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.FirstOrDefault(x => x > 5).ShouldBe(0);
    }

    [Fact]
    public void FirstOrDefault_WithPredicate_HasMatch_ReturnsFirstMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.FirstOrDefault(x => x > 2).ShouldBe(3);
    }

    [Fact]
    public void FirstOrDefault_WithPredicateAndDefaultValue_NoMatch_ReturnsDefaultValue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.FirstOrDefault(x => x > 5, 99).ShouldBe(99);
    }

    [Fact]
    public void FirstOrDefault_WithPredicateAndDefaultValue_HasMatch_ReturnsFirstMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.FirstOrDefault(x => x > 2, 99).ShouldBe(3);
    }

    [Fact]
    public void FirstOrDefault_WithPredicateAndArg_NoMatch_ReturnsDefault()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.FirstOrDefault(5, (x, threshold) => x > threshold).ShouldBe(0);
    }

    [Fact]
    public void FirstOrDefault_WithPredicateAndArg_HasMatch_ReturnsFirstMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.FirstOrDefault(2, (x, threshold) => x > threshold).ShouldBe(3);
    }

    [Fact]
    public void FirstOrDefault_WithPredicateArgAndDefaultValue_NoMatch_ReturnsDefaultValue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.FirstOrDefault(5, (x, threshold) => x > threshold, 99).ShouldBe(99);
    }

    [Fact]
    public void FirstOrDefault_WithPredicateArgAndDefaultValue_HasMatch_ReturnsFirstMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.FirstOrDefault(2, (x, threshold) => x > threshold, 99).ShouldBe(3);
    }

    [Fact]
    public void FirstOrDefault_WithNullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.FirstOrDefault(null!);
        });
    }

    [Fact]
    public void FirstOrDefault_WithReferenceType_EmptyBuilder_ReturnsNull()
    {
        using RefArrayBuilder<string> builder = new(4);

        builder.FirstOrDefault().ShouldBeNull();
    }

    [Fact]
    public void FirstOrDefault_WithReferenceType_WithElements_ReturnsFirstElement()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.AddRange(["hello", "world"]);

        builder.FirstOrDefault().ShouldBe("hello");
    }

    [Fact]
    public void Last_EmptyBuilder_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);

            return builder.Last();
        });
    }

    [Fact]
    public void Last_WithElements_ReturnsLastElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.Last().ShouldBe(3);
    }

    [Fact]
    public void Last_SingleElement_ReturnsThatElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(42);

        builder.Last().ShouldBe(42);
    }

    [Fact]
    public void Last_WithPredicate_NoMatch_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.AddRange([1, 2, 3]);

            return builder.Last(x => x > 5);
        });
    }

    [Fact]
    public void Last_WithPredicate_HasMatch_ReturnsLastMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.Last(x => x < 4).ShouldBe(3);
    }

    [Fact]
    public void Last_WithPredicate_EmptyBuilder_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);

            return builder.Last(x => x > 0);
        });
    }

    [Fact]
    public void Last_WithPredicateAndArg_NoMatch_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.AddRange([1, 2, 3]);

            return builder.Last(5, (x, threshold) => x > threshold);
        });
    }

    [Fact]
    public void Last_WithPredicateAndArg_HasMatch_ReturnsLastMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.Last(3, (x, threshold) => x < threshold).ShouldBe(2);
    }

    [Fact]
    public void Last_WithNullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.Last(null!);
        });
    }

    [Fact]
    public void Last_WithPredicateAndArg_NullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.Last(5, null!);
        });
    }

    [Fact]
    public void LastOrDefault_EmptyBuilder_ReturnsDefault()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.LastOrDefault().ShouldBe(0);
    }

    [Fact]
    public void LastOrDefault_WithElements_ReturnsLastElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.LastOrDefault().ShouldBe(3);
    }

    [Fact]
    public void LastOrDefault_WithDefaultValue_EmptyBuilder_ReturnsDefaultValue()
    {
        using RefArrayBuilder<int> builder = new(4);

        builder.LastOrDefault(99).ShouldBe(99);
    }

    [Fact]
    public void LastOrDefault_WithDefaultValue_WithElements_ReturnsLastElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.LastOrDefault(99).ShouldBe(3);
    }

    [Fact]
    public void LastOrDefault_WithPredicate_NoMatch_ReturnsDefault()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.LastOrDefault(x => x > 5).ShouldBe(0);
    }

    [Fact]
    public void LastOrDefault_WithPredicate_HasMatch_ReturnsLastMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.LastOrDefault(x => x < 4).ShouldBe(3);
    }

    [Fact]
    public void LastOrDefault_WithPredicateAndDefaultValue_NoMatch_ReturnsDefaultValue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.LastOrDefault(x => x > 5, 99).ShouldBe(99);
    }

    [Fact]
    public void LastOrDefault_WithPredicateAndDefaultValue_HasMatch_ReturnsLastMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.LastOrDefault(x => x < 4, 99).ShouldBe(3);
    }

    [Fact]
    public void LastOrDefault_WithPredicateAndArg_NoMatch_ReturnsDefault()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.LastOrDefault(5, (x, threshold) => x > threshold).ShouldBe(0);
    }

    [Fact]
    public void LastOrDefault_WithPredicateAndArg_HasMatch_ReturnsLastMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.LastOrDefault(3, (x, threshold) => x < threshold).ShouldBe(2);
    }

    [Fact]
    public void LastOrDefault_WithPredicateArgAndDefaultValue_NoMatch_ReturnsDefaultValue()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3]);

        builder.LastOrDefault(5, (x, threshold) => x > threshold, 99).ShouldBe(99);
    }

    [Fact]
    public void LastOrDefault_WithPredicateArgAndDefaultValue_HasMatch_ReturnsLastMatch()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([1, 2, 3, 4, 5]);

        builder.LastOrDefault(3, (x, threshold) => x < threshold, 99).ShouldBe(2);
    }

    [Fact]
    public void LastOrDefault_WithNullPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            using RefArrayBuilder<int> builder = new(4);
            builder.Add(1);

            return builder.LastOrDefault(null!);
        });
    }

    [Fact]
    public void LastOrDefault_WithReferenceType_EmptyBuilder_ReturnsNull()
    {
        using RefArrayBuilder<string> builder = new(4);

        builder.LastOrDefault().ShouldBeNull();
    }

    [Fact]
    public void LastOrDefault_WithReferenceType_WithElements_ReturnsLastElement()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.AddRange(["hello", "world"]);

        builder.LastOrDefault().ShouldBe("world");
    }

    [Fact]
    public void AsRef_ModificationsViaRef_AreReflectedInOriginalBuilder()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[4]);

        ref RefArrayBuilder<int> builderRef = ref builder.AsRef();
        builderRef.Add(10);
        builderRef.Add(20);
        builderRef.Add(30);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(10);
        builder[1].ShouldBe(20);
        builder[2].ShouldBe(30);
    }

    [Fact]
    public void AsRef_AllowsPassingUsingDeclaredBuilderToRefMethod()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        AddItemsToBuilder(ref builder.AsRef(), [3, 4, 5]);

        builder.Count.ShouldBe(5);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void AsRef_AllowsGrowthThroughRef()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[2]);
        builder.Add(1);
        builder.Add(2);

        // Passing by ref allows the helper to grow the builder beyond the initial stack buffer.
        AddItemsToBuilder(ref builder.AsRef(), [3, 4, 5]);

        builder.Count.ShouldBe(5);

        for (int i = 0; i < 5; i++)
        {
            builder[i].ShouldBe(i + 1);
        }
    }

    private static void AddItemsToBuilder(ref RefArrayBuilder<int> builder, ReadOnlySpan<int> items)
        => builder.AddRange(items);
}
