// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Opt-in, low-overhead instrumentation that measures the wall-clock time the engine spends
    /// transferring large packet bodies over the node transport, split by mechanism (named pipe vs
    /// shared memory) and by direction (send vs receive). Enabled with <c>MSBUILDIPCSTATS=1</c>.
    /// </summary>
    /// <remarks>
    /// Only payloads at or above the direction-specific threshold are diverted, so the same set of
    /// packets is compared whether or not the shared-memory feature is on. The hot
    /// path is a single <see cref="Stopwatch.GetTimestamp"/> pair plus a few interlocked adds. Totals
    /// are written to the file named by <c>MSBUILDIPCSTATSFILE</c> (or the comm trace) at process exit.
    /// </remarks>
    internal static class IpcTransferStats
    {
        internal static readonly bool Enabled =
            Environment.GetEnvironmentVariable("MSBUILDIPCSTATS") == "1";

        private static long s_pipeWriteTicks;
        private static long s_pipeWriteBytes;
        private static long s_pipeWriteCount;

        private static long s_pipeReadTicks;
        private static long s_pipeReadBytes;
        private static long s_pipeReadCount;

        private static long s_shmSendTicks;
        private static long s_shmSendBytes;
        private static long s_shmSendCount;

        private static long s_shmRecvTicks;
        private static long s_shmRecvBytes;
        private static long s_shmRecvCount;

        // Per-packet-type and per-task breakdown of EVERY sent packet (regardless of size), with
        // time split by mechanism (pipe vs shm), to answer "which tasks improve by how much".
        private static readonly ConcurrentDictionary<string, SizeBucket> s_byPacketType = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, SizeBucket> s_byTask = new(StringComparer.Ordinal);

        // Per-packet-type breakdown of EVERY received packet (parent <- child: logging + outputs).
        private static readonly ConcurrentDictionary<string, SizeBucket> s_recvByPacketType = new(StringComparer.Ordinal);

        private static int s_dumped;

        static IpcTransferStats()
        {
            if (Enabled)
            {
                AppDomain.CurrentDomain.ProcessExit += static (_, _) => Dump();
            }
        }

        internal static long StartTimestamp() => Enabled ? Stopwatch.GetTimestamp() : 0;

        internal static long Elapsed(long startTimestamp) => Stopwatch.GetTimestamp() - startTimestamp;

        /// <summary>
        /// Records a packet sent (parent -&gt; child): aggregate mechanism totals plus per-type and
        /// per-task (for <see cref="TaskHostConfiguration"/>) bytes and time, split by mechanism.
        /// </summary>
        internal static void RecordSend(INodePacket packet, int payloadLength, long elapsedTicks, bool viaSharedMemory)
        {
            if (!Enabled)
            {
                return;
            }

            if (viaSharedMemory)
            {
                Interlocked.Add(ref s_shmSendTicks, elapsedTicks);
                Interlocked.Add(ref s_shmSendBytes, payloadLength);
                Interlocked.Increment(ref s_shmSendCount);
            }
            else
            {
                Interlocked.Add(ref s_pipeWriteTicks, elapsedTicks);
                Interlocked.Add(ref s_pipeWriteBytes, payloadLength);
                Interlocked.Increment(ref s_pipeWriteCount);
            }

            s_byPacketType.GetOrAdd(packet.Type.ToString(), static _ => new SizeBucket()).Add(payloadLength, elapsedTicks, viaSharedMemory);

            if (packet is TaskHostConfiguration config)
            {
                string taskName = string.IsNullOrEmpty(config.TaskName) ? "(unknown)" : config.TaskName;
                s_byTask.GetOrAdd(taskName, static _ => new SizeBucket()).Add(payloadLength, elapsedTicks, viaSharedMemory);
            }
        }

        /// <summary>
        /// Records a packet received (parent &lt;- child): aggregate mechanism totals plus per-type
        /// bytes and time (logging + task outputs), split by mechanism.
        /// </summary>
        internal static void RecordReceive(NodePacketType packetType, int payloadLength, long elapsedTicks, bool viaSharedMemory)
        {
            if (!Enabled)
            {
                return;
            }

            if (viaSharedMemory)
            {
                Interlocked.Add(ref s_shmRecvTicks, elapsedTicks);
                Interlocked.Add(ref s_shmRecvBytes, payloadLength);
                Interlocked.Increment(ref s_shmRecvCount);
            }
            else
            {
                Interlocked.Add(ref s_pipeReadTicks, elapsedTicks);
                Interlocked.Add(ref s_pipeReadBytes, payloadLength);
                Interlocked.Increment(ref s_pipeReadCount);
            }

            s_recvByPacketType.GetOrAdd(packetType.ToString(), static _ => new SizeBucket()).Add(payloadLength, elapsedTicks, viaSharedMemory);
        }

        internal static void Dump()
        {
            if (!Enabled || Interlocked.Exchange(ref s_dumped, 1) != 0)
            {
                return;
            }

            string report = BuildReport();

            string? file = Environment.GetEnvironmentVariable("MSBUILDIPCSTATSFILE");
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    File.AppendAllText(file, report + Environment.NewLine);
                }
                catch (IOException)
                {
                    CommunicationsUtilities.Trace($"IPC stats: failed to write '{file}'.");
                }
            }

            CommunicationsUtilities.Trace($"IPC transfer stats:{Environment.NewLine}{report}");
        }

        private static string BuildReport()
        {
            double ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
            string line(string name, long count, long bytes, long ticks) =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0,-10} count={1,6} bytes={2,12:N0} time={3,10:N2} ms  ({4:N1} MB/s)",
                    name,
                    count,
                    bytes,
                    ms(ticks),
                    ms(ticks) > 0 ? (bytes / 1024.0 / 1024.0) / (ms(ticks) / 1000.0) : 0);

            int pid = Environment.ProcessId;
            var sb = new StringBuilder();
            sb.AppendLine($"IPCSTATS pid={pid} sendThr={NodeSharedMemoryChannel.SendThreshold} returnThr={NodeSharedMemoryChannel.ReturnThreshold} slot={NodeSharedMemoryChannel.SlotCapacity} shmEnabled={NodeSharedMemoryChannel.FeatureEnabled}");
            sb.AppendLine(line("pipe-write", s_pipeWriteCount, s_pipeWriteBytes, s_pipeWriteTicks));
            sb.AppendLine(line("pipe-read", s_pipeReadCount, s_pipeReadBytes, s_pipeReadTicks));
            sb.AppendLine(line("shm-send", s_shmSendCount, s_shmSendBytes, s_shmSendTicks));
            sb.AppendLine(line("shm-recv", s_shmRecvCount, s_shmRecvBytes, s_shmRecvTicks));
            sb.AppendLine($"  send total time = {ms(s_pipeWriteTicks + s_shmSendTicks):N2} ms; recv total time = {ms(s_pipeReadTicks + s_shmRecvTicks):N2} ms; ALL = {ms(s_pipeWriteTicks + s_shmSendTicks + s_pipeReadTicks + s_shmRecvTicks):N2} ms");

            AppendBreakdown(sb, "SENT PACKETS BY TYPE (parent -> child)", s_byPacketType);
            AppendBreakdown(sb, "SENT TaskHostConfiguration BY TASK", s_byTask);
            AppendBreakdown(sb, "RECEIVED PACKETS BY TYPE (parent <- child)", s_recvByPacketType);
            return sb.ToString().TrimEnd();
        }

        private static void AppendBreakdown(StringBuilder sb, string title, ConcurrentDictionary<string, SizeBucket> map)
        {
            if (map.IsEmpty)
            {
                return;
            }

            double ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
            sb.AppendLine($"  --- {title} ---");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "    {0,-42} {1,7} {2,14} {3,10} {4,10} {5,11} {6,11}", "name", "count", "total bytes", "avg", "max", "pipe ms", "shm ms"));
            foreach (var kvp in map.OrderByDescending(p => p.Value.TotalBytes))
            {
                SizeBucket b = kvp.Value;
                long count = b.Count;
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0,-42} {1,7} {2,14:N0} {3,10:N0} {4,10:N0} {5,11:N2} {6,11:N2}",
                    kvp.Key.Length <= 42 ? kvp.Key : kvp.Key.Substring(0, 41) + "…",
                    count,
                    b.TotalBytes,
                    count > 0 ? b.TotalBytes / count : 0,
                    b.MaxBytes,
                    ms(b.PipeTicks),
                    ms(b.ShmTicks)));
            }
        }

        private sealed class SizeBucket
        {
            private long _count;
            private long _totalBytes;
            private long _maxBytes;
            private long _pipeTicks;
            private long _shmTicks;

            internal long Count => Interlocked.Read(ref _count);

            internal long TotalBytes => Interlocked.Read(ref _totalBytes);

            internal long MaxBytes => Interlocked.Read(ref _maxBytes);

            internal long PipeTicks => Interlocked.Read(ref _pipeTicks);

            internal long ShmTicks => Interlocked.Read(ref _shmTicks);

            internal void Add(int bytes, long ticks, bool viaSharedMemory)
            {
                Interlocked.Increment(ref _count);
                Interlocked.Add(ref _totalBytes, bytes);
                Interlocked.Add(ref viaSharedMemory ? ref _shmTicks : ref _pipeTicks, ticks);

                long current = Interlocked.Read(ref _maxBytes);
                while (bytes > current)
                {
                    long prior = Interlocked.CompareExchange(ref _maxBytes, bytes, current);
                    if (prior == current)
                    {
                        break;
                    }

                    current = prior;
                }
            }
        }
    }
}
#endif
