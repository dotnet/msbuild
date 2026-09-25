// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Build.UnitTests.Shared;

/// <summary>
/// An isolated sandbox for an end-to-end test of MSBuild node behaviour, observed through the node lifecycle journal
/// instead of through OS side effects and wall-clock waits.
/// </summary>
/// <remarks>
/// <para>
/// A scenario gives every process it starts a unique handshake salt (so they can never connect to nodes of other
/// tests or of the developer's builds), private temp and debug directories, a known-clean set of node-related
/// environment variables, and a shared journal file (<c>MSBUILDNODEJOURNAL</c>) in which every process records its
/// lifecycle decisions in one total order.
/// </para>
/// <para>
/// Tests wait with <see cref="Await(Func{NodeJournalRecord, bool}, string)"/>, which has no timeout of its own: the only
/// limit is one generous hang ceiling per scenario that, when hit, fails the test with the full journal timeline.
/// Ordering is asserted with <see cref="AssertOrder"/>, absence with <see cref="AssertNever"/> after the journal
/// has gone quiet, interleavings are forced with <see cref="Gate"/>, and failures are injected with <see cref="Fault"/>.
/// </para>
/// <para>
/// <see cref="Dispose"/> kills every journaled process that is still alive and did not record that it is exiting, and
/// fails the test for each such leak and for any MSBuild failure dump, so leaks never reach the next test.
/// </para>
/// <para>See documentation/wiki/NodeScenario-Tests.md.</para>
/// </remarks>
internal sealed class NodeScenario : IDisposable
{
    public const string TraitName = "Category";
    public const string TraitValue = "NodeScenario";

    /// <summary>Multiplies the hang ceiling and the quiescence period (for slow or instrumented machines).</summary>
    public const string TimeScaleEnvVarName = "MSBUILDNODESCENARIOTIMESCALE";

    /// <summary>Overrides the hang ceiling, in seconds, before scaling.</summary>
    public const string HangCeilingEnvVarName = "MSBUILDNODESCENARIOHANGCEILING";

    internal static readonly IReadOnlyCollection<KeyValuePair<string, string>> Traits = [new(TraitName, TraitValue)];

    /// <summary>
    /// Node-related settings a scenario starts without, so ambient developer or CI configuration cannot change what a
    /// test observes. <c>MSBUILDNODECHAOS</c> is intentionally not listed: it is how scenarios are stressed.
    /// </summary>
    private static readonly string[] s_clearedEnvironmentVariables =
    [
        "MSBUILDNOINPROCNODE",
        "MSBUILDFORCEMULTITHREADED",
        "MSBUILDDISABLENODEREUSE",
        "MSBUILDFORCEALLTASKSOUTOFPROC",
        "MSBUILDDISABLEFEATURESFROMVERSION",
        "MSBUILDREUSETASKHOSTNODES",
        "MSBUILDNODECONNECTIONTIMEOUT",
        "MSBUILDENSURESTDOUTFORTASKPROCESSES",
        "MSBUILDNODEWINDOW",
        "MSBUILDDEBUGONSTART",
        "MSBUILDDEBUGSCHEDULER",
        "MSBuildDebugEngine",
        "MSBUILDNODEFAULT",
        "MSBUILDNODESHUTDOWNTIMEOUT",
        "DOTNET_CLI_USE_MSBUILD_SERVER",
    ];

    private const int PollIntervalMilliseconds = 20;
    private const int DefaultHangCeilingSeconds = 300;
    private const int QuiescenceMilliseconds = 1_000;
    private const int MaxTimelineRecords = 400;
    private const int CommTraceTailLines = 40;

    private static int s_active;

    private readonly ITestOutputHelper _output;
    private readonly TestEnvironment _env;
    private readonly NodeLifecycleJournalWriter _writer;
    private readonly int _processId = EnvironmentUtilities.CurrentProcessId;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly TimeSpan _hangCeiling;
    private readonly TimeSpan _quiescence;
    private readonly bool _journalsThisProcess;

    private readonly object _lock = new();
    private readonly List<NodeJournalRecord> _records = [];
    private long _readOffset;

    private readonly List<NodeScenarioRun> _runs = [];
    private readonly List<GateHandle> _gates = [];
    private readonly List<BuildManager> _buildManagers = [];
    private readonly List<NodeFaultSpec> _faults = [];

    private int _assertionFailures;
    private bool _failureReported;
    private bool _disposed;

    private NodeScenario(ITestOutputHelper output)
    {
        _output = output;
        double scale = ReadDouble(TimeScaleEnvVarName, 1.0);
        _hangCeiling = TimeSpan.FromSeconds(ReadDouble(HangCeilingEnvVarName, DefaultHangCeilingSeconds) * scale);
        _quiescence = TimeSpan.FromMilliseconds(QuiescenceMilliseconds * scale);

        _env = TestEnvironment.Create(output, ignoreBuildErrorFiles: true);
        try
        {
            TransientTestFolder root = _env.CreateFolder();
            RootDirectory = root.Path;
            JournalPath = Path.Combine(RootDirectory, "node-journal.jsonl");
            DebugDirectory = Directory.CreateDirectory(Path.Combine(RootDirectory, "debug")).FullName;
            TempDirectory = Directory.CreateDirectory(Path.Combine(RootDirectory, "temp")).FullName;
            HandshakeSalt = Guid.NewGuid().ToString("N");

            foreach (string name in s_clearedEnvironmentVariables)
            {
                _env.SetEnvironmentVariable(name, null);
            }

            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
            _env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", HandshakeSalt);
            _env.SetEnvironmentVariable("MSBUILDDEBUGCOMM", "1");
            _env.SetEnvironmentVariable("MSBUILDDEBUGPATH", DebugDirectory);
            _env.SetEnvironmentVariable(Framework.Traits.NodeJournalEnvVarName, JournalPath);
            _env.SetTempPath(TempDirectory);

            _writer = new NodeLifecycleJournalWriter(JournalPath);
            _journalsThisProcess = NodeLifecycleJournal.SetSinkForCurrentProcess(JournalPath);

            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        }
        catch
        {
            _env.Dispose();
            throw;
        }

        ChaosSeed = System.Environment.GetEnvironmentVariable(Framework.Traits.NodeChaosEnvVarName);
        Log($"NodeScenario: journal {JournalPath}, salt {HandshakeSalt}, chaos seed {ChaosSeed ?? "<none>"}, hang ceiling {_hangCeiling.TotalSeconds:0}s.");
        Marker("ScenarioStarted");
    }

    /// <summary>
    /// Creates a scenario. Dispose it at the end of the test (<c>using NodeScenario scenario = ...</c>).
    /// </summary>
    public static NodeScenario Create(ITestOutputHelper output)
    {
        if (Interlocked.CompareExchange(ref s_active, 1, 0) != 0)
        {
            throw new InvalidOperationException("Another NodeScenario is still active. Scenarios share machine-wide node state and must not overlap; dispose the previous one and do not run scenario tests in parallel.");
        }

        try
        {
            return new NodeScenario(output);
        }
        catch
        {
            Volatile.Write(ref s_active, 0);
            throw;
        }
    }

    /// <summary>The test environment of the scenario, for creating project files and further settings.</summary>
    public TestEnvironment Environment => _env;

    public string RootDirectory { get; }

    public string JournalPath { get; }

    /// <summary>MSBUILDDEBUGPATH of every process in the scenario; holds the communication traces.</summary>
    public string DebugDirectory { get; }

    /// <summary>TMP/TEMP of every process in the scenario.</summary>
    public string TempDirectory { get; }

    public string HandshakeSalt { get; }

    /// <summary>The ambient <c>MSBUILDNODECHAOS</c> seed the scenario runs with, if any.</summary>
    public string? ChaosSeed { get; }

    public int TestProcessId => _processId;

    /// <summary>A predicate for <see cref="Await(Func{NodeJournalRecord, bool}, string)"/> and friends.</summary>
    public static Func<NodeJournalRecord, bool> Is(
        NodeJournalEvent evt,
        NodeJournalKind kind = NodeJournalKind.None,
        string? detail = null,
        NodeJournalKind role = NodeJournalKind.None,
        int processId = 0)
        => r => r.Event == evt
            && (kind == NodeJournalKind.None || r.Kind == kind)
            && (role == NodeJournalKind.None || r.Role == role)
            && (detail is null || string.Equals(r.Detail, detail, StringComparison.Ordinal))
            && (processId == 0 || r.ProcessId == processId);

    #region Driving the scenario

    /// <summary>
    /// Starts the bootstrapped MSBuild with <paramref name="arguments"/> without waiting for it. Its process tree is
    /// killed if it outlives the hang ceiling.
    /// </summary>
    public NodeScenarioRun StartBootstrapped(string arguments)
    {
        ThrowIfDisposed();
        int timeout = (int)Math.Max(1_000, (_hangCeiling - _clock.Elapsed).TotalMilliseconds);
        Marker("RunStarted", arguments);
        Task<(bool, string)> task = RunnerUtilities.ExecBootstrappedMSBuildAsync(arguments, outputHelper: _output, timeoutMilliseconds: timeout);
        var run = new NodeScenarioRun(this, arguments, task);
        lock (_lock)
        {
            _runs.Add(run);
        }

        return run;
    }

    /// <summary>
    /// Runs the bootstrapped MSBuild with <paramref name="arguments"/> to completion.
    /// </summary>
    public (bool Success, string Output) RunBootstrapped(string arguments) => StartBootstrapped(arguments).Wait();

    /// <summary>
    /// Creates a <see cref="BuildManager"/> in the test process whose node decisions are journaled too.
    /// The scenario shuts down its nodes and disposes it at the end if the test did not.
    /// </summary>
    public BuildManager CreateBuildManager(string hostName = "NodeScenario")
    {
        ThrowIfDisposed();
        if (!_journalsThisProcess)
        {
            throw new InvalidOperationException(
                "The node lifecycle journal is not armed in this test process, so an in-process BuildManager would be invisible to the scenario. " +
                "MSBuildTestPipelineStartup arms it; make sure the test assembly uses it and that nothing reads Traits before it.");
        }

        var manager = new BuildManager(hostName);
        lock (_lock)
        {
            _buildManagers.Add(manager);
        }

        return manager;
    }

    /// <summary>
    /// Makes every process started from now on fail at <paramref name="point"/>: the <paramref name="occurrence"/>-th
    /// time a process of <paramref name="role"/> (any role when <see cref="NodeJournalKind.None"/>) records it.
    /// </summary>
    /// <remarks>Only affects processes started afterwards; the test process itself is never faulted.</remarks>
    public void Fault(NodeJournalEvent point, NodeFaultAction action, NodeJournalKind role = NodeJournalKind.None, int occurrence = 1)
    {
        ThrowIfDisposed();
        var spec = new NodeFaultSpec(point, role, occurrence, action);
        NodeFaultSpec.TryParse(spec.ToString(), out _, out string? error).ShouldBeTrue(error);
        _faults.Add(spec);
        _env.SetEnvironmentVariable(Framework.Traits.NodeFaultEnvVarName, string.Join(";", _faults.Select(f => f.ToString())));
        Log($"NodeScenario: fault {spec} armed.");
    }

    /// <summary>
    /// Creates a gate that code in any scenario process blocks on with <see cref="NodeScenarioGate.Enter"/>.
    /// </summary>
    public GateHandle Gate(string name)
    {
        ThrowIfDisposed();
        var gate = new GateHandle(this, name);
        lock (_lock)
        {
            _gates.Add(gate);
        }

        return gate;
    }

    /// <summary>Writes a <see cref="NodeJournalEvent.Marker"/> record, e.g. to split a scenario into phases.</summary>
    public NodeJournalRecord Marker(string name, string? detail = null)
    {
        long sequence = _writer.Append(_processId, NodeJournalKind.Main, NodeJournalEvent.Marker, NodeJournalKind.None, 0, 0, detail is null ? name : name + ": " + detail);
        return AwaitSequence(sequence);
    }

    #endregion

    #region Observing the scenario

    /// <summary>All complete records written so far.</summary>
    public IReadOnlyList<NodeJournalRecord> Records
    {
        get
        {
            Refresh();
            lock (_lock)
            {
                return _records.ToArray();
            }
        }
    }

    /// <summary>
    /// Waits until the journal contains a record that matches <paramref name="predicate"/> and returns the first one.
    /// </summary>
    /// <remarks>
    /// There is no per-call timeout. The wait ends with a failure only when the scenario's hang ceiling is reached, or
    /// when nothing is left that could still write the record (no run in progress, no journaled process alive).
    /// </remarks>
    public NodeJournalRecord Await(Func<NodeJournalRecord, bool> predicate, string description)
    {
        ThrowIfDisposed();
        int scanned = 0;
        bool finalCheck = false;
        while (true)
        {
            Refresh();
            lock (_lock)
            {
                for (; scanned < _records.Count; scanned++)
                {
                    if (predicate(_records[scanned]))
                    {
                        return _records[scanned];
                    }
                }
            }

            if (finalCheck)
            {
                throw Fail($"Nothing that is still running can record '{description}': every run has completed and every journaled process has exited.");
            }

            if (_clock.Elapsed > _hangCeiling)
            {
                throw Fail($"Hang ceiling of {_hangCeiling.TotalSeconds:0}s reached while waiting for '{description}'.");
            }

            finalCheck = !IsAnythingRunning();
            if (!finalCheck)
            {
                Thread.Sleep(PollIntervalMilliseconds);
            }
        }
    }

    /// <summary>Waits for a record of <paramref name="evt"/> (see <see cref="Is"/> for the filters).</summary>
    public NodeJournalRecord Await(NodeJournalEvent evt, NodeJournalKind kind = NodeJournalKind.None, string? detail = null, NodeJournalKind role = NodeJournalKind.None, int processId = 0)
        => Await(Is(evt, kind, detail, role, processId), Describe(evt, kind, detail, role, processId));

    /// <summary>
    /// Waits for the first records matching <paramref name="first"/> and <paramref name="second"/> and asserts that the
    /// first one was recorded earlier.
    /// </summary>
    public (NodeJournalRecord First, NodeJournalRecord Second) AssertOrder(
        Func<NodeJournalRecord, bool> first, string firstDescription,
        Func<NodeJournalRecord, bool> second, string secondDescription)
    {
        NodeJournalRecord a = Await(first, firstDescription);
        NodeJournalRecord b = Await(second, secondDescription);
        if (a.Sequence >= b.Sequence)
        {
            throw Fail($"Expected '{firstDescription}' (#{a.Sequence}) to happen before '{secondDescription}' (#{b.Sequence}).");
        }

        return (a, b);
    }

    /// <summary>
    /// Waits until the journal has been quiet for a while and then asserts that no record after
    /// <paramref name="after"/> (or at all, when <see langword="null"/>) matches <paramref name="predicate"/>.
    /// </summary>
    /// <remarks>
    /// Quiescence cannot prove that something will never happen, only that it has not happened while every process
    /// was idle or blocked. So this can miss a bug on a slow machine, but it can never fail a correct product.
    /// Prefer holding the system still with a <see cref="Gate"/> while asserting absence.
    /// </remarks>
    public void AssertNever(Func<NodeJournalRecord, bool> predicate, string description, NodeJournalRecord? after = null)
    {
        ThrowIfDisposed();
        WaitForQuiescence();
        long from = after?.Sequence ?? 0;
        NodeJournalRecord? match;
        lock (_lock)
        {
            match = _records.FirstOrDefault(r => r.Sequence > from && predicate(r));
        }

        if (match is not null)
        {
            throw Fail($"'{description}' must not happen{(after is null ? string.Empty : $" after #{after.Sequence}")}, but #{match.Sequence} did: {match}");
        }
    }

    /// <summary>The number of records matching <paramref name="predicate"/> so far.</summary>
    public int Count(Func<NodeJournalRecord, bool> predicate)
    {
        Refresh();
        lock (_lock)
        {
            return _records.Count(predicate);
        }
    }

    /// <summary>
    /// Waits until no process has written to the journal for the quiescence period.
    /// </summary>
    public void WaitForQuiescence()
    {
        Refresh();
        int count = RecordCount();
        Stopwatch quiet = Stopwatch.StartNew();
        while (quiet.Elapsed < _quiescence)
        {
            if (_clock.Elapsed > _hangCeiling)
            {
                throw Fail($"Hang ceiling of {_hangCeiling.TotalSeconds:0}s reached while waiting for the journal to become quiet.");
            }

            Thread.Sleep(PollIntervalMilliseconds);
            Refresh();
            int now = RecordCount();
            if (now != count)
            {
                count = now;
                quiet.Restart();
            }
        }
    }

    /// <summary>
    /// Fails the test with the scenario diagnostics: the journal timeline, communication traces and live processes.
    /// </summary>
    public Exception Fail(string message)
    {
        string report = BuildReport(message);
        if (!_failureReported)
        {
            _failureReported = true;
            Log(report);
        }

        return new XunitException(report);
    }

    #endregion

    #region Teardown

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<Exception> errors = [];
        List<string> leaks = [];
        try
        {
            // Unblock anything still parked on a gate so it can finish (or be killed) instead of hanging.
            foreach (GateHandle gate in SnapshotOf(_gates))
            {
                gate.ReleaseIfNeeded();
            }

            foreach (NodeScenarioRun run in SnapshotOf(_runs))
            {
                try
                {
                    run.Task.Wait();
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }

            foreach (BuildManager manager in SnapshotOf(_buildManagers))
            {
                try
                {
                    manager.ShutdownAllNodes();
                    manager.Dispose();
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }

            try
            {
                if (_clock.Elapsed < _hangCeiling)
                {
                    WaitForQuiescence();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            // Always reap, even when the hang ceiling has already expired: that is when leaks are most likely.
            try
            {
                leaks = ReapProcesses();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            // Only after every process is gone, so no dump can still be written.
            try
            {
                leaks.AddRange(FindFailureDumps());
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
            bool testFailed = Volatile.Read(ref _assertionFailures) > 0 || _failureReported;
            string? report = null;
            if (leaks.Count > 0 || errors.Count > 0 || testFailed)
            {
                string reason = leaks.Count > 0
                    ? "The scenario left processes or failure dumps behind:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, leaks)
                    : errors.Count > 0 ? "The scenario did not shut down cleanly: " + errors[0] : "The test failed.";
                report = BuildReport(reason);
                if (!_failureReported)
                {
                    Log(report);
                }
            }

            if (_journalsThisProcess)
            {
                NodeLifecycleJournal.SetSinkForCurrentProcess(null);
            }

            _writer.Dispose();
            try
            {
                _env.Dispose();
            }
            finally
            {
                Volatile.Write(ref s_active, 0);
            }

            // Do not mask the original failure of the test body with a leak it most likely caused.
            if (report is not null && !testFailed)
            {
                throw new XunitException(report);
            }
        }
    }

    private List<string> FindFailureDumps()
    {
        List<string> dumps = [];
        foreach (string directory in new[] { DebugDirectory, TempDirectory })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "MSBuild*failure.txt", SearchOption.AllDirectories))
            {
                dumps.Add($"  failure dump {Path.GetFileName(file)}:{System.Environment.NewLine}{File.ReadAllText(file)}");
            }
        }

        return dumps;
    }

    private List<string> ReapProcesses()
    {
        List<string> leaks = [];
        foreach (JournaledProcess journaled in GetJournaledProcesses())
        {
            using Process? process = journaled.TryOpen();
            if (process is null)
            {
                continue;
            }

            // A process that recorded that it is exiting only has to finish doing so.
            if (journaled.RecordedExit)
            {
                TimeSpan remaining = _hangCeiling - _clock.Elapsed;
                if (process.WaitForExit((int)Math.Max(0, remaining.TotalMilliseconds)))
                {
                    continue;
                }
            }

            leaks.Add($"  pid {journaled.ProcessId} ({journaled.Role}){(journaled.RecordedExit ? " recorded Exited but did not terminate" : string.Empty)}");

            try
            {
                process.KillTree(timeoutMilliseconds: 5_000);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Exited in the meantime.
            }
        }

        return leaks;
    }

    #endregion

    #region Internals

    internal NodeJournalRecord ReleaseGate(string name)
    {
        long sequence = _writer.Append(_processId, NodeJournalKind.Main, NodeJournalEvent.GateReleased, NodeJournalKind.None, 0, 0, name);
        return AwaitSequence(sequence);
    }

    private NodeJournalRecord AwaitSequence(long sequence)
    {
        // Our own record is complete once Append returns; it only has to be read back.
        Refresh();
        lock (_lock)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Sequence == sequence)
                {
                    return _records[i];
                }
            }
        }

        throw new InvalidOperationException($"Record #{sequence} is missing from {JournalPath}.");
    }

    private void Refresh()
    {
        lock (_lock)
        {
            NodeLifecycleJournalReader.ReadNew(JournalPath, ref _readOffset, _records);
        }
    }

    private int RecordCount()
    {
        lock (_lock)
        {
            return _records.Count;
        }
    }

    private bool IsAnythingRunning()
    {
        lock (_lock)
        {
            if (_runs.Any(r => !r.Task.IsCompleted) || _buildManagers.Count > 0)
            {
                return true;
            }
        }

        foreach (JournaledProcess journaled in GetJournaledProcesses())
        {
            using Process? process = journaled.TryOpen();
            if (process is not null)
            {
                return true;
            }
        }

        return false;
    }

    private List<JournaledProcess> GetJournaledProcesses()
    {
        Refresh();
        var byId = new Dictionary<int, JournaledProcess>();
        lock (_lock)
        {
            foreach (NodeJournalRecord record in _records)
            {
                Note(record.ProcessId, record.Timestamp, record.Role, record.Event == NodeJournalEvent.Exited);
                if (record.Event == NodeJournalEvent.Launched && record.SubjectProcessId != 0)
                {
                    Note(record.SubjectProcessId, record.Timestamp, record.Kind, exited: false);
                }
            }
        }

        return [.. byId.Values];

        void Note(int processId, DateTime seen, NodeJournalKind role, bool exited)
        {
            if (processId == _processId || processId == 0)
            {
                return;
            }

            if (!byId.TryGetValue(processId, out JournaledProcess? journaled))
            {
                journaled = new JournaledProcess(processId, seen, role);
                byId.Add(processId, journaled);
            }

            if (role != NodeJournalKind.None && role != NodeJournalKind.Main)
            {
                journaled.Role = role;
            }

            journaled.RecordedExit |= exited;
        }
    }

    private string BuildReport(string message)
    {
        var sb = new StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine();
        sb.AppendLine($"=== Node journal {JournalPath} (test process is pid {_processId}, chaos seed {ChaosSeed ?? "<none>"}) ===");
        NodeJournalRecord[] records;
        try
        {
            Refresh();
            lock (_lock)
            {
                records = [.. _records];
            }
        }
        catch (Exception ex)
        {
            records = [];
            sb.AppendLine($"<unable to read the journal: {ex.Message}>");
        }

        int skip = Math.Max(0, records.Length - MaxTimelineRecords);
        if (skip > 0)
        {
            sb.AppendLine($"... {skip} earlier records omitted ...");
        }

        for (int i = skip; i < records.Length; i++)
        {
            sb.AppendLine(records[i].ToString());
        }

        sb.AppendLine();
        sb.AppendLine("=== Journaled processes ===");
        foreach (JournaledProcess journaled in GetJournaledProcessesSafe())
        {
            using Process? process = journaled.TryOpen();
            sb.AppendLine($"pid {journaled.ProcessId} ({journaled.Role}): {(process is null ? "exited" : "ALIVE " + process.ProcessName)}{(journaled.RecordedExit ? ", recorded Exited" : string.Empty)}");
        }

        AppendCommTraces(sb);
        return sb.ToString();
    }

    private IEnumerable<JournaledProcess> GetJournaledProcessesSafe()
    {
        try
        {
            return GetJournaledProcesses();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private void AppendCommTraces(StringBuilder sb)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(DebugDirectory, "*.txt", SearchOption.AllDirectories);
        }
        catch (Exception)
        {
            return;
        }

        foreach (string file in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            sb.AppendLine();
            sb.AppendLine($"=== {Path.GetFileName(file)} (last {CommTraceTailLines} lines) ===");
            try
            {
                string[] lines = File.ReadAllLines(file);
                foreach (string line in lines.Skip(Math.Max(0, lines.Length - CommTraceTailLines)))
                {
                    sb.AppendLine(line);
                }
            }
            catch (IOException ex)
            {
                sb.AppendLine($"<unreadable: {ex.Message}>");
            }
        }
    }

    private void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        if (e.Exception is XunitException or ShouldAssertException)
        {
            Interlocked.Increment(ref _assertionFailures);
        }
    }

    private void Log(string message)
    {
        try
        {
            _output.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            // The test has already finished reporting output.
        }
    }

    private List<T> SnapshotOf<T>(List<T> list)
    {
        lock (_lock)
        {
            return [.. list];
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NodeScenario));
        }
    }

    private static string Describe(NodeJournalEvent evt, NodeJournalKind kind, string? detail, NodeJournalKind role, int processId)
    {
        var sb = new StringBuilder(evt.ToString());
        if (kind != NodeJournalKind.None)
        {
            sb.Append(" kind=").Append(kind);
        }

        if (role != NodeJournalKind.None)
        {
            sb.Append(" role=").Append(role);
        }

        if (detail is not null)
        {
            sb.Append(" detail=").Append(detail);
        }

        if (processId != 0)
        {
            sb.Append(" pid=").Append(processId.ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static double ReadDouble(string name, double defaultValue)
        => double.TryParse(System.Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0
            ? value
            : defaultValue;

    private sealed class JournaledProcess(int processId, DateTime firstSeenUtc, NodeJournalKind role)
    {
        public int ProcessId { get; } = processId;

        public NodeJournalKind Role { get; set; } = role;

        public bool RecordedExit { get; set; }

        /// <summary>
        /// Opens the process if it is still the one the journal saw (and not a later process that reused its id).
        /// </summary>
        public Process? TryOpen()
        {
            Process? process = null;
            try
            {
                process = Process.GetProcessById(ProcessId);
                if (process.HasExited || process.StartTime.ToUniversalTime() > firstSeenUtc.AddSeconds(1))
                {
                    process.Dispose();
                    return null;
                }

                return process;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                process?.Dispose();
                return null;
            }
        }
    }

    #endregion

    /// <summary>
    /// The test side of a gate. See <see cref="NodeScenarioGate"/> for the side that blocks.
    /// </summary>
    internal sealed class GateHandle
    {
        private readonly NodeScenario _scenario;
        private int _released;

        internal GateHandle(NodeScenario scenario, string name)
        {
            _scenario = scenario;
            Name = name;
        }

        public string Name { get; }

        /// <summary>Waits until a process has entered the gate and returns that record.</summary>
        public NodeJournalRecord AwaitEntered()
            => _scenario.Await(NodeJournalEvent.GateEntered, detail: Name);

        /// <summary>Lets every process waiting on the gate, now or later, continue.</summary>
        public NodeJournalRecord Release()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                throw new InvalidOperationException($"Gate '{Name}' was already released.");
            }

            return _scenario.ReleaseGate(Name);
        }

        internal void ReleaseIfNeeded()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _scenario.ReleaseGate(Name);
            }
        }
    }
}

/// <summary>
/// A bootstrapped MSBuild invocation started by <see cref="NodeScenario.StartBootstrapped"/>.
/// </summary>
internal sealed class NodeScenarioRun
{
    private readonly NodeScenario _scenario;

    internal NodeScenarioRun(NodeScenario scenario, string arguments, Task<(bool Success, string Output)> task)
    {
        _scenario = scenario;
        Arguments = arguments;
        Task = task;
    }

    public string Arguments { get; }

    internal Task<(bool Success, string Output)> Task { get; }

    public bool IsCompleted => Task.IsCompleted;

    /// <summary>
    /// Waits for the process to exit. It is killed, and the test fails, if it outlives the scenario's hang ceiling.
    /// </summary>
    public (bool Success, string Output) Wait()
    {
        try
        {
            return Task.GetAwaiter().GetResult();
        }
        catch (TimeoutException ex)
        {
            throw _scenario.Fail($"'msbuild {Arguments}' hit the scenario hang ceiling: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for the process to exit and asserts that it succeeded.
    /// </summary>
    public string WaitForSuccess()
    {
        (bool success, string output) = Wait();
        if (!success)
        {
            throw _scenario.Fail($"'msbuild {Arguments}' failed:{System.Environment.NewLine}{output}");
        }

        return output;
    }
}
