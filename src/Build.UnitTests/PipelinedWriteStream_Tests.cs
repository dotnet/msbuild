// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class PipelinedWriteStreamTests(ITestOutputHelper output)
{
    [Fact]
    public void CopiesCallerBufferBeforeReturning()
    {
        var destination = new MemoryStream();
        using var pipeline = new PipelinedWriteStream(destination);
        byte[] bytes = [1, 2, 3];
        pipeline.Write(bytes, 0, bytes.Length);
        bytes[0] = 9;
        pipeline.Flush();
        destination.ToArray().ShouldBe([(byte)1, 2, 3]);
    }

    [Fact]
    public void PreservesOrderingAndFlushBoundaries()
    {
        byte[] bytes = new byte[5 * PipelinedWriteStream.BufferSize + 17];
        new Random(42).NextBytes(bytes);
        var destination = new MemoryStream();
        using (var pipeline = new PipelinedWriteStream(destination))
        {
            pipeline.Write(bytes, 0, 13);
            pipeline.Flush();
            destination.ToArray().ShouldBe(bytes.Take(13).ToArray());
            pipeline.Write(bytes, 13, bytes.Length - 13);
            pipeline.Flush();
            destination.ToArray().ShouldBe(bytes);
            pipeline.WriteByte(123);
        }

        destination.ToArray().ShouldBe(bytes.Append((byte)123).ToArray());
        destination.CanWrite.ShouldBeFalse();
    }

    [Theory]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.Fastest)]
    public void DisposeFinalizesGzip(CompressionLevel level)
    {
        byte[] bytes = new byte[3 * PipelinedWriteStream.BufferSize + 19];
        new Random(42).NextBytes(bytes);
        using var destination = new MemoryStream();
        var pipeline = new PipelinedWriteStream(new GZipStream(destination, level, leaveOpen: true));
        pipeline.Write(bytes, 0, bytes.Length);
        pipeline.Dispose();
        pipeline.WorkerCompleted.ShouldBeTrue();
        pipeline.CanWrite.ShouldBeFalse();
        pipeline.Dispose();

        destination.Position = 0;
        using var decoded = new MemoryStream();
        using (var gzip = new GZipStream(destination, CompressionMode.Decompress, leaveOpen: true))
        {
            gzip.CopyTo(decoded);
        }

        decoded.ToArray().ShouldBe(bytes);
        Should.Throw<ObjectDisposedException>(() => pipeline.Write(bytes, 0, 1));
        Should.Throw<ObjectDisposedException>(pipeline.Flush);
    }

    [Theory]
    [InlineData(CompressionLevel.Optimal, false)]
    [InlineData(CompressionLevel.Fastest, false)]
    [InlineData(CompressionLevel.Optimal, true)]
    [InlineData(CompressionLevel.Fastest, true)]
    public void PreservesSerializedEventBytes(CompressionLevel level, bool pipelined)
    {
        ITaskItem[] items =
        [
            new TaskItemData("first", new Dictionary<string, string> { ["metadata"] = "value" }),
            new TaskItemData("second", null)
        ];
        BuildEventArgs[] events = new BuildEventArgs[25];
        events[0] = new TaskParameterEventArgs(TaskParameterMessageKind.TaskInput,
            "parameter", "property", "item", items, true, DateTime.MinValue);
        for (int i = 1; i < events.Length; i++)
        {
            events[i] = new BuildMessageEventArgs(new string((char)('a' + i), 32768 + i),
                null, "test", MessageImportance.Low, DateTime.MinValue);
        }

        using var expected = new MemoryStream();
        Serialize(expected);
        expected.Length.ShouldBeGreaterThan(PipelinedWriteStream.BufferSize * PipelinedWriteStream.QueueCapacity);

        using var compressed = new MemoryStream();
        Stream destination = new GZipStream(compressed, level, leaveOpen: true);
        if (pipelined)
        {
            destination = new PipelinedWriteStream(destination);
        }

        using (var buffered = new BufferedStream(destination, 32768))
        {
            Serialize(buffered);
        }

        compressed.Position = 0;
        using var decoded = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: true))
        {
            gzip.CopyTo(decoded);
        }

        decoded.ToArray().ShouldBe(expected.ToArray());

        void Serialize(Stream stream)
        {
            using var binaryWriter = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            var eventWriter = new BuildEventArgsWriter(binaryWriter);
            foreach (BuildEventArgs buildEvent in events)
            {
                eventWriter.Write(buildEvent);
            }
        }
    }

    [Fact]
    public async Task AppliesBackpressureAtTheQueueBound()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var destination = new BlockingStream(entered, release);
        using var pipeline = new PipelinedWriteStream(destination);
        byte[] bytes = new byte[(PipelinedWriteStream.QueueCapacity + 3) * PipelinedWriteStream.BufferSize];
        Task producer = Task.Run(() => pipeline.Write(bytes, 0, bytes.Length));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            SpinWait.SpinUntil(() => pipeline.QueuedBufferCount == PipelinedWriteStream.QueueCapacity,
                TimeSpan.FromSeconds(5)).ShouldBeTrue();
            producer.IsCompleted.ShouldBeFalse();
            release.Set();
            (await Task.WhenAny(producer, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(producer);
            await producer;
            pipeline.Flush();
            destination.Length.ShouldBe(bytes.Length);
        }
        finally
        {
            release.Set();
            await producer;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FlushAndDisposeWaitForPendingWrite(bool dispose)
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var destination = new BlockingStream(entered, release);
        using var pipeline = new PipelinedWriteStream(destination);
        pipeline.WriteByte(42);
        Task completion = Task.Run(() =>
        {
            if (dispose)
            {
                pipeline.Dispose();
            }
            else
            {
                pipeline.Flush();
            }
        });
        try
        {
            entered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            completion.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.Set();
            (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(completion);
            await completion;
        }

        destination.ToArray().ShouldBe([(byte)42]);
        if (dispose)
        {
            pipeline.WorkerCompleted.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task FailureReleasesProducerBlockedOnFullQueue()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var expected = new IOException("failure after producer blocks");
        using var pipeline = new PipelinedWriteStream(new BlockingStream(entered, release, expected));
        byte[] bytes = new byte[(PipelinedWriteStream.QueueCapacity + 3) * PipelinedWriteStream.BufferSize];
        Task<Exception?> producer = Task.Run(() => Record.Exception(() => pipeline.Write(bytes, 0, bytes.Length)));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            SpinWait.SpinUntil(() => pipeline.QueuedBufferCount == PipelinedWriteStream.QueueCapacity,
                TimeSpan.FromSeconds(5)).ShouldBeTrue();
            producer.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.Set();
        }

        (await Task.WhenAny(producer, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(producer);
        (await producer).ShouldBeSameAs(expected);
        Should.Throw<IOException>(pipeline.Dispose).ShouldBeSameAs(expected);
        pipeline.WorkerCompleted.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropagatesWorkerFailureAndUnblocksProducer(bool failOnFlush)
    {
        var expected = new IOException("expected write or flush failure");
        var destination = new FailingStream(failOnFlush ? null : expected, failOnFlush ? expected : null, null);
        var pipeline = new PipelinedWriteStream(destination);
        byte[] bytes = new byte[(PipelinedWriteStream.QueueCapacity + 3) * PipelinedWriteStream.BufferSize];
        Should.Throw<IOException>(() =>
        {
            pipeline.Write(bytes, 0, bytes.Length);
            pipeline.Flush();
        }).ShouldBeSameAs(expected);

        Should.Throw<IOException>(() => pipeline.Write(bytes, 0, 1)).ShouldBeSameAs(expected);
        Should.Throw<IOException>(pipeline.Dispose).ShouldBeSameAs(expected);
        pipeline.WorkerCompleted.ShouldBeTrue();
        destination.WasDisposed.ShouldBeTrue();
        pipeline.Dispose();
    }

    [Fact]
    public void PropagatesFinalizationFailure()
    {
        var expected = new IOException("expected finalization failure");
        var destination = new FailingStream(null, null, expected);
        var pipeline = new PipelinedWriteStream(destination);
        pipeline.WriteByte(42);
        Should.Throw<IOException>(pipeline.Dispose).ShouldBeSameAs(expected);
        pipeline.WorkerCompleted.ShouldBeTrue();
        destination.WasDisposed.ShouldBeTrue();
        pipeline.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesBothPrimaryAndFinalizationFailures(bool failOnFlush)
    {
        var primary = new IOException("primary failure");
        var finalization = new IOException("finalization failure");
        var destination = new FailingStream(failOnFlush ? null : primary, failOnFlush ? primary : null, finalization);
        var pipeline = new PipelinedWriteStream(destination);
        pipeline.WriteByte(42);
        var exception = Should.Throw<AggregateException>(pipeline.Dispose);
        exception.InnerExceptions.ShouldBe([primary, finalization]);
        pipeline.WorkerCompleted.ShouldBeTrue();
        destination.WasDisposed.ShouldBeTrue();
        pipeline.Dispose();
    }

    [Fact]
    public void InitializationFailureClosesTheBinlog()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDBINLOGASYNCCOMPRESSION", "1");
        string path = env.CreateFile(".binlog").Path;
        var logger = new BinaryLogger { Parameters = path, CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.None };
        var source = A.Fake<IEventSource>();
        var expected = new InvalidOperationException("event subscription failure");
        A.CallTo(source).Where(call => call.Method.Name == "add_AnyEventRaised").Throws(expected);
        try
        {
            Should.Throw<InvalidOperationException>(() => logger.Initialize(source)).ShouldBeSameAs(expected);
            using var exclusive = new FileStream(path, FileMode.Open, System.IO.FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            logger.Shutdown();
        }
    }

    [Fact]
    public async Task ImportArchiveFailureClosesTheBinlog()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDBINLOGASYNCCOMPRESSION", "1");
        string path = env.CreateFile(".binlog").Path;
        var logger = new BinaryLogger { Parameters = path };
        logger.Initialize(new EventSourceSink());
        var collector = (ProjectImportsCollector)typeof(BinaryLogger)
            .GetField("projectImportsCollector", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logger)!;
        FieldInfo taskField = typeof(ProjectImportsCollector).GetField("_currentTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)taskField.GetValue(collector)!;
        var expected = new IOException("archive failure");
        taskField.SetValue(collector, Task.FromException(expected));
        try
        {
            Should.Throw<AggregateException>(logger.Shutdown).GetBaseException().ShouldBeSameAs(expected);
            using var exclusive = new FileStream(path, FileMode.Open, System.IO.FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            taskField.SetValue(collector, Task.CompletedTask);
            collector.DeleteArchive();
            logger.Shutdown();
        }
    }

    private sealed class BlockingStream(ManualResetEventSlim entered, ManualResetEventSlim release, Exception? failure = null) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The test did not release the stream.");
            }

            if (failure is not null)
            {
                throw failure;
            }

            base.Write(buffer, offset, count);
        }
    }

    private sealed class FailingStream(Exception? writeFailure, Exception? flushFailure, Exception? disposeFailure) : MemoryStream
    {
        internal bool WasDisposed { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (writeFailure is not null)
            {
                throw writeFailure;
            }

            base.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            if (flushFailure is not null)
            {
                throw flushFailure;
            }

            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
            if (disposeFailure is not null)
            {
                throw disposeFailure;
            }
        }
    }
}
