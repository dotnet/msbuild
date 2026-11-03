// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// A count and the summed time per labeled bucket. Only evaluation cache diagnostics use it, and it is not thread safe.
/// </summary>
internal sealed class TimedBuckets(string[] labels)
{
    internal string[] Labels { get; } = labels;

    internal long[] Counts { get; } = new long[labels.Length];

    internal long[] Ticks { get; } = new long[labels.Length];

    internal void Add(int index, long ticks)
    {
        Counts[index]++;
        Ticks[index] += ticks;
    }

    internal void Merge(TimedBuckets other)
    {
        for (int i = 0; i < Counts.Length; i++)
        {
            Counts[i] += other.Counts[i];
            Ticks[i] += other.Ticks[i];
        }
    }

    /// <summary>Formats every used bucket as label:count:microseconds.</summary>
    internal string Format()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < Counts.Length; i++)
        {
            if (Counts[i] == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(Labels[i]).Append(':').Append(Counts[i]).Append(':').Append(Ticks[i] * 1_000_000 / Stopwatch.Frequency);
        }

        return builder.ToString();
    }
}

/// <summary>Shared duration buckets, from microsecond file system calls to whole-request phases.</summary>
internal static class LatencyBuckets
{
    private static readonly long[] s_upperBoundMicroseconds = [10, 25, 50, 100, 250, 500, 1_000, 2_500, 5_000, 10_000, 25_000, 100_000, 250_000];

    private static readonly long[] s_upperBoundTicks = ComputeUpperBoundTicks();

    internal static readonly string[] Labels =
    [
        "Le10us", "Le25us", "Le50us", "Le100us", "Le250us", "Le500us", "Le1ms",
        "Le2500us", "Le5ms", "Le10ms", "Le25ms", "Le100ms", "Le250ms", "Gt250ms",
    ];

    internal static int Index(long ticks)
    {
        for (int i = 0; i < s_upperBoundTicks.Length; i++)
        {
            if (ticks <= s_upperBoundTicks[i])
            {
                return i;
            }
        }

        return s_upperBoundTicks.Length;
    }

    private static long[] ComputeUpperBoundTicks()
    {
        var bounds = new long[s_upperBoundMicroseconds.Length];
        for (int i = 0; i < bounds.Length; i++)
        {
            bounds[i] = s_upperBoundMicroseconds[i] * Stopwatch.Frequency / 1_000_000;
        }

        return bounds;
    }
}
