// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for <see cref="BufferScope{T}"/>.
    /// </summary>
    public class BufferScope_Tests
    {
        [Fact]
        public void MinimumLengthConstructor_RentsAtLeastRequestedSize()
        {
            using BufferScope<char> buffer = new(16);
            buffer.Length.ShouldBeGreaterThanOrEqualTo(16);
        }

        [Fact]
        public void InitialBufferConstructor_UsesProvidedSpan()
        {
            Span<char> initial = stackalloc char[8];
            using BufferScope<char> buffer = new(initial);
            buffer.Length.ShouldBe(8);
        }

        [Fact]
        public void InitialBufferWithMinimum_UsesInitialWhenLargeEnough()
        {
            Span<byte> initial = stackalloc byte[32];
            using BufferScope<byte> buffer = new(initial, 16);
            buffer.Length.ShouldBe(32);
        }

        [Fact]
        public void InitialBufferWithMinimum_RentsWhenInitialTooSmall()
        {
            Span<byte> initial = stackalloc byte[4];
            using BufferScope<byte> buffer = new(initial, 128);
            buffer.Length.ShouldBeGreaterThanOrEqualTo(128);
        }

        [Fact]
        public void Indexer_GetsAndSetsValues()
        {
            using BufferScope<int> buffer = new(4);
            buffer[0] = 10;
            buffer[1] = 20;
            buffer[2] = 30;
            buffer[0].ShouldBe(10);
            buffer[1].ShouldBe(20);
            buffer[2].ShouldBe(30);
        }

        [Fact]
        public void Slice_ReturnsRequestedRange()
        {
            using BufferScope<char> buffer = new(10);
            buffer[0] = 'a';
            buffer[1] = 'b';
            buffer[2] = 'c';
            buffer[3] = 'd';

            Span<char> slice = buffer.Slice(1, 2);
            slice.Length.ShouldBe(2);
            slice[0].ShouldBe('b');
            slice[1].ShouldBe('c');
        }

        [Fact]
        public void ToString_ReturnsSpanContents()
        {
            Span<char> initial = stackalloc char[5];
            using BufferScope<char> buffer = new(initial);
            buffer[0] = 'h';
            buffer[1] = 'e';
            buffer[2] = 'l';
            buffer[3] = 'l';
            buffer[4] = 'o';

            buffer.ToString().ShouldBe("hello");
        }

        [Fact]
        public void EnsureCapacity_NoOpWhenAlreadyLargeEnough()
        {
            using BufferScope<int> buffer = new(64);
            int originalLength = buffer.Length;
            buffer.EnsureCapacity(32);
            buffer.Length.ShouldBe(originalLength);
        }

        [Fact]
        public void EnsureCapacity_GrowsWhenNeeded()
        {
            Span<byte> initial = stackalloc byte[4];
            using BufferScope<byte> buffer = new(initial);
            buffer.Length.ShouldBe(4);

            buffer.EnsureCapacity(128);
            buffer.Length.ShouldBeGreaterThanOrEqualTo(128);
        }

        [Fact]
        public void EnsureCapacity_WithCopy_PreservesExistingContents()
        {
            Span<int> initial = stackalloc int[4];
            using BufferScope<int> buffer = new(initial);
            buffer[0] = 1;
            buffer[1] = 2;
            buffer[2] = 3;
            buffer[3] = 4;

            buffer.EnsureCapacity(64, copy: true);

            buffer[0].ShouldBe(1);
            buffer[1].ShouldBe(2);
            buffer[2].ShouldBe(3);
            buffer[3].ShouldBe(4);
        }

        [Fact]
        public void AsSpan_ReturnsUnderlyingSpan()
        {
            using BufferScope<int> buffer = new(8);
            Span<int> span = buffer.AsSpan();
            span.Length.ShouldBe(buffer.Length);
        }

        [Fact]
        public void ImplicitSpanConversion_Works()
        {
            using BufferScope<int> buffer = new(8);
            buffer[0] = 42;
            Span<int> span = buffer;
            span[0].ShouldBe(42);
        }

        [Fact]
        public void ImplicitReadOnlySpanConversion_Works()
        {
            using BufferScope<int> buffer = new(8);
            buffer[0] = 42;
            ReadOnlySpan<int> span = buffer;
            span[0].ShouldBe(42);
        }

        [Fact]
        public void GetEnumerator_IteratesOverElements()
        {
            using BufferScope<int> buffer = new(stackalloc int[3]);
            buffer[0] = 1;
            buffer[1] = 2;
            buffer[2] = 3;

            int sum = 0;
            foreach (int value in buffer)
            {
                sum += value;
            }
            sum.ShouldBe(6);
        }

        [Fact]
        public void MinimumLengthConstructor_HandlesZeroLength()
        {
            using BufferScope<int> buffer = new(0);
            buffer.Length.ShouldBeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void InitialBufferConstructor_HandlesEmptySpan()
        {
            using BufferScope<int> buffer = new([]);
            buffer.Length.ShouldBe(0);
        }

        [Fact]
        public void InitialBufferWithMinimum_UsesInitialWhenEqualToMinimum()
        {
            using BufferScope<char> buffer = new(stackalloc char[10], 10);
            buffer.Length.ShouldBe(10);
        }

        [Fact]
        public void EnsureCapacity_GrowWithoutCopy_ExpandsBuffer()
        {
            using BufferScope<int> buffer = new(10);
            buffer[0] = 42;
            buffer[5] = 100;

            buffer.EnsureCapacity(50, copy: false);

            buffer.Length.ShouldBeGreaterThanOrEqualTo(50);
        }

        [Fact]
        public void EnsureCapacity_MultipleGrows_PreservesCopiedData()
        {
            using BufferScope<int> buffer = new(5);
            buffer[0] = 1;
            buffer[1] = 2;

            buffer.EnsureCapacity(10, copy: true);
            buffer[0].ShouldBe(1);
            buffer[1].ShouldBe(2);

            buffer.EnsureCapacity(50, copy: true);
            buffer.Length.ShouldBeGreaterThanOrEqualTo(50);
            buffer[0].ShouldBe(1);
            buffer[1].ShouldBe(2);
        }

        [Fact]
        public void RangeSlicing_FullRange_ReturnsAllElements()
        {
            using BufferScope<char> buffer = new(stackalloc char[5]);
            buffer[0] = 'A';
            buffer[1] = 'B';
            buffer[2] = 'C';
            buffer[3] = 'D';
            buffer[4] = 'E';

            Span<char> slice = buffer[..];
            slice.Length.ShouldBe(5);
            slice[0].ShouldBe('A');
            slice[4].ShouldBe('E');
        }

        [Fact]
        public void RangeSlicing_PartialRange_ReturnsExpectedElements()
        {
            using BufferScope<int> buffer = new(stackalloc int[10]);
            for (int i = 0; i < 10; i++)
            {
                buffer[i] = i;
            }

            Span<int> slice = buffer[2..8];
            slice.Length.ShouldBe(6);
            slice[0].ShouldBe(2);
            slice[5].ShouldBe(7);
        }

        [Fact]
        public void RangeSlicing_EmptyRange_ReturnsEmptySpan()
        {
            using BufferScope<byte> buffer = new(10);
            Span<byte> slice = buffer[5..5];
            slice.Length.ShouldBe(0);
        }

        [Fact]
        public void Slice_CanReturnZeroLengthSpan()
        {
            using BufferScope<int> buffer = new(10);
            Span<int> slice = buffer.Slice(5, 0);
            slice.Length.ShouldBe(0);
        }

        [Fact]
        public void GetEnumerator_EmptyBuffer_YieldsNoElements()
        {
            using BufferScope<string> buffer = new([]);

            int count = 0;
            foreach (string value in buffer)
            {
                _ = value;
                count++;
            }

            count.ShouldBe(0);
        }

        [Fact]
        public void ToString_EmptyBuffer_ReturnsEmptyString()
        {
            using BufferScope<char> buffer = new([]);
            buffer.ToString().ShouldBe(string.Empty);
        }

        [Fact]
        public void GetPinnableReference_CanModifyUnderlyingMemory()
        {
            using BufferScope<byte> buffer = new(stackalloc byte[10]);
            buffer[0] = 255;
            buffer[9] = 128;

            ref byte reference = ref buffer.GetPinnableReference();
            reference.ShouldBe((byte)255);
            reference = 100;

            buffer[0].ShouldBe((byte)100);
        }

        [Fact]
        public void GetPinnableReference_EmptyBuffer_DoesNotThrow()
        {
            using BufferScope<int> buffer = new([]);
            buffer.GetPinnableReference();
            buffer.Length.ShouldBe(0);
        }

        [Fact]
        public void Fixed_PinsPooledBuffer()
        {
            using BufferScope<char> buffer = new(64);
            buffer[0] = 'Y';

            unsafe
            {
                fixed (char* p = buffer)
                {
                    (*p).ShouldBe('Y');
                    *p = 'Z';
                }
            }

            buffer[0].ShouldBe('Z');
        }

        [Fact]
        public void WorksWithReferenceTypes()
        {
            using BufferScope<string> buffer = new(5);
            buffer[0] = "Hello";
            buffer[1] = "World";

            buffer[0].ShouldBe("Hello");
            buffer[1].ShouldBe("World");
        }

        [Fact]
        public void WorksWithValueTypeStructs()
        {
            using BufferScope<DateTime> buffer = new(3);
            DateTime date1 = new(2025, 1, 1);
            DateTime date2 = new(2025, 12, 31);

            buffer[0] = date1;
            buffer[1] = date2;

            buffer[0].ShouldBe(date1);
            buffer[1].ShouldBe(date2);
        }

        [Fact]
        public void CombinedOperations_GrowSliceAndEnumerate()
        {
            using BufferScope<int> buffer = new(stackalloc int[5], 10);

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = i + 1;
            }

            buffer.EnsureCapacity(20, copy: true);

            for (int i = 0; i < 5; i++)
            {
                buffer[i].ShouldBe(i + 1);
            }

            Span<int> slice = buffer[1..4];
            slice.Length.ShouldBe(3);
            slice[0].ShouldBe(2);
            slice[2].ShouldBe(4);

            int sum = 0;
            foreach (int value in buffer.AsSpan()[..5])
            {
                sum += value;
            }

            sum.ShouldBe(15);
        }

        [Fact]
        public void Dispose_ClearsSpan()
        {
            BufferScope<byte> buffer = new(16);
            buffer.Length.ShouldBeGreaterThan(0);
            buffer.Dispose();
            buffer.Length.ShouldBe(0);
        }

        [Fact]
        public void Dispose_SafeToCallMultipleTimes()
        {
            BufferScope<int> buffer = new(8);
            buffer.Dispose();
            // Calling Dispose a second time must not throw. ref structs cannot be
            // captured by a lambda, so invoke directly.
            buffer.Dispose();
        }

        [Fact]
        public void Fixed_PinsUnderlyingMemory()
        {
            using BufferScope<char> buffer = new(stackalloc char[8]);
            buffer[0] = 'x';
            unsafe
            {
                fixed (char* p = buffer)
                {
                    (*p).ShouldBe('x');
                }
            }
        }
    }
}
