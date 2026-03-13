// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class WorkerNodeTelemetryData_Tests
{
    /// <summary>
    /// Tests that concurrent AddTask calls on the same WorkerNodeTelemetryData instance
    /// do not corrupt the internal dictionary (regression test for #12867).
    /// </summary>
    [Fact]
    public void AddTask_ConcurrentAccess_DoesNotCorrupt()
    {
        var telemetryData = new WorkerNodeTelemetryData();
        const int threadCount = 20;
        const int iterationsPerThread = 200;

        // Use a barrier to maximize contention by starting all threads simultaneously
        using var barrier = new Barrier(threadCount);

        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    // Shared key to exercise same-key contention
                    var sharedKey = new TaskOrTargetTelemetryKey($"SharedTask_{i % 10}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
                    telemetryData.AddTask(sharedKey, TimeSpan.FromMilliseconds(1), executionsCount: 1, totalMemoryConsumption: 100, factoryName: null, taskHostRuntime: null);

                    // Unique key to trigger dictionary resizing
                    if (i % 5 == 0)
                    {
                        var uniqueKey = new TaskOrTargetTelemetryKey($"UniqueTask_{threadId}_{i}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
                        telemetryData.AddTask(uniqueKey, TimeSpan.FromMilliseconds(1), executionsCount: 1, totalMemoryConsumption: 100, factoryName: null, taskHostRuntime: null);
                    }
                }
            });
        }

        // Should not throw (previously crashed with ArgumentException in Dictionary.Resize)
        Task.WaitAll(tasks);

        // Verify data integrity: shared tasks should have accumulated counts
        for (int i = 0; i < 10; i++)
        {
            var key = new TaskOrTargetTelemetryKey($"SharedTask_{i}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
            telemetryData.TasksExecutionData.ShouldContainKey(key);
            telemetryData.TasksExecutionData[key].ExecutionsCount.ShouldBeGreaterThan(0);
        }
    }

    /// <summary>
    /// Tests that concurrent AddTarget calls on the same WorkerNodeTelemetryData instance
    /// do not corrupt the internal dictionary (regression test for #12867).
    /// </summary>
    [Fact]
    public void AddTarget_ConcurrentAccess_DoesNotCorrupt()
    {
        var telemetryData = new WorkerNodeTelemetryData();
        const int threadCount = 20;
        const int iterationsPerThread = 200;

        using var barrier = new Barrier(threadCount);

        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    // Shared key to exercise same-key contention
                    var sharedKey = new TaskOrTargetTelemetryKey($"SharedTarget_{i % 10}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
                    telemetryData.AddTarget(sharedKey, wasExecuted: i % 2 == 0, skipReason: TargetSkipReason.None);

                    // Unique key to trigger dictionary resizing
                    if (i % 5 == 0)
                    {
                        var uniqueKey = new TaskOrTargetTelemetryKey($"UniqueTarget_{threadId}_{i}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
                        telemetryData.AddTarget(uniqueKey, wasExecuted: true, skipReason: TargetSkipReason.None);
                    }
                }
            });
        }

        // Should not throw
        Task.WaitAll(tasks);

        // Verify data integrity
        for (int i = 0; i < 10; i++)
        {
            var key = new TaskOrTargetTelemetryKey($"SharedTarget_{i}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
            telemetryData.TargetsExecutionData.ShouldContainKey(key);
        }
    }

    /// <summary>
    /// Tests that concurrent Add (merge) calls on the same WorkerNodeTelemetryData instance
    /// do not corrupt the internal dictionaries (regression test for #12867).
    /// </summary>
    [Fact]
    public void Add_ConcurrentMerge_DoesNotCorrupt()
    {
        var telemetryData = new WorkerNodeTelemetryData();
        const int threadCount = 20;

        using var barrier = new Barrier(threadCount);

        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                // Each thread creates its own data and merges it
                var localData = new WorkerNodeTelemetryData();
                for (int i = 0; i < 50; i++)
                {
                    var key = new TaskOrTargetTelemetryKey($"Task_{i}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
                    localData.AddTask(key, TimeSpan.FromMilliseconds(1), executionsCount: 1, totalMemoryConsumption: 100, factoryName: null, taskHostRuntime: null);

                    var targetKey = new TaskOrTargetTelemetryKey($"Target_{i}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
                    localData.AddTarget(targetKey, wasExecuted: true);
                }

                barrier.SignalAndWait();
                // All threads merge into the shared instance at the same time
                telemetryData.Add(localData);
            });
        }

        // Should not throw
        Task.WaitAll(tasks);

        // Verify all tasks and targets are present
        for (int i = 0; i < 50; i++)
        {
            var key = new TaskOrTargetTelemetryKey($"Task_{i}", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
            telemetryData.TasksExecutionData.ShouldContainKey(key);
            // Each of 20 threads contributed 1 execution
            telemetryData.TasksExecutionData[key].ExecutionsCount.ShouldBe(threadCount);
        }
    }
}
