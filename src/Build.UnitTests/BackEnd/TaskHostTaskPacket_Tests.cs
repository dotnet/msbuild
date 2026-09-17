// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskHostTaskPacket_Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TaskOriginatedPayloadPreservesInvocationAndContent(int kind)
    {
        var handler = new RecordingHandler();
        var factory = new NodePacketFactory();
        factory.RegisterPacketHandler(NodePacketType.LogMessage, LogMessagePacket.FactoryForDeserialization, handler);
        factory.RegisterPacketHandler(NodePacketType.TaskHostTaskComplete, TaskHostTaskComplete.FactoryForDeserialization, handler);
        factory.RegisterPacketHandler(NodePacketType.TaskHostBuildRequest, TaskHostBuildRequest.FactoryForDeserialization, handler);
        factory.RegisterPacketHandler(NodePacketType.TaskHostCoresRequest, TaskHostCoresRequest.FactoryForDeserialization, handler);
        factory.RegisterPacketHandler(NodePacketType.TaskHostIsRunningMultipleNodesRequest, TaskHostIsRunningMultipleNodesRequest.FactoryForDeserialization, handler);
        INodePacket payload = kind switch
        {
            0 => new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(3,
                new BuildMessageEventArgs("invocation-owned log", null, "task", MessageImportance.High))),
            1 => new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null),
            2 => new TaskHostBuildRequest(["Child.proj"], ["Build"], null, null, null, true) { RequestId = 23 },
            3 => new TaskHostCoresRequest(2, false) { RequestId = 23 },
            _ => new TaskHostIsRunningMultipleNodesRequest { RequestId = 23 }
        };

        using MemoryStream stream = new();
        ITranslator writer = BinaryTranslator.GetWriteTranslator(stream);
        writer.NegotiatedPacketVersion = NodePacketTypeExtensions.TaskHostInvocationMinVersion;
        new TaskHostTaskPacket(1001, payload).Translate(writer);
        stream.Position = 0;
        ITranslator reader = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.CreateSharedBuffer());
        reader.NegotiatedPacketVersion = NodePacketTypeExtensions.TaskHostInvocationMinVersion;
        var roundTrip = (TaskHostTaskPacket)TaskHostTaskPacket.FactoryForDeserialization(reader, factory);

        roundTrip.InvocationId.ShouldBe(1001);
        roundTrip.Packet.Type.ShouldBe(payload.Type);
        stream.Position.ShouldBe(stream.Length);
        switch (roundTrip.Packet)
        {
            case LogMessagePacket log:
                log.NodeBuildEvent.HasValue.ShouldBeTrue();
                log.NodeBuildEvent.GetValueOrDefault().Value.Message.ShouldBe("invocation-owned log");
                break;
            case TaskHostTaskComplete complete:
                complete.TaskResult.ShouldBe(TaskCompleteType.Success);
                break;
            case ITaskHostCallbackPacket callback:
                callback.RequestId.ShouldBe(23);
                break;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void LegacyProtocolCannotWriteInvocationEnvelope(byte version)
    {
        using MemoryStream stream = new();
        ITranslator writer = BinaryTranslator.GetWriteTranslator(stream);
        writer.NegotiatedPacketVersion = version;
        var packet = new TaskHostTaskPacket(1, new TaskHostCoresRequest(1, false));
        Should.Throw<InvalidDataException>(() => packet.Translate(writer));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvocationIdentityMustBePositive(long invocationId)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new TaskHostTaskPacket(invocationId, new TaskHostCoresRequest(1, false)));
    }

    [Fact]
    public void ConnectionNotificationCannotBeEnclosed()
    {
        Should.Throw<InvalidDataException>(() =>
            new TaskHostTaskPacket(1, new NodeShutdown(NodeShutdownReason.ConnectionFailed)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 1234)]
    public void ConfigurationIdentityRequiresNegotiatedSupport(byte version, long expectedId)
    {
        var configuration = new TaskHostConfiguration(
            nodeId: 1, startupDirectory: Directory.GetCurrentDirectory(), buildProcessEnvironment: null,
            culture: CultureInfo.InvariantCulture, uiCulture: CultureInfo.InvariantCulture, hostServices: null,
#if FEATURE_APPDOMAIN
            appDomainSetup: null,
#endif
            lineNumberOfTask: 1, columnNumberOfTask: 1, projectFileOfTask: "test.proj",
            continueOnError: false, taskName: "TestTask", taskLocation: "TestTask.dll",
            isTaskInputLoggingEnabled: false, taskParameters: null, globalParameters: null,
            targetName: "Build", projectFile: "test.proj", warningsAsErrors: null,
            warningsNotAsErrors: null, warningsAsMessages: null)
        {
            TaskInvocationId = 1234
        };
        using MemoryStream stream = new();
        ITranslator writer = BinaryTranslator.GetWriteTranslator(stream);
        writer.NegotiatedPacketVersion = version;
        configuration.Translate(writer);
        stream.Position = 0;
        ITranslator reader = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.CreateSharedBuffer());
        reader.NegotiatedPacketVersion = version;
        var roundTrip = (TaskHostConfiguration)TaskHostConfiguration.FactoryForDeserialization(reader);

        roundTrip.TaskInvocationId.ShouldBe(expectedId);
        roundTrip.TaskName.ShouldBe("TestTask");
        stream.Position.ShouldBe(stream.Length);
    }

    private sealed class RecordingHandler : INodePacketHandler
    {
        public void PacketReceived(int node, INodePacket packet)
            => throw new InvalidOperationException("Deserialization must not dispatch the enclosed packet.");
    }
}
