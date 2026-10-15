// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class RefStack_Tests
{
    [Fact]
    public void Initialization_DefaultValue_CreatesEmptyStack()
    {
        using RefStack<int> stack = default;

        stack.IsEmpty.ShouldBeTrue();
        stack.TryPeek(out int peeked).ShouldBeFalse();
        peeked.ShouldBe(0);
        stack.TryPop(out int popped).ShouldBeFalse();
        popped.ShouldBe(0);
        stack.PeekOrDefault().ShouldBe(0);
        stack.PeekOrDefault(99).ShouldBe(99);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Constructor_WithCapacity_InitializesEmpty(int capacity)
    {
        using RefStack<int> stack = new(capacity);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
        => Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            using RefStack<int> stack = new(-1);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(10)]
    public void Constructor_WithScratchBuffer_InitializesEmpty(int capacity)
    {
        using RefStack<int> stack = new(stackalloc int[capacity]);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Push_SingleItem_AddsToStack()
    {
        using RefStack<int> stack = new(4);

        stack.Push(42);

        stack.IsEmpty.ShouldBeFalse();
        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Fact]
    public void Push_MultipleItems_MakesLastItemTheTop()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.Peek().ShouldBe(3);
    }

    [Fact]
    public void Push_WithScratchBuffer_PreservesLifoOrder()
    {
        using RefStack<int> stack = new(stackalloc int[10]);

        stack.Push(1);
        stack.Push(2);

        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Push_BeyondScratchBuffer_GrowsAndPreservesAllItems(int capacity)
    {
        using RefStack<int> stack = new(stackalloc int[capacity]);

        for (int i = 0; i < 100; i++)
        {
            stack.Push(i);
        }

        stack.Peek().ShouldBe(99);
        stack.PeekOrDefault(-1).ShouldBe(99);
        stack.TryPeek(out int top).ShouldBeTrue();
        top.ShouldBe(99);

        for (int i = 99; i >= 0; i--)
        {
            stack.TryPop(out int value).ShouldBeTrue();
            value.ShouldBe(i);
        }

        stack.IsEmpty.ShouldBeTrue();
        stack.TryPop(out _).ShouldBeFalse();
        stack.Push(42);
        stack.Pop().ShouldBe(42);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Push_DefaultInitializedStack_CanGrowAndBeReused()
    {
        using RefStack<int> stack = default;

        for (int i = 0; i < 100; i++)
        {
            stack.Push(i);
        }

        for (int i = 99; i >= 0; i--)
        {
            stack.Pop().ShouldBe(i);
        }

        stack.IsEmpty.ShouldBeTrue();
        stack.Push(42);
        stack.Peek().ShouldBe(42);
        stack.Pop().ShouldBe(42);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Push_ManyItemsWithRentedBuffer_PreservesLifoOrder()
    {
        using RefStack<int> stack = new(4);

        for (int i = 0; i < 100; i++)
        {
            stack.Push(i);
        }

        for (int i = 99; i >= 0; i--)
        {
            stack.Pop().ShouldBe(i);
        }

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_EmptyStack_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() =>
        {
            using RefStack<int> stack = new(4);

            return stack.Pop();
        });

    [Fact]
    public void Pop_DefaultInitializedStack_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() =>
        {
            using RefStack<int> stack = default;

            return stack.Pop();
        });

    [Fact]
    public void Pop_AfterDraining_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() =>
        {
            using RefStack<int> stack = new(4);
            stack.Push(42);
            stack.Pop().ShouldBe(42);

            return stack.Pop();
        });

    [Fact]
    public void Pop_SingleItem_ReturnsItemAndMakesEmpty()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        stack.Pop().ShouldBe(42);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_MultipleItems_ReturnsInLifoOrder()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_ReferenceTypes_ReturnsInLifoOrder()
    {
        using RefStack<string> stack = new(4);
        stack.Push("first");
        stack.Push("second");
        stack.Push("third");

        stack.Pop().ShouldBe("third");
        stack.Peek().ShouldBe("second");
        stack.Pop().ShouldBe("second");
        stack.Pop().ShouldBe("first");
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_NullableReferenceTypes_ReturnsStoredNull()
    {
        using RefStack<string?> stack = new(4);
        stack.Push(null);
        stack.Push("value");

        stack.Pop().ShouldBe("value");
        stack.Pop().ShouldBeNull();
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_Structs_ReturnsInLifoOrder()
    {
        using RefStack<(int X, int Y)> stack = new(4);
        stack.Push((1, 2));
        stack.Push((3, 4));

        stack.Pop().ShouldBe((3, 4));
        stack.Pop().ShouldBe((1, 2));
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_NullableValueTypes_ReturnsStoredNull()
    {
        using RefStack<int?> stack = new(4);
        stack.Push(null);
        stack.Push(42);

        stack.Peek().ShouldBe(42);
        stack.Pop().ShouldBe(42);
        stack.Peek().ShouldBeNull();
        stack.Pop().ShouldBeNull();
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_ReferenceType_ClearsRemovedSlot()
    {
        string?[] buffer = new string?[2];
        using RefStack<string?> stack = new(buffer);
        stack.Push("first");
        stack.Push("second");

        stack.Pop().ShouldBe("second");
        buffer[0].ShouldBe("first");
        buffer[1].ShouldBeNull();

        stack.Pop().ShouldBe("first");
        buffer[0].ShouldBeNull();
    }

    [Fact]
    public void Pop_StructContainingReferences_ClearsRemovedSlot()
    {
        (string? Text, int Number)[] buffer = new (string?, int)[1];
        using RefStack<(string? Text, int Number)> stack = new(buffer);
        stack.Push(("value", 42));

        stack.Pop().ShouldBe(("value", 42));
        buffer[0].Text.ShouldBeNull();
        buffer[0].Number.ShouldBe(0);
    }

    [Fact]
    public void TryPop_EmptyStack_ReturnsFalseAndDefault()
    {
        using RefStack<int> stack = new(4);

        stack.TryPop(out int value).ShouldBeFalse();
        value.ShouldBe(0);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPop_SingleItem_ReturnsItemAndMakesEmpty()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        stack.TryPop(out int value).ShouldBeTrue();
        value.ShouldBe(42);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPop_MultipleItems_ReturnsInLifoOrder()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.TryPop(out int value1).ShouldBeTrue();
        value1.ShouldBe(3);
        stack.TryPop(out int value2).ShouldBeTrue();
        value2.ShouldBe(2);
        stack.TryPop(out int value3).ShouldBeTrue();
        value3.ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPop_AfterDraining_ReturnsFalseAndDefault()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.TryPop(out _).ShouldBeTrue();
        stack.TryPop(out _).ShouldBeTrue();

        stack.TryPop(out int value).ShouldBeFalse();
        value.ShouldBe(0);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPop_ReferenceTypes_ReturnsItemsThenNull()
    {
        using RefStack<string> stack = new(4);
        stack.Push("first");
        stack.Push("second");

        stack.TryPop(out string? value1).ShouldBeTrue();
        value1.ShouldBe("second");
        stack.TryPop(out string? value2).ShouldBeTrue();
        value2.ShouldBe("first");
        stack.TryPop(out string? value3).ShouldBeFalse();
        value3.ShouldBeNull();
    }

    [Fact]
    public void TryPop_StoredNull_ReturnsTrue()
    {
        using RefStack<string?> stack = new(4);
        stack.Push(null);
        stack.Push("value");

        stack.TryPop(out string? value1).ShouldBeTrue();
        value1.ShouldBe("value");
        stack.TryPop(out string? value2).ShouldBeTrue();
        value2.ShouldBeNull();
        stack.TryPop(out string? value3).ShouldBeFalse();
        value3.ShouldBeNull();
    }

    [Fact]
    public void TryPop_ReferenceType_ClearsRemovedSlot()
    {
        string?[] buffer = new string?[1];
        using RefStack<string?> stack = new(buffer);
        stack.Push("value");

        stack.TryPop(out string? value).ShouldBeTrue();
        value.ShouldBe("value");
        buffer[0].ShouldBeNull();
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Peek_EmptyStack_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() =>
        {
            using RefStack<int> stack = new(4);

            return stack.Peek();
        });

    [Fact]
    public void Peek_DefaultInitializedStack_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() =>
        {
            using RefStack<int> stack = default;

            return stack.Peek();
        });

    [Fact]
    public void Peek_AfterDraining_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() =>
        {
            using RefStack<int> stack = new(4);
            stack.Push(42);
            stack.Pop().ShouldBe(42);

            return stack.Peek();
        });

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Peek_WithItems_ReturnsTopWithoutRemoving(int count)
    {
        using RefStack<int> stack = new(4);
        for (int i = 1; i <= count; i++)
        {
            stack.Push(i);
        }

        stack.Peek().ShouldBe(count);
        stack.Peek().ShouldBe(count);
        stack.Pop().ShouldBe(count);
        stack.IsEmpty.ShouldBe(count == 1);
    }

    [Fact]
    public void Peek_ReferenceTypes_ReturnsTopWithoutRemoving()
    {
        using RefStack<string> stack = new(4);
        stack.Push("first");
        stack.Push("second");

        stack.Peek().ShouldBe("second");
        stack.Pop().ShouldBe("second");
        stack.Peek().ShouldBe("first");
    }

    [Fact]
    public void TryPeek_EmptyStack_ReturnsFalseAndDefault()
    {
        using RefStack<int> stack = new(4);

        stack.TryPeek(out int value).ShouldBeFalse();
        value.ShouldBe(0);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void TryPeek_WithItems_ReturnsTopWithoutRemoving(int count)
    {
        using RefStack<int> stack = new(4);
        for (int i = 1; i <= count; i++)
        {
            stack.Push(i);
        }

        stack.TryPeek(out int value1).ShouldBeTrue();
        value1.ShouldBe(count);
        stack.TryPeek(out int value2).ShouldBeTrue();
        value2.ShouldBe(count);
        stack.Pop().ShouldBe(count);
        stack.IsEmpty.ShouldBe(count == 1);
    }

    [Fact]
    public void TryPeek_ReferenceTypes_ReturnsTopWithoutRemoving()
    {
        using RefStack<string> stack = new(4);
        stack.Push("first");
        stack.Push("second");

        stack.TryPeek(out string? value).ShouldBeTrue();
        value.ShouldBe("second");
        stack.Pop().ShouldBe("second");
    }

    [Fact]
    public void TryPeek_EmptyReferenceStack_ReturnsFalseAndNull()
    {
        using RefStack<string> stack = new(4);

        stack.TryPeek(out string? value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void TryPeek_StoredNull_ReturnsTrueWithoutRemoving()
    {
        using RefStack<string?> stack = new(4);
        stack.Push(null);

        stack.TryPeek(out string? value).ShouldBeTrue();
        value.ShouldBeNull();
        stack.IsEmpty.ShouldBeFalse();
        stack.Pop().ShouldBeNull();
    }

    [Fact]
    public void PeekOrDefault_EmptyStack_ReturnsDefault()
    {
        using RefStack<int> stack = new(4);

        stack.PeekOrDefault().ShouldBe(0);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void PeekOrDefault_WithItems_ReturnsTopWithoutRemoving(int count)
    {
        using RefStack<int> stack = new(4);
        for (int i = 1; i <= count; i++)
        {
            stack.Push(i);
        }

        stack.PeekOrDefault().ShouldBe(count);
        stack.PeekOrDefault().ShouldBe(count);
        stack.Pop().ShouldBe(count);
        stack.IsEmpty.ShouldBe(count == 1);
    }

    [Fact]
    public void PeekOrDefault_EmptyReferenceStack_ReturnsNull()
    {
        using RefStack<string> stack = new(4);

        stack.PeekOrDefault().ShouldBeNull();
    }

    [Fact]
    public void PeekOrDefault_ReferenceTypes_ReturnsTopWithoutRemoving()
    {
        using RefStack<string> stack = new(4);
        stack.Push("hello");
        stack.Push("world");

        stack.PeekOrDefault().ShouldBe("world");
        stack.Pop().ShouldBe("world");
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void PeekOrDefault_WithFallbackAndEmptyStack_ReturnsFallback(int fallback)
    {
        using RefStack<int> stack = new(4);

        stack.PeekOrDefault(fallback).ShouldBe(fallback);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void PeekOrDefault_WithFallbackAndItems_ReturnsTopWithoutRemoving(int count)
    {
        using RefStack<int> stack = new(4);
        for (int i = 1; i <= count; i++)
        {
            stack.Push(i);
        }

        stack.PeekOrDefault(99).ShouldBe(count);
        stack.PeekOrDefault(99).ShouldBe(count);
        stack.Pop().ShouldBe(count);
        stack.IsEmpty.ShouldBe(count == 1);
    }

    [Fact]
    public void PeekOrDefault_WithFallbackAndStoredDefault_ReturnsStoredDefault()
    {
        using RefStack<int> stack = new(4);
        stack.Push(0);

        stack.PeekOrDefault(99).ShouldBe(0);
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void PeekOrDefault_WithFallbackAndEmptyReferenceStack_ReturnsFallback()
    {
        using RefStack<string> stack = new(4);

        stack.PeekOrDefault("fallback").ShouldBe("fallback");
    }

    [Fact]
    public void PeekOrDefault_WithFallbackAndReferenceTypes_ReturnsTopWithoutRemoving()
    {
        using RefStack<string> stack = new(4);
        stack.Push("hello");
        stack.Push("world");

        stack.PeekOrDefault("fallback").ShouldBe("world");
        stack.Pop().ShouldBe("world");
    }

    [Fact]
    public void PeekOrDefault_WithFallbackAndStoredNull_ReturnsStoredNull()
    {
        using RefStack<string?> stack = new(4);
        stack.Push(null);

        stack.PeekOrDefault("fallback").ShouldBeNull();
        stack.PeekOrDefault().ShouldBeNull();
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void IsEmpty_PushAndPop_ReflectsStackState()
    {
        using RefStack<int> stack = new(4);

        stack.IsEmpty.ShouldBeTrue();
        stack.Push(1);
        stack.IsEmpty.ShouldBeFalse();
        stack.Push(2);
        stack.IsEmpty.ShouldBeFalse();
        stack.Pop().ShouldBe(2);
        stack.IsEmpty.ShouldBeFalse();
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void PushAndPop_Interleaved_PreservesLifoOrder()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.Push(2);
        stack.Pop().ShouldBe(2);
        stack.Push(3);
        stack.Push(4);
        stack.Pop().ShouldBe(4);
        stack.Pop().ShouldBe(3);
        stack.Push(5);
        stack.Pop().ShouldBe(5);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void PushAndTryPop_Interleaved_PreservesLifoOrder()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.TryPop(out int value1).ShouldBeTrue();
        value1.ShouldBe(1);
        stack.Push(2);
        stack.Push(3);
        stack.TryPop(out int value2).ShouldBeTrue();
        value2.ShouldBe(3);
        stack.Push(4);
        stack.TryPop(out int value3).ShouldBeTrue();
        value3.ShouldBe(4);
        stack.TryPop(out int value4).ShouldBeTrue();
        value4.ShouldBe(2);
        stack.TryPop(out _).ShouldBeFalse();
        stack.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(10)]
    public void PeekMethods_AfterPopAndPush_ObserveTheNewTop(int scratchCapacity)
    {
        using RefStack<int> stack = new(stackalloc int[scratchCapacity]);
        stack.Push(1);
        stack.Push(2);
        stack.Pop().ShouldBe(2);
        stack.Push(3);

        stack.Peek().ShouldBe(3);
        stack.PeekOrDefault().ShouldBe(3);
        stack.PeekOrDefault(99).ShouldBe(3);
        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(3);
        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void PeekMethods_RepeatedReads_DoNotModifyStack()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        for (int i = 0; i < 100; i++)
        {
            stack.Peek().ShouldBe(3);
            stack.PeekOrDefault().ShouldBe(3);
            stack.PeekOrDefault(99).ShouldBe(3);
            stack.TryPeek(out int value).ShouldBeTrue();
            value.ShouldBe(3);
        }

        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void PeekMethods_AfterDraining_ObserveEmptyStack()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);

        stack.PeekOrDefault().ShouldBe(0);
        stack.PeekOrDefault(99).ShouldBe(99);
        stack.TryPeek(out int value).ShouldBeFalse();
        value.ShouldBe(0);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_DefaultInitializedStack_CanBeCalledMultipleTimes()
    {
        RefStack<int> stack = default;

        stack.Dispose();
        stack.Dispose();
    }

    [Fact]
    public void Dispose_WithRentedBuffer_CanBeCalledMultipleTimes()
    {
        RefStack<int> stack = new(4);
        stack.Push(1);

        stack.Dispose();
        stack.Dispose();
    }

    [Fact]
    public void Dispose_WithScratchBuffer_CanBeCalledMultipleTimes()
    {
        RefStack<int> stack = new(stackalloc int[2]);
        stack.Push(1);

        stack.Dispose();
        stack.Dispose();
    }

    [Fact]
    public void Dispose_AfterGrowingBeyondScratchBuffer_CanBeCalledMultipleTimes()
    {
        RefStack<int> stack = new(stackalloc int[2]);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.Dispose();
        stack.Dispose();
    }
}
