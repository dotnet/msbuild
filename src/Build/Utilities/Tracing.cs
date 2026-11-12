// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if DEBUG
using System;
#endif
using System.Collections.Generic;
using System.Diagnostics;
#if DEBUG
using System.Globalization;
#endif

#nullable disable

namespace Microsoft.Build.Internal;

/// <summary>
///  A debug-only helper class for engine tracing.
/// </summary>
internal static class Tracing
{
#if DEBUG
    /// <summary>
    ///  The named counters and their current values.
    /// </summary>
    private static readonly Dictionary<string, int> s_counts = [];

    /// <summary>
    ///  The time of the most recent trace emitted by <see cref="Record"/>.
    /// </summary>
    private static DateTime s_last = DateTime.MinValue;

    /// <summary>
    ///  The minimum interval between traces emitted by <see cref="Record"/>.
    /// </summary>
    private static readonly TimeSpan s_interval = GetTraceInterval();

    /// <summary>
    ///  The tagged value saved for the next trace emitted by <see cref="Record"/>.
    /// </summary>
    private static string s_slot = string.Empty;

    /// <summary>
    ///  Short name of the current assembly, included in trace output.
    /// </summary>
    private static readonly string s_currentAssemblyName = typeof(Tracing).Assembly.GetName().Name;

    /// <summary>
    ///  Reads the trace interval from <c>MSBUILDTRACEINTERVAL</c>.
    /// </summary>
    /// <returns>
    ///  The configured interval in seconds, or one second if the environment variable cannot be parsed.
    /// </returns>
    private static TimeSpan GetTraceInterval()
    {
        string val = Environment.GetEnvironmentVariable("MSBUILDTRACEINTERVAL");
        return !string.IsNullOrEmpty(val) && double.TryParse(
            val, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(1);
    }
#endif

    /// <summary>
    ///  Stores a tagged value for the next trace emitted by <see cref="Record"/>.
    /// </summary>
    /// <param name="tag">A label identifying the value.</param>
    /// <param name="value">The value to store.</param>
    [Conditional("DEBUG")]
    internal static void Slot(string tag, string value)
    {
#if DEBUG
        lock (s_counts)
        {
            s_slot = tag + ": " + value;
        }
#endif
    }

    /// <summary>
    ///  Stores a tagged representation of <paramref name="value"/> for the next trace emitted by <see cref="Record"/>.
    /// </summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="V">The value type.</typeparam>
    /// <param name="tag">A label identifying the value.</param>
    /// <param name="value">The pair used to format the stored value.</param>
    [Conditional("DEBUG")]
    internal static void Slot<K, V>(string tag, KeyValuePair<K, V> value)
    {
#if DEBUG
        Slot(tag, $"{value.Key}={value.Key}");
#endif
    }

    /// <summary>
    ///  Increments a named counter and emits a trace when the configured interval has elapsed.
    /// </summary>
    /// <param name="counter">The name of the counter to increment.</param>
    [Conditional("DEBUG")]
    internal static void Record(string counter)
    {
#if DEBUG
        lock (s_counts)
        {
            int newCount = s_counts.TryGetValue(counter, out int oldCount)
                ? oldCount + 1
                : 1;
            s_counts[counter] = newCount;
            DateTime now = DateTime.Now;

            if (now > s_last + s_interval)
            {
                Trace.WriteLine("================================================");
                Trace.WriteLine(s_slot);
                s_slot = string.Empty;
                Dump();
                Trace.WriteLine(Environment.StackTrace);
                s_last = now;
            }
        }
#endif
    }

    /// <summary>
    ///  Writes each item in <paramref name="items"/> to the trace output.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to write.</param>
    [Conditional("DEBUG")]
    internal static void List<T>(IEnumerable<T> items)
    {
#if DEBUG
        foreach (T item in items)
        {
            Trace.WriteLine(item.ToString());
        }
#endif
    }

    /// <summary>
    ///  Writes the assembly name and current counter values to the trace output when any counters exist.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void Dump()
    {
#if DEBUG
        if (s_counts.Count > 0)
        {
            Trace.WriteLine(s_currentAssemblyName);
            foreach (KeyValuePair<string, int> count in s_counts)
            {
                Trace.WriteLine($"# {count.Key}={count.Value}");
            }
        }
#endif
    }
}
