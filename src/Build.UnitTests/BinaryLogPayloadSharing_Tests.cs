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

public class BinaryLogPayloadSharingTests
{
    private readonly ITestOutputHelper _output;

    public BinaryLogPayloadSharingTests(ITestOutputHelper output)
    {
        _output = output;
        _ = ItemGroupLoggingHelper.ItemGroupIncludeLogMessagePrefix;
    }

    [Theory]
    [InlineData(4095, "exact", false)]
    [InlineData(4096, "exact", true)]
    [InlineData(1048576, "exact", true)]
    [InlineData(1048577, "exact", false)]
    [InlineData(8192, "missing", false)]
    [InlineData(8192, "case", false)]
    [InlineData(8192, "not-suffix", false)]
    [InlineData(8192, "wrapper", false)]
    [InlineData(8192, "quote", false)]
    [InlineData(8192, "oversized-source", false)]
    public void SlicesRequireAnEligibleExactSuffix(int length, string scenario, bool shared)
    {
        string payload = new('x', length);
        string source = scenario switch
        {
            "case" => payload.ToUpperInvariant(),
            "not-suffix" => payload + " trailing",
            "oversized-source" => new string('p', StringSliceSourceCache.MaximumLength) + payload,
            _ => payload,
        };
        string response = scenario switch
        {
            "wrapper" => "buildResponseFile = '" + payload + "'",
            "quote" => "BuildResponseFile = '" + payload,
            _ => Response(payload),
        };
        byte[] bytes = Encode((writer, _) =>
        {
            if (scenario != "missing")
            {
                writer.WriteStringRecord(source);
            }
            writer.WriteStringRecord(response, allowSlice: true);
        });
        WireRecord record = Scan(bytes).Last();
        record.Kind.ShouldBe(shared ? BinaryLogRecordKind.StringSlice : BinaryLogRecordKind.String);
        if (shared)
        {
            (bytes.Length - record.Start).ShouldBeLessThan(64);
        }
        List<string> originals = [];
        Read(bytes, reader => reader.StringReadDone += args => originals.Add(args.OriginalString)).ShouldBeEmpty();
        originals.Last().ShouldBe(response);
    }

    [Fact]
    public void SourceWindowUsesOnlyEligibleOrdinaryOriginals()
    {
        string payload = new('x', 8192);
        string response = Response(payload);
        byte[] bytes = Encode((writer, _) =>
        {
            writer.WriteStringRecord(payload);
            for (int i = 0; i < StringSliceSourceCache.Capacity; i++)
            {
                writer.WriteStringRecord("short");
                writer.WriteStringRecord(response, allowSlice: true);
            }
            for (int i = 1; i < StringSliceSourceCache.Capacity; i++)
            {
                writer.WriteStringRecord(new string((char)('a' + i), 4096));
            }
            writer.WriteStringRecord(response, allowSlice: true);
            writer.WriteStringRecord(new string('z', 4096));
            writer.WriteStringRecord(response, allowSlice: true);
        });
        Scan(bytes).Count(r => r.Kind == BinaryLogRecordKind.StringSlice).ShouldBe(9);
        List<string> originals = [];
        Read(bytes, reader => reader.StringReadDone += args =>
        {
            originals.Add(args.OriginalString);
            args.StringToBeUsed = args.OriginalString == "short"
                ? new string('s', StringSliceSourceCache.MaximumLength + 1) : "scrubbed";
        });
        originals.Count(s => s == response).ShouldBe(10);
    }

    [Theory]
    [InlineData(0, 0, 1, false)]
    [InlineData(1, 0, 1, false)]
    [InlineData(9, 0, 1, false)]
    [InlineData(11, 0, 1, false)]
    [InlineData(-1, 0, 1, false)]
    [InlineData(10, -1, 1, false)]
    [InlineData(10, 0, -1, false)]
    [InlineData(10, 4097, 0, false)]
    [InlineData(10, 4095, 2, false)]
    [InlineData(10, 1, int.MaxValue, false)]
    [InlineData(10, 0, 1, true)]
    public void InvalidOrExpiredSliceReferencesCannotBeSkipped(int source, int start, int length, bool expired)
    {
        byte[] bytes = Encode((writer, binary) =>
        {
            for (int i = 0; i < (expired ? StringSliceSourceCache.Capacity + 1 : 1); i++)
            {
                writer.WriteStringRecord(new string('x', 4096));
            }
            WriteInts(binary, (int)BinaryLogRecordKind.StringSlice, source, start, length);
            binary.Write("prefix");
            binary.Write("suffix");
        });
        Should.Throw<InvalidDataException>(() => Read(bytes, reader =>
        {
            reader.SkipUnknownEvents = true;
            reader.RecoverableReadError += _ => throw new InvalidOperationException("Corrupt dependencies cannot be skipped.");
        }));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void InterruptedDependenciesPreserveOnlyCompletedEvents(bool slice, bool raw)
    {
        string payload = new('x', 8192);
        int completed = 0;
        byte[] bytes = Encode((writer, binary) =>
        {
            writer.Write(new TaskCommandLineEventArgs(payload, "Csc", MessageImportance.Low));
            completed = (int)binary.BaseStream.Position;
            if (slice)
            {
                writer.WriteStringRecord(Response(payload), allowSlice: true);
            }
            else
            {
                WriteInts(binary, (int)BinaryLogRecordKind.ItemSequence, ItemSequenceCache.MinimumBytes);
                binary.Write(new byte[ItemSequenceCache.MinimumBytes]);
            }
        });
        for (int length = completed; length < bytes.Length - 1; length++)
        {
            using BinaryReader binary = new(new MemoryStream(bytes, 0, length));
            using BuildEventArgsReader reader = new(binary, 30);
            List<string> originals = [];
            reader.StringReadDone += args => originals.Add(args.OriginalString);
            if (raw)
            {
                reader.ReadRaw().RecordKind.ShouldBe(BinaryLogRecordKind.TaskCommandLine);
                Should.Throw<EndOfStreamException>(() => reader.ReadRaw());
            }
            else
            {
                reader.Read().ShouldBeOfType<TaskCommandLineEventArgs>().CommandLine.ShouldBe(payload);
                Should.Throw<EndOfStreamException>(() => reader.Read());
            }
            originals.ShouldNotContain(Response(payload));
        }
    }

    [Fact]
    public void SequencesPreserveOrderMetadataAndIndependentItems()
    {
        ITaskItem[] items = Items("first", "value%3b");
        ITaskItem[] reversed = items.Reverse().ToArray();
        ITaskItem[] changed = Items("first", "different");
        byte[] bytes = Serialize([Parameter(items), Parameter(reversed), Parameter(changed),
            Parameter(items, false), Parameter(changed, false), Parameter(items)]);
        List<WireRecord> records = Scan(bytes);
        records.Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(4);
        WireRecord definition = records.First(r => r.Kind == BinaryLogRecordKind.ItemSequence);
        (Serialize([Parameter(items), Parameter(items)]).Length - Serialize([Parameter(items)]).Length)
            .ShouldBeLessThan(definition.Payload.Length);
        TaskParameterEventArgs[] events = Read(bytes).Cast<TaskParameterEventArgs>().ToArray();
        ItemSnapshot(events[0].Items).ShouldBe(ItemSnapshot(items));
        ItemSnapshot(events[1].Items).ShouldBe(ItemSnapshot(reversed));
        ItemSnapshot(events[2].Items).ShouldBe(ItemSnapshot(changed));
        events.Skip(3).Take(2).SelectMany(e => e.Items.Cast<ITaskItem>()).ShouldAllBe(item => item.MetadataCount == 0);
        ItemSnapshot(events[5].Items).ShouldBe(ItemSnapshot(items));
        var first = events[0].Items[0].ShouldBeOfType<TaskItemData>();
        var last = events[5].Items[0].ShouldBeOfType<TaskItemData>();
        first.ShouldNotBeSameAs(last);
        first.Metadata.ShouldNotBeSameAs(last.Metadata);
        first.ItemSpec = "changed";
        first.Metadata["Metadata"] = "changed";
        last.ItemSpec.ShouldBe("first");
        last.GetMetadata("Metadata").ShouldBe("value%3b");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(65536)]
    public void IneligibleListsStayInline(int count)
    {
        object?[]? items = count < 0 ? null : Enumerable.Repeat<object?>(null, count).ToArray();
        byte[] bytes = Serialize([Parameter(items)]);
        Scan(bytes).ShouldNotContain(r => r.Kind == BinaryLogRecordKind.ItemSequence);
        var result = Read(bytes).Single().ShouldBeOfType<TaskParameterEventArgs>();
        if (count <= 0)
        {
            result.Items.ShouldBeNull();
        }
        else
        {
            result.Items.Count.ShouldBe(count);
            result.Items.Cast<ITaskItem>().ShouldAllBe(item => item.ItemSpec == string.Empty && item.MetadataCount == 0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharingDoesNotReenumerateItemsOrReadSuppressedMetadata(bool metadata)
    {
        var taskItem = new CountingTaskItem();
        var itemData = new CountingItemData();
        object[] items = [new Utilities.TaskItem("escaped%253b"), taskItem, itemData,
            new AbsolutePath("relative.txt", new AbsolutePath(Path.GetFullPath("."))),
            default(AbsolutePath), "value", .. Enumerable.Range(0, 32).Select(i => (object)$"item-{i}")];
        var outputs = new SinglePassItems(items);
        byte[] bytes = Serialize([Parameter(items, metadata), Parameter(items, metadata),
            new TargetFinishedEventArgs(null, null, "Target", "project.proj", "targets", true, outputs)]);
        outputs.Enumerations.ShouldBe(1);
        taskItem.MetadataReads.ShouldBe(metadata ? 3 : 1);
        itemData.MetadataReads.ShouldBe(metadata ? 3 : 1);
        itemData.IncludeReads.ShouldBe(3);
        Scan(bytes).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(metadata ? 1 : 2);
        List<BuildEventArgs> result = Read(bytes);
        foreach (TaskParameterEventArgs e in result.OfType<TaskParameterEventArgs>())
        {
            var actual = e.Items.Cast<ITaskItem>().ToArray();
            actual.Skip(1).Take(2).ShouldAllBe(item => metadata ? item.GetMetadata("Metadata") == "value%3b" : item.MetadataCount == 0);
            actual.Take(6).Select(item => item.ItemSpec).ShouldBe(["escaped%3b", "custom", "data", "relative.txt", "", "value"]);
        }
        result.Last().ShouldBeOfType<TargetFinishedEventArgs>().TargetOutputs.Cast<ITaskItem>().Count().ShouldBe(items.Length);
    }

    [Theory]
    [InlineData(32, 4096)]
    [InlineData(65536, 64)]
    public void WriterAndReaderEnforceDictionaryBudgets(int length, int limit)
    {
        var cache = new ItemSequenceCache();
        foreach (int invalidLength in new[] { 31, 65537 })
        {
            cache.TryGetOrAdd(new(new byte[invalidLength]), out _, out _).ShouldBeFalse();
            cache.ResetRequired.ShouldBeFalse();
        }
        byte[] bytes = Encode((_, binary) =>
        {
            for (int i = 0; i <= limit; i++)
            {
                byte[] data = new byte[length];
                BitConverter.GetBytes(i).CopyTo(data, 0);
                cache.TryGetOrAdd(new(data), out int id, out bool added).ShouldBe(i < limit);
                added.ShouldBe(i < limit);
                if (added)
                {
                    id.ShouldBe(i);
                }
                WriteInts(binary, (int)BinaryLogRecordKind.ItemSequence, length);
                binary.Write(data);
            }
        });
        cache.Count.ShouldBe(limit);
        cache.ByteCount.ShouldBe(length * limit);
        cache.ResetRequired.ShouldBeTrue();
        cache.TryGetOrAdd(new(new byte[length]), out int existing, out bool duplicate).ShouldBeTrue();
        existing.ShouldBe(0);
        duplicate.ShouldBeFalse();
        Should.Throw<InvalidDataException>(() => Read(bytes));
        cache.Clear();
        cache.Count.ShouldBe(0);
        cache.ByteCount.ShouldBe(0);
        cache.ResetRequired.ShouldBeFalse();
    }

    [Fact]
    public void HashCollisionsAndCallerMutationDoNotAliasSequences()
    {
        byte[] first = Encoding.ASCII.GetBytes("costarring" + new string('x', 32));
        byte[] second = Encoding.ASCII.GetBytes("liquid" + new string('x', 32));
        ItemSequenceCache.ByteComparer.Instance.GetHashCode(new(first))
            .ShouldBe(ItemSequenceCache.ByteComparer.Instance.GetHashCode(new(second)));
        var cache = new ItemSequenceCache();
        cache.TryGetOrAdd(new(first), out int firstId, out _).ShouldBeTrue();
        cache.TryGetOrAdd(new(second), out int secondId, out _).ShouldBeTrue();
        firstId.ShouldNotBe(secondId);
        cache.TryGetOrAdd(new(first.ToArray()), out int duplicateId, out bool added).ShouldBeTrue();
        duplicateId.ShouldBe(firstId);
        added.ShouldBeFalse();
        first[0] = 0;
        cache.TryGetOrAdd(new(first), out int changedId, out added).ShouldBeTrue();
        changedId.ShouldNotBe(firstId);
        added.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(65537)]
    public void InvalidSequenceLengthsAreRejected(int length)
        => Should.Throw<InvalidDataException>(() => Read(Encode((_, binary) =>
            WriteInts(binary, (int)BinaryLogRecordKind.ItemSequence, length))));

    [Fact]
    public void ResetInvalidatesOldSequenceReferences()
    {
        byte[] bytes = Serialize([Parameter(Items("first", "value")), Parameter(Items("first", "value"))]);
        int second = Scan(bytes).Last(r => r.Kind == BinaryLogRecordKind.TaskParameter).Start;
        byte[] invalid = [.. bytes.Take(second), (byte)BinaryLogRecordKind.ItemSequence, 0, .. bytes.Skip(second)];
        Should.Throw<InvalidDataException>(() => Read(invalid)).ToString().ShouldContain("Invalid item sequence reference");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedListsResetBetweenEventsAndSurviveUnknownEventsAndSourceEviction(bool rawRewrite)
    {
        string payload = new('x', 8192);
        ITaskItem[] initial = Items(Response(payload), "initial");
        var entries = new List<DictionaryEntry>();
        for (int i = 0; i <= ItemSequenceCache.MaximumEntries; i++)
        {
            string type = i == 1 ? "EmbedInBinlog" : $"Type{i}";
            entries.AddRange((i == 0 ? initial : Items($"item-{i}", "fill")).Select(item => new DictionaryEntry(type, item)));
        }
        var evaluation = new ProjectEvaluationFinishedEventArgs(null, null)
        {
            Items = entries,
            ProjectFile = Path.Combine(Path.GetFullPath("."), "project.proj"),
        };
        ITaskItem[] afterReset = Items(Response(payload.Substring(1)), Response(payload.Substring(2)));
        List<BuildEventArgs> events = [new TaskCommandLineEventArgs(payload, "Csc", MessageImportance.Low),
            Parameter(initial), evaluation, Parameter(afterReset), Parameter(afterReset)];
        events.AddRange(Enumerable.Range(0, 8).Select(i => Message(new string((char)('a' + i), 4096))));
        events.Add(Message(Response(payload.Substring(3))));
        events.Add(Parameter(afterReset));
        List<string> embedded = [];
        byte[] bytes = Serialize(events, embedded.Add);
        embedded.Count.ShouldBe(16);
        List<WireRecord> records = Scan(bytes);
        records.Count(r => r.Kind == BinaryLogRecordKind.StringSlice).ShouldBe(3);
        WireRecord reset = records.Single(r => r.Kind == BinaryLogRecordKind.ItemSequence && r.Payload.Length == 0);
        records.TakeWhile(r => r.Start < reset.Start).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(4096);
        records[records.IndexOf(reset) - 1].Kind.ShouldBe(BinaryLogRecordKind.ProjectEvaluationFinished);
        bytes[records.First(r => r.Kind == BinaryLogRecordKind.TaskParameter && r.Start > reset.Start).Start] = 100;
        List<BinaryLogReaderErrorEventArgs> errors = [];
        List<BuildEventArgs> result = Read(rawRewrite ? RawRewrite(bytes) : bytes, reader =>
        {
            reader.SkipUnknownEvents = true;
            reader.RecoverableReadError += errors.Add;
        });
        errors.Single().ErrorType.ShouldBe(ReaderErrorType.UnknownEventType);
        result.Count.ShouldBe(events.Count - 1);
        var evaluated = result[2].ShouldBeOfType<ProjectEvaluationFinishedEventArgs>().Items.ShouldNotBeNull().Cast<DictionaryEntry>();
        evaluated.Select(e => e.Key).ShouldBe(entries.Select(e => e.Key));
        ItemSnapshot(evaluated.Select(e => e.Value)).ShouldBe(ItemSnapshot(entries.Select(e => e.Value)));
        ItemSnapshot(result[3].ShouldBeOfType<TaskParameterEventArgs>().Items).ShouldBe(ItemSnapshot(afterReset));
        result[result.Count - 2].Message.ShouldBe(Response(payload.Substring(3)));
        ItemSnapshot(result.Last().ShouldBeOfType<TaskParameterEventArgs>().Items).ShouldBe(ItemSnapshot(afterReset));
    }

    [Theory]
    [InlineData(false, false, false, 30)]
    [InlineData(false, true, false, 30)]
    [InlineData(true, false, false, 30)]
    [InlineData(true, true, false, 30)]
    [InlineData(false, false, true, 30)]
    [InlineData(false, true, true, 30)]
    [InlineData(true, false, true, 30)]
    [InlineData(true, true, true, 30)]
    [InlineData(false, false, false, 18)]
    [InlineData(false, false, false, 21)]
    [InlineData(false, false, false, 28)]
    public void ReplayPreservesDependenciesContentHeadersAndScrubbing(bool structured, bool scrub, bool direct, int version)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithEnvironmentInvariant();
        string payload = (version == 30 ? new string('x', 8192) : "legacy") + " \u00e9\u4e2d\U0001f600 %3b \"quoted\";";
        string command = "compiler/\U0001f600 " + payload;
        string response = Response(payload);
        string metadata = Response(payload.Substring(2));
        ITaskItem[] items = Items(response, metadata).Take(version == 30 ? 16 : 1).ToArray();
        BuildEventArgs ListEvent(ITaskItem[] values) => version < 21
            ? new TargetFinishedEventArgs(null, null, "Target", "project.proj", "targets", true, values)
            : Parameter(values);
        BuildEventArgs[] events = [new TaskCommandLineEventArgs(command, "Csc", MessageImportance.Low),
            ListEvent(items), Message(response), ListEvent(items), ListEvent(items.Reverse().ToArray()),
            Message(null), Message(string.Empty), Message("after slice"),
            new BuildWarningEventArgs("subcategory", "W123", "file.cs", 2, 3, 4, 5, "warning \u00e9", "help", "sender"),
            new BuildErrorEventArgs("subcategory", "E123", "file.cs", 6, 7, 8, 9, "error %3b", "help", "sender"),
            new ProjectImportedEventArgs(10, 11, "import") { ProjectFile = "project.proj", ImportedProjectFile = "shared.props" }];
        byte[] bytes = Serialize(events);
        List<WireRecord> records = Scan(bytes);
        records.Count(r => r.Kind == BinaryLogRecordKind.StringSlice).ShouldBe(version == 30 ? 2 : 0);
        records.Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(version == 30 ? 2 : 0);
        if (version == 30)
        {
            records.FindLastIndex(r => r.Kind == BinaryLogRecordKind.StringSlice)
                .ShouldBeLessThan(records.FindIndex(r => r.Kind == BinaryLogRecordKind.ItemSequence));
        }
        string input = env.ExpectFile(".binlog").Path;
        string output = env.ExpectFile(".binlog").Path;
        using (BinaryWriter writer = new(new GZipStream(File.Create(input), CompressionLevel.Optimal)))
        {
            writer.Write(version);
            writer.Write(version == 30 ? 30 : 18);
            writer.Write(bytes);
        }
        List<string> originals = [];
        string Sanitize(string text) => !scrub ? text : text == command ? "command redacted"
            : text == response ? "response redacted" : text == metadata ? "metadata redacted" : text;
        BinaryLogReplayEventSource replay = new();
        if (structured)
        {
            replay.AnyEventRaised += (_, _) => { };
        }
        ((IBuildEventArgsReaderNotifications)replay).StringReadDone += args =>
        {
            originals.Add(args.OriginalString);
            args.StringToBeUsed = Sanitize(args.OriginalString);
        };
        BinaryLogger logger = new() { Parameters = $"LogFile={output};OmitInitialInfo;ProjectImports=Embed" };
        try
        {
            logger.Initialize(replay);
            if (direct)
            {
                using BinaryReader binary = new(new ChunkedReadStream(bytes));
                using BuildEventArgsReader reader = new(binary, version);
                replay.Replay(reader, default);
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
        originals.Count(s => s == command).ShouldBe(1);
        originals.Count(s => s == response).ShouldBe(1);
        originals.Count(s => s == metadata).ShouldBe(1);
        originals.ShouldNotContain("BuildResponseFile = '");
        originals.ShouldNotContain("'");
        using BuildEventArgsReader resultReader = BinaryLogReplayEventSource.OpenBuildEventsReader(output);
        resultReader.FileFormatVersion.ShouldBe(version);
        resultReader.MinimumReaderVersion.ShouldBe(version == 30 ? 30 : 18);
        List<ArchiveFile> imports = [];
        resultReader.ArchiveFileEncountered += args => imports.Add(args.ArchiveData.ToArchiveFile());
        List<BuildEventArgs> result = Drain(resultReader);
        events[0] = new TaskCommandLineEventArgs(Sanitize(command), "Csc", MessageImportance.Low);
        foreach (ITaskItem item in items)
        {
            item.ItemSpec = Sanitize(item.ItemSpec);
            ((TaskItemData)item).Metadata["Metadata"] = Sanitize(item.GetMetadata("Metadata"));
        }
        events[2] = Message(Sanitize(response));
        Snapshot(result).ShouldBe(Snapshot(events));
        imports.Single().FullPath.ShouldBe("shared.props");
        imports.Single().Content.ShouldBe("<Project><!-- \u00e9 %3b --></Project>");
        using BinaryReader wire = BinaryLogReplayEventSource.OpenReader(output);
        wire.ReadInt32().ShouldBe(version);
        wire.ReadInt32().ShouldBe(version == 30 ? 30 : 18);
        using MemoryStream body = new();
        wire.BaseStream.CopyTo(body);
        List<WireRecord> rewritten = Scan(body.ToArray());
        rewritten.Count(r => r.Kind == BinaryLogRecordKind.StringSlice).ShouldBe(version == 30 && structured && !scrub ? 2 : 0);
        if (!structured)
        {
            rewritten.Where(r => r.Kind == BinaryLogRecordKind.ItemSequence).Select(r => Convert.ToBase64String(r.Payload))
                .ShouldBe(records.Where(r => r.Kind == BinaryLogRecordKind.ItemSequence).Select(r => Convert.ToBase64String(r.Payload)));
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(16, 16)]
    [InlineData(18, 18)]
    [InlineData(28, 18)]
    [InlineData(29, 0)]
    [InlineData(30, 30)]
    [InlineData(31, 31)]
    public void DirectReaderMinimumIsTruthful(int version, int minimum)
    {
        using BinaryReader binary = new(new MemoryStream());
        if (version == 29)
        {
            Should.Throw<NotSupportedException>(() => new BuildEventArgsReader(binary, version));
            return;
        }
        using BuildEventArgsReader reader = new(binary, version);
        reader.MinimumReaderVersion.ShouldBe(minimum);
    }

    [Theory]
    [InlineData(30, 30, false, true)]
    [InlineData(30, 18, false, true)]
    [InlineData(28, 18, false, true)]
    [InlineData(31, 30, true, true)]
    [InlineData(31, 30, false, false)]
    [InlineData(30, 31, true, false)]
    [InlineData(28, 31, true, false)]
    [InlineData(29, 29, false, false)]
    [InlineData(29, 29, true, false)]
    public void FileReaderHonorsExplicitMinimum(int format, int minimum, bool forward, bool supported)
    {
        BinaryLogger.ForwardCompatibilityMinimalVersion.ShouldBe(18);
        BinaryLogger.FileFormatVersion.ShouldBe(30);
        BinaryLogger.MinimumReaderVersion.ShouldBe(30);
        byte[] bytes = Encode((_, binary) =>
        {
            binary.Write(format);
            binary.Write(minimum);
        });
        using BinaryReader binary = new(new MemoryStream(bytes));
        if (!supported)
        {
            Should.Throw<NotSupportedException>(() => BinaryLogReplayEventSource.OpenBuildEventsReader(binary, false, forward));
            return;
        }
        using BuildEventArgsReader reader = BinaryLogReplayEventSource.OpenBuildEventsReader(binary, false, forward);
        reader.FileFormatVersion.ShouldBe(format);
        reader.MinimumReaderVersion.ShouldBe(minimum);
        reader.Read().ShouldBeNull();
    }

    private static ITaskItem[] Items(string first, string metadata)
        => Enumerable.Range(0, 16).Select(i => (ITaskItem)new TaskItemData(i == 0 ? first : $"item-{i}-%3b-\u00e9",
            new Dictionary<string, string> { ["Metadata"] = metadata })).ToArray();
    private static BuildMessageEventArgs Message(string? text) => new(text, null, null, MessageImportance.Low);
    private static string Response(string payload) => "BuildResponseFile = '" + payload + "'";
    private static TaskParameterEventArgs Parameter(IList? items, bool metadata = true)
        => new(TaskParameterMessageKind.TaskInput, null, null, "Items", items, metadata, DateTime.MinValue);
    private static string[] ItemSnapshot(IEnumerable items)
        => items.Cast<ITaskItem>().Select(item => item.ItemSpec + "|" + item.GetMetadata("Metadata")).ToArray();
    private static string[] Snapshot(IEnumerable<BuildEventArgs> events)
        => events.Select(e => e.GetType().Name + "|" + e.Message + "|" + (e switch
        {
            TaskParameterEventArgs parameter => string.Join("\n", ItemSnapshot(parameter.Items)),
            TargetFinishedEventArgs target => string.Join("\n", ItemSnapshot(target.TargetOutputs)),
            TaskCommandLineEventArgs command => command.CommandLine,
            BuildWarningEventArgs warning => $"{warning.Code}|{warning.File}|{warning.LineNumber}|{warning.ColumnNumber}|{warning.EndLineNumber}|{warning.EndColumnNumber}",
            BuildErrorEventArgs error => $"{error.Code}|{error.File}|{error.LineNumber}|{error.ColumnNumber}|{error.EndLineNumber}|{error.EndColumnNumber}",
            ProjectImportedEventArgs import => $"{import.ProjectFile}|{import.ImportedProjectFile}|{import.LineNumber}|{import.ColumnNumber}",
            _ => string.Empty,
        })).ToArray();

    private static byte[] Serialize(IEnumerable<BuildEventArgs> events, Action<string>? embed = null)
        => Encode((writer, _) =>
        {
            writer.EmbedFile += embed;
            foreach (BuildEventArgs e in events)
            {
                writer.Write(e);
            }
            using MemoryStream archive = new();
            using (ZipArchive zip = new(archive, ZipArchiveMode.Create, leaveOpen: true))
            {
                using StreamWriter entry = new(zip.CreateEntry("shared.props").Open(), new UTF8Encoding(false));
                entry.Write("<Project><!-- \u00e9 %3b --></Project>");
            }
            archive.Position = 0;
            writer.WriteBlob(BinaryLogRecordKind.ProjectImportArchive, archive);
        });

    private static byte[] Encode(Action<BuildEventArgsWriter, BinaryWriter> write)
    {
        using MemoryStream stream = new();
        using BinaryWriter binary = new(stream, Encoding.UTF8, leaveOpen: true);
        write(new BuildEventArgsWriter(binary), binary);
        stream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
        return stream.ToArray();
    }

    private static List<BuildEventArgs> Read(byte[] bytes, Action<BuildEventArgsReader>? configure = null)
    {
        using BinaryReader binary = new(new ChunkedReadStream(bytes));
        using BuildEventArgsReader reader = new(binary, 30);
        configure?.Invoke(reader);
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
        => Encode((writer, _) =>
        {
            using BinaryReader binary = new(new ChunkedReadStream(bytes));
            using BuildEventArgsReader reader = new(binary, 30);
            reader.StringReadDone += args => writer.WriteStringRecord(args.StringToBeUsed);
            reader.EmbeddedContentRead += args => writer.WriteBlob(args.ContentKind, args.ContentStream);
            BuildEventArgsReader.RawRecord record;
            while ((record = reader.ReadRaw()).RecordKind != BinaryLogRecordKind.EndOfFile)
            {
                writer.WriteBlob(record.RecordKind, record.Stream);
            }
        });

    private sealed record WireRecord(BinaryLogRecordKind Kind, int Start, byte[] Payload);

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

    private static void WriteInts(BinaryWriter writer, params int[] values)
    {
        foreach (int value in values)
        {
            writer.Write7BitEncodedInt(value);
        }
    }

    private sealed class SinglePassItems(object[] items) : IEnumerable
    {
        internal int Enumerations { get; private set; }
        public IEnumerator GetEnumerator()
        {
            (++Enumerations).ShouldBe(1);
            return items.GetEnumerator();
        }
    }

    private sealed class CountingItemData : IItemData
    {
        internal int IncludeReads { get; private set; }
        internal int MetadataReads { get; private set; }
        public string EvaluatedInclude
        {
            get
            {
                IncludeReads++;
                return "data";
            }
        }
        public IEnumerable<KeyValuePair<string, string>> EnumerateMetadata()
        {
            MetadataReads++;
            return [new("Metadata", "value%3b")];
        }
    }

    private sealed class CountingTaskItem : ITaskItem
    {
        internal int MetadataReads { get; private set; }
        public string ItemSpec { get; set; } = "custom";
        public int MetadataCount => 1;
        public ICollection MetadataNames => new[] { "Metadata" };
        public string GetMetadata(string name) => name == "Metadata" ? "value%3b" : string.Empty;
        public IDictionary CloneCustomMetadata()
        {
            MetadataReads++;
            return new Dictionary<string, string> { ["Metadata"] = "value%3b" };
        }
        public void SetMetadata(string name, string value) => throw new NotSupportedException();
        public void RemoveMetadata(string name) => throw new NotSupportedException();
        public void CopyMetadataTo(ITaskItem destinationItem) => throw new NotSupportedException();
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
