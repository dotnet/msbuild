# NodeScenario tests

End-to-end tests of node behaviour (worker nodes, TaskHosts and sidecars, the MSBuild server, node reuse) are hard to
make reliable. When a test can only observe operating-system side effects, it ends up waiting on the wall clock, asserting
exact process ids or worker counts, killing processes at racy moments, and leaking nodes into later tests. The
**NodeScenario** system replaces those side effects with a record of what the nodes actually decided.

Part of [#15113](https://github.com/dotnet/msbuild/issues/15113).

## The node lifecycle journal (product side)

`Microsoft.Build.Framework.NodeLifecycleJournal` (internal) records the lifecycle decisions of every MSBuild process
into one shared, append-only file:

* **Opt-in.** Recording is on only when the `MSBUILDNODEJOURNAL` environment variable is set when the process starts
  (`Traits.NodeJournalEnabled`, a `static readonly bool`). When it is off, each call site costs a single branch that the
  JIT folds away. Nothing is allocated and nothing is initialized. The journal decides whether to record, so product code
  never checks `IsEnabled`. It passes values it already has. Enum details go through the generic `Record` overload, and
  launches go through `RecordLaunched(nodeId, pid, commandLineArgs)`, so formatting and command-line parsing happen only
  behind the gate.
* **Totally ordered across processes.** Writers take a named mutex derived from the file path, read and increment the
  global sequence number kept in a fixed-size header, and append one line of JSON per record. Every record has a
  unique, gap-free `seq`, so "A happened before B" is a comparison of two numbers even when A and B come from different
  processes.
* **Never fails a build.** I/O errors are swallowed. A writer that dies while holding the lock (for example because a
  fault crashed it) leaves at most one unterminated line, which readers skip.

```
#MSBuildNodeJournal v1 next=0000000000000000044
{"seq":36,"ticks":639...,"pid":3896,"role":"Main","event":"Launched","kind":"TaskHost","node":1,"subject":41704,"detail":"<salt>"}
{"seq":37,"ticks":639...,"pid":41704,"role":"TaskHost","event":"NodeStarted","kind":"None","node":0,"subject":0}
```

| Event | Recorded by | Detail |
|---|---|---|
| `NodeStarted` | a node process entering its node loop | |
| `Launched` | `NodeLauncher`, for the new process (`subject`) | handshake salt |
| `Connected` | node providers (`new`/`reused`) and nodes (`parent`) | |
| `HandshakeRejected` | node providers when probing a candidate, and nodes when a host fails | handshake status |
| `ReuseDecision` | node providers and `MSBuildClient` | `new` or `reused` |
| `ServerBusyFallback` | `MSBuildClient`, `OutOfProcServerNode` | reason or exit type |
| `BuildStarted` / `BuildEnded` | `BuildManager` (kind `InProc`), `MSBuildClient` (kind `Server`) | host name |
| `ShutdownSent` | node providers, `MSBuildClient` | `reuse`, `terminate`, or the TaskHost action |
| `DisposalBegin` / `DisposalEnd` | worker and TaskHost nodes, around disposal of build-scoped task objects | |
| `TaskHostRetired` | the TaskHost node provider | |
| `Disconnected` | the TaskHost node provider, when a node's connection ends | |
| `Exited` | node processes leaving their node loop | shutdown reason |
| `FaultInjected`, `GateEntered`, `GateReleased`, `Marker` | fault injection and test code | |

The `role` of a record is the kind of process that wrote it (`Main`, `Worker`, `TaskHost`, `Rar`, `Server`). The `kind`
is the kind of node the event is about.

### Fault points: `MSBUILDNODEFAULT`

Every journal point is also a fault point. A process started with

```
MSBUILDNODEFAULT=<Event>[@<Role>][#<Occurrence>]:Crash|Hang[;...]
```

records `FaultInjected` and then either kills itself (`Crash`: no `finally` blocks and no graceful disconnect, the same
as an external kill) or blocks forever (`Hang`). The fault triggers the `Occurrence`-th time (default 1) a process of
`Role` (default any) records `Event`. For example, `DisposalBegin@TaskHost:Crash` or `Connected@Worker#2:Hang`. Names
are case-insensitive, numbers are not accepted, and invalid entries are ignored.

Faults replace `Process.Kill` in tests: the failure happens at a named point in the node's own control flow, not
whenever the test thread happens to run.

### Chaos: `MSBUILDNODECHAOS`

`MSBUILDNODECHAOS=<int seed>` makes every journal point sleep for 0-50 ms. The delay is a pure function of the seed, the
event, the process role, and the occurrence. Run a scenario with many seeds to shake out interleavings. Rerun it with a
failing seed to reproduce the failure (as far as a delay can).

## The harness (`src/UnitTests.Shared`)

The harness types are `internal`, and visible to the Engine, CommandLine and Framework unit tests.

```csharp
[NodeScenarioTheory]
[InlineData(true)]
[InlineData(false)]
public void Example(bool reuse)
{
    using NodeScenario scenario = NodeScenario.Create(_output);
    TransientTestFile project = scenario.Environment.CreateFile("p.proj", "...");

    NodeScenario.GateHandle disposal = scenario.Gate("disposal");      // test code in a node calls NodeScenarioGate.Enter("disposal")
    NodeScenarioRun run = scenario.StartBootstrapped($"\"{project.Path}\" -nr:false");

    NodeJournalRecord entered = disposal.AwaitEntered();
    scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.BuildEnded), "build ending during disposal", after: entered);
    disposal.Release();
    run.WaitForSuccess();
}
```

### `NodeScenario.Create(output)`

It creates a sandbox for one test:

* a unique `MSBUILDNODEHANDSHAKESALT`, so the test can never connect to nodes of other tests or of the developer's
  builds, and nodes it leaves behind can never be reused by anyone;
* a private `TMP`/`TEMP`, and an `MSBUILDDEBUGPATH` with `MSBUILDDEBUGCOMM=1`, so communication traces are available
  when the test fails;
* `MSBUILDUSESERVER=0`, and the node-related settings that change what a test observes are cleared:
  `MSBUILDNOINPROCNODE`, `MSBUILDFORCEMULTITHREADED`, `MSBUILDDISABLENODEREUSE`, `MSBUILDFORCEALLTASKSOUTOFPROC`,
  `MSBUILDDISABLEFEATURESFROMVERSION`, `MSBUILDREUSETASKHOSTNODES`, `MSBUILDNODECONNECTIONTIMEOUT`, `MSBUILDNODEFAULT`,
  and others. An ambient `MSBUILDNODECHAOS` is kept and logged;
* `MSBUILDNODEJOURNAL` pointing at the scenario's journal. The test process itself also records into it. Test assemblies
  arm the journal at startup (`MSBuildTestPipelineStartup`), and `NodeLifecycleJournal.SetSinkForCurrentProcess`
  points it at each scenario in turn.

All of these are restored on `Dispose`.

### Driving

* `StartBootstrapped(args)` / `RunBootstrapped(args)` run the bootstrapped MSBuild. The process tree is killed if it
  outlives the hang ceiling.
* `CreateBuildManager(hostName)` creates an in-process `BuildManager` that the scenario shuts down and disposes at the end.
* `Fault(point, action, role, occurrence)` arms a fault for every process started afterwards.
* `Gate(name)` returns a handle. Code running in any scenario process (usually a test task, or a disposable it
  registers) calls `NodeScenarioGate.Enter(name)`, which records `GateEntered` and blocks until the test calls
  `Release()`. That records `GateReleased`. A gate holds the system still at an exact point, which is how a test
  asserts on the state *during* an operation.
* `Marker(name)` writes a free-form record, for example to split the timeline into phases.
* `AllowSurvivor(pid)` stops teardown from reporting a process that is expected to outlive the scenario. The process
  is still killed.

### Observing

* `Await(predicate, description)` / `Await(event, kind, detail, role, processId)` waits for the first matching record
  and returns it. **There is no per-call timeout.** The wait ends in one of three ways: the record shows up; the
  scenario's hang ceiling is reached; or nothing is left that could still write it (no run in progress, no in-process
  BuildManager, and no journaled process alive), in which case it fails immediately.
* `AssertOrder(first, second)` waits for both and compares their sequence numbers.
* `AssertNever(predicate, description, after)` waits until the journal has been quiet for a while (quiescence), then
  asserts that no matching record exists after `after`. This can miss a bug on a slow machine, but it cannot fail a
  correct product. Hold the system still with a gate while asserting absence.
* `Records`, `Count(predicate)` and `WaitForQuiescence()` expose the timeline directly. `NodeScenario.Is(...)` builds
  predicates.

### Hang ceiling

Each scenario has one global ceiling, 300 s by default, measured from `Create`. `MSBUILDNODESCENARIOHANGCEILING`
(seconds) overrides it, and `MSBUILDNODESCENARIOTIMESCALE` multiplies both the ceiling and the quiescence period. The
ceiling exists to turn a hang into a diagnosable failure, not to time an operation. A test never relies on reaching it.

### Teardown and leak detection

`Dispose`:

1. releases every gate, waits for every run, and shuts down and disposes the in-process build managers;
2. waits for the journal to become quiet;
3. gives every journaled process that recorded `Exited` time to terminate. Journaled processes are the processes that
   wrote a record plus every `Launched` subject, and a process is matched by pid *and* start time so that pid reuse
   cannot confuse it;
4. **kills every other journaled process that is still alive and fails the test** with the list of leaks, unless the
   test has already failed (the original failure is not masked).

Every harness failure reports the full journal timeline, the live or exited state of every journaled process, and the
tail of every communication trace and `MSBuild_*.txt` failure file in the debug directory.

### Rules

* Mark scenario tests `[NodeScenarioFact]` or `[NodeScenarioTheory]`. They add the trait `Category=NodeScenario`, so the
  tests can be selected with `-- --filter-trait "Category=NodeScenario"` and run, retried, or stressed as a group.
* Scenarios never overlap: they share machine-wide state (processes, pipes, mutexes). Test assemblies already run
  serially, and `NodeScenario.Create` throws if another scenario is still active.
* Do not wait with `Thread.Sleep`, `Task.Wait(timeout)`, `SpinWait.SpinUntil(..., timeout)` or `WaitForExit(timeout)`,
  and do not assert exact pids or node counts derived from the OS. Assert on the journal instead.
* Do not `Kill` nodes; use `Fault`.
* Stress a new scenario locally with several chaos seeds before relying on it:

  ```powershell
  foreach ($seed in 1..20) { $env:MSBUILDNODECHAOS = $seed; dotnet test --project src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.csproj -f net11.0 -- --filter-trait "Category=NodeScenario" }
  ```

## Known gaps

* **MSBuildTaskHost (the .NET 3.5 TaskHost)** does not record anything. It is compiled separately against .NET 3.5,
  doesn't reference `Microsoft.Build.Framework`, and would need its own writer, mutex and fault parser. Its launch
  (`Launched`) and its connection from the parent's side (`Connected`, `HandshakeRejected`, `ShutdownSent`,
  `Disconnected`) are still journaled by the parent, but its own `NodeStarted`, disposal and `Exited` records are
  missing. So teardown sees it only as a `Launched` subject: if it is still alive at the end it is killed and reported
  as a leak, because it could not record `Exited`. Scenarios involving it should wait for the parent's `Disconnected`
  record.
* Records are written from the process's own point of view. Nothing is recorded for a process that is killed from
  the outside before it starts, and a crash leaves no `Exited`.
