// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class NodeLifecycleJournal_Tests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NodeJournalTests_" + Guid.NewGuid().ToString("N"));

    public NodeLifecycleJournal_Tests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string NewJournal() => Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".jsonl");

    [Fact]
    public void RecordsRoundTripThroughTheFile()
    {
        string path = NewJournal();
        using (var writer = new NodeLifecycleJournalWriter(path))
        {
            writer.Append(42, NodeJournalKind.Main, NodeJournalEvent.Launched, NodeJournalKind.TaskHost, 3, 4242, "salt").ShouldBe(1);
            writer.Append(4242, NodeJournalKind.TaskHost, NodeJournalEvent.Exited, NodeJournalKind.None, 0, 0, null).ShouldBe(2);
        }

        List<NodeJournalRecord> records = NodeLifecycleJournalReader.ReadAll(path);
        records.Count.ShouldBe(2);

        NodeJournalRecord launched = records[0];
        launched.Sequence.ShouldBe(1);
        launched.ProcessId.ShouldBe(42);
        launched.Role.ShouldBe(NodeJournalKind.Main);
        launched.Event.ShouldBe(NodeJournalEvent.Launched);
        launched.EventName.ShouldBe("Launched");
        launched.Kind.ShouldBe(NodeJournalKind.TaskHost);
        launched.NodeId.ShouldBe(3);
        launched.SubjectProcessId.ShouldBe(4242);
        launched.Detail.ShouldBe("salt");
        launched.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
        (DateTime.UtcNow - launched.Timestamp).ShouldBeLessThan(TimeSpan.FromMinutes(5));

        NodeJournalRecord exited = records[1];
        exited.Sequence.ShouldBe(2);
        exited.Event.ShouldBe(NodeJournalEvent.Exited);
        exited.Detail.ShouldBeNull();
        exited.ToString().ShouldContain("pid 4242 (TaskHost) Exited");
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("quote \" and backslash \\ inside")]
    [InlineData("line\nbreak\r\tand tab")]
    [InlineData("control \u0001 and unicode \u00e9\u4e2d")]
    [InlineData("")]
    public void DetailIsEscaped(string detail)
    {
        var sb = new StringBuilder();
        NodeLifecycleJournalWriter.FormatRecord(sb, 7, DateTime.UtcNow.Ticks, 1, NodeJournalKind.Worker, NodeJournalEvent.Marker, NodeJournalKind.None, 0, 0, detail);
        string line = sb.ToString().TrimEnd('\n');
        line.ShouldNotContain("\n");
        line.ShouldNotContain("\r");

        NodeJournalRecord record = NodeJournalRecord.TryParse(line).ShouldNotBeNull();
        record.Sequence.ShouldBe(7);
        record.Detail.ShouldBe(detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("#MSBuildNodeJournal v1 next=0000000000000000001")]
    [InlineData("{\"seq\":1,\"event\":\"Marker\"")]
    [InlineData("{\"seq\":\"unterminated}")]
    public void MalformedLinesAreNotRecords(string line) => NodeJournalRecord.TryParse(line).ShouldBeNull();

    [Fact]
    public void UnknownEventsKeepTheirName()
    {
        NodeJournalRecord record = NodeJournalRecord.TryParse("{\"seq\":3,\"ticks\":0,\"pid\":1,\"role\":\"Main\",\"event\":\"FromTheFuture\",\"future\":7,\"more\":\"x\"}").ShouldNotBeNull();
        record.Event.ShouldBe(NodeJournalEvent.None);
        record.EventName.ShouldBe("FromTheFuture");
    }

    [Fact]
    public void ReaderIgnoresAnIncompleteTrailingRecordUntilItIsComplete()
    {
        string path = NewJournal();
        using (var writer = new NodeLifecycleJournalWriter(path))
        {
            writer.Append(1, NodeJournalKind.Main, NodeJournalEvent.Marker, NodeJournalKind.None, 0, 0, "first");
        }

        var sb = new StringBuilder();
        NodeLifecycleJournalWriter.FormatRecord(sb, 2, DateTime.UtcNow.Ticks, 1, NodeJournalKind.Main, NodeJournalEvent.Marker, NodeJournalKind.None, 0, 0, "second");
        string second = sb.ToString();
        File.AppendAllText(path, second.Substring(0, second.Length / 2));

        var records = new List<NodeJournalRecord>();
        long offset = 0;
        NodeLifecycleJournalReader.ReadNew(path, ref offset, records).ShouldBe(1);
        records.Single().Detail.ShouldBe("first");

        File.AppendAllText(path, second.Substring(second.Length / 2));
        NodeLifecycleJournalReader.ReadNew(path, ref offset, records).ShouldBe(1);
        records.Select(r => r.Detail).ShouldBe(["first", "second"]);

        NodeLifecycleJournalReader.ReadNew(path, ref offset, records).ShouldBe(0);
    }

    [Fact]
    public void ReaderToleratesAMissingFile()
    {
        var records = new List<NodeJournalRecord>();
        long offset = 0;
        NodeLifecycleJournalReader.ReadNew(NewJournal(), ref offset, records).ShouldBe(0);
        NodeLifecycleJournalReader.ReadAll(NewJournal()).ShouldBeEmpty();
    }

    [Fact]
    public void ConcurrentWritersProduceOneDenseTotalOrder()
    {
        const int Writers = 6;
        const int RecordsPerWriter = 150;
        string path = NewJournal();

        // Every writer has its own file handle, exactly like writers in different processes.
        using var start = new ManualResetEventSlim();
        Thread[] threads = Enumerable.Range(1, Writers).Select(w => new Thread(() =>
        {
            using var writer = new NodeLifecycleJournalWriter(path);
            start.Wait();
            for (int i = 0; i < RecordsPerWriter; i++)
            {
                writer.Append(w, NodeJournalKind.Worker, NodeJournalEvent.Marker, NodeJournalKind.None, i, 0, "w" + w);
            }
        })).ToArray();

        foreach (Thread thread in threads)
        {
            thread.Start();
        }

        start.Set();
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        List<NodeJournalRecord> records = NodeLifecycleJournalReader.ReadAll(path);
        records.Count.ShouldBe(Writers * RecordsPerWriter);
        records.Select(r => r.Sequence).ShouldBe(Enumerable.Range(1, Writers * RecordsPerWriter).Select(i => (long)i));

        // Program order of each writer is preserved in the global order.
        foreach (IGrouping<int, NodeJournalRecord> writer in records.GroupBy(r => r.ProcessId))
        {
            writer.Select(r => r.NodeId).ShouldBe(Enumerable.Range(0, RecordsPerWriter));
        }
    }

    [Fact]
    public void SequenceContinuesAcrossWriterInstances()
    {
        string path = NewJournal();
        using (var first = new NodeLifecycleJournalWriter(path))
        {
            first.Append(1, NodeJournalKind.Main, NodeJournalEvent.Marker, NodeJournalKind.None, 0, 0, null).ShouldBe(1);
        }

        using var second = new NodeLifecycleJournalWriter(path);
        second.Append(2, NodeJournalKind.Main, NodeJournalEvent.Marker, NodeJournalKind.None, 0, 0, null).ShouldBe(2);
    }

    [Theory]
    [InlineData("DisposalBegin:Crash", "DisposalBegin", "None", 1, "Crash")]
    [InlineData("disposalbegin@taskhost:hang", "DisposalBegin", "TaskHost", 1, "Hang")]
    [InlineData("Connected@Worker#3:Crash", "Connected", "Worker", 3, "Crash")]
    [InlineData(" GateEntered#2 : Hang ", "GateEntered", "None", 2, "Hang")]
    public void FaultSpecsParse(string text, string point, string role, int occurrence, string action)
    {
        NodeFaultSpec.TryParse(text, out NodeFaultSpec spec, out string? error).ShouldBeTrue(error);
        spec.Point.ToString().ShouldBe(point);
        spec.Role.ToString().ShouldBe(role);
        spec.Occurrence.ShouldBe(occurrence);
        spec.Action.ToString().ShouldBe(action);

        NodeFaultSpec.TryParse(spec.ToString(), out NodeFaultSpec again, out _).ShouldBeTrue();
        again.ShouldBe(spec);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DisposalBegin")]
    [InlineData("DisposalBegin:Explode")]
    [InlineData("NoSuchEvent:Crash")]
    [InlineData("11:Crash")]
    [InlineData("None:Crash")]
    [InlineData("FaultInjected:Crash")]
    [InlineData("DisposalBegin@Nobody:Crash")]
    [InlineData("DisposalBegin#0:Crash")]
    [InlineData("DisposalBegin#x:Crash")]
    public void InvalidFaultSpecsAreRejected(string? text)
    {
        NodeFaultSpec.TryParse(text, out _, out string? error).ShouldBeFalse();
        if (text is not null && text.Length > 0)
        {
            error.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void FaultListSkipsInvalidEntries()
    {
        NodeFaultSpec[] faults = NodeFaultSpec.ParseList("Connected:Crash; bogus ;DisposalEnd@TaskHost#2:Hang;");
        faults.Select(f => f.ToString()).ShouldBe(["Connected#1:Crash", "DisposalEnd@TaskHost#2:Hang"]);
        NodeFaultSpec.ParseList(null).ShouldBeEmpty();
    }

    [Fact]
    public void FaultsMatchOnlyTheirPointRoleAndOccurrence()
    {
        NodeFaultSpec.TryParse("DisposalBegin@TaskHost#2:Crash", out NodeFaultSpec spec, out _).ShouldBeTrue();
        spec.Matches(NodeJournalEvent.DisposalBegin, NodeJournalKind.TaskHost, 2).ShouldBeTrue();
        spec.Matches(NodeJournalEvent.DisposalBegin, NodeJournalKind.TaskHost, 1).ShouldBeFalse();
        spec.Matches(NodeJournalEvent.DisposalBegin, NodeJournalKind.Worker, 2).ShouldBeFalse();
        spec.Matches(NodeJournalEvent.DisposalEnd, NodeJournalKind.TaskHost, 2).ShouldBeFalse();

        NodeFaultSpec.TryParse("DisposalBegin:Crash", out NodeFaultSpec anyRole, out _).ShouldBeTrue();
        anyRole.Matches(NodeJournalEvent.DisposalBegin, NodeJournalKind.Worker, 1).ShouldBeTrue();
    }

    [Fact]
    public void ChaosDelaysAreReproducibleAndBounded()
    {
        var delays = new HashSet<int>();
        foreach (NodeJournalEvent evt in Enum.GetValues(typeof(NodeJournalEvent)))
        {
            for (int occurrence = 1; occurrence <= 20; occurrence++)
            {
                int delay = NodeLifecycleJournal.ComputeChaosDelay(12345, evt, NodeJournalKind.TaskHost, occurrence);
                delay.ShouldBeInRange(0, NodeLifecycleJournal.MaxChaosDelayMilliseconds);
                NodeLifecycleJournal.ComputeChaosDelay(12345, evt, NodeJournalKind.TaskHost, occurrence).ShouldBe(delay);
                delays.Add(delay);
            }
        }

        // Not a constant: the seed actually spreads the delays.
        delays.Count.ShouldBeGreaterThan(10);
        Enumerable.Range(1, 50).Select(seed => NodeLifecycleJournal.ComputeChaosDelay(seed, NodeJournalEvent.Connected, NodeJournalKind.Worker, 1))
            .Distinct().Count().ShouldBeGreaterThan(5);
    }

    [Fact]
    public void TheGateIsAStaticReadonlyBool()
    {
        // The JIT folds a static readonly bool of an initialized class into a constant, so every Record call site
        // compiles to nothing when journaling is off.
        FieldInfo enabled = typeof(Traits).GetField(nameof(Traits.NodeJournalEnabled), BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();
        enabled.FieldType.ShouldBe(typeof(bool));
        enabled.IsInitOnly.ShouldBeTrue();
        typeof(Traits).GetField(nameof(Traits.NodeJournalPath), BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull().IsInitOnly.ShouldBeTrue();
    }

    [Fact]
    public void EveryRecordMethodIsOnlyABranchOnTheGate()
    {
        MethodInfo[] records = typeof(NodeLifecycleJournal)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.StartsWith(nameof(NodeLifecycleJournal.Record), StringComparison.Ordinal))
            .ToArray();
        records.Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal)
            .ShouldBe([nameof(NodeLifecycleJournal.Record), nameof(NodeLifecycleJournal.RecordLaunched)]);
        records.Length.ShouldBe(3);

        foreach (MethodInfo record in records)
        {
            record.MethodImplementationFlags.HasFlag(MethodImplAttributes.AggressiveInlining).ShouldBeTrue(record.ToString());

            byte[] il = record.GetMethodBody().ShouldNotBeNull().GetILAsByteArray().ShouldNotBeNull();

            // ldsfld Traits.NodeJournalEnabled; brfalse(.s) ret; ldarg xN; call *Core; ret
            // Debug builds add nop/stloc/ldloc around it, which the JIT removes. Code coverage (non-Windows CI) prepends
            // a hit counter to every method, so find the gate load rather than expecting it at offset 0.
            int gate = FindGateLoad(il);
            gate.ShouldBeGreaterThanOrEqualTo(0, $"{record} must load {nameof(Traits.NodeJournalEnabled)}");

            // Coverage also adds a counter per block, so only uninstrumented builds (Windows CI, local) can check the size:
            // small enough that there is no room for anything but testing the gate and forwarding the arguments.
            bool instrumented = Array.FindIndex(il, 0, gate, b => b != 0x00) >= 0;
            if (!instrumented)
            {
                il.Length.ShouldBeLessThan(40, $"{record} must not do anything but test the gate and forward");
            }
        }
    }

    private static int FindGateLoad(byte[] il)
    {
        Module module = typeof(NodeLifecycleJournal).Module;
        for (int i = 0; i + 5 <= il.Length; i++)
        {
            if (il[i] != 0x7E)
            {
                continue;
            }

            try
            {
                if (module.ResolveField(BitConverter.ToInt32(il, i + 1))?.Name == nameof(Traits.NodeJournalEnabled))
                {
                    return i;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or BadImageFormatException)
            {
                // 0x7E inside another instruction's operand.
            }
        }

        return -1;
    }

    [Fact]
    public void EnumDetailIsFormattedByTheJournal()
    {
        string path = NewJournal();
        string? previous = NodeLifecycleJournal.CurrentSink;
        NodeLifecycleJournal.SetSinkForCurrentProcess(path).ShouldBeTrue("test assemblies arm the journal at startup");
        try
        {
            NodeLifecycleJournal.Record(NodeJournalEvent.Marker, NodeJournalKind.Worker, 1, 2, NodeJournalKind.TaskHost);
            NodeLifecycleJournal.RecordLaunched(3, 4, "/nologo /nodemode:2");
        }
        finally
        {
            NodeLifecycleJournal.SetSinkForCurrentProcess(previous);
        }

        List<NodeJournalRecord> records = NodeLifecycleJournalReader.ReadAll(path);
        records.Count.ShouldBe(2);
        records[0].Detail.ShouldBe(nameof(NodeJournalKind.TaskHost));
        records[1].Event.ShouldBe(NodeJournalEvent.Launched);
        records[1].Kind.ShouldBe(NodeJournalKind.TaskHost);
        records[1].NodeId.ShouldBe(3);
        records[1].SubjectProcessId.ShouldBe(4);
    }

#if NET
    [Fact]
    public void RecordDoesNotAllocateWithoutASink()
    {
        // The test process is armed (see TestAssemblyInfo), so this measures the armed-but-detached path, which does
        // strictly more work than the disabled path.
        string? previous = NodeLifecycleJournal.CurrentSink;
        NodeLifecycleJournal.SetSinkForCurrentProcess(null).ShouldBeTrue();
        try
        {
            NodeLifecycleJournal.Record(NodeJournalEvent.Marker, detail: "warm-up");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1_000; i++)
            {
                NodeLifecycleJournal.Record(NodeJournalEvent.Marker, NodeJournalKind.Worker, i, i, "detail");
                NodeLifecycleJournal.Record(NodeJournalEvent.Marker, NodeJournalKind.Worker, i, i, NodeJournalKind.TaskHost);
                NodeLifecycleJournal.RecordLaunched(i, i, "/nodemode:2");
            }

            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
        }
        finally
        {
            NodeLifecycleJournal.SetSinkForCurrentProcess(previous);
        }
    }
#endif

    [Fact]
    public void RecordWritesToTheSinkOfTheProcess()
    {
        string path = NewJournal();
        string? previous = NodeLifecycleJournal.CurrentSink;
        NodeLifecycleJournal.SetSinkForCurrentProcess(path).ShouldBeTrue("test assemblies arm the journal at startup");
        try
        {
            NodeLifecycleJournal.Record(NodeJournalEvent.Marker, NodeJournalKind.Worker, 5, 6, "hello");
        }
        finally
        {
            NodeLifecycleJournal.SetSinkForCurrentProcess(previous);
        }

        NodeJournalRecord record = NodeLifecycleJournalReader.ReadAll(path).ShouldHaveSingleItem();
        record.ProcessId.ShouldBe(EnvironmentUtilities.CurrentProcessId);
        record.Role.ShouldBe(NodeLifecycleJournal.CurrentRole);
        record.Event.ShouldBe(NodeJournalEvent.Marker);
        record.Kind.ShouldBe(NodeJournalKind.Worker);
        record.NodeId.ShouldBe(5);
        record.SubjectProcessId.ShouldBe(6);
        record.Detail.ShouldBe("hello");
    }

    [Fact]
    public void KindOfMapsNodeModes()
    {
        NodeLifecycleJournal.KindOf(NodeMode.OutOfProcNode).ShouldBe(NodeJournalKind.Worker);
        NodeLifecycleJournal.KindOf(NodeMode.OutOfProcTaskHostNode).ShouldBe(NodeJournalKind.TaskHost);
        NodeLifecycleJournal.KindOf(NodeMode.OutOfProcServerNode).ShouldBe(NodeJournalKind.Server);
        NodeLifecycleJournal.KindOf(null).ShouldBe(NodeJournalKind.None);
    }
}
