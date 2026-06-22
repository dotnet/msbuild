// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

#nullable enable

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Opt-in, file-based profiler that records (a) per-field serialized byte sizes of
    /// <c>TaskHostConfiguration</c> / <c>TaskHostTaskComplete</c> and (b) per-phase wall time, so the
    /// content and cost of task-host IPC packets can be quantified. This is the instrumentation used to
    /// measure that the build process environment is ~14% of every config and ~42% of every result packet
    /// (see dotnet/msbuild#14097).
    ///
    /// Byte sizes are read from the *write side* only: the translator's <c>Writer</c> serializes into an
    /// in-memory <see cref="MemoryStream"/> whose <c>Position</c> is seekable; the read side consumes a
    /// non-seekable pipe and cannot be position-measured. The serialized byte count is identical in both
    /// directions, so write-side measurement is exact.
    ///
    /// Enable by setting <c>MSBUILDTHPROFILE</c> to a writable directory; each process flushes a per-PID CSV
    /// at exit. An external aggregator sums per field across the build-manager (configs) and all task-host
    /// processes (results). Zero overhead when the env var is unset.
    /// </summary>
    internal static class ThProfile
    {
        private static readonly string? s_dir = Environment.GetEnvironmentVariable("MSBUILDTHPROFILE");

        internal static readonly bool Enabled = !string.IsNullOrEmpty(s_dir);

        // name -> [count, totalBytes]
        private static readonly ConcurrentDictionary<string, long[]> s_fields = new(StringComparer.Ordinal);

        // name -> [count, totalTicks]
        private static readonly ConcurrentDictionary<string, long[]> s_phases = new(StringComparer.Ordinal);

        private static int s_dumped;

        static ThProfile()
        {
            if (Enabled)
            {
                AppDomain.CurrentDomain.ProcessExit += static (_, _) => Dump();
            }
        }

        internal static long Now() => Enabled ? Stopwatch.GetTimestamp() : 0;

        /// <summary>Records the serialized byte size contributed by a single field of a packet.</summary>
        internal static void AddField(string name, long bytes)
        {
            if (Enabled && bytes >= 0)
            {
                Accumulate(s_fields, name, bytes);
            }
        }

        /// <summary>Records the wall time of a serialization/deserialization phase.</summary>
        internal static void AddPhase(string name, long startTimestamp)
        {
            if (Enabled && startTimestamp != 0)
            {
                Accumulate(s_phases, name, Stopwatch.GetTimestamp() - startTimestamp);
            }
        }

        private static void Accumulate(ConcurrentDictionary<string, long[]> map, string name, long value)
        {
            long[] entry = map.GetOrAdd(name, static _ => new long[2]);
            lock (entry)
            {
                entry[0]++;
                entry[1] += value;
            }
        }

        private static void Dump()
        {
            if (!Enabled || System.Threading.Interlocked.Exchange(ref s_dumped, 1) != 0)
            {
                return;
            }

            if (s_fields.IsEmpty && s_phases.IsEmpty)
            {
                return;
            }

            double msPerTick = 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder();
            sb.Append("kind,name,count,total\n");
            foreach (var kvp in s_fields)
            {
                long[] e = kvp.Value;
                long c;
                long v;
                lock (e)
                {
                    c = e[0];
                    v = e[1];
                }

                sb.Append("BYTES,").Append(kvp.Key).Append(',').Append(c.ToString(CultureInfo.InvariantCulture)).Append(',').Append(v.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            foreach (var kvp in s_phases)
            {
                long[] e = kvp.Value;
                long c;
                long v;
                lock (e)
                {
                    c = e[0];
                    v = e[1];
                }

                sb.Append("MS,").Append(kvp.Key).Append(',').Append(c.ToString(CultureInfo.InvariantCulture)).Append(',').Append((v * msPerTick).ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            }

            try
            {
                Directory.CreateDirectory(s_dir!);
                int pid =
#if NET
                    Environment.ProcessId;
#else
                    Process.GetCurrentProcess().Id;
#endif
                File.WriteAllText(Path.Combine(s_dir!, $"thprofile_{pid}.csv"), sb.ToString());
            }
            catch (IOException)
            {
                // Best effort; losing one process's stats file is acceptable for a measurement run.
            }
        }
    }
}
