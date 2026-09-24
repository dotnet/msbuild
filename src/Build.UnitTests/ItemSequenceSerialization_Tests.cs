// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using UtilitiesTaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.Build.UnitTests;

public class ItemSequenceSerializationTests
{
    private readonly ITestOutputHelper _output;

    public ItemSequenceSerializationTests(ITestOutputHelper output)
    {
        _output = output;
        _ = ItemGroupLoggingHelper.ItemGroupIncludeLogMessagePrefix;
    }

    [Fact]
    public void ItemSequencesPreserveLegacyBytesAndReduceRepeatedPayload()
    {
        ITaskItem[] items = CreateItems(64);
        byte[] bytes = Serialize([Parameter(items), Parameter(items), Parameter(items)]);
        List<(BinaryLogRecordKind Kind, byte[] Bytes)> records = ReadRecords(bytes);
        records.Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(1);
        records.Count(r => r.Kind == BinaryLogRecordKind.NameValueList).ShouldBe(1);

        byte[] inlineBytes = ExpandTaskParameterSequences(bytes);
        bytes.Length.ShouldBeLessThan(inlineBytes.Length);
        List<BuildEventArgs> replayed = ReadEvents(bytes);
        List<BuildEventArgs> legacy = ReadEvents(inlineBytes, version: 28);
        replayed.Count.ShouldBe(3);
        for (int i = 0; i < replayed.Count; i++)
        {
            Snapshot(((TaskParameterEventArgs)replayed[i]).Items).ShouldBe(Snapshot(items));
            Snapshot(((TaskParameterEventArgs)legacy[i]).Items).ShouldBe(Snapshot(items));
        }

        Serialize(replayed).ShouldBe(bytes);

        var first = (TaskItemData)((TaskParameterEventArgs)replayed[0]).Items[0]!;
        var second = (TaskItemData)((TaskParameterEventArgs)replayed[1]).Items[0]!;
        first.ShouldNotBeSameAs(second);
        first.Metadata.ShouldNotBeSameAs(second.Metadata);
        first.ItemSpec = "changed";
        first.Metadata["Metadata"] = "changed";
        second.ItemSpec.ShouldBe(items[0].ItemSpec);
        second.GetMetadata("Metadata").ShouldBe("value%3b");
    }

    [Fact]
    public void ItemSequencesDistinguishOrderMetadataAndMetadataSuppression()
    {
        ITaskItem[] items = CreateItems(32);
        ITaskItem[] reversed = items.Reverse().ToArray();
        ITaskItem[] changed = CreateItems(32);
        ((TaskItemData)changed[0]).Metadata["Metadata"] = "different";
        byte[] bytes = Serialize([
            Parameter(items), Parameter(reversed), Parameter(changed),
            Parameter(items, metadata: false), Parameter(changed, metadata: false),
            Parameter(items)]);

        ReadRecords(bytes).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(4);
        List<BuildEventArgs> events = ReadEvents(bytes);
        Snapshot(((TaskParameterEventArgs)events[0]).Items).ShouldBe(Snapshot(items));
        Snapshot(((TaskParameterEventArgs)events[1]).Items).ShouldBe(Snapshot(reversed));
        Snapshot(((TaskParameterEventArgs)events[2]).Items).ShouldBe(Snapshot(changed));
        for (int i = 3; i <= 4; i++)
        {
            ((TaskParameterEventArgs)events[i]).Items.Cast<ITaskItem>()
                .Select(item => item.MetadataCount).ShouldAllBe(count => count == 0);
        }
        Snapshot(((TaskParameterEventArgs)events[5]).Items).ShouldBe(Snapshot(items));
    }

    [Fact]
    public void ItemSequencesPreserveMixedItemsEscapingAndMetadataCache()
    {
        ImmutableDictionary<string, string> metadata = ImmutableDictionaryExtensions.EmptyMetadata.Add("Metadata", "value%253b");
        var custom = new CountingTaskItem();
        var itemData = new CountingItemData();
        var value = new CountingValue();
        var shared = new UtilitiesTaskItem("escaped%253b");
        ((IMetadataContainer)shared).ImportMetadata(metadata);
        object?[] items = [shared, custom, itemData, new AbsolutePath("relative.txt", new AbsolutePath(Path.GetFullPath("."))), default(AbsolutePath), null, value,
            .. Enumerable.Range(0, 32).Select(i => (object)$"item-{i}")];
        using var stream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var writer = new BuildEventArgsWriter(binaryWriter);
        writer.Write(Parameter(items));
        writer.Write(Parameter(items));
        stream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);

        custom.ItemSpecReads.ShouldBe(2);
        custom.MetadataReads.ShouldBe(2);
        itemData.MetadataReads.ShouldBe(2);
        itemData.IncludeReads.ShouldBe(2);
        value.Reads.ShouldBe(2);
#if DEBUG
        writer.MetadataReferenceCacheHits.ShouldBe(1);
#endif
        byte[] bytes = stream.ToArray();
        ReadRecords(bytes).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(1);
        foreach (TaskParameterEventArgs e in ReadEvents(bytes))
        {
            ITaskItem[] result = e.Items.Cast<ITaskItem>().ToArray();
            result[0].ItemSpec.ShouldBe("escaped%3b");
            result[0].GetMetadata("Metadata").ShouldBe("value%3b");
            result[1].GetMetadata("Metadata").ShouldBe("value%3b");
            result[2].GetMetadata("Metadata").ShouldBe("value%3b");
            result[3].ItemSpec.ShouldBe("relative.txt");
            result[4].ItemSpec.ShouldBe(string.Empty);
            result[5].ItemSpec.ShouldBe(string.Empty);
            result[6].ItemSpec.ShouldBe("value");
        }
    }

    [Fact]
    public void ItemSequencesDoNotReadSuppressedMetadata()
    {
        var custom = new CountingTaskItem();
        var itemData = new CountingItemData();
        object[] items = [.. Enumerable.Repeat<object>(custom, 16), .. Enumerable.Repeat<object>(itemData, 16)];
        byte[] bytes = Serialize([Parameter(items, metadata: false), Parameter(items, metadata: false)]);
        ReadRecords(bytes).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(1);
        custom.MetadataReads.ShouldBe(0);
        itemData.MetadataReads.ShouldBe(0);
        custom.ItemSpecReads.ShouldBe(32);
        itemData.IncludeReads.ShouldBe(32);
    }

    [Fact]
    public void ItemSequencesKeepNullEmptyTinyAndOversizedListsInline()
    {
        byte[] bytes = Serialize([
            Parameter(null), Parameter(Array.Empty<object>()), Parameter(new object?[] { null }),
            Parameter(CreateItems(1)), Parameter(CreateItems(ItemSequenceCache.MaximumSequenceBytes))]);
        ReadRecords(bytes).ShouldNotContain(r => r.Kind == BinaryLogRecordKind.ItemSequence);
        List<BuildEventArgs> events = ReadEvents(bytes);
        ((TaskParameterEventArgs)events[0]).Items.ShouldBeNull();
        ((TaskParameterEventArgs)events[1]).Items.ShouldBeNull();
        ((ITaskItem)((TaskParameterEventArgs)events[2]).Items[0]!).ItemSpec.ShouldBe(string.Empty);
        ((TaskParameterEventArgs)events[4]).Items.Count.ShouldBe(ItemSequenceCache.MaximumSequenceBytes);
        ReadEvents(bytes, version: 28).Count.ShouldBe(events.Count);
    }

    [Fact]
    public void ItemSequencesEnumerateTargetOutputsOnlyOnce()
    {
        var items = new SinglePassItems(CreateItems(32));
        var e = new TargetFinishedEventArgs(null, null, "Target", "project.proj", "targets", true, items);
        byte[] bytes = Serialize([e]);
        items.Enumerations.ShouldBe(1);
        var result = (TargetFinishedEventArgs)ReadEvents(bytes)[0];
        result.TargetOutputs.Cast<ITaskItem>().Count().ShouldBe(32);
        ReadRecords(bytes).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(1);
    }

    [Fact]
    public void ItemSequencesKeepNestedListsAndEmbedScanning()
    {
        ITaskItem[] items = CreateItems(32);
        var entries = new List<DictionaryEntry>();
        foreach (string type in new[] { "First", "EmbedInBinlog", "Last" })
        {
            entries.AddRange(items.Select(item => new DictionaryEntry(type, item)));
        }

        var evaluation = new ProjectEvaluationFinishedEventArgs(null, null)
        {
            Items = entries,
            ProjectFile = Path.Combine(Path.GetFullPath("."), "project.proj"),
        };
        var embedded = new List<string>();
        byte[] bytes = Serialize([evaluation, evaluation], embedded.Add);
        ReadRecords(bytes).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(1);
        embedded.Count.ShouldBe(64);
        embedded.Take(32).ShouldBe(embedded.Skip(32));
        foreach (ProjectEvaluationFinishedEventArgs e in ReadEvents(bytes))
        {
            e.Items.ShouldNotBeNull();
            e.Items.Cast<DictionaryEntry>().Select(entry => (string)entry.Key)
                .ShouldBe(entries.Select(entry => (string)entry.Key));
            Snapshot(e.Items.Cast<DictionaryEntry>().Select(entry => entry.Value)).ShouldBe(Snapshot(entries.Select(entry => entry.Value)));
        }
    }

    [Fact]
    public void ItemSequencesDoNotResetWithinAnEvent()
    {
        var entries = new List<DictionaryEntry>();
        for (int i = 0; i < ItemSequenceCache.MaximumEntries + 1; i++)
        {
            entries.AddRange(CreateItems(16, prefix: $"{i}-").Select(item => new DictionaryEntry($"Type{i}", item)));
        }

        var evaluation = new ProjectEvaluationFinishedEventArgs(null, null) { Items = entries };
        byte[] bytes = Serialize([evaluation, Parameter(CreateItems(32))]);
        List<(BinaryLogRecordKind Kind, byte[] Bytes)> records = ReadRecords(bytes);
        int eventIndex = records.FindIndex(r => r.Kind == BinaryLogRecordKind.ProjectEvaluationFinished);
        records[eventIndex + 1].Kind.ShouldBe(BinaryLogRecordKind.ItemSequence);
        records[eventIndex + 1].Bytes.ShouldBeEmpty();
        records.Take(eventIndex).Count(r => r.Kind == BinaryLogRecordKind.ItemSequence).ShouldBe(ItemSequenceCache.MaximumEntries);
        var replayed = (ProjectEvaluationFinishedEventArgs)ReadEvents(bytes)[0];
        replayed.Items.ShouldNotBeNull();
        Snapshot(replayed.Items.Cast<DictionaryEntry>().Select(e => e.Value)).ShouldBe(Snapshot(entries.Select(e => e.Value)));
    }

    [Fact]
    public void ItemSequenceCacheBoundsAndReset()
    {
        var cache = new ItemSequenceCache();
        for (int i = 0; i < ItemSequenceCache.MaximumEntries; i++)
        {
            byte[] data = new byte[ItemSequenceCache.MinimumBytes];
            BitConverter.GetBytes(i).CopyTo(data, 0);
            cache.TryGetOrAdd(new(data), out int id, out bool added).ShouldBeTrue();
            id.ShouldBe(i);
            added.ShouldBeTrue();
        }
        cache.TryGetOrAdd(new(new byte[ItemSequenceCache.MinimumBytes + 1]), out _, out _).ShouldBeFalse();
        cache.Count.ShouldBe(ItemSequenceCache.MaximumEntries);
        cache.ResetRequired.ShouldBeTrue();
        cache.TryGetOrAdd(new(new byte[ItemSequenceCache.MinimumBytes]), out int existingId, out bool existingAdded).ShouldBeTrue();
        existingId.ShouldBe(0);
        existingAdded.ShouldBeFalse();
        cache.Clear();
        cache.Count.ShouldBe(0);
        cache.ByteCount.ShouldBe(0);
        cache.ResetRequired.ShouldBeFalse();

        for (int i = 0; i < ItemSequenceCache.MaximumBytes / ItemSequenceCache.MaximumSequenceBytes; i++)
        {
            byte[] data = new byte[ItemSequenceCache.MaximumSequenceBytes];
            BitConverter.GetBytes(i).CopyTo(data, 0);
            cache.TryGetOrAdd(new(data), out _, out _).ShouldBeTrue();
        }
        cache.ByteCount.ShouldBe(ItemSequenceCache.MaximumBytes);
        cache.TryGetOrAdd(new(new byte[ItemSequenceCache.MinimumBytes]), out _, out _).ShouldBeFalse();
        cache.ByteCount.ShouldBe(ItemSequenceCache.MaximumBytes);
        cache.ResetRequired.ShouldBeTrue();
    }

    [Fact]
    public void ItemSequenceCacheComparesBytesAfterHashCollision()
    {
        // These two strings have the same FNV-1a 32-bit hash, but different bytes.
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

    [Fact]
    public void ItemSequencesSurviveSkippedUnknownEvent()
    {
        byte[] bytes = ReplaceFirstTaskParameterKind(
            Serialize([Parameter(CreateItems(32)), Parameter(CreateItems(32))]),
            (BinaryLogRecordKind)100);
        using var stream = new MemoryStream(bytes);
        using var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        stream.Position = 0;
        using var eventsReader = new BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion) { SkipUnknownEvents = true };
        var errors = new List<BinaryLogReaderErrorEventArgs>();
        eventsReader.RecoverableReadError += errors.Add;
        var e = eventsReader.Read().ShouldBeOfType<TaskParameterEventArgs>();
        e.Items.Count.ShouldBe(32);
        errors.Count.ShouldBe(1);
        errors[0].ErrorType.ShouldBe(ReaderErrorType.UnknownEventType);
        eventsReader.Read().ShouldBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(ItemSequenceCache.MaximumSequenceBytes + 1)]
    public void ItemSequencesRejectInvalidDictionaryLengths(int length)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write7BitEncodedInt((int)BinaryLogRecordKind.ItemSequence);
        writer.Write7BitEncodedInt(length);
        stream.Position = 0;
        using var binaryReader = new BinaryReader(stream);
        using var reader = new BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion);
        Should.Throw<InvalidDataException>(() => reader.Read());
    }

    [Theory]
    [InlineData(ItemSequenceCache.MinimumBytes, ItemSequenceCache.MaximumEntries)]
    [InlineData(ItemSequenceCache.MaximumSequenceBytes, ItemSequenceCache.MaximumBytes / ItemSequenceCache.MaximumSequenceBytes)]
    public void ItemSequencesReaderEnforcesDictionaryBounds(int length, int maximumEntries)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        byte[] bytes = new byte[length];
        for (int i = 0; i <= maximumEntries; i++)
        {
            writer.Write7BitEncodedInt((int)BinaryLogRecordKind.ItemSequence);
            writer.Write7BitEncodedInt(length);
            writer.Write(bytes);
        }
        writer.Write((byte)0);
        stream.Position = 0;
        using var binaryReader = new BinaryReader(stream);
        using var reader = new BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion);
        Should.Throw<InvalidDataException>(() => reader.Read());
    }

    [Fact]
    public void ItemSequencesResetInvalidatesReferences()
    {
        byte[] bytes = Serialize([Parameter(CreateItems(32)), Parameter(CreateItems(32))]);
        using var input = new MemoryStream(bytes);
        using var rawReader = new BinaryReader(input);
        using var reader = new BuildEventArgsReader(rawReader, BinaryLogger.FileFormatVersion);
        while (reader.ReadRaw().RecordKind != BinaryLogRecordKind.TaskParameter)
        {
        }
        // Consume the first event to locate the second, which contains a reference but no definition.
        var second = reader.ReadRaw();
        second.RecordKind.ShouldBe(BinaryLogRecordKind.TaskParameter);
        using var payload = new MemoryStream();
        second.Stream.CopyTo(payload);

        using var invalid = new MemoryStream();
        using var writer = new BinaryWriter(invalid, Encoding.UTF8, leaveOpen: true);
        writer.Write7BitEncodedInt((int)BinaryLogRecordKind.ItemSequence);
        writer.Write7BitEncodedInt(0);
        writer.Write7BitEncodedInt((int)second.RecordKind);
        writer.Write7BitEncodedInt((int)payload.Length);
        writer.Write(payload.ToArray());
        writer.Write((byte)0);

        // Preserve the real string and dictionary definitions, then replace the second event
        // with a reset followed by that same (now invalid) reference.
        long secondEventStart = input.Position - payload.Length - 2;
        using var combined = new MemoryStream();
        combined.Write(bytes, 0, (int)secondEventStart);
        invalid.Position = 0;
        invalid.CopyTo(combined);
        combined.Position = 0;
        using var combinedReader = new BinaryReader(combined);
        using var eventsReader = new BuildEventArgsReader(combinedReader, BinaryLogger.FileFormatVersion);
        eventsReader.Read().ShouldBeOfType<TaskParameterEventArgs>();
        Should.Throw<InvalidDataException>(() => eventsReader.Read()).ToString().ShouldContain("Invalid item sequence reference");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ItemSequencesReplayPreservesHeaderAndContent(bool structured, bool directReader)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable(
            "MSBUILDTARGETOUTPUTLOGGING",
            Environment.GetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING"));
        env.SetEnvironmentVariable(
            "MSBUILDLOGIMPORTS",
            Environment.GetEnvironmentVariable("MSBUILDLOGIMPORTS"));
        env.SetEnvironmentVariable(
            "MSBUILDBINARYLOGGERENABLED",
            Environment.GetEnvironmentVariable("MSBUILDBINARYLOGGERENABLED"));
        string input = env.ExpectFile(".binlog").Path;
        string output = env.ExpectFile(".binlog").Path;
        byte[] bytes = Serialize([Parameter(CreateItems(64)), Parameter(CreateItems(64))]);
        using (var file = File.Create(input))
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        using (var writer = new BinaryWriter(gzip))
        {
            writer.Write(BinaryLogger.FileFormatVersion);
            writer.Write(BinaryLogger.MinimumReaderVersion);
            writer.Write(bytes);
        }

        var replay = new BinaryLogReplayEventSource();
        if (structured)
        {
            replay.AnyEventRaised += (_, _) => { };
        }
        var logger = new BinaryLogger { Parameters = $"LogFile={output};OmitInitialInfo;ProjectImports=None" };
        try
        {
            logger.Initialize(replay);
            if (directReader)
            {
                using var eventStream = new MemoryStream(bytes);
                using var binaryReader = new BinaryReader(eventStream);
                using var reader = new BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion);
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

        using var resultReader = BinaryLogReplayEventSource.OpenReader(output);
        resultReader.ReadInt32().ShouldBe(30);
        resultReader.ReadInt32().ShouldBe(30);
        using var payload = new MemoryStream();
        resultReader.BaseStream.CopyTo(payload);
        payload.ToArray().ShouldBe(bytes);
        ReadEvents(payload.ToArray()).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(16, 16)]
    [InlineData(18, 18)]
    [InlineData(28, 18)]
    [InlineData(30, 30)]
    [InlineData(31, 31)]
    public void ItemSequencesDirectReaderPreservesLegacyMinimum(int version, int minimum)
    {
        using var stream = new MemoryStream();
        using var binaryReader = new BinaryReader(stream);
        using var reader = new BuildEventArgsReader(binaryReader, version);
        reader.MinimumReaderVersion.ShouldBe(minimum);
    }

    private static TaskParameterEventArgs Parameter(IList? items, bool metadata = true)
        => new(TaskParameterMessageKind.TaskInput, null, null, "Items", items, metadata, DateTime.MinValue);

    private static ITaskItem[] CreateItems(int count, string prefix = "item-")
        => Enumerable.Range(0, count)
            .Select(i => (ITaskItem)new TaskItemData(prefix + i, new Dictionary<string, string> { ["Metadata"] = "value%3b" }))
            .ToArray();

    private static string[] Snapshot(IEnumerable items)
        => items.Cast<ITaskItem>().Select(item => $"{item.ItemSpec}|{item.GetMetadata("Metadata")}").ToArray();

    private static byte[] Serialize(IEnumerable<BuildEventArgs> events, Action<string>? embed = null)
    {
        using var stream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var writer = new BuildEventArgsWriter(binaryWriter);
        writer.EmbedFile += embed;
        foreach (BuildEventArgs e in events)
        {
            writer.Write(e);
        }
        stream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
        return stream.ToArray();
    }

    private static List<BuildEventArgs> ReadEvents(byte[] bytes, int version = BinaryLogger.FileFormatVersion)
    {
        using var stream = new MemoryStream(bytes);
        using var binaryReader = new BinaryReader(stream);
        using var reader = new BuildEventArgsReader(binaryReader, version);
        var result = new List<BuildEventArgs>();
        while (reader.Read() is BuildEventArgs e)
        {
            result.Add(e);
        }
        return result;
    }

    private static List<(BinaryLogRecordKind Kind, byte[] Bytes)> ReadRecords(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var binaryReader = new BinaryReader(stream);
        using var reader = new BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion);
        var result = new List<(BinaryLogRecordKind, byte[])>();
        while (true)
        {
            var record = reader.ReadRaw();
            if (record.RecordKind == BinaryLogRecordKind.EndOfFile)
            {
                return result;
            }
            using var payload = new MemoryStream();
            record.Stream.CopyTo(payload);
            result.Add((record.RecordKind, payload.ToArray()));
        }
    }

    private static byte[] ExpandTaskParameterSequences(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = new BinaryReader(input);
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        var sequences = new List<byte[]>();
        while (true)
        {
            var kind = (BinaryLogRecordKind)reader.Read7BitEncodedInt();
            if (kind == BinaryLogRecordKind.ItemSequence)
            {
                int length = reader.Read7BitEncodedInt();
                if (length == 0)
                {
                    sequences.Clear();
                }
                else
                {
                    sequences.Add(reader.ReadBytes(length));
                }
                continue;
            }
            writer.Write7BitEncodedInt((int)kind);
            if (kind == BinaryLogRecordKind.EndOfFile)
            {
                return output.ToArray();
            }
            if (kind == BinaryLogRecordKind.String)
            {
                writer.Write(reader.ReadString());
                continue;
            }

            byte[] payload = reader.ReadBytes(reader.Read7BitEncodedInt());
            if (kind == BinaryLogRecordKind.TaskParameter)
            {
                // These test events end with the five-byte reference and two null name IDs.
                int prefixLength = payload.Length - 7;
                using var referenceStream = new MemoryStream(payload, prefixLength, 5);
                using var referenceReader = new BinaryReader(referenceStream);
                byte[] sequence = sequences[~referenceReader.Read7BitEncodedInt()];
                writer.Write7BitEncodedInt(prefixLength + sequence.Length + 2);
                writer.Write(payload, 0, prefixLength);
                writer.Write(sequence);
                writer.Write((byte)0);
                writer.Write((byte)0);
            }
            else
            {
                writer.Write7BitEncodedInt(payload.Length);
                writer.Write(payload);
            }
        }
    }

    private static byte[] ReplaceFirstTaskParameterKind(byte[] bytes, BinaryLogRecordKind replacementKind)
    {
        using var input = new MemoryStream(bytes);
        using var reader = new BinaryReader(input);
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        bool replaced = false;
        while (true)
        {
            BinaryLogRecordKind kind = (BinaryLogRecordKind)reader.Read7BitEncodedInt();
            if (!replaced && kind == BinaryLogRecordKind.TaskParameter)
            {
                kind = replacementKind;
                replaced = true;
            }

            writer.Write7BitEncodedInt((int)kind);
            if (kind == BinaryLogRecordKind.EndOfFile)
            {
                replaced.ShouldBeTrue();
                return output.ToArray();
            }

            if (kind == BinaryLogRecordKind.String)
            {
                writer.Write(reader.ReadString());
                continue;
            }

            int length = reader.Read7BitEncodedInt();
            writer.Write7BitEncodedInt(length);
            writer.Write(reader.ReadBytes(length));
        }
    }

    private sealed class SinglePassItems(ITaskItem[] items) : IEnumerable
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

    private sealed class CountingValue
    {
        internal int Reads { get; private set; }
        public override string ToString()
        {
            Reads++;
            return "value";
        }
    }

    private sealed class CountingTaskItem : ITaskItem
    {
        internal int ItemSpecReads { get; private set; }
        internal int MetadataReads { get; private set; }
        public string ItemSpec
        {
            get
            {
                ItemSpecReads++;
                return "custom";
            }
            set => throw new NotSupportedException();
        }
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
}
