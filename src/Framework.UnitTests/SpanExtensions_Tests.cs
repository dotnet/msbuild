// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the Span polyfills under <see cref="System.SpanExtensions"/>.
    /// On .NET 7+/8+ these methods come from the framework, so the tests only
    /// run on net472 / netstandard2.0 where the polyfills are compiled in.
    /// </summary>
    public class SpanExtensions_Tests
    {
        [Fact]
        public void Replace_BasicReplacement_ReplacesCharacter()
        {
            Span<char> span = "hello".ToCharArray();
            span.Replace('e', 'a');
            span.ToString().ShouldBe("hallo");
        }

        [Fact]
        public void Replace_MultipleOccurrences_ReplacesAllCharacters()
        {
            Span<char> span = "mississippi".ToCharArray();
            span.Replace('i', 'x');
            span.ToString().ShouldBe("mxssxssxppx");
        }

        [Fact]
        public void Replace_CharacterNotFound_MakesNoChanges()
        {
            Span<char> span = "hello".ToCharArray();
            span.Replace('z', 'a');
            span.ToString().ShouldBe("hello");
        }

        [Fact]
        public void Replace_SameCharacters_ReturnsImmediately()
        {
            Span<char> span = "hello".ToCharArray();
            span.Replace('e', 'e');
            span.ToString().ShouldBe("hello");
        }

        [Fact]
        public void Replace_EmptySpan_HandlesCorrectly()
        {
            Span<char> span = Span<char>.Empty;
            span.Replace('a', 'b');
            span.IsEmpty.ShouldBeTrue();
        }

        [Fact]
        public void Replace_SingleCharacter_ReplacesIfMatches()
        {
            Span<char> span = new[] { 'a' };
            span.Replace('a', 'b');
            span.ToString().ShouldBe("b");
        }

        [Fact]
        public void Replace_AllSameCharacters_ReplacesAll()
        {
            Span<char> span = "aaaaa".ToCharArray();
            span.Replace('a', 'b');
            span.ToString().ShouldBe("bbbbb");
        }

        [Fact]
        public void IndexOfAnyExcept_AllMatch_ReturnsNegativeOne()
        {
            ReadOnlySpan<int> span = new[] { 1, 1, 1 };
            span.IndexOfAnyExcept(1).ShouldBe(-1);
        }

        [Fact]
        public void IndexOfAnyExcept_FirstDifferent_ReturnsZero()
        {
            ReadOnlySpan<int> span = new[] { 2, 1, 1 };
            span.IndexOfAnyExcept(1).ShouldBe(0);
        }

        [Fact]
        public void IndexOfAnyExcept_LaterDifferent_ReturnsIndex()
        {
            ReadOnlySpan<int> span = new[] { 1, 1, 1, 2, 1 };
            span.IndexOfAnyExcept(1).ShouldBe(3);
        }

        [Fact]
        public void IndexOfAnyExcept_Empty_ReturnsNegativeOne()
        {
            ReadOnlySpan<int> span = ReadOnlySpan<int>.Empty;
            span.IndexOfAnyExcept(0).ShouldBe(-1);
        }

        [Fact]
        public void IndexOfAnyExcept_Char_ReturnsExpected()
        {
            ReadOnlySpan<char> span = "    hello".AsSpan();
            span.IndexOfAnyExcept(' ').ShouldBe(4);
        }
    }
}

#endif
