﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Microsoft.Build.Framework.Telemetry;

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
                        { (TaskOrTargetTelemetryKey)"task1", new TaskExecutionStats(TimeSpan.FromMinutes(1), 5, 1234) },
                        { (TaskOrTargetTelemetryKey)"task2", new TaskExecutionStats(TimeSpan.Zero, 0, 0) },
                        { (TaskOrTargetTelemetryKey)"task3", new TaskExecutionStats(TimeSpan.FromTicks(1234), 12, 987654321) }
                },
                new Dictionary<TaskOrTargetTelemetryKey, bool>() { { (TaskOrTargetTelemetryKey)"target1", false }, { (TaskOrTargetTelemetryKey)"target2", true }, });

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
        }
    }
}
