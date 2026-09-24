// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class BinaryLogCombinedSerializationTests
{
    private readonly ITestOutputHelper _output;

    public BinaryLogCombinedSerializationTests(ITestOutputHelper output)
    {
        _output = output;
        _ = ItemGroupLoggingHelper.ItemGroupIncludeLogMessagePrefix;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedDependenciesPrecedeDefinitionsAndSurviveStreamedReplay(bool rawRewrite)
    {
        var fixture = CreateFixture();
        byte[] bytes = Serialize(fixture.Events);
        List<WireRecord> records = Scan(bytes);
        records.Count(r => r.Kind == BinaryLogRecordKind.StringSlice).ShouldBe(2);
        records.Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(2);
        records.FindLastIndex(r => r.Kind == BinaryLogRecordKind.StringSlice)
            .ShouldBeLessThan(records.FindIndex(r => r.Kind == BinaryLogRecordKind.ItemSequence));

        if (rawRewrite)
        {
            byte[] rewritten = RawRewrite(bytes);
            Scan(rewritten).ShouldNotContain(r => r.Kind == BinaryLogRecordKind.StringSlice);
            SequencePayloads(rewritten).ShouldBe(SequencePayloads(bytes));
            bytes = rewritten;
        }

        List<BuildEventArgs> result = ReadEvents(bytes);
        result.Count.ShouldBe(fixture.Events.Length);
        result[1].ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe(fixture.Command);
        AssertItems(result[2].ShouldBeOfType<TaskParameterEventArgs>().Items, fixture.Items);
        result[3].Message.ShouldBe(fixture.Response);
        AssertItems(result[4].ShouldBeOfType<TaskParameterEventArgs>().Items, fixture.Items);
        AssertItems(result[5].ShouldBeOfType<TaskParameterEventArgs>().Items, fixture.Items.Reverse());
        AssertItems(result[9].ShouldBeOfType<TargetFinishedEventArgs>().TargetOutputs, fixture.Items);
        Snapshot(result).ShouldBe(Snapshot(ReadEvents(Serialize(fixture.Events))));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void CombinedBinaryLoggerReplayPreservesDiagnosticsImportsAndScrubbedItems(bool structured, bool scrub, bool directReader)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithEnvironmentInvariant();
        var fixture = CreateFixture();
        byte[] bytes = Serialize(fixture.Events, includeArchive: true);
        string input = env.ExpectFile(".binlog").Path;
        string output = env.ExpectFile(".binlog").Path;
        using (BinaryWriter writer = new(new GZipStream(File.Create(input), CompressionLevel.Optimal)))
        {
            writer.Write(30);
            writer.Write(30);
            writer.Write(bytes);
        }

        List<string> originals = [];
        void Scrub(StringReadEventArgs args)
        {
            originals.Add(args.OriginalString);
            if (scrub)
            {
                args.StringToBeUsed = args.OriginalString == fixture.Command ? "command redacted"
                    : args.OriginalString == fixture.Response ? "response redacted"
                    : args.OriginalString == fixture.Metadata ? "metadata redacted"
                    : args.OriginalString;
            }
        }

        BinaryLogReplayEventSource replay = new();
        if (structured)
        {
            replay.AnyEventRaised += (_, _) => { };
        }
        ((IBuildEventArgsReaderNotifications)replay).StringReadDone += Scrub;
        BinaryLogger logger = new() { Parameters = $"LogFile={output};OmitInitialInfo;ProjectImports=Embed" };
        try
        {
            logger.Initialize(replay);
            if (directReader)
            {
                using ChunkedReadStream stream = new(bytes);
                using BinaryReader binaryReader = new(stream);
                using BuildEventArgsReader direct = new(binaryReader, 30);
                direct.MinimumReaderVersion.ShouldBe(30);
                replay.Replay(direct, default);
            }
            else
            {
                replay.Replay(input);
            }
        }
        finally
        {
            logger.Shutdown();
        }

        originals.Count(s => s == fixture.Command).ShouldBe(1);
        originals.Count(s => s == fixture.Response).ShouldBe(1);
        originals.Count(s => s == fixture.Metadata).ShouldBe(1);
        originals.ShouldNotContain("BuildResponseFile = '");
        originals.ShouldNotContain("'");

        using BuildEventArgsReader reader = BinaryLogReplayEventSource.OpenBuildEventsReader(output);
        reader.FileFormatVersion.ShouldBe(30);
        reader.MinimumReaderVersion.ShouldBe(30);
        List<ArchiveFile> imports = [];
        reader.ArchiveFileEncountered += args => imports.Add(args.ArchiveData.ToArchiveFile());
        List<BuildEventArgs> result = Drain(reader);
        result.Count.ShouldBe(fixture.Events.Length);
        result[1].ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe(scrub ? "command redacted" : fixture.Command);
        var parameter = result[2].ShouldBeOfType<TaskParameterEventArgs>();
        var first = parameter.Items[0].ShouldBeOfType<TaskItemData>();
        first.ItemSpec.ShouldBe(scrub ? "response redacted" : fixture.Response);
        first.GetMetadata("Metadata").ShouldBe(scrub ? "metadata redacted" : fixture.Metadata);
        Snapshot(result).ShouldBe(Snapshot(ReadEvents(bytes, Scrub)));
        imports.Count.ShouldBe(1);
        imports[0].FullPath.ShouldBe("combined.props");
        imports[0].Content.ShouldBe("<Project><!-- \u00e9 %3b --></Project>");

        using BinaryReader wire = BinaryLogReplayEventSource.OpenReader(output);
        wire.ReadInt32().ShouldBe(30);
        wire.ReadInt32().ShouldBe(30);
        using MemoryStream payload = new();
        wire.BaseStream.CopyTo(payload);
        List<WireRecord> outputRecords = Scan(payload.ToArray());
        outputRecords.Count(r => r.Kind == BinaryLogRecordKind.StringSlice).ShouldBe(structured && !scrub ? 2 : 0);
        outputRecords.Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(2);
        if (!structured)
        {
            SequencePayloads(payload.ToArray()).ShouldBe(SequencePayloads(bytes));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedResetAndSourceEvictionSurviveSkippedUnknownEvent(bool rawRewrite)
    {
        string payload = new('x', 8192);
        string command = "compiler " + payload;
        ITaskItem[] initial = CreateItems(Response(payload), "initial");
        List<BuildEventArgs> events = [new TaskCommandLineEventArgs(command, "Csc", MessageImportance.Low), Parameter(initial)];
        for (int i = 0; i < ItemSequenceCache.MaximumEntries; i++)
        {
            events.Add(Parameter(CreateItems("item-" + i, "fill")));
        }

        // The next event starts a new dictionary epoch and defines slices used by its item sequence.
        ITaskItem[] afterReset = CreateItems(Response(payload.Substring(1)), Response(payload.Substring(2)));
        events.Add(Parameter(afterReset));
        events.Add(Parameter(afterReset));
        for (int i = 0; i < StringSliceSourceCache.Capacity; i++)
        {
            events.Add(Message(new string((char)('a' + i), 4096)));
        }
        string afterEviction = Response(payload.Substring(3));
        events.Add(Message(afterEviction));
        events.Add(Parameter(afterReset));

        byte[] bytes = Serialize(events);
        List<WireRecord> records = Scan(bytes);
        records.Count(r => r.Kind == BinaryLogRecordKind.StringSlice).ShouldBe(3);
        WireRecord reset = records.Single(r => r.Kind == BinaryLogRecordKind.ItemSequence && r.Payload.Length == 0);
        WireRecord unknown = records.First(r => r.Kind == BinaryLogRecordKind.TaskParameter && r.Start > reset.Start);
        // All current record kinds and this test kind fit in one byte; leave the real payload length untouched.
        bytes[unknown.Start] = 100;
        if (rawRewrite)
        {
            bytes = RawRewrite(bytes);
        }

        List<BinaryLogReaderErrorEventArgs> errors = [];
        using ChunkedReadStream stream = new(bytes);
        using BinaryReader binaryReader = new(stream);
        using BuildEventArgsReader reader = new(binaryReader, 30) { SkipUnknownEvents = true };
        reader.RecoverableReadError += errors.Add;
        List<BuildEventArgs> result = Drain(reader);
        errors.Count.ShouldBe(1);
        errors[0].ErrorType.ShouldBe(ReaderErrorType.UnknownEventType);
        result.Count.ShouldBe(events.Count - 1);
        AssertItems(result[ItemSequenceCache.MaximumEntries + 2].ShouldBeOfType<TaskParameterEventArgs>().Items, afterReset);
        result[result.Count - 2].Message.ShouldBe(afterEviction);
        AssertItems(result[result.Count - 1].ShouldBeOfType<TaskParameterEventArgs>().Items, afterReset);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedReaderRejectsBothAmbiguous29Schemas(bool allowForwardCompatibility)
    {
        foreach (bool itemSchema in new[] { false, true })
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(29);
            writer.Write(29);
            writer.Write7BitEncodedInt(40);
            if (itemSchema)
            {
                writer.Write7BitEncodedInt(0);
            }
            else
            {
                writer.Write7BitEncodedInt(10);
                writer.Write7BitEncodedInt(0);
                writer.Write7BitEncodedInt(4096);
                writer.Write("prefix");
                writer.Write("suffix");
            }

            stream.Position = 0;
            using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
            Should.Throw<NotSupportedException>(() =>
                BinaryLogReplayEventSource.OpenBuildEventsReader(binaryReader, false, allowForwardCompatibility))
                .Message.ShouldContain("29");
            Should.Throw<NotSupportedException>(() => new BuildEventArgsReader(binaryReader, 29))
                .Message.ShouldContain("29");
        }
    }

    [Theory]
    [InlineData(30, 30, false, true)]
    [InlineData(30, 18, false, true)]
    [InlineData(28, 18, false, true)]
    [InlineData(31, 30, true, true)]
    [InlineData(31, 30, false, false)]
    [InlineData(30, 31, true, false)]
    [InlineData(28, 31, true, false)]
    public void CombinedFileReaderHonorsDeclaredMinimum(int format, int minimum, bool forward, bool supported)
    {
        BinaryLogger.ForwardCompatibilityMinimalVersion.ShouldBe(18);
        BinaryLogger.FileFormatVersion.ShouldBe(30);
        BinaryLogger.MinimumReaderVersion.ShouldBe(30);
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(format);
        writer.Write(minimum);
        stream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
        stream.Position = 0;
        using BinaryReader binaryReader = new(stream, Encoding.UTF8, leaveOpen: true);
        if (supported)
        {
            using BuildEventArgsReader reader = BinaryLogReplayEventSource.OpenBuildEventsReader(binaryReader, false, forward);
            reader.FileFormatVersion.ShouldBe(format);
            reader.MinimumReaderVersion.ShouldBe(minimum);
            reader.Read().ShouldBeNull();
        }
        else
        {
            Should.Throw<NotSupportedException>(() => BinaryLogReplayEventSource.OpenBuildEventsReader(binaryReader, false, forward));
        }
    }

    [Theory]
    [InlineData(18)]
    [InlineData(21)]
    [InlineData(28)]
    public void CombinedReaderPreservesLegacyInlineItems(int version)
    {
        ITaskItem[] items = [new TaskItemData("item%3b\u00e9", new Dictionary<string, string> { ["Metadata"] = "value" })];
        TargetFinishedEventArgs target = new(null, null, "Target", "project.proj", "targets", true, items);
        byte[] bytes = Serialize([Message("legacy"), target, target]);
        Scan(bytes).ShouldNotContain(r => r.Kind == BinaryLogRecordKind.ItemSequence || r.Kind == BinaryLogRecordKind.StringSlice);
        using MemoryStream stream = new(bytes);
        using BinaryReader binaryReader = new(stream);
        using BuildEventArgsReader reader = new(binaryReader, version);
        reader.Read().ShouldBeOfType<BuildMessageEventArgs>().Message.ShouldBe("legacy");
        AssertItems(reader.Read().ShouldBeOfType<TargetFinishedEventArgs>().TargetOutputs, items);
        AssertItems(reader.Read().ShouldBeOfType<TargetFinishedEventArgs>().TargetOutputs, items);
        reader.Read().ShouldBeNull();
    }

    private static (BuildEventArgs[] Events, ITaskItem[] Items, string Command, string Response, string Metadata) CreateFixture()
    {
        string payload = new string('x', 8192) + " \u00e9\u4e2d\U0001f600 %3b \"quoted\";";
        string command = "compiler/\U0001f600 " + payload;
        string response = Response(payload);
        string metadata = Response(payload.Substring(2));
        ITaskItem[] items = CreateItems(response, metadata);
        BuildEventArgs[] events =
        [
            new BuildStartedEventArgs("start", string.Empty),
            new TaskCommandLineEventArgs(command, "Csc", MessageImportance.Low),
            Parameter(items),
            Message(response),
            Parameter(items),
            Parameter(items.Reverse().ToArray()),
            new BuildWarningEventArgs("subcategory", "W123", "file.cs", 2, 3, 4, 5, "warning \u00e9", "help", "sender"),
            new BuildErrorEventArgs("subcategory", "E123", "file.cs", 6, 7, 8, 9, "error %3b", "help", "sender"),
            new ProjectImportedEventArgs(10, 11, "import") { ProjectFile = "project.proj", ImportedProjectFile = "combined.props", UnexpandedProject = "$(Import)" },
            new TargetFinishedEventArgs(null, null, "Compile", "project.proj", "combined.props", false, items),
            new BuildFinishedEventArgs("finish", null, false),
        ];
        return (events, items, command, response, metadata);
    }

    private static ITaskItem[] CreateItems(string first, string metadata)
        => Enumerable.Range(0, 16)
            .Select(i => (ITaskItem)new TaskItemData(i == 0 ? first : $"item-{i}-%3b-\u00e9", new Dictionary<string, string> { ["Metadata"] = metadata }))
            .ToArray();

    private static BuildMessageEventArgs Message(string message) => new(message, null, null, MessageImportance.Low);
    private static string Response(string payload) => "BuildResponseFile = '" + payload + "'";
    private static TaskParameterEventArgs Parameter(IList items)
        => new(TaskParameterMessageKind.TaskInput, null, null, "Items", items, true, DateTime.MinValue);

    private static void AssertItems(IEnumerable actual, IEnumerable expected)
        => ItemSnapshot(actual).ShouldBe(ItemSnapshot(expected));

    private static string[] ItemSnapshot(IEnumerable items)
        => items.Cast<ITaskItem>().Select(item => item.ItemSpec + "|" + item.GetMetadata("Metadata")).ToArray();

    private static string[] Snapshot(IEnumerable<BuildEventArgs> events)
        => events.Select(e => e.GetType().Name + "|" + e.Message + "|" + (e switch
        {
            TaskParameterEventArgs parameter => parameter.Kind + "|" + parameter.ItemType + "|" + string.Join("\n", ItemSnapshot(parameter.Items)),
            TargetFinishedEventArgs target => target.TargetName + "|" + target.ProjectFile + "|" + target.TargetFile + "|" + target.Succeeded + "|" + string.Join("\n", ItemSnapshot(target.TargetOutputs)),
            BuildWarningEventArgs warning => $"{warning.Code}|{warning.File}|{warning.LineNumber}|{warning.ColumnNumber}|{warning.EndLineNumber}|{warning.EndColumnNumber}|{warning.Subcategory}|{warning.HelpKeyword}|{warning.SenderName}",
            BuildErrorEventArgs error => $"{error.Code}|{error.File}|{error.LineNumber}|{error.ColumnNumber}|{error.EndLineNumber}|{error.EndColumnNumber}|{error.Subcategory}|{error.HelpKeyword}|{error.SenderName}",
            ProjectImportedEventArgs import => $"{import.ProjectFile}|{import.ImportedProjectFile}|{import.UnexpandedProject}|{import.ImportIgnored}|{import.LineNumber}|{import.ColumnNumber}",
            TaskCommandLineEventArgs command => command.CommandLine + "|" + command.TaskName + "|" + command.Importance,
            BuildFinishedEventArgs finished => finished.Succeeded.ToString(),
            _ => string.Empty,
        })).ToArray();

    private static byte[] Serialize(IEnumerable<BuildEventArgs> events, bool includeArchive = false)
    {
        using MemoryStream stream = new();
        using BinaryWriter binaryWriter = new(stream, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter writer = new(binaryWriter);
        foreach (BuildEventArgs e in events)
        {
            writer.Write(e);
        }
        if (includeArchive)
        {
            using MemoryStream archive = new();
            using (ZipArchive zip = new(archive, ZipArchiveMode.Create, leaveOpen: true))
            {
                using StreamWriter entry = new(zip.CreateEntry("combined.props").Open(), new UTF8Encoding(false));
                entry.Write("<Project><!-- \u00e9 %3b --></Project>");
            }
            archive.Position = 0;
            writer.WriteBlob(BinaryLogRecordKind.ProjectImportArchive, archive);
        }
        stream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
        return stream.ToArray();
    }

    private static List<BuildEventArgs> ReadEvents(byte[] bytes, Action<StringReadEventArgs>? scrub = null)
    {
        using ChunkedReadStream stream = new(bytes);
        using BinaryReader binaryReader = new(stream);
        using BuildEventArgsReader reader = new(binaryReader, 30);
        reader.StringReadDone += scrub;
        return Drain(reader);
    }

    private static List<BuildEventArgs> Drain(BuildEventArgsReader reader)
    {
        List<BuildEventArgs> events = [];
        while (reader.Read() is BuildEventArgs e)
        {
            events.Add(e);
        }
        return events;
    }

    private static byte[] RawRewrite(byte[] bytes)
    {
        using ChunkedReadStream input = new(bytes);
        using BinaryReader binaryReader = new(input);
        using BuildEventArgsReader reader = new(binaryReader, 30);
        using MemoryStream output = new();
        using BinaryWriter binaryWriter = new(output, Encoding.UTF8, leaveOpen: true);
        BuildEventArgsWriter writer = new(binaryWriter);
        reader.StringReadDone += args => writer.WriteStringRecord(args.StringToBeUsed);
        reader.EmbeddedContentRead += args => writer.WriteBlob(args.ContentKind, args.ContentStream);
        BuildEventArgsReader.RawRecord record;
        while ((record = reader.ReadRaw()).RecordKind != BinaryLogRecordKind.EndOfFile)
        {
            writer.WriteBlob(record.RecordKind, record.Stream);
        }
        output.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
        return output.ToArray();
    }

    private sealed record WireRecord(BinaryLogRecordKind Kind, int Start, byte[] Payload);

    private static string[] SequencePayloads(byte[] bytes)
        => Scan(bytes).Where(r => r.Kind == BinaryLogRecordKind.ItemSequence).Select(r => Convert.ToBase64String(r.Payload)).ToArray();

    private static List<WireRecord> Scan(byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        using BinaryReader reader = new(stream);
        List<WireRecord> records = [];
        while (true)
        {
            int start = (int)stream.Position;
            BinaryLogRecordKind kind = (BinaryLogRecordKind)reader.Read7BitEncodedInt();
            if (kind == BinaryLogRecordKind.EndOfFile)
            {
                stream.Position.ShouldBe(stream.Length);
                return records;
            }
            byte[] payload = [];
            if (kind == BinaryLogRecordKind.String)
            {
                reader.ReadString();
            }
            else if (kind == BinaryLogRecordKind.StringSlice)
            {
                reader.Read7BitEncodedInt();
                reader.Read7BitEncodedInt();
                reader.Read7BitEncodedInt();
                reader.ReadString();
                reader.ReadString();
            }
            else
            {
                int length = reader.Read7BitEncodedInt();
                payload = reader.ReadBytes(length);
                payload.Length.ShouldBe(length);
            }
            records.Add(new(kind, start, payload));
        }
    }

    private sealed class ChunkedReadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, 7));
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
