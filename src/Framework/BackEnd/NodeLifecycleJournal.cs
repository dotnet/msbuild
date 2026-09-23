// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Build.Framework;

/// <summary>
/// A decision or state change in the life of an MSBuild node, recorded by <see cref="NodeLifecycleJournal"/>.
/// </summary>
/// <remarks>
/// Names are persisted in journal files, so rename or remove members only together with every reader.
/// </remarks>
internal enum NodeJournalEvent : byte
{
    None = 0,

    /// <summary>This process started running as a node of <see cref="NodeJournalRecord.Role"/>.</summary>
    NodeStarted,

    /// <summary>This process launched a node process (<see cref="NodeJournalRecord.SubjectProcessId"/>).</summary>
    Launched,

    /// <summary>This process completed a handshake with a node (or, for a node, with its host).</summary>
    Connected,

    /// <summary>A handshake failed. <see cref="NodeJournalRecord.Detail"/> is the reason.</summary>
    HandshakeRejected,

    /// <summary>A node was acquired. <see cref="NodeJournalRecord.Detail"/> is <c>reused</c> or <c>new</c>.</summary>
    ReuseDecision,

    /// <summary>The MSBuild server could not be used and the client falls back to an in-process build.</summary>
    ServerBusyFallback,

    /// <summary>A <c>BuildManager</c> began a build. <see cref="NodeJournalRecord.Detail"/> is its host name.</summary>
    BuildStarted,

    /// <summary>A build ended. For a <c>BuildManager</c>, <see cref="NodeJournalRecord.Detail"/> is its host name.</summary>
    BuildEnded,

    /// <summary>End-of-build (or shutdown) was sent to a node. <see cref="NodeJournalRecord.Detail"/> is the action.</summary>
    ShutdownSent,

    /// <summary>A node started disposing build-scoped registered task objects.</summary>
    DisposalBegin,

    /// <summary>A node finished disposing build-scoped registered task objects.</summary>
    DisposalEnd,

    /// <summary>A sidecar TaskHost was retired: it can no longer be acquired and nothing waits for its cleanup.</summary>
    TaskHostRetired,

    /// <summary>This process lost (or closed) its connection to a node.</summary>
    Disconnected,

    /// <summary>A node is about to exit. <see cref="NodeJournalRecord.Detail"/> is the shutdown reason.</summary>
    Exited,

    /// <summary>A fault requested through <c>MSBUILDNODEFAULT</c> is being injected.</summary>
    FaultInjected,

    /// <summary>Test code reached a named gate and waits for it to be released.</summary>
    GateEntered,

    /// <summary>A named gate was released.</summary>
    GateReleased,

    /// <summary>A free-form marker written by test code.</summary>
    Marker,
}

/// <summary>
/// The role of a process, or the kind of node an event is about.
/// </summary>
internal enum NodeJournalKind : byte
{
    None = 0,

    /// <summary>An entry-point process (MSBuild.exe, dotnet build, a test host or any other API host).</summary>
    Main,

    /// <summary>An out-of-process worker node (<c>/nodemode:1</c>).</summary>
    Worker,

    /// <summary>A TaskHost node (<c>/nodemode:2</c>).</summary>
    TaskHost,

    /// <summary>The RAR service node (<c>/nodemode:3</c>).</summary>
    Rar,

    /// <summary>The MSBuild server node (<c>/nodemode:8</c>).</summary>
    Server,

    /// <summary>The in-process node of a <c>BuildManager</c>.</summary>
    InProc,
}

/// <summary>
/// Test-only diagnostics that record the decisions MSBuild's node system makes (launch, handshake, reuse,
/// end-of-build, disposal, exit, ...) into a single file shared by every process of a scenario.
/// </summary>
/// <remarks>
/// <para>
/// Turned on by setting <c>MSBUILDNODEJOURNAL</c> to an absolute file path before the process starts. Records from
/// all processes are appended under a cross-process lock and carry a global, gap-free sequence number, so the file
/// gives a total order of what happened across processes. Tests assert on that order instead of on wall-clock time.
/// </para>
/// <para>
/// Every <see cref="Record"/> call is also a fault point (<c>MSBUILDNODEFAULT=Event[@Role][#Occurrence]:Crash|Hang</c>)
/// and a chaos point (<c>MSBUILDNODECHAOS=seed</c> adds a reproducible random delay).
/// </para>
/// <para>
/// When the environment variable is not set, <see cref="Record"/> is a single branch on a <see langword="static readonly"/>
/// field, which the JIT folds away, and it allocates nothing. Callers must not build arguments (for example by string
/// interpolation) outside an <see cref="IsEnabled"/> check.
/// </para>
/// </remarks>
internal static class NodeLifecycleJournal
{
    /// <summary>
    /// Value of <c>MSBUILDNODEJOURNAL</c> that turns journaling on without a target file, for a process (a test host)
    /// that later points the journal at a file with <see cref="SetSinkForCurrentProcess"/>.
    /// </summary>
    internal const string ArmedWithoutSinkValue = "*";

    /// <summary>
    /// Upper bound of the delay <c>MSBUILDNODECHAOS</c> adds at a journal point.
    /// </summary>
    internal const int MaxChaosDelayMilliseconds = 50;

    /// <summary>
    /// Whether journaling is on in this process.
    /// </summary>
    public static bool IsEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Traits.NodeJournalEnabled;
    }

    /// <summary>
    /// Records <paramref name="evt"/>, then applies chaos delays and injects faults configured for this point.
    /// Never throws for I/O problems: journaling must not change the outcome of a build.
    /// </summary>
    /// <param name="evt">What happened.</param>
    /// <param name="kind">The kind of node the event is about, if any.</param>
    /// <param name="nodeId">The node id the event is about, if any.</param>
    /// <param name="subjectProcessId">The process the event is about, if it is not the current process.</param>
    /// <param name="detail">A short reason or value. Must already exist; do not format it unless <see cref="IsEnabled"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Record(
        NodeJournalEvent evt,
        NodeJournalKind kind = NodeJournalKind.None,
        int nodeId = 0,
        int subjectProcessId = 0,
        string? detail = null)
    {
        if (Traits.NodeJournalEnabled)
        {
            RecordCore(evt, kind, nodeId, subjectProcessId, detail);
        }
    }

    /// <summary>
    /// Declares the role this process plays. Node entry points call this before their first record.
    /// </summary>
    public static void SetProcessRole(NodeJournalKind role)
    {
        if (Traits.NodeJournalEnabled)
        {
            JournalState.Role = role;
        }
    }

    /// <summary>
    /// Maps a node mode to the journal kind of that node.
    /// </summary>
    public static NodeJournalKind KindOf(NodeMode? nodeMode) => nodeMode switch
    {
        NodeMode.OutOfProcNode => NodeJournalKind.Worker,
        NodeMode.OutOfProcTaskHostNode => NodeJournalKind.TaskHost,
        NodeMode.OutOfProcRarNode => NodeJournalKind.Rar,
        NodeMode.OutOfProcServerNode => NodeJournalKind.Server,
        _ => NodeJournalKind.None,
    };

    /// <summary>
    /// Points the journal of this process at <paramref name="path"/>, or detaches it when <see langword="null"/>.
    /// Only has an effect when journaling was turned on at process start; returns whether it was.
    /// </summary>
    /// <remarks>
    /// For test hosts, which start once and then run many isolated scenarios. Child processes are unaffected: they
    /// read <c>MSBUILDNODEJOURNAL</c> from the environment they are started with.
    /// </remarks>
    internal static bool SetSinkForCurrentProcess(string? path)
    {
        if (!Traits.NodeJournalEnabled)
        {
            return false;
        }

        JournalState.SetSink(path);
        return true;
    }

    /// <summary>
    /// The file this process currently records into, or <see langword="null"/>.
    /// </summary>
    internal static string? CurrentSink => Traits.NodeJournalEnabled ? JournalState.Sink : null;

    /// <summary>
    /// The role this process declared with <see cref="SetProcessRole"/>.
    /// </summary>
    internal static NodeJournalKind CurrentRole => Traits.NodeJournalEnabled ? JournalState.Role : NodeJournalKind.None;

    /// <summary>
    /// A reproducible delay for the <paramref name="occurrence"/>-th time this process passes <paramref name="evt"/>.
    /// </summary>
    internal static int ComputeChaosDelay(int seed, NodeJournalEvent evt, NodeJournalKind role, int occurrence)
    {
        ulong hash = NodeLifecycleJournalWriter.FnvOffsetBasis;
        hash = NodeLifecycleJournalWriter.Fnv(hash, unchecked((uint)seed));
        hash = NodeLifecycleJournalWriter.Fnv(hash, (byte)evt);
        hash = NodeLifecycleJournalWriter.Fnv(hash, (byte)role);
        hash = NodeLifecycleJournalWriter.Fnv(hash, unchecked((uint)occurrence));
        return (int)(hash % (MaxChaosDelayMilliseconds + 1));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RecordCore(NodeJournalEvent evt, NodeJournalKind kind, int nodeId, int subjectProcessId, string? detail)
        => JournalState.Record(evt, kind, nodeId, subjectProcessId, detail);

    /// <summary>
    /// All mutable journal state. Kept out of <see cref="NodeLifecycleJournal"/> so that nothing here, including the
    /// fault and chaos configuration, is initialized unless journaling is on and something is recorded.
    /// </summary>
    private static class JournalState
    {
        private static readonly object s_writerLock = new();
        private static readonly int s_processId = EnvironmentUtilities.CurrentProcessId;
        private static readonly NodeFaultSpec[] s_faults = NodeFaultSpec.ParseList(Environment.GetEnvironmentVariable(Traits.NodeFaultEnvVarName));
        private static readonly int? s_chaosSeed = ParseChaosSeed(Environment.GetEnvironmentVariable(Traits.NodeChaosEnvVarName));
        private static readonly int[] s_occurrences = new int[256];

        private static string? s_sink = Traits.NodeJournalPath == ArmedWithoutSinkValue ? null : Traits.NodeJournalPath;
        private static NodeLifecycleJournalWriter? s_writer;

        internal static NodeJournalKind Role { get; set; } = NodeJournalKind.Main;

        internal static string? Sink => Volatile.Read(ref s_sink);

        internal static void SetSink(string? path)
        {
            lock (s_writerLock)
            {
                Volatile.Write(ref s_sink, path);
                s_writer?.Dispose();
                s_writer = null;
            }
        }

        internal static void Record(NodeJournalEvent evt, NodeJournalKind kind, int nodeId, int subjectProcessId, string? detail)
        {
            if (Sink is null)
            {
                return;
            }

            NodeJournalKind role = Role;
            TryAppend(role, evt, kind, nodeId, subjectProcessId, detail);

            int occurrence = Interlocked.Increment(ref s_occurrences[(byte)evt]);

            if (s_chaosSeed is int seed)
            {
                int delay = ComputeChaosDelay(seed, evt, role, occurrence);
                if (delay > 0)
                {
                    Thread.Sleep(delay);
                }
            }

            foreach (NodeFaultSpec fault in s_faults)
            {
                if (fault.Matches(evt, role, occurrence))
                {
                    Inject(fault, role);
                }
            }
        }

        private static void TryAppend(NodeJournalKind role, NodeJournalEvent evt, NodeJournalKind kind, int nodeId, int subjectProcessId, string? detail)
        {
            try
            {
                NodeLifecycleJournalWriter? writer = GetWriter();
                writer?.Append(s_processId, role, evt, kind, nodeId, subjectProcessId, detail);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Journaling is diagnostics only; never let it fail a build.
            }
        }

        private static NodeLifecycleJournalWriter? GetWriter()
        {
            lock (s_writerLock)
            {
                string? sink = s_sink;
                if (sink is null)
                {
                    return null;
                }

                return s_writer ??= new NodeLifecycleJournalWriter(sink);
            }
        }

        private static void Inject(NodeFaultSpec fault, NodeJournalKind role)
        {
            TryAppend(role, NodeJournalEvent.FaultInjected, NodeJournalKind.None, 0, 0, fault.ToString());

            switch (fault.Action)
            {
                case NodeFaultAction.Crash:
                    // The closest equivalent of an external kill: no finally blocks, no graceful disconnect.
                    Process.GetCurrentProcess().Kill();

                    // Kill is asynchronous; make sure this thread does not run on meanwhile.
                    Thread.Sleep(Timeout.Infinite);
                    break;
                case NodeFaultAction.Hang:
                    Thread.Sleep(Timeout.Infinite);
                    break;
            }
        }

        private static int? ParseChaosSeed(string? value)
            => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seed) ? seed : null;
    }
}
