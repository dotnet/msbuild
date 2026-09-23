// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging;

/// <summary>
/// Experimental single-producer byte pipeline. Event ordering and string interning stay on the producer.
/// </summary>
internal sealed class PipelinedWriteStream : Stream
{
    private const int BufferSize = 256 * 1024;
    private readonly Stream _destination;
    private readonly BlockingCollection<Work> _queue = new(boundedCapacity: 8);
    private readonly CancellationTokenSource _stopped = new();
    private readonly Task _worker;
    private byte[]? _buffer;
    private int _count;
    private bool _disposed;

    internal PipelinedWriteStream(Stream destination)
    {
        _destination = destination;
        _worker = Task.Factory.StartNew(Pump, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count)
        {
            throw new ArgumentException(null, nameof(count));
        }

        while (count > 0)
        {
            _buffer ??= ArrayPool<byte>.Shared.Rent(BufferSize);
            int copied = Math.Min(count, BufferSize - _count);
            Buffer.BlockCopy(buffer, offset, _buffer, _count, copied);
            offset += copied;
            count -= copied;
            _count += copied;
            if (_count == BufferSize)
            {
                PublishBuffer();
            }
        }
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PublishBuffer();
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new Work(null, 0, completion));
        Task.WhenAny(completion.Task, _worker).GetAwaiter().GetResult();
        if (_worker.IsCompleted)
        {
            _worker.GetAwaiter().GetResult();
        }
        completion.Task.GetAwaiter().GetResult();
    }

    private void PublishBuffer()
    {
        if (_count == 0)
        {
            return;
        }

        byte[] buffer = _buffer!;
        int count = _count;
        _buffer = null;
        _count = 0;
        try
        {
            Enqueue(new Work(buffer, count, null));
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    private void Enqueue(Work work)
    {
        try
        {
            _queue.Add(work, _stopped.Token);
        }
        catch (OperationCanceledException) when (_stopped.IsCancellationRequested)
        {
            // Preserve the original compression/I/O failure rather than reporting queue cancellation.
            _worker.GetAwaiter().GetResult();
            throw;
        }
        catch (InvalidOperationException) when (_stopped.IsCancellationRequested)
        {
            _worker.GetAwaiter().GetResult();
            throw;
        }
    }

    private void Pump()
    {
        try
        {
            foreach (Work work in _queue.GetConsumingEnumerable())
            {
                if (work.Buffer is { } buffer)
                {
                    try
                    {
                        _destination.Write(buffer, 0, work.Count);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                else
                {
                    _destination.Flush();
                    work.Completion!.SetResult(true);
                }
            }
        }
        finally
        {
            _stopped.Cancel();
            _queue.CompleteAdding();
            while (_queue.TryTake(out Work work))
            {
                if (work.Buffer is { } buffer)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            _destination.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            try
            {
                Flush();
            }
            finally
            {
                _queue.CompleteAdding();
                try
                {
                    _worker.GetAwaiter().GetResult();
                }
                finally
                {
                    _disposed = true;
                    if (_buffer is not null)
                    {
                        ArrayPool<byte>.Shared.Return(_buffer);
                        _buffer = null;
                    }
                    _queue.Dispose();
                    _stopped.Dispose();
                }
            }
        }
        base.Dispose(disposing);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    private readonly record struct Work(byte[]? Buffer, int Count, TaskCompletionSource<bool>? Completion);
}
