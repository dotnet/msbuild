// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class WorkerNodeTelemetryEventArgs_Tests
    {
        [Fact]
        public void SerializationDeserializationTest()
        {
            WorkerNodeTelemetryData td = new WorkerNodeTelemetryData(
                new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>()
                {
                        { (TaskOrTargetTelemetryKey)"task1", new TaskExecutionStats(TimeSpan.FromMinutes(1), 5, 1234, "AssemblyTaskFactory", "CLR4") },
                        { (TaskOrTargetTelemetryKey)"task2", new TaskExecutionStats(TimeSpan.Zero, 0, null, null, null) },
                        { (TaskOrTargetTelemetryKey)"task3", new TaskExecutionStats(TimeSpan.FromTicks(1234), 12, 987654321, "CodeTaskFactory", "NET") }
                },
                new Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats>()
                {
                    { (TaskOrTargetTelemetryKey)"target1", TargetExecutionStats.Skipped(TargetSkipReason.OutputsUpToDate) },
                    { (TaskOrTargetTelemetryKey)"target2", TargetExecutionStats.Executed() },
                    { (TaskOrTargetTelemetryKey)"target3", TargetExecutionStats.Skipped(TargetSkipReason.ConditionWasFalse) },
                },
                upToDateInputOutputTargetsCount: 7,
                executedInputOutputTargetsCount: 2,
                executedTargetsCount: 11,
                skippedTargetsCount: 5,
                skippedDueToUpToDateCount: 2,
                skippedDueToConditionCount: 1,
                skippedDueToPreviouslyBuiltCount: 2);

            WorkerNodeTelemetryEventArgs args = new WorkerNodeTelemetryEventArgs(td);

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(stream);
            args.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new BinaryReader(stream);
            WorkerNodeTelemetryEventArgs argDeserialized = new();
            int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;

            argDeserialized.CreateFromStream(br, packetVersion);
            argDeserialized.WorkerNodeTelemetryData.TargetsExecutionData.ShouldBeEquivalentTo(td.TargetsExecutionData);
            argDeserialized.WorkerNodeTelemetryData.TasksExecutionData.ShouldBeEquivalentTo(td.TasksExecutionData);
            argDeserialized.WorkerNodeTelemetryData.UpToDateInputOutputTargetsCount.ShouldBe(7);
            argDeserialized.WorkerNodeTelemetryData.ExecutedInputOutputTargetsCount.ShouldBe(2);
            argDeserialized.WorkerNodeTelemetryData.ExecutedTargetsCount.ShouldBe(11);
            argDeserialized.WorkerNodeTelemetryData.SkippedTargetsCount.ShouldBe(5);
            argDeserialized.WorkerNodeTelemetryData.SkippedDueToUpToDateCount.ShouldBe(2);
            argDeserialized.WorkerNodeTelemetryData.SkippedDueToConditionCount.ShouldBe(1);
            argDeserialized.WorkerNodeTelemetryData.SkippedDueToPreviouslyBuiltCount.ShouldBe(2);
            argDeserialized.WorkerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"task2"].TotalMemoryBytes.ShouldBeNull();
        }

        [Fact]
        public void DeserializesPayloadWithoutAggregateCounters()
        {
            WorkerNodeTelemetryData data = new(
                new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
                {
                    { (TaskOrTargetTelemetryKey)"task", new TaskExecutionStats(TimeSpan.FromSeconds(1), 1, 42, "AssemblyTaskFactory", null) },
                },
                new Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats>
                {
                    { (TaskOrTargetTelemetryKey)"target", TargetExecutionStats.Executed() },
                });

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            new WorkerNodeTelemetryEventArgs(data).WriteToStream(writer);
            writer.Flush();
            stream.SetLength(stream.Length - (7 * sizeof(int)));
            stream.Position = 0;

            using BinaryReader reader = new(stream);
            WorkerNodeTelemetryEventArgs deserialized = new();
            int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;
            deserialized.CreateFromStream(reader, packetVersion);

            deserialized.WorkerNodeTelemetryData.TasksExecutionData.ShouldBeEquivalentTo(data.TasksExecutionData);
            deserialized.WorkerNodeTelemetryData.TargetsExecutionData.ShouldBeEquivalentTo(data.TargetsExecutionData);
            deserialized.WorkerNodeTelemetryData.ExecutedTargetsCount.ShouldBe(0);
            deserialized.WorkerNodeTelemetryData.SkippedTargetsCount.ShouldBe(0);
            deserialized.WorkerNodeTelemetryData.UpToDateInputOutputTargetsCount.ShouldBe(0);
            deserialized.WorkerNodeTelemetryData.ExecutedInputOutputTargetsCount.ShouldBe(0);
        }
    }
}
