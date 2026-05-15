// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for <see cref="ValueStringBuilder"/>.
    /// </summary>
    public class ValueStringBuilder_Tests
    {
        [Fact]
        public void Append_String_AppendsContent()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("hello");
            builder.ToString().ShouldBe("hello");
            builder.Length.ShouldBe(5);
        }

        [Fact]
        public void Append_NullString_DoesNothing()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            string? nullString = null;
            builder.Append(nullString);
            builder.Length.ShouldBe(0);
            builder.ToString().ShouldBe(string.Empty);
        }

        [Fact]
        public void Append_SingleChar_TakesSingleCharFastPath()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append('a');
            builder.Append('b');
            builder.Append('c');
            builder.ToString().ShouldBe("abc");
        }

        [Fact]
        public void Append_CharCount_AppendsRepeatedChar()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append('-', 5);
            builder.ToString().ShouldBe("-----");
        }

        [Fact]
        public void Append_ReadOnlySpan_AppendsContent()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("abc".AsSpan());
            builder.Append("def".AsSpan());
            builder.ToString().ShouldBe("abcdef");
        }

        [Fact]
        public void Append_GrowsBeyondInitialBuffer()
        {
            using ValueStringBuilder builder = new(stackalloc char[4]);
            builder.Append("0123456789");
            builder.ToString().ShouldBe("0123456789");
            builder.Capacity.ShouldBeGreaterThanOrEqualTo(10);
        }

        [Fact]
        public unsafe void Append_CharPointer_AppendsContent()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            ReadOnlySpan<char> source = "abcdef";
            fixed (char* p = source)
            {
                builder.Append(p, 6);
            }
            builder.ToString().ShouldBe("abcdef");
        }

        [Fact]
        public void Insert_Char_InsertsAtIndex()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("abef");
            builder.Insert(2, 'c', 2);
            builder.ToString().ShouldBe("abccef");
        }

        [Fact]
        public void Insert_String_InsertsAtIndex()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("abef");
            builder.Insert(2, "cd");
            builder.ToString().ShouldBe("abcdef");
        }

        [Fact]
        public void Insert_NullString_DoesNothing()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("abc");
            string? nullString = null;
            builder.Insert(1, nullString);
            builder.ToString().ShouldBe("abc");
        }

        [Fact]
        public void Replace_ReplacesAllOccurrences()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("mississippi");
            builder.Replace('i', 'x');
            builder.ToString().ShouldBe("mxssxssxppx");
        }

        [Fact]
        public void Replace_SameChar_ReturnsImmediately()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("hello");
            builder.Replace('e', 'e');
            builder.ToString().ShouldBe("hello");
        }

        [Fact]
        public void Replace_EmptyBuilder_DoesNothing()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Replace('a', 'b');
            builder.Length.ShouldBe(0);
        }

        [Fact]
        public void Indexer_GetSet()
        {
            ValueStringBuilder builder = new(stackalloc char[16]);
            try
            {
                builder.Append("abc");
                builder[1].ShouldBe('b');
                builder[1] = 'B';
                builder.ToString().ShouldBe("aBc");
            }
            finally
            {
                builder.Dispose();
            }
        }

        [Fact]
        public void Length_Set_TruncatesContent()
        {
            ValueStringBuilder builder = new(stackalloc char[16]);
            try
            {
                builder.Append("abcdef");
                builder.Length = 3;
                builder.ToString().ShouldBe("abc");
            }
            finally
            {
                builder.Dispose();
            }
        }

        [Fact]
        public void Clear_ResetsLengthButKeepsCapacity()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("abcdef");
            int capacity = builder.Capacity;
            builder.Clear();
            builder.Length.ShouldBe(0);
            builder.Capacity.ShouldBe(capacity);
            builder.ToString().ShouldBe(string.Empty);
        }

        [Fact]
        public void EnsureCapacity_GrowsWhenNeeded()
        {
            using ValueStringBuilder builder = new(stackalloc char[4]);
            builder.EnsureCapacity(64);
            builder.Capacity.ShouldBeGreaterThanOrEqualTo(64);
        }

        [Fact]
        public void AsSpan_Terminate_AppendsNullChar()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("abc");
            ReadOnlySpan<char> span = builder.AsSpan(terminate: true);
            span.ToString().ShouldBe("abc");
            builder.Capacity.ShouldBeGreaterThanOrEqualTo(4);
            // The terminating '\0' is past Length, but writable from the underlying span.
        }

        [Fact]
        public void TryCopyTo_SufficientDestination_ReturnsTrue()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("hello");
            Span<char> destination = stackalloc char[16];
            bool ok = builder.TryCopyTo(destination, out int written);
            ok.ShouldBeTrue();
            written.ShouldBe(5);
            destination.Slice(0, written).ToString().ShouldBe("hello");
        }

        [Fact]
        public void TryCopyTo_TooSmall_ReturnsFalse()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("hello");
            Span<char> destination = stackalloc char[2];
            bool ok = builder.TryCopyTo(destination, out int written);
            ok.ShouldBeFalse();
            written.ShouldBe(0);
        }

        [Fact]
        public void AppendSpan_ReturnsWritableRegion()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("ab");
            Span<char> region = builder.AppendSpan(3);
            region.Length.ShouldBe(3);
            region[0] = 'c';
            region[1] = 'd';
            region[2] = 'e';
            builder.ToString().ShouldBe("abcde");
        }

        [Fact]
        public void Slice_ReturnsExpectedRange()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("abcdef");
            builder.Slice(2, 3).ToString().ShouldBe("cde");
            builder.Slice(2).ToString().ShouldBe("cdef");
        }

        [Fact]
        public unsafe void GetPinnableReference_NullTerminates()
        {
            ValueStringBuilder builder = new(stackalloc char[8]);
            try
            {
                builder.Append("abc");
                fixed (char* p = builder)
                {
                    p[0].ShouldBe('a');
                    p[1].ShouldBe('b');
                    p[2].ShouldBe('c');
                    p[3].ShouldBe('\0');
                }
            }
            finally
            {
                builder.Dispose();
            }
        }

        [Fact]
        public void ToStringAndDispose_ReturnsContentAndDisposesBuilder()
        {
#pragma warning disable CA2000 // ToStringAndDispose is the explicit dispose path under test.
            ValueStringBuilder builder = new(stackalloc char[16]);
#pragma warning restore CA2000
            builder.Append("hello");
            string result = builder.ToStringAndDispose();
            result.ShouldBe("hello");
            // After disposal the builder is in default state — Length is 0 again.
            builder.Length.ShouldBe(0);
        }

        [Fact]
        public void ImplicitOperator_ReadOnlySpan_ReturnsContent()
        {
            using ValueStringBuilder builder = new(stackalloc char[16]);
            builder.Append("hello");
            ReadOnlySpan<char> span = builder;
            span.ToString().ShouldBe("hello");
        }
    }
}
