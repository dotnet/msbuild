// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests.Shared;

/// <summary>
/// The node side of a <see cref="NodeScenario.Gate"/>: code running in any process of a scenario (typically a test
/// task, or a disposable it registers) blocks here until the test releases the gate of the same name.
/// </summary>
/// <remarks>
/// Entering records <see cref="NodeJournalEvent.GateEntered"/>, which is also a fault point
/// (<c>GateEntered@TaskHost:Crash</c>). There is deliberately no timeout: a gate that is never released is a test bug
/// and is caught by the scenario's hang ceiling, which also kills the process.
/// </remarks>
internal static class NodeScenarioGate
{
    private const int PollIntervalMilliseconds = 10;

    /// <summary>
    /// Records that this process reached gate <paramref name="name"/> and waits until the test releases it.
    /// </summary>
    public static void Enter(string name)
    {
        string journal = NodeLifecycleJournal.CurrentSink
            ?? throw new InvalidOperationException($"Gate '{name}' was entered in a process that has no node lifecycle journal. Gates only work inside a NodeScenario.");

        NodeLifecycleJournal.Record(NodeJournalEvent.GateEntered, detail: name);

        var records = new List<NodeJournalRecord>();
        long offset = 0;
        while (true)
        {
            int start = records.Count;
            NodeLifecycleJournalReader.ReadNew(journal, ref offset, records);
            for (int i = start; i < records.Count; i++)
            {
                if (IsRelease(records[i], name))
                {
                    return;
                }
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }
    }

    internal static bool IsRelease(NodeJournalRecord record, string name)
        => record.Event == NodeJournalEvent.GateReleased && string.Equals(record.Detail, name, StringComparison.Ordinal);
}
