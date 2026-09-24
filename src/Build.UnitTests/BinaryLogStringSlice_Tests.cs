// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class BinaryLogStringSliceTests
{
    private readonly ITestOutputHelper _output;

    public BinaryLogStringSliceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void StringSlice_RoundtripsEventsAndSequentialIds()
    {
        string payload = new string('x', 8192) + "\u00e9\u4e2d\U0001f600";
        string command = "compiler/\U0001f600 " + payload;
        string response = Response(payload);
        BuildEventArgs[] events =
        [
            new TaskCommandLineEventArgs(command, "Csc", MessageImportance.Low),
            Message(response),
            Message(null),
            Message(string.Empty),
            Message("after slice"),
            Message(response),
        ];

        using MemoryStream stream = new();
        WriteEvents(stream, events);
        using BinaryReader wire = new(stream, Encoding.UTF8, leaveOpen: true);
        CountSlices(wire).ShouldBe(1);

        stream.Position = 0;
        using BuildEventArgsReader reader = new(wire, BinaryLogger.FileFormatVersion);
        List<string> strings = [];
        reader.StringReadDone += args => strings.Add(args.OriginalString);
        foreach (BuildEventArgs expected in events)
        {
            BuildEventArgs actual = reader.Read().ShouldNotBeNull();
            actual.GetType().ShouldBe(expected.GetType());
            actual.Message.ShouldBe(expected.Message);
        }

        reader.Read().ShouldBeNull();
        strings.FindAll(value => value == response).Count.ShouldBe(1);
        strings.ShouldContain("after slice");
    }

    [Theory]
    [InlineData(4095, false)]
    [InlineData(4096, true)]
    [InlineData(1048576, true)]
    [InlineData(1048577, false)]
    public void StringSlice_LengthBounds(int length, bool shared)
    {
        string payload = new('x', length);
        using MemoryStream stream = new();
        using BinaryWriter binaryWriter = new(stream, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter writer = new(binaryWriter);
        writer.WriteStringRecord(payload);
        long targetStart = stream.Position;
        writer.WriteStringRecord(Response(payload), allowSlice: true);
        long targetLength = stream.Position - targetStart;
        stream.Position = targetStart;
        using BinaryReader wire = new(stream, Encoding.UTF8, leaveOpen: true);
        ReadKind(wire).ShouldBe(shared ? BinaryLogRecordKind.StringSlice : BinaryLogRecordKind.String);
        if (shared)
        {
            targetLength.ShouldBeLessThan(64);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("mismatch")]
    [InlineData("contained-not-suffix")]
    [InlineData("wrong-wrapper")]
    [InlineData("missing-quote")]
    [InlineData("oversized-source")]
    public void StringSlice_LegitimateMismatchesUseOrdinaryStrings(string scenario)
    {
        string payload = new('x', 8192);
        string response = Response(payload);
        string source = scenario switch
        {
            "mismatch" => "compiler " + new string('X', payload.Length),
            "contained-not-suffix" => "compiler " + payload + " trailing",
            "oversized-source" => new string('p', StringSliceSourceCache.MaximumLength) + payload,
            _ => "compiler " + payload,
        };
        response = scenario switch
        {
            "wrong-wrapper" => "buildResponseFile = '" + payload + "'",
            "missing-quote" => "BuildResponseFile = '" + payload,
            _ => response,
        };

        using MemoryStream stream = new();
        using BinaryWriter binaryWriter = new(stream, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter writer = new(binaryWriter);
        if (scenario != "missing")
        {
            writer.WriteStringRecord(source);
        }

        long targetStart = stream.Position;
        writer.WriteStringRecord(response, allowSlice: true);
        stream.Position = targetStart;
        using BinaryReader wire = new(stream, Encoding.UTF8, leaveOpen: true);
        ReadKind(wire).ShouldBe(BinaryLogRecordKind.String);
        wire.ReadString().ShouldBe(response);
    }

    [Fact]
    public void StringSlice_WindowEvictsOnlyEligibleOrdinaryOriginals()
    {
        string payload = new('x', 8192);
        string source = "compiler " + payload;
        string response = Response(payload);
        using MemoryStream stream = new();
        using BinaryWriter binaryWriter = new(stream, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter writer = new(binaryWriter);
        writer.WriteStringRecord(source);
        for (int i = 0; i < StringSliceSourceCache.Capacity; i++)
        {
            writer.WriteStringRecord("short " + i);
            writer.WriteStringRecord(response, allowSlice: true);
        }

        for (int i = 1; i < StringSliceSourceCache.Capacity; i++)
        {
            writer.WriteStringRecord(new string((char)('a' + i), 4096));
        }

        writer.WriteStringRecord(response, allowSlice: true);
        writer.WriteStringRecord(new string('z', 4096));
        writer.WriteStringRecord(response, allowSlice: true);
        binaryWriter.Write((byte)BinaryLogRecordKind.EndOfFile);

        stream.Position = 0;
        using BinaryReader wire = new(stream, Encoding.UTF8, leaveOpen: true);
        CountSlices(wire).ShouldBe(StringSliceSourceCache.Capacity + 1);

        stream.Position = 0;
        using BuildEventArgsReader reader = new(wire, BinaryLogger.FileFormatVersion);
        List<string> strings = [];
        reader.StringReadDone += args =>
        {
            strings.Add(args.OriginalString);
            // Neither changed source length nor oversized replacements for small strings affect eligibility.
            args.StringToBeUsed = args.OriginalString.StartsWith("short ", StringComparison.Ordinal)
                ? new string('s', StringSliceSourceCache.MaximumLength + 1)
                : "scrubbed";
        };
        reader.Read().ShouldBeNull();
        strings.FindAll(value => value == response).Count.ShouldBe(StringSliceSourceCache.Capacity + 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StringSlice_SourceAndTargetScrubbingAreIndependent(bool raw)
    {
        string payload = new('x', 8192);
        string command = "compiler " + payload;
        string response = Response(payload);
        using MemoryStream stream = new();
        WriteEvents(stream,
        [
            new TaskCommandLineEventArgs(command, "Csc", MessageImportance.Low),
            Message(response),
        ]);

        using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
        using BuildEventArgsReader reader = new(binaryReader, BinaryLogger.FileFormatVersion);
        List<string> originals = [];
        reader.StringReadDone += args =>
        {
            originals.Add(args.OriginalString);
            if (args.OriginalString == command)
            {
                args.StringToBeUsed = "redacted command";
            }
            else if (args.OriginalString == response)
            {
                args.StringToBeUsed = "redacted response";
            }
        };

        if (raw)
        {
            reader.ReadRaw().RecordKind.ShouldBe(BinaryLogRecordKind.TaskCommandLine);
            reader.ReadRaw().RecordKind.ShouldBe(BinaryLogRecordKind.Message);
            reader.ReadRaw().RecordKind.ShouldBe(BinaryLogRecordKind.EndOfFile);
        }
        else
        {
            reader.Read().ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe("redacted command");
            reader.Read().ShouldBeOfType<BuildMessageEventArgs>().Message.ShouldBe("redacted response");
            reader.Read().ShouldBeNull();
        }

        originals.FindAll(value => value == response).Count.ShouldBe(1);
        originals.ShouldNotContain("BuildResponseFile = '");
        originals.ShouldNotContain("'");
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(9, 0, 1)]
    [InlineData(11, 0, 1)]
    [InlineData(12, 0, 1)]
    [InlineData(-1, 0, 1)]
    [InlineData(10, -1, 1)]
    [InlineData(10, 0, -1)]
    [InlineData(10, 4097, 0)]
    [InlineData(10, 4095, 2)]
    [InlineData(10, 1, int.MaxValue)]
    public void StringSlice_RejectsCorruptReferences(int sourceId, int start, int length)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        new BuildEventArgsWriter(writer).WriteStringRecord(new string('x', 4096));
        WriteSlice(writer, sourceId, start, length);
        writer.Write((byte)BinaryLogRecordKind.EndOfFile);
        stream.Position = 0;
        using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
        using BuildEventArgsReader reader = new(binaryReader, BinaryLogger.FileFormatVersion);
        reader.SkipUnknownEvents = true;
        reader.RecoverableReadError += _ => throw new InvalidOperationException("A corrupt string table cannot be skipped.");
        Should.Throw<InvalidDataException>(() => reader.Read());
    }

    [Fact]
    public void StringSlice_RejectsExpiredReference()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter eventWriter = new(writer);
        for (int i = 0; i <= StringSliceSourceCache.Capacity; i++)
        {
            eventWriter.WriteStringRecord(new string('x', 4096));
        }

        WriteSlice(writer, BuildEventArgsWriter.StringStartIndex, 0, 4096);
        stream.Position = 0;
        using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
        using BuildEventArgsReader reader = new(binaryReader, BinaryLogger.FileFormatVersion);
        Should.Throw<InvalidDataException>(() => reader.ReadRaw());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StringSlice_InterruptedRecordPreservesEarlierEvents(bool raw)
    {
        string payload = new('x', 8192);
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter eventWriter = new(writer);
        eventWriter.Write(new TaskCommandLineEventArgs("compiler " + payload, "Csc", MessageImportance.Low));
        long completedLength = stream.Length;
        eventWriter.WriteStringRecord(Response(payload), allowSlice: true);
        byte[] bytes = stream.ToArray();
        for (int length = (int)completedLength; length < bytes.Length; length++)
        {
            using MemoryStream truncated = new(bytes, 0, length);
            using BinaryReader binaryReader = new(truncated);
            using BuildEventArgsReader reader = new(binaryReader, BinaryLogger.FileFormatVersion);
            List<string> strings = [];
            reader.StringReadDone += args => strings.Add(args.OriginalString);
            if (raw)
            {
                reader.ReadRaw().RecordKind.ShouldBe(BinaryLogRecordKind.TaskCommandLine);
                Should.Throw<EndOfStreamException>(() => reader.ReadRaw());
            }
            else
            {
                reader.Read().ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe("compiler " + payload);
                Should.Throw<EndOfStreamException>(() => reader.Read());
            }

            strings.ShouldNotContain(Response(payload));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StringSlice_BinaryLoggerReplayPreservesEvents(bool structured, bool scrub)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithEnvironmentInvariant();
        string payload = new('x', 8192);
        string command = "compiler " + payload;
        string response = Response(payload);
        string input = env.ExpectFile(".binlog").Path;
        string output = env.ExpectFile(".binlog").Path;
        using (BinaryWriter writer = new(new GZipStream(File.Create(input), CompressionLevel.Optimal)))
        {
            writer.Write(BinaryLogger.FileFormatVersion);
            writer.Write(BinaryLogger.MinimumReaderVersion);
            BuildEventArgsWriter eventWriter = new(writer);
            eventWriter.Write(new TaskCommandLineEventArgs(command, "Csc", MessageImportance.Low));
            eventWriter.Write(Message(response));
            eventWriter.Write(Message("after slice"));
            writer.Write((byte)BinaryLogRecordKind.EndOfFile);
        }

        BinaryLogReplayEventSource replay = new();
        if (structured)
        {
            replay.AnyEventRaised += (_, _) => { };
        }

        if (scrub)
        {
            ((IBuildEventArgsReaderNotifications)replay).StringReadDone += args =>
            {
                if (args.OriginalString == command)
                {
                    args.StringToBeUsed = "redacted command";
                }
                else if (args.OriginalString == response)
                {
                    args.StringToBeUsed = "redacted response";
                }
            };
        }

        BinaryLogger logger = new() { Parameters = $"LogFile={output};OmitInitialInfo;ProjectImports=None" };
        try
        {
            logger.Initialize(replay);
            replay.Replay(input);
        }
        finally
        {
            logger.Shutdown();
        }

        using BuildEventArgsReader outputReader = BinaryLogReplayEventSource.OpenBuildEventsReader(output);
        outputReader.FileFormatVersion.ShouldBe(30);
        outputReader.MinimumReaderVersion.ShouldBe(30);
        outputReader.Read().ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe(scrub ? "redacted command" : command);
        outputReader.Read().ShouldBeOfType<BuildMessageEventArgs>().Message.ShouldBe(scrub ? "redacted response" : response);
        outputReader.Read().ShouldBeOfType<BuildMessageEventArgs>().Message.ShouldBe("after slice");
        outputReader.Read().ShouldBeNull();

        using BinaryReader wire = BinaryLogReplayEventSource.OpenReader(output);
        wire.ReadInt32().ShouldBe(30);
        wire.ReadInt32().ShouldBe(30);
        CountSlices(wire).ShouldBe(structured && !scrub ? 1 : 0);
    }

    [Fact]
    public void StringSlice_OlderFormatStillReadsButCannotContainSlices()
    {
        using MemoryStream stream = new();
        WriteEvents(stream, [Message("older message")]);
        using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
        using (BuildEventArgsReader reader = new(binaryReader, 28))
        {
            reader.Read().ShouldBeOfType<BuildMessageEventArgs>().Message.ShouldBe("older message");
            reader.Read().ShouldBeNull();
        }

        stream.SetLength(0);
        stream.Position = 0;
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        WriteSlice(writer, 10, 0, 0);
        stream.Position = 0;
        using BuildEventArgsReader olderReader = new(binaryReader, 28);
        Should.Throw<InvalidDataException>(() => olderReader.Read());
    }

    [Fact]
    public void StringSlice_RawReplayOfOlderLogPreservesItsFormat()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithEnvironmentInvariant();
        string payload = new('x', 8192);
        string command = "compiler " + payload;
        string response = Response(payload);
        string input = env.ExpectFile(".binlog").Path;
        string output = env.ExpectFile(".binlog").Path;
        using MemoryStream currentFormat = new();
        WriteEvents(currentFormat,
        [
            new TaskCommandLineEventArgs(command, "Csc", MessageImportance.Low),
            Message(response),
        ]);

        // Expand all strings first to produce a valid format 28 input with the same events.
        using (BinaryWriter writer = new(new GZipStream(File.Create(input), CompressionLevel.Optimal)))
        using (BinaryReader binaryReader = new(currentFormat, Encoding.UTF8, leaveOpen: true))
        using (BuildEventArgsReader reader = new(binaryReader, BinaryLogger.FileFormatVersion))
        {
            writer.Write(28);
            writer.Write(18);
            BuildEventArgsWriter eventWriter = new(writer);
            reader.StringReadDone += args => eventWriter.WriteStringRecord(args.StringToBeUsed);
            BuildEventArgsReader.RawRecord record;
            while ((record = reader.ReadRaw()).RecordKind != BinaryLogRecordKind.EndOfFile)
            {
                eventWriter.WriteBlob(record.RecordKind, record.Stream);
            }

            writer.Write((byte)BinaryLogRecordKind.EndOfFile);
        }

        BinaryLogReplayEventSource replay = new();
        BinaryLogger logger = new() { Parameters = $"LogFile={output};OmitInitialInfo;ProjectImports=None" };
        try
        {
            logger.Initialize(replay);
            replay.Replay(input);
        }
        finally
        {
            logger.Shutdown();
        }

        using BuildEventArgsReader outputReader = BinaryLogReplayEventSource.OpenBuildEventsReader(output);
        outputReader.FileFormatVersion.ShouldBe(28);
        outputReader.MinimumReaderVersion.ShouldBe(18);
        outputReader.Read().ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe(command);
        outputReader.Read().ShouldBeOfType<BuildMessageEventArgs>().Message.ShouldBe(response);
        outputReader.Read().ShouldBeNull();

        using BinaryReader wire = BinaryLogReplayEventSource.OpenReader(output);
        wire.ReadInt32().ShouldBe(28);
        wire.ReadInt32().ShouldBe(18);
        CountSlices(wire).ShouldBe(0);
    }

    [Fact]
    public void StringSlice_DirectReaderRawReplayRequiresReader30()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithEnvironmentInvariant();
        string payload = new('x', 8192);
        using MemoryStream stream = new();
        WriteEvents(stream,
        [
            new TaskCommandLineEventArgs("compiler " + payload, "Csc", MessageImportance.Low),
            Message(Response(payload)),
        ]);
        using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
        using BuildEventArgsReader reader = new(binaryReader, BinaryLogger.FileFormatVersion);
        reader.MinimumReaderVersion.ShouldBe(30);

        string output = env.ExpectFile(".binlog").Path;
        BinaryLogReplayEventSource replay = new();
        BinaryLogger logger = new() { Parameters = $"LogFile={output};OmitInitialInfo;ProjectImports=None" };
        try
        {
            logger.Initialize(replay);
            replay.Replay(reader, CancellationToken.None);
        }
        finally
        {
            logger.Shutdown();
        }

        using BuildEventArgsReader outputReader = BinaryLogReplayEventSource.OpenBuildEventsReader(output);
        outputReader.FileFormatVersion.ShouldBe(30);
        outputReader.MinimumReaderVersion.ShouldBe(30);
        outputReader.Read().ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe("compiler " + payload);
        outputReader.Read().ShouldBeOfType<BuildMessageEventArgs>().Message.ShouldBe(Response(payload));
        outputReader.Read().ShouldBeNull();
    }

    [Fact]
    public void StringSlice_ExplicitHeaderMinimumOverridesDirectReaderDefault()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(30);
        writer.Write(18);
        writer.Write((byte)BinaryLogRecordKind.EndOfFile);
        stream.Position = 0;
        using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
        using BuildEventArgsReader reader = BinaryLogReplayEventSource.OpenBuildEventsReader(binaryReader, closeInput: false);
        reader.MinimumReaderVersion.ShouldBe(18);
        reader.Read().ShouldBeNull();
    }

    private static BuildMessageEventArgs Message(string? text) => new(text, null, null, MessageImportance.Low);
    private static string Response(string payload) => "BuildResponseFile = '" + payload + "'";

    private static void WriteEvents(MemoryStream stream, BuildEventArgs[] events)
    {
        using BinaryWriter binaryWriter = new(stream, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter writer = new(binaryWriter);
        foreach (BuildEventArgs args in events)
        {
            writer.Write(args);
        }

        binaryWriter.Write((byte)BinaryLogRecordKind.EndOfFile);
        stream.Position = 0;
    }

    private static void WriteSlice(BinaryWriter writer, int sourceId, int start, int length)
    {
        BinaryWriterExtensions.Write7BitEncodedInt(writer, (int)BinaryLogRecordKind.StringSlice);
        BinaryWriterExtensions.Write7BitEncodedInt(writer, sourceId);
        BinaryWriterExtensions.Write7BitEncodedInt(writer, start);
        BinaryWriterExtensions.Write7BitEncodedInt(writer, length);
        writer.Write("prefix");
        writer.Write("suffix");
    }

    private static BinaryLogRecordKind ReadKind(BinaryReader reader)
        => (BinaryLogRecordKind)BinaryReaderExtensions.Read7BitEncodedInt(reader);

    private static int CountSlices(BinaryReader reader)
    {
        int count = 0;
        BinaryLogRecordKind kind;
        while ((kind = ReadKind(reader)) != BinaryLogRecordKind.EndOfFile)
        {
            if (kind == BinaryLogRecordKind.String)
            {
                reader.ReadString();
            }
            else if (kind == BinaryLogRecordKind.StringSlice)
            {
                count++;
                BinaryReaderExtensions.Read7BitEncodedInt(reader);
                BinaryReaderExtensions.Read7BitEncodedInt(reader);
                BinaryReaderExtensions.Read7BitEncodedInt(reader);
                reader.ReadString();
                reader.ReadString();
            }
            else
            {
                int length = BinaryReaderExtensions.Read7BitEncodedInt(reader);
                reader.ReadBytes(length).Length.ShouldBe(length);
            }
        }

        return count;
    }
}
