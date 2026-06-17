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
    /// Only payloads at or above <see cref="NodeSharedMemoryChannel.PayloadThreshold"/> are timed, so
    /// the same set of packets is compared whether or not the shared-memory feature is on. The hot
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

        // Per-packet-type and per-task breakdown of EVERY sent packet (regardless of size), to
        // answer "which packets / which tasks are big". Keyed by packet type name and task name.
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

        internal static void RecordPipeWrite(long startTimestamp, int bytes)
        {
            Interlocked.Add(ref s_pipeWriteTicks, Stopwatch.GetTimestamp() - startTimestamp);
            Interlocked.Add(ref s_pipeWriteBytes, bytes);
            Interlocked.Increment(ref s_pipeWriteCount);
        }

        internal static void RecordPipeRead(long startTimestamp, int bytes)
        {
            Interlocked.Add(ref s_pipeReadTicks, Stopwatch.GetTimestamp() - startTimestamp);
            Interlocked.Add(ref s_pipeReadBytes, bytes);
            Interlocked.Increment(ref s_pipeReadCount);
        }

        internal static void RecordShmSend(long startTimestamp, int bytes)
        {
            Interlocked.Add(ref s_shmSendTicks, Stopwatch.GetTimestamp() - startTimestamp);
            Interlocked.Add(ref s_shmSendBytes, bytes);
            Interlocked.Increment(ref s_shmSendCount);
        }

        internal static void RecordShmRecv(long startTimestamp, int bytes)
        {
            Interlocked.Add(ref s_shmRecvTicks, Stopwatch.GetTimestamp() - startTimestamp);
            Interlocked.Add(ref s_shmRecvBytes, bytes);
            Interlocked.Increment(ref s_shmRecvCount);
        }

        /// <summary>
        /// Records the type and size of every packet sent (any size), plus the task name when the
        /// packet is a <see cref="TaskHostConfiguration"/>, so we can report which packet types and
        /// which tasks dominate the payload.
        /// </summary>
        internal static void RecordSend(INodePacket packet, int payloadLength)
        {
            if (!Enabled)
            {
                return;
            }

            s_byPacketType.GetOrAdd(packet.Type.ToString(), static _ => new SizeBucket()).Add(payloadLength);

            if (packet is TaskHostConfiguration config)
            {
                string taskName = string.IsNullOrEmpty(config.TaskName) ? "(unknown)" : config.TaskName;
                s_byTask.GetOrAdd(taskName, static _ => new SizeBucket()).Add(payloadLength);
            }
        }

        /// <summary>
        /// Records the type and size of every packet received (parent &lt;- child), so we can report
        /// the logging and task-output traffic that flows back from TaskHosts.
        /// </summary>
        internal static void RecordReceive(NodePacketType packetType, int payloadLength)
        {
            if (!Enabled)
            {
                return;
            }

            s_recvByPacketType.GetOrAdd(packetType.ToString(), static _ => new SizeBucket()).Add(payloadLength);
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
            sb.AppendLine($"IPCSTATS pid={pid} (large-packet body transfer, threshold={NodeSharedMemoryChannel.PayloadThreshold} bytes)");
            sb.AppendLine(line("pipe-write", s_pipeWriteCount, s_pipeWriteBytes, s_pipeWriteTicks));
            sb.AppendLine(line("pipe-read", s_pipeReadCount, s_pipeReadBytes, s_pipeReadTicks));
            sb.AppendLine(line("shm-send", s_shmSendCount, s_shmSendBytes, s_shmSendTicks));
            sb.AppendLine(line("shm-recv", s_shmRecvCount, s_shmRecvBytes, s_shmRecvTicks));
            sb.AppendLine($"  pipe total time = {ms(s_pipeWriteTicks + s_pipeReadTicks):N2} ms; shm total time = {ms(s_shmSendTicks + s_shmRecvTicks):N2} ms");

            AppendBreakdown(sb, "SENT PACKETS BY TYPE (parent -> child, all sizes)", s_byPacketType);
            AppendBreakdown(sb, "SENT TaskHostConfiguration BY TASK (all sizes)", s_byTask);
            AppendBreakdown(sb, "RECEIVED PACKETS BY TYPE (parent <- child, all sizes)", s_recvByPacketType);
            return sb.ToString().TrimEnd();
        }

        private static void AppendBreakdown(StringBuilder sb, string title, ConcurrentDictionary<string, SizeBucket> map)
        {
            if (map.IsEmpty)
            {
                return;
            }

            sb.AppendLine($"  --- {title} ---");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "    {0,-40} {1,7} {2,15} {3,11} {4,11}", "name", "count", "total bytes", "avg bytes", "max bytes"));
            foreach (var kvp in map.OrderByDescending(p => p.Value.TotalBytes))
            {
                SizeBucket b = kvp.Value;
                long count = b.Count;
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0,-40} {1,7} {2,15:N0} {3,11:N0} {4,11:N0}",
                    kvp.Key.Length <= 40 ? kvp.Key : kvp.Key.Substring(0, 39) + "…",
                    count,
                    b.TotalBytes,
                    count > 0 ? b.TotalBytes / count : 0,
                    b.MaxBytes));
            }
        }

        private sealed class SizeBucket
        {
            private long _count;
            private long _totalBytes;
            private long _maxBytes;

            internal long Count => Interlocked.Read(ref _count);

            internal long TotalBytes => Interlocked.Read(ref _totalBytes);

            internal long MaxBytes => Interlocked.Read(ref _maxBytes);

            internal void Add(int bytes)
            {
                Interlocked.Increment(ref _count);
                Interlocked.Add(ref _totalBytes, bytes);

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
