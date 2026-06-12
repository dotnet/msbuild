// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

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
            return string.Join(
                Environment.NewLine,
                $"IPCSTATS pid={pid} (large-packet body transfer, threshold={NodeSharedMemoryChannel.PayloadThreshold} bytes)",
                line("pipe-write", s_pipeWriteCount, s_pipeWriteBytes, s_pipeWriteTicks),
                line("pipe-read", s_pipeReadCount, s_pipeReadBytes, s_pipeReadTicks),
                line("shm-send", s_shmSendCount, s_shmSendBytes, s_shmSendTicks),
                line("shm-recv", s_shmRecvCount, s_shmRecvBytes, s_shmRecvTicks),
                $"  pipe total time = {ms(s_pipeWriteTicks + s_pipeReadTicks):N2} ms; shm total time = {ms(s_shmSendTicks + s_shmRecvTicks):N2} ms");
        }
    }
}
#endif
