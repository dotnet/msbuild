// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
#if NET7_0_OR_GREATER
using System.Runtime.CompilerServices;
#else
using System.Text;
using System.Threading;
#endif

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Helper class to indent all the lines of a potentially multi-line string with
/// minimal CPU and memory overhead.
/// </summary>
/// <remarks>
/// <see cref="IndentString"/> is a functional replacement for the following code:
/// <code>
///     string IndentString(string s, int indent)
///     {
///         string[] newLines = { "\r\n", "\n" };
///         string[] subStrings = s.Split(newLines, StringSplitOptions.None);
///
///         StringBuilder result = new StringBuilder(
///             (subStrings.Length * indent) +
///             (subStrings.Length * Environment.NewLine.Length) +
///             s.Length);
///
///         for (int i = 0; i &lt; subStrings.Length; i++)
///         {
///             result.Append(' ', indent).Append(subStrings[i]);
///             result.AppendLine();
///         }
///
///         return result.ToString();
///     }
/// </code>
/// On net472, benchmarks show that the optimized version runs in about 50-60% of the time
/// and has about 15% of the memory overhead of the code that it replaces.
/// <para>
/// On net7.0, the optimized version runs in about 45-55% of the time and has about 30%
/// of the memory overhead of the code that it replaces.
/// </para>
/// </remarks>
internal static class OptimizedStringIndenter
{
#nullable enable
#if NET7_0_OR_GREATER
    [SkipLocalsInit]
#endif
    internal static unsafe string IndentString(string? s, int indent)
    {
        if (s is null)
        {
            return string.Empty;
        }

        Span<StringSegment> segments = GetStringSegments(s.AsSpan(), stackalloc StringSegment[128], out StringSegment[]? pooledArray);

        int indentedStringLength = segments.Length * (Environment.NewLine.Length + indent);
        foreach (StringSegment segment in segments)
        {
            indentedStringLength += segment.Length;
        }

#if NET7_0_OR_GREATER
#pragma warning disable CS8500
        string result = string.Create(indentedStringLength, (s, (IntPtr)(&segments), indent), static (output, state) =>
        {
            ReadOnlySpan<char> input = state.s;
            foreach (StringSegment segment in *(Span<StringSegment>*)state.Item2)
            {
                // Append indent
                output.Slice(0, state.indent).Fill(' ');
                output = output.Slice(state.indent);

                // Append string segment
                input.Slice(0, segment.Length).CopyTo(output);
                input = input.Slice(segment.TotalLength);
                output = output.Slice(segment.Length);

                // Append newline
                Environment.NewLine.CopyTo(output);
                output = output.Slice(Environment.NewLine.Length);
            }
        });
#pragma warning restore CS8500
#else
        using RentedBuilder rental = RentBuilder(indentedStringLength);

        foreach (StringSegment segment in segments)
        {
            rental.Builder
                .Append(' ', indent)
                .Append(s, segment.Start, segment.Length)
                .AppendLine();
        }

        string result = rental.Builder.ToString();
#endif

        if (pooledArray is not null)
        {
            ArrayPool<StringSegment>.Shared.Return(pooledArray);
        }

        return result;
    }

    private static Span<StringSegment> GetStringSegments(ReadOnlySpan<char> input, Span<StringSegment> segments, out StringSegment[]? pooledArray)
    {
        if (input.IsEmpty)
        {
            segments = segments.Slice(0, 1);
            segments[0] = new StringSegment(0, 0, 0);
            pooledArray = null;
            return segments;
        }

        int segmentCount = 1;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '\n')
            {
                segmentCount++;
            }
        }

        if (segmentCount <= segments.Length)
        {
            pooledArray = null;
            segments = segments.Slice(0, segmentCount);
        }
        else
        {
            pooledArray = ArrayPool<StringSegment>.Shared.Rent(segmentCount);
            segments = pooledArray.AsSpan(0, segmentCount);
        }

        int start = 0;
        for (int i = 0; i < segments.Length; i++)
        {
            int index = input.IndexOf('\n');
            if (index < 0)
            {
                segments[i] = new StringSegment(start, input.Length, 0);
                break;
            }

            int newLineLength = 1;
            if (index > 0 && input[index - 1] == '\r')
            {
                newLineLength++;
                index--;
            }

            int totalLength = index + newLineLength;
            segments[i] = new StringSegment(start, index, totalLength);

            start += totalLength;
            input = input.Slice(totalLength);
        }

        return segments;
    }

    private struct StringSegment
    {
        public StringSegment(int start, int length, int totalLength)
        {
            Start = start;
            Length = length;
            TotalLength = totalLength;
        }

        public int Start { get; }
        public int Length { get; }
        public int TotalLength { get; }
    }

#if !NET7_0_OR_GREATER
    private static RentedBuilder RentBuilder(int capacity) => new RentedBuilder(capacity);

    private ref struct RentedBuilder
    {
        // The maximum capacity for a StringBuilder that we'll cache.  StringBuilders with
        // larger capacities will be allowed to be GC'd.
        private const int MaxStringBuilderCapacity = 512;

        private static StringBuilder? _cachedBuilder;

        public RentedBuilder(int capacity)
        {
            Builder = Interlocked.Exchange(ref _cachedBuilder, null) ?? new StringBuilder(capacity);
            Builder.EnsureCapacity(capacity);
        }

        public void Dispose()
        {
            // if builder's capacity is within our limits, return it to the cache
            if (Builder.Capacity <= MaxStringBuilderCapacity)
            {
                Builder.Clear();
                Interlocked.Exchange(ref _cachedBuilder, Builder);
            }
        }

        public StringBuilder Builder { get; }
    }
#endif
}
