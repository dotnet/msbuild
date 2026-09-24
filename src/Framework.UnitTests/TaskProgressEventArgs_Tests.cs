// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.Framework.UnitTests
{
    public class TaskProgressEventArgs_Tests
    {
        private static readonly int s_packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;

        [Fact]
        public void TaskProgressStartedEventArgsSerializationDeserializationTest()
        {
            TaskProgressStartedEventArgs args = new(
                operationId: 42,
                title: "Downloading",
                unit: TaskProgressUnit.Bytes,
                helpKeyword: "keyword",
                senderName: "Sender");

            using MemoryStream stream = new();
            using BinaryWriter bw = new(stream);
            args.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new(stream);
            TaskProgressStartedEventArgs deserialized = new();
            deserialized.CreateFromStream(br, s_packetVersion);

            deserialized.OperationId.ShouldBe(args.OperationId);
            deserialized.Title.ShouldBe(args.Title);
            deserialized.Unit.ShouldBe(args.Unit);
            deserialized.HelpKeyword.ShouldBe(args.HelpKeyword);
            deserialized.SenderName.ShouldBe(args.SenderName);
        }

        [Fact]
        public void TaskProgressUpdatedEventArgsSerializationDeserializationTest()
        {
            TaskProgressUpdatedEventArgs args = new(
                operationId: 42,
                sequence: 3,
                completed: 50,
                total: 100,
                status: "Halfway there");

            using MemoryStream stream = new();
            using BinaryWriter bw = new(stream);
            args.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new(stream);
            TaskProgressUpdatedEventArgs deserialized = new();
            deserialized.CreateFromStream(br, s_packetVersion);

            deserialized.OperationId.ShouldBe(args.OperationId);
            deserialized.Sequence.ShouldBe(args.Sequence);
            deserialized.Completed.ShouldBe(args.Completed);
            deserialized.Total.ShouldBe(args.Total);
            deserialized.Status.ShouldBe(args.Status);
        }

        [Fact]
        public void TaskProgressUpdatedEventArgsWithNullTotalSerializationDeserializationTest()
        {
            TaskProgressUpdatedEventArgs args = new(
                operationId: 7,
                sequence: 1,
                completed: 5,
                total: null,
                status: null);

            using MemoryStream stream = new();
            using BinaryWriter bw = new(stream);
            args.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new(stream);
            TaskProgressUpdatedEventArgs deserialized = new();
            deserialized.CreateFromStream(br, s_packetVersion);

            deserialized.Total.ShouldBeNull();
            deserialized.Status.ShouldBeNull();
        }

        [Theory]
        [InlineData(TaskProgressOutcome.Completed)]
        [InlineData(TaskProgressOutcome.Canceled)]
        [InlineData(TaskProgressOutcome.Failed)]
        [InlineData(TaskProgressOutcome.Abandoned)]
        public void TaskProgressFinishedEventArgsSerializationDeserializationTest(TaskProgressOutcome outcome)
        {
            TaskProgressFinishedEventArgs args = new(
                operationId: 42,
                sequence: 9,
                outcome: outcome,
                completed: 100,
                total: 100,
                summary: "All done");

            using MemoryStream stream = new();
            using BinaryWriter bw = new(stream);
            args.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new(stream);
            TaskProgressFinishedEventArgs deserialized = new();
            deserialized.CreateFromStream(br, s_packetVersion);

            deserialized.OperationId.ShouldBe(args.OperationId);
            deserialized.Sequence.ShouldBe(args.Sequence);
            deserialized.Outcome.ShouldBe(args.Outcome);
            deserialized.Completed.ShouldBe(args.Completed);
            deserialized.Total.ShouldBe(args.Total);
            deserialized.Summary.ShouldBe(args.Summary);
        }
    }
}
