// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging;

/// <summary>
/// Single-producer, bounded byte pipeline. Disposal waits for destination finalization.
/// </summary>
internal sealed class PipelinedWriteStream : Stream
{
    internal const int BufferSize = 64 * 1024;
    internal const int QueueCapacity = 4;
    private readonly Stream _destination;
    private readonly BlockingCollection<Work> _queue = new(QueueCapacity);
    private readonly CancellationTokenSource _stopped = new();
    private readonly Task _worker;
    private byte[]? _buffer;
    private int _count;
    private bool _disposed;

    internal PipelinedWriteStream(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException(null, nameof(destination));
        }

        _destination = destination;
        _worker = Task.Factory.StartNew(Pump, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    internal int QueuedBufferCount => _queue.Count;
    internal bool WorkerCompleted => _worker.IsCompleted;

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

#if NET
        Write(buffer.AsSpan(offset, count));
#else
        CheckWorker();
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
#endif
    }

#if NET
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CheckWorker();
        while (!buffer.IsEmpty)
        {
            _buffer ??= ArrayPool<byte>.Shared.Rent(BufferSize);
            int copied = Math.Min(buffer.Length, BufferSize - _count);
            buffer.Slice(0, copied).CopyTo(_buffer.AsSpan(_count));
            buffer = buffer.Slice(copied);
            _count += copied;
            if (_count == BufferSize)
            {
                PublishBuffer();
            }
        }
    }
#endif

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CheckWorker();
        PublishBuffer();
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new Work(null, 0, completion));
        Task.WhenAny(completion.Task, _worker).GetAwaiter().GetResult();
        CheckWorker();
        completion.Task.GetAwaiter().GetResult();
    }

    private void CheckWorker()
    {
        if (_worker.IsCompleted)
        {
            _worker.GetAwaiter().GetResult();
        }
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
        bool published = false;
        try
        {
            Enqueue(new Work(buffer, count, null));
            published = true;
        }
        finally
        {
            if (!published)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
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
            // Surface the worker's failure, not the cancellation used to unblock producers.
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
        ExceptionDispatchInfo? failure = null;
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
        catch (Exception exception)
        {
            // Transport failures to the producer after releasing buffers and closing the destination.
            failure = ExceptionDispatchInfo.Capture(exception);
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

            try
            {
                _destination.Dispose();
            }
            catch (Exception finalizationFailure) when (failure is not null)
            {
                throw new AggregateException(failure.SourceException, finalizationFailure);
            }
        }

        failure?.Throw();
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
