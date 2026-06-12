// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

// All Windows-only kernel-object APIs below (named MemoryMappedFile / Semaphore) are only ever
// reached when FeatureEnabled is true, which itself requires NativeMethodsShared.IsWindows.
#pragma warning disable CA1416 // Validate platform compatibility

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// A shared-memory fast path for large node packet payloads.
    /// </summary>
    /// <remarks>
    /// The existing named-pipe transport continues to carry every packet header and all small
    /// payloads inline, so framing, ordering, version negotiation, and the connection lifecycle
    /// (handshake, disconnect detection, node reuse, shutdown) are completely unchanged. Only the
    /// body of a packet whose serialized payload exceeds <see cref="PayloadThreshold"/> is moved
    /// out of the pipe and copied through a per-direction memory-mapped slot, signalled by a pair
    /// of semaphores. When this happens the writer sets <see cref="SharedMemoryFlag"/> on the
    /// 32-bit packet-length field of the pipe header; the reader detects the flag and reads the
    /// body from shared memory instead of the pipe.
    ///
    /// Each direction is strictly single-producer / single-consumer: the parent process writes the
    /// "p2c" slot and reads the "c2p" slot; the child does the opposite. The writer of a slot
    /// creates the underlying objects, the reader opens them (with a brief retry to absorb the
    /// creation race). Because the writer always creates its slot before placing the flagged header
    /// on the pipe, and the reader only opens after observing that header, opening is race-free.
    ///
    /// The feature is Windows-only (it relies on cross-process named memory-mapped files and
    /// semaphores) and opt-in via the <c>MSBUILDSHAREDMEMORYIPC</c> environment variable, which is
    /// inherited by launched nodes so both endpoints agree. Compiled only for modern .NET; the
    /// .NET Framework / CLR2 task host always uses the pipe.
    /// </remarks>
    internal sealed class NodeSharedMemoryChannel : IDisposable
    {
        /// <summary>
        /// High bit of the 32-bit packet length field, used to flag that the packet body was
        /// delivered through shared memory rather than inline on the pipe. Packet bodies never
        /// approach 2 GiB, so this bit is always free.
        /// </summary>
        internal const int SharedMemoryFlag = unchecked((int)0x80000000);

        /// <summary>
        /// Mask that recovers the real payload length from a (possibly flagged) length field.
        /// </summary>
        internal const int LengthMask = 0x7FFFFFFF;

        /// <summary>
        /// Default minimum serialized payload size (in bytes) for which the shared-memory path is used.
        /// </summary>
        private const int DefaultThreshold = 8 * 1024;

        /// <summary>
        /// Size of each direction's shared-memory window. Payloads larger than this are streamed
        /// through the window in multiple chunks.
        /// </summary>
        private const int SlotCapacity = 1024 * 1024;

        /// <summary>
        /// Maximum time to block on a semaphore between checks for disposal.
        /// </summary>
        private const int WaitQuantumMs = 100;

        /// <summary>
        /// Overall safety timeout for a single chunk hand-off, in case a peer dies mid-transfer.
        /// </summary>
        private const int AbandonTimeoutMs = 120_000;

        internal static readonly bool FeatureEnabled = ComputeEnabled();

        internal static readonly int PayloadThreshold = ComputeThreshold();

        /// <summary>Number of packets transferred out of this process via shared memory (diagnostics).</summary>
        internal static long PacketsSentViaSharedMemory;

        /// <summary>Number of payload bytes transferred out of this process via shared memory (diagnostics).</summary>
        internal static long BytesSentViaSharedMemory;

        private readonly string _baseName;
        private readonly bool _isParent;
        private readonly object _outgoingLock = new();

        private Slot? _outgoing;
        private Slot? _incoming;
        private volatile bool _disposed;

        internal NodeSharedMemoryChannel(int childProcessId, bool isParent)
        {
            _baseName = "MSBuildShm_" + childProcessId.ToString(CultureInfo.InvariantCulture);
            _isParent = isParent;
        }

        internal bool IsDisposed => _disposed;

        /// <summary>
        /// Returns true if a payload of the given size should be transferred via shared memory.
        /// </summary>
        internal static bool ShouldUseSharedMemory(int payloadLength) =>
            FeatureEnabled && payloadLength >= PayloadThreshold;

        /// <summary>
        /// True if the payload fits in a single slot window, so the producer can publish the whole
        /// body up front (before signalling the header on the pipe) without risk of deadlock.
        /// </summary>
        internal static bool IsSingleChunk(int payloadLength) => payloadLength <= SlotCapacity;

        /// <summary>
        /// Creates the outgoing slot if needed. Must be called by the writer before the flagged
        /// header is written to the pipe so the reader can open the slot once it sees the header.
        /// </summary>
        internal void EnsureOutgoingReady()
        {
            if (_outgoing is null)
            {
                lock (_outgoingLock)
                {
                    _outgoing ??= Slot.Create(OutgoingName);
                }
            }
        }

        /// <summary>
        /// Writes a payload to the outgoing slot, chunking if it exceeds the window size.
        /// </summary>
        internal void SendPayload(byte[] buffer, int offset, int count)
        {
            Slot slot = _outgoing ?? throw new InvalidOperationException("Outgoing shared-memory slot is not initialized.");
            int sent = 0;
            while (sent < count)
            {
                int chunk = Math.Min(count - sent, SlotCapacity);
                AcquireOrThrow(slot.Empty);
                slot.Write(buffer, offset + sent, chunk);
                Release(slot.Full);
                sent += chunk;
            }

            Interlocked.Increment(ref PacketsSentViaSharedMemory);
            Interlocked.Add(ref BytesSentViaSharedMemory, count);
        }

        /// <summary>
        /// Reads a payload of the given size from the incoming slot, opening it lazily on first use.
        /// </summary>
        internal void ReceivePayload(byte[] buffer, int offset, int count)
        {
            Slot slot = _incoming ??= Slot.Open(IncomingName, this);
            int received = 0;
            while (received < count)
            {
                int chunk = Math.Min(count - received, SlotCapacity);
                AcquireOrThrow(slot.Full);
                slot.Read(buffer, offset + received, chunk);
                Release(slot.Empty);
                received += chunk;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CommunicationsUtilities.Trace($"Shared-memory IPC channel '{_baseName}' disposed. Process total: {PacketsSentViaSharedMemory} packets / {BytesSentViaSharedMemory} bytes sent via shared memory.");
            _outgoing?.Dispose();
            _incoming?.Dispose();
        }

        private void AcquireOrThrow(Semaphore semaphore)
        {
            int waited = 0;
            while (true)
            {
                if (_disposed)
                {
                    throw new IOException("Shared-memory channel has been disposed.");
                }

                bool signaled;
                try
                {
                    signaled = semaphore.WaitOne(WaitQuantumMs);
                }
                catch (ObjectDisposedException)
                {
                    throw new IOException("Shared-memory channel has been disposed.");
                }

                if (signaled)
                {
                    return;
                }

                waited += WaitQuantumMs;
                if (waited >= AbandonTimeoutMs)
                {
                    throw new IOException("Timed out waiting on the shared-memory channel.");
                }
            }
        }

        private static void Release(Semaphore semaphore)
        {
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Best-effort signal; the slot is single-entry so an extra release is harmless.
            }
            catch (ObjectDisposedException)
            {
                // The channel was disposed concurrently; nothing left to signal.
            }
        }

        private string OutgoingName => _isParent ? _baseName + "_p2c" : _baseName + "_c2p";

        private string IncomingName => _isParent ? _baseName + "_c2p" : _baseName + "_p2c";

        private static bool ComputeEnabled()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return false;
            }

            string? value = Environment.GetEnvironmentVariable("MSBUILDSHAREDMEMORYIPC");
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ComputeThreshold()
        {
            string? value = Environment.GetEnvironmentVariable("MSBUILDSHAREDMEMORYIPCTHRESHOLD");
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out int parsed) && parsed > 0
                ? parsed
                : DefaultThreshold;
        }

        /// <summary>
        /// One direction of the channel: a memory-mapped window plus the two semaphores used to
        /// hand a single chunk back and forth between the producer and consumer.
        /// </summary>
        private sealed class Slot : IDisposable
        {
            private readonly MemoryMappedFile _mmf;
            private readonly MemoryMappedViewAccessor _view;
            private readonly IntPtr _dataPtr;

            private Slot(MemoryMappedFile mmf, MemoryMappedViewAccessor view, Semaphore empty, Semaphore full)
            {
                _mmf = mmf;
                _view = view;
                Empty = empty;
                Full = full;
                _dataPtr = IntPtr.Add(view.SafeMemoryMappedViewHandle.DangerousGetHandle(), (int)view.PointerOffset);
            }

            /// <summary>Signalled when the window is free for the producer to fill.</summary>
            internal Semaphore Empty { get; }

            /// <summary>Signalled when the window holds a chunk for the consumer to drain.</summary>
            internal Semaphore Full { get; }

            internal static Slot Create(string name)
            {
                MemoryMappedFile mmf = MemoryMappedFile.CreateNew(name, SlotCapacity);
                MemoryMappedViewAccessor view = mmf.CreateViewAccessor(0, SlotCapacity, MemoryMappedFileAccess.ReadWrite);
                Semaphore empty = new(initialCount: 1, maximumCount: 1, name + "_e");
                Semaphore full = new(initialCount: 0, maximumCount: 1, name + "_f");
                CommunicationsUtilities.Trace($"Shared-memory IPC: created slot '{name}'.");
                return new Slot(mmf, view, empty, full);
            }

            internal static Slot Open(string name, NodeSharedMemoryChannel owner)
            {
                int waited = 0;
                while (true)
                {
                    try
                    {
                        MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite);
                        MemoryMappedViewAccessor view = mmf.CreateViewAccessor(0, SlotCapacity, MemoryMappedFileAccess.ReadWrite);
                        Semaphore empty = Semaphore.OpenExisting(name + "_e");
                        Semaphore full = Semaphore.OpenExisting(name + "_f");
                        CommunicationsUtilities.Trace($"Shared-memory IPC: opened slot '{name}'.");
                        return new Slot(mmf, view, empty, full);
                    }
                    catch (Exception e) when (e is FileNotFoundException or WaitHandleCannotBeOpenedException)
                    {
                        if (owner._disposed)
                        {
                            throw new IOException("Shared-memory channel has been disposed.");
                        }

                        Thread.Sleep(WaitQuantumMs);
                        waited += WaitQuantumMs;
                        if (waited >= AbandonTimeoutMs)
                        {
                            throw new IOException("Timed out opening the shared-memory channel.", e);
                        }
                    }
                }
            }

            internal void Write(byte[] buffer, int offset, int count) => Marshal.Copy(buffer, offset, _dataPtr, count);

            internal void Read(byte[] buffer, int offset, int count) => Marshal.Copy(_dataPtr, buffer, offset, count);

            public void Dispose()
            {
                _view.Dispose();
                _mmf.Dispose();
                Empty.Dispose();
                Full.Dispose();
            }
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility
#endif
