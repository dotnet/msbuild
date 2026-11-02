// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Logging;

public sealed class LoggingQueueCapacity_Tests(ITestOutputHelper output)
{
    private const long MiB = 1024 * 1024;
    private const long GiB = 1024 * MiB;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // The logger outlasts the waits of the test, so that a regression fails the test instead of the logging thread.
    private static readonly TimeSpan LoggerTimeout = TimeSpan.FromSeconds(60);
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void FullQueueGrowsInsteadOfBlockingTheProducer()
    {
        using TestEnvironment env = CreateEnvironment();
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        LoggingService service = CreateService(LoggerMode.Asynchronous);
        MockLogger logger = RegisterLoggerBlockedOnFirstEvent(service, entered, release);
        service.QueueCapacity = 2;
        service.MemoryHeadroomProvider = () => long.MaxValue;
        try
        {
            FillQueue(service, entered);

            Task.Run(() => service.ProcessLoggingEvent(Message("last"))).Wait(Timeout)
                .ShouldBeTrue("a producer must not wait for the logger while the queue can grow");
            service.QueueCapacity.ShouldBe(4);
        }
        finally
        {
            ReleaseAndShutDown(service, release);
        }

        logger.BuildMessageEvents.Select(e => e.Message).ShouldBe(["first", "second", "third", "last"]);
    }

    [Fact]
    public void FullQueueBlocksTheProducerWhenThereIsNoMemoryToGrow()
    {
        using TestEnvironment env = CreateEnvironment();
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        using ManualResetEventSlim growthAttempted = new(false);
        LoggingService service = CreateService(LoggerMode.Asynchronous);
        MockLogger logger = RegisterLoggerBlockedOnFirstEvent(service, entered, release);
        service.QueueCapacity = 2;
        service.MemoryHeadroomProvider = () =>
        {
            growthAttempted.Set();
            return 0;
        };
        try
        {
            FillQueue(service, entered);

            Task producer = Task.Run(() => service.ProcessLoggingEvent(Message("last")));

            // Only a producer that has reached the full queue can be blocked by it.
            growthAttempted.Wait(Timeout).ShouldBeTrue();
            producer.Wait(TimeSpan.FromMilliseconds(200)).ShouldBeFalse();
            service.QueueCapacity.ShouldBe(2);

            release.Set();
            producer.Wait(Timeout).ShouldBeTrue();
        }
        finally
        {
            ReleaseAndShutDown(service, release);
        }

        logger.BuildMessageEvents.Select(e => e.Message).ShouldBe(["first", "second", "third", "last"]);
    }

    [Fact]
    public void ProducersWaitingForMemoryNoticeWhenItRecoversWhileTheLoggerIsStuck()
    {
        using TestEnvironment env = CreateEnvironment();
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        LoggingService service = CreateService(LoggerMode.Asynchronous);
        MockLogger logger = RegisterLoggerBlockedOnFirstEvent(service, entered, release);
        service.QueueCapacity = 2;
        service.QueueGrowthRetryInterval = TimeSpan.FromMilliseconds(50);
        int evaluations = 0;
        long headroom = 0;
        service.MemoryHeadroomProvider = () =>
        {
            Interlocked.Increment(ref evaluations);
            return Volatile.Read(ref headroom);
        };
        try
        {
            FillQueue(service, entered);

            Task fourth = Task.Run(() => service.ProcessLoggingEvent(Message("fourth")));
            Task fifth = Task.Run(() => service.ProcessLoggingEvent(Message("fifth")));

            // The logger is stuck and nothing is dequeued, so only the producers that wait can look at the memory again.
            SpinWait.SpinUntil(() => Volatile.Read(ref evaluations) >= 3, Timeout).ShouldBeTrue();
            fourth.IsCompleted.ShouldBeFalse();
            fifth.IsCompleted.ShouldBeFalse();

            Volatile.Write(ref headroom, long.MaxValue);

            Task.WaitAll([fourth, fifth], Timeout).ShouldBeTrue("waiting producers must notice that there is memory for the queue to grow");
            service.QueueCapacity.ShouldBe(4);
        }
        finally
        {
            ReleaseAndShutDown(service, release);
        }

        logger.BuildMessageEvents.Select(e => e.Message).ShouldBe(["first", "second", "third", "fourth", "fifth"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProducersThatFindTheQueueFullTogetherShareOneMemoryEvaluation(bool memoryAvailable)
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        service.QueueGrowthRetryInterval = TimeSpan.FromHours(1);
        int evaluations = 0;
        service.MemoryHeadroomProvider = () =>
        {
            Interlocked.Increment(ref evaluations);

            // Gives the other producers time to arrive while this one decides.
            Thread.Sleep(100);
            return memoryAvailable ? long.MaxValue : 0;
        };
        long capacity = service.QueueCapacity;
        using Barrier start = new(3);

        Task<bool>[] producers = Enumerable.Range(0, 3)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait(Timeout).ShouldBeTrue();
                    return service.TryGrowQueueCapacity(capacity);
                },
                TaskCreationOptions.LongRunning))
            .ToArray();

        Task.WaitAll(producers, Timeout).ShouldBeTrue();
        producers.Select(producer => producer.Result).ShouldAllBe(grew => grew == memoryAvailable);
        service.QueueCapacity.ShouldBe(memoryAvailable ? capacity * 2 : capacity);
        evaluations.ShouldBe(1);
    }

    [Fact]
    public void QueueDoublesWhileTheAddedCapacityFitsInTheHeadroom()
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        service.MemoryHeadroomProvider = () => 10L * 1024 * 1024 * 1024;

        List<long> capacities = [service.QueueCapacity];
        for (int attempts = 0; attempts < 20 && service.TryGrowQueueCapacity(service.QueueCapacity); attempts++)
        {
            capacities.Add(service.QueueCapacity);
        }

        // 3.2M -> 6.4M adds 3.2M events (6.1 GiB), which fits; the next doubling would add 6.4M events (12.2 GiB).
        capacities.ShouldBe([200_000, 400_000, 800_000, 1_600_000, 3_200_000, 6_400_000]);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public void GrowthNeedsMemoryForTheAddedCapacity(long headroomOffset, bool grows)
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        service.QueueCapacity = 1000;
        service.MemoryHeadroomProvider = () => (1000 * LoggingService.AssumedBytesPerQueuedEvent) + headroomOffset;

        service.TryGrowQueueCapacity(1000).ShouldBe(grows);
        service.QueueCapacity.ShouldBe(grows ? 2000 : 1000);
    }

    [Fact]
    public void RefusedGrowthIsNotEvaluatedAgainAtOnce()
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        int evaluations = 0;
        service.MemoryHeadroomProvider = () =>
        {
            evaluations++;
            return 0;
        };

        Stopwatch stopwatch = Stopwatch.StartNew();
        service.TryGrowQueueCapacity(service.QueueCapacity).ShouldBeFalse();
        service.TryGrowQueueCapacity(service.QueueCapacity).ShouldBeFalse();

        // The refusal is remembered for a second: only a machine stalled for longer could evaluate it again.
        if (stopwatch.Elapsed < TimeSpan.FromMilliseconds(250))
        {
            evaluations.ShouldBe(1);
        }
    }

    [Fact]
    public void ProducerAdoptsGrowthDoneByAnotherProducer()
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        service.MemoryHeadroomProvider = () => throw new InvalidOperationException("The capacity has already grown.");
        service.QueueCapacity = 400;

        service.TryGrowQueueCapacity(200).ShouldBeTrue();
        service.QueueCapacity.ShouldBe(400);
    }

    [Theory]
    [InlineData("MSBUILDLOGGINGQUEUECAPACITY", "1000")]
    [InlineData("MSBUILDLOGGINGQUEUECAPACITY", "not a number")]
    [InlineData("MSBUILDDISABLEFEATURESFROMVERSION", "18.13")]
    public void QueueDoesNotGrowWhenItsCapacityIsConfiguredOrTheChangeWaveIsDisabled(string variable, string value)
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(variable, value);
        ChangeWaves.ResetStateForTests();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        service.MemoryHeadroomProvider = () => long.MaxValue;
        long capacity = service.QueueCapacity;

        service.TryGrowQueueCapacity(capacity).ShouldBeFalse();
        service.QueueCapacity.ShouldBe(capacity);
    }

    [Theory]
    [InlineData(57 * GiB, 27 * GiB, 64 * GiB, 7 * MiB, 30 * GiB)]
    [InlineData(57 * GiB, 27 * GiB, 1 * GiB, 7 * MiB, (1 * GiB) - (7 * MiB))]
    [InlineData(57 * GiB, 27 * GiB, 1 * GiB, 2 * GiB, 0L)]
    [InlineData(57 * GiB, 60 * GiB, 64 * GiB, 7 * MiB, 0L)]
    public void MemoryHeadroomIsBoundedByTheMachineAndByTheGcHeapHardLimit(long threshold, long load, long available, long committed, long expected)
    {
        LoggingService.ComputeMemoryHeadroom(threshold, load, available, committed).ShouldBe(expected);
    }

#if NET
    [Fact]
    public void DefaultMemoryHeadroomIsNeverNegative()
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);

        GC.Collect();

        service.MemoryHeadroomProvider().ShouldBeGreaterThanOrEqualTo(0);
    }
#else
    [Fact]
    public void DotNetFrameworkCannotMeasureMemoryAndKeepsTheCapacityFixed()
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        long capacity = service.QueueCapacity;

        service.MemoryHeadroomProvider.ShouldBeNull();
        service.TryGrowQueueCapacity(capacity).ShouldBeFalse();
        service.QueueCapacity.ShouldBe(capacity);
    }
#endif

    [Fact]
    public void QueueDoesNotGrowWithoutAMemoryProvider()
    {
        using TestEnvironment env = CreateEnvironment();
        LoggingService service = CreateService(LoggerMode.Synchronous);
        service.MemoryHeadroomProvider = null;
        long capacity = service.QueueCapacity;

        service.TryGrowQueueCapacity(capacity).ShouldBeFalse();
        service.QueueCapacity.ShouldBe(capacity);
    }

    private TestEnvironment CreateEnvironment()
    {
        TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDLOGGINGQUEUECAPACITY", null);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        ChangeWaves.ResetStateForTests();
        return env;
    }

    private static LoggingService CreateService(LoggerMode mode)
    {
        LoggingService service = (LoggingService)LoggingService.CreateLoggingService(mode, 1);
        if (mode == LoggerMode.Asynchronous)
        {
            service.InitializeComponent(new MockHost());
        }

        return service;
    }

    private MockLogger RegisterLoggerBlockedOnFirstEvent(LoggingService service, ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        MockLogger logger = new(_output, printEventsToStdout: false);
        logger.AdditionalHandlers.Add((_, args) =>
        {
            if (args is BuildMessageEventArgs { Message: "first" })
            {
                entered.Set();
                release.Wait(LoggerTimeout).ShouldBeTrue();
            }
        });
        service.RegisterLogger(logger);
        return logger;
    }

    // Leaves the logger stuck on the first event and the queue holding two more, at the capacity set by the test.
    private static void FillQueue(LoggingService service, ManualResetEventSlim loggerEntered)
    {
        service.ProcessLoggingEvent(Message("first"));
        loggerEntered.Wait(Timeout).ShouldBeTrue();
        service.ProcessLoggingEvent(Message("second"));
        service.ProcessLoggingEvent(Message("third"));
    }

    // Shutting down stops forwarding events to the loggers, so the queue has to be empty by then.
    private static void ReleaseAndShutDown(LoggingService service, ManualResetEventSlim release)
    {
        release.Set();
        service.WaitForLoggingToProcessEvents();
        service.ShutdownComponent();
    }

    private static BuildMessageEventArgs Message(string text) => new(text, null, null, MessageImportance.High);
}
