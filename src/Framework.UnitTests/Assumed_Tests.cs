// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class Assumed_Tests
{
    [Fact]
    public void Null_DoesNotThrow_WhenNull()
    {
        Assumed.Null<string>(null);
    }

    [Fact]
    public void Null_Throws_WhenNotNull()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Null("hello"));
    }

    [Fact]
    public void Null_InterpolatedHandler_DoesNotThrow_WhenNull()
    {
        Assumed.Null<string>(null, $"should not format");
    }

    [Fact]
    public void Null_InterpolatedHandler_Throws_WhenNotNull()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            string value = "hello";
            Assumed.Null(value, $"was {value}");
        });
    }

    [Fact]
    public void NotNull_DoesNotThrow_WhenNotNull()
    {
        Assumed.NotNull("hello");
    }

    [Fact]
    public void NotNull_Throws_WhenNull()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotNull<string>(null));
    }

    [Fact]
    public void NotNull_InterpolatedHandler_Throws_WhenNull()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.NotNull<string>(null, $"was null");
        });
    }

    [Fact]
    public void NotNullOrEmpty_DoesNotThrow_WhenValid()
    {
        Assumed.NotNullOrEmpty("hello");
    }

    [Fact]
    public void NotNullOrEmpty_Throws_WhenNull()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(null));
    }

    [Fact]
    public void NotNullOrEmpty_Throws_WhenEmpty()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(""));
    }

    [Fact]
    public void NotNullOrEmpty_InterpolatedHandler_Throws_WhenEmpty()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.NotNullOrEmpty("", $"was empty");
        });
    }

    [Fact]
    public void True_DoesNotThrow_WhenTrue()
    {
        Assumed.True(true);
    }

    [Fact]
    public void True_Throws_WhenFalse()
    {
        Should.Throw<InternalErrorException>(() => Assumed.True(false));
    }

    [Fact]
    public void True_InterpolatedHandler_Throws_WhenFalse()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.True(false, $"condition failed");
        });
    }

    [Fact]
    public void False_DoesNotThrow_WhenFalse()
    {
        Assumed.False(false);
    }

    [Fact]
    public void False_Throws_WhenTrue()
    {
        Should.Throw<InternalErrorException>(() => Assumed.False(true));
    }

    [Fact]
    public void False_InterpolatedHandler_Throws_WhenTrue()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.False(true, $"was true");
        });
    }

    [Fact]
    public void Equal_DoesNotThrow_WhenEqual()
    {
        Assumed.Equal(42, 42);
    }

    [Fact]
    public void Equal_Throws_WhenNotEqual()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Equal(1, 2));
    }

    [Fact]
    public void Equal_InterpolatedHandler_Throws_WhenNotEqual()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.Equal(1, 2, $"not equal");
        });
    }

    [Fact]
    public void Equal_String_DoesNotThrow_WhenEqual()
    {
        Assumed.Equal("abc", "abc");
    }

    [Fact]
    public void Equal_String_Throws_WhenNotEqual()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Equal("abc", "def"));
    }

    [Fact]
    public void NotEqual_DoesNotThrow_WhenNotEqual()
    {
        Assumed.NotEqual(1, 2);
    }

    [Fact]
    public void NotEqual_Throws_WhenEqual()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotEqual(5, 5));
    }

    [Fact]
    public void NotEqual_InterpolatedHandler_Throws_WhenEqual()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.NotEqual(5, 5, $"were equal");
        });
    }

    [Fact]
    public void GreaterThan_DoesNotThrow_WhenGreater()
    {
        Assumed.GreaterThan(5, 3);
    }

    [Fact]
    public void GreaterThan_Throws_WhenEqual()
    {
        Should.Throw<InternalErrorException>(() => Assumed.GreaterThan(3, 3));
    }

    [Fact]
    public void GreaterThan_Throws_WhenLess()
    {
        Should.Throw<InternalErrorException>(() => Assumed.GreaterThan(2, 3));
    }

    [Fact]
    public void GreaterThan_InterpolatedHandler_Throws_WhenNotGreater()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.GreaterThan(2, 3, $"not greater");
        });
    }

    [Fact]
    public void GreaterThanOrEqual_DoesNotThrow_WhenEqual()
    {
        Assumed.GreaterThanOrEqual(3, 3);
    }

    [Fact]
    public void GreaterThanOrEqual_DoesNotThrow_WhenGreater()
    {
        Assumed.GreaterThanOrEqual(5, 3);
    }

    [Fact]
    public void GreaterThanOrEqual_Throws_WhenLess()
    {
        Should.Throw<InternalErrorException>(() => Assumed.GreaterThanOrEqual(2, 3));
    }

    [Fact]
    public void GreaterThanOrEqual_InterpolatedHandler_Throws_WhenLess()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.GreaterThanOrEqual(2, 3, $"too small");
        });
    }

    [Fact]
    public void LessThan_DoesNotThrow_WhenLess()
    {
        Assumed.LessThan(2, 5);
    }

    [Fact]
    public void LessThan_Throws_WhenEqual()
    {
        Should.Throw<InternalErrorException>(() => Assumed.LessThan(3, 3));
    }

    [Fact]
    public void LessThan_Throws_WhenGreater()
    {
        Should.Throw<InternalErrorException>(() => Assumed.LessThan(5, 3));
    }

    [Fact]
    public void LessThan_InterpolatedHandler_Throws_WhenNotLess()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.LessThan(5, 3, $"not less");
        });
    }

    [Fact]
    public void LessThanOrEqual_DoesNotThrow_WhenEqual()
    {
        Assumed.LessThanOrEqual(3, 3);
    }

    [Fact]
    public void LessThanOrEqual_DoesNotThrow_WhenLess()
    {
        Assumed.LessThanOrEqual(2, 5);
    }

    [Fact]
    public void LessThanOrEqual_Throws_WhenGreater()
    {
        Should.Throw<InternalErrorException>(() => Assumed.LessThanOrEqual(5, 3));
    }

    [Fact]
    public void LessThanOrEqual_InterpolatedHandler_Throws_WhenGreater()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.LessThanOrEqual(5, 3, $"too big");
        });
    }

    [Fact]
    public void Zero_DoesNotThrow_WhenZero()
    {
        Assumed.Zero(0);
    }

    [Fact]
    public void Zero_Throws_WhenNonZero()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Zero(1));
        Should.Throw<InternalErrorException>(() => Assumed.Zero(-1));
    }

    [Fact]
    public void Zero_InterpolatedHandler_Throws_WhenNonZero()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.Zero(5, $"not zero");
        });
    }

    [Fact]
    public void Negative_DoesNotThrow_WhenNegative()
    {
        Assumed.Negative(-1);
    }

    [Fact]
    public void Negative_Throws_WhenZero()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Negative(0));
    }

    [Fact]
    public void Negative_Throws_WhenPositive()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Negative(1));
    }

    [Fact]
    public void Negative_InterpolatedHandler_Throws_WhenNotNegative()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.Negative(0, $"not negative");
        });
    }

    [Fact]
    public void NegativeOrZero_DoesNotThrow_WhenNegative()
    {
        Assumed.NegativeOrZero(-1);
    }

    [Fact]
    public void NegativeOrZero_DoesNotThrow_WhenZero()
    {
        Assumed.NegativeOrZero(0);
    }

    [Fact]
    public void NegativeOrZero_Throws_WhenPositive()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NegativeOrZero(1));
    }

    [Fact]
    public void NegativeOrZero_InterpolatedHandler_Throws_WhenPositive()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.NegativeOrZero(1, $"was positive");
        });
    }

    [Fact]
    public void Positive_DoesNotThrow_WhenPositive()
    {
        Assumed.Positive(1);
    }

    [Fact]
    public void Positive_Throws_WhenZero()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Positive(0));
    }

    [Fact]
    public void Positive_Throws_WhenNegative()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Positive(-1));
    }

    [Fact]
    public void Positive_InterpolatedHandler_Throws_WhenNotPositive()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.Positive(0, $"not positive");
        });
    }

    [Fact]
    public void PositiveOrZero_DoesNotThrow_WhenPositive()
    {
        Assumed.PositiveOrZero(1);
    }

    [Fact]
    public void PositiveOrZero_DoesNotThrow_WhenZero()
    {
        Assumed.PositiveOrZero(0);
    }

    [Fact]
    public void PositiveOrZero_Throws_WhenNegative()
    {
        Should.Throw<InternalErrorException>(() => Assumed.PositiveOrZero(-1));
    }

    [Fact]
    public void PositiveOrZero_InterpolatedHandler_Throws_WhenNegative()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.PositiveOrZero(-1, $"was negative");
        });
    }

    [Fact]
    public void InRange_DoesNotThrow_WhenInRange()
    {
        Assumed.InRange(5, 1, 10);
    }

    [Fact]
    public void InRange_DoesNotThrow_AtLowerBound()
    {
        Assumed.InRange(1, 1, 10);
    }

    [Fact]
    public void InRange_DoesNotThrow_AtUpperBound()
    {
        Assumed.InRange(10, 1, 10);
    }

    [Fact]
    public void InRange_Throws_WhenBelowRange()
    {
        Should.Throw<InternalErrorException>(() => Assumed.InRange(0, 1, 10));
    }

    [Fact]
    public void InRange_Throws_WhenAboveRange()
    {
        Should.Throw<InternalErrorException>(() => Assumed.InRange(11, 1, 10));
    }

    [Fact]
    public void InRange_InterpolatedHandler_Throws_WhenOutOfRange()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.InRange(0, 1, 10, $"out of range");
        });
    }

    [Fact]
    public void Unreachable_AlwaysThrows()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Unreachable());
    }

    [Fact]
    public void Unreachable_WithMessage_AlwaysThrows()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable("should not get here"));
        ex.Message.ShouldContain("should not get here");
    }

    [Fact]
    public void Unreachable_InterpolatedHandler_AlwaysThrows()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.Unreachable($"unreachable at {42}");
        });
    }

    [Fact]
    public void Unreachable_Generic_AlwaysThrows()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>());
    }

    [Fact]
    public void Unreachable_Generic_WithMessage_AlwaysThrows()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>("nope"));
    }

    [Fact]
    public void Unreachable_Generic_InterpolatedHandler_AlwaysThrows()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.Unreachable<string>($"unreachable");
        });
    }

    [Fact]
    public void True_WithCustomMessage_IncludesMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false, "custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void True_WithDefaultMessage_IncludesExpression()
    {
        bool myCondition = false;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(myCondition));
        ex.Message.ShouldContain("myCondition");
    }

    [Fact]
    public void NotNull_WithDefaultMessage_IncludesExpression()
    {
        string? myValue = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull(myValue));
        ex.Message.ShouldContain("myValue");
    }

    [Fact]
    public void Null_WithDefaultMessage_IncludesExpression()
    {
        string myValue = "hello";
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Null(myValue));
        ex.Message.ShouldContain("myValue");
    }

    [Fact]
    public void Equal_WithDefaultMessage_IncludesExpression()
    {
        int myValue = 1;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Equal(myValue, 2));
        ex.Message.ShouldContain("myValue");
    }

    [Fact]
    public void NotNullOrEmpty_WithDefaultMessage_IncludesExpression()
    {
        string? myValue = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(myValue));
        ex.Message.ShouldContain("myValue");
    }

    // Equal with StringComparison

    [Fact]
    public void Equal_StringComparison_DoesNotThrow_WhenEqualOrdinal()
    {
        Assumed.Equal("abc", "abc", StringComparison.Ordinal);
    }

    [Fact]
    public void Equal_StringComparison_DoesNotThrow_WhenEqualIgnoreCase()
    {
        Assumed.Equal("ABC", "abc", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Equal_StringComparison_Throws_WhenNotEqualOrdinal()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Equal("ABC", "abc", StringComparison.Ordinal));
    }

    [Fact]
    public void Equal_StringComparison_Throws_WhenNotEqual()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Equal("abc", "def", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Equal_StringComparison_HandlesNulls()
    {
        Assumed.Equal(null, null, StringComparison.Ordinal);
        Should.Throw<InternalErrorException>(() => Assumed.Equal(null, "abc", StringComparison.Ordinal));
        Should.Throw<InternalErrorException>(() => Assumed.Equal("abc", null, StringComparison.Ordinal));
    }

    [Fact]
    public void Equal_StringComparison_InterpolatedHandler_DoesNotThrow_WhenEqual()
    {
        Assumed.Equal("ABC", "abc", StringComparison.OrdinalIgnoreCase, $"should not format");
    }

    [Fact]
    public void Equal_StringComparison_InterpolatedHandler_Throws_WhenNotEqual()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.Equal("ABC", "abc", StringComparison.Ordinal, $"not equal");
        });
    }

    [Fact]
    public void Equal_StringComparison_WithDefaultMessage_IncludesExpression()
    {
        string myValue = "ABC";
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Equal(myValue, "abc", StringComparison.Ordinal));
        ex.Message.ShouldContain("myValue");
    }

    // NotEqual with StringComparison

    [Fact]
    public void NotEqual_StringComparison_DoesNotThrow_WhenNotEqualOrdinal()
    {
        Assumed.NotEqual("ABC", "abc", StringComparison.Ordinal);
    }

    [Fact]
    public void NotEqual_StringComparison_DoesNotThrow_WhenNotEqual()
    {
        Assumed.NotEqual("abc", "def", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotEqual_StringComparison_Throws_WhenEqualOrdinal()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotEqual("abc", "abc", StringComparison.Ordinal));
    }

    [Fact]
    public void NotEqual_StringComparison_Throws_WhenEqualIgnoreCase()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotEqual("ABC", "abc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NotEqual_StringComparison_HandlesNulls()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotEqual(null, null, StringComparison.Ordinal));
        Assumed.NotEqual(null, "abc", StringComparison.Ordinal);
        Assumed.NotEqual("abc", null, StringComparison.Ordinal);
    }

    [Fact]
    public void NotEqual_StringComparison_InterpolatedHandler_DoesNotThrow_WhenNotEqual()
    {
        Assumed.NotEqual("ABC", "abc", StringComparison.Ordinal, $"should not format");
    }

    [Fact]
    public void NotEqual_StringComparison_InterpolatedHandler_Throws_WhenEqual()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.NotEqual("ABC", "abc", StringComparison.OrdinalIgnoreCase, $"were equal");
        });
    }

    [Fact]
    public void NotEqual_StringComparison_WithDefaultMessage_IncludesExpression()
    {
        string myValue = "abc";
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotEqual(myValue, "abc", StringComparison.Ordinal));
        ex.Message.ShouldContain("myValue");
    }

    // NotNullOrEmpty for IReadOnlyCollection<T>

    [Fact]
    public void NotNullOrEmpty_Collection_DoesNotThrow_WhenNonEmpty()
    {
        Assumed.NotNullOrEmpty<int>([1, 2, 3]);
    }

    [Fact]
    public void NotNullOrEmpty_Collection_Throws_WhenNull()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty<int>(null));
    }

    [Fact]
    public void NotNullOrEmpty_Collection_Throws_WhenEmpty()
    {
        Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty<int>([]));
    }

    [Fact]
    public void NotNullOrEmpty_Collection_InterpolatedHandler_DoesNotThrow_WhenNonEmpty()
    {
        List<int> items = [1, 2, 3];
        Assumed.NotNullOrEmpty(items, $"should not format");
    }

    [Fact]
    public void NotNullOrEmpty_Collection_InterpolatedHandler_Throws_WhenNull()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.NotNullOrEmpty<int>(null, $"was null");
        });
    }

    [Fact]
    public void NotNullOrEmpty_Collection_InterpolatedHandler_Throws_WhenEmpty()
    {
        Should.Throw<InternalErrorException>(() =>
        {
            Assumed.NotNullOrEmpty(Array.Empty<int>(), $"was empty");
        });
    }

    [Fact]
    public void NotNullOrEmpty_Collection_WithDefaultMessage_IncludesExpression()
    {
        List<string>? myCollection = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(myCollection));
        ex.Message.ShouldContain("myCollection");
    }
}
