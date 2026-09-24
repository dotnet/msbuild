# Task progress reporting

**Status:** Proposed

## Summary

Long-running MSBuild tasks need a way to report progress without taking ownership of the console.
This specification defines a semantic progress protocol in which:

- tasks report the state of an operation,
- MSBuild transports, correlates, and rate-limits that state, and
- loggers decide whether and how to present it.

The task-facing API uses the BCL `IProgress<T>` abstraction for updates, wrapped by an
MSBuild-owned lifecycle interface. The protocol does not expose cursor movement, ANSI escape
sequences, terminal rows, or rendering locks to tasks.

The first implementation should establish the task-facing contract and operation lifecycle in a
single process. Distributed transport, Terminal Logger rendering, binary-log persistence, and
first-party task adoption should follow as separate vertical slices.

## Motivation

[dotnet/msbuild#6944](https://github.com/dotnet/msbuild/issues/6944) requests a way for tasks such
as NuGet restore to show progress and control cursor placement. A task cannot safely implement
that behavior by combining `TaskLoggingHelper` with direct console access:

- logging is asynchronous,
- messages from parallel projects and tasks are interleaved,
- events can cross process and node boundaries,
- a logger may buffer or redirect output,
- the active logger might not own a terminal, and
- the Terminal Logger already maintains a shared, dynamically rendered region.

This is an ownership problem, not only a missing cursor API. A task knows what work it has
completed, but only the logger knows whether the output is interactive and how all concurrent
build output must be composed.

Progress also differs from ordinary build logging:

- it is transient and frequently updated,
- intermediate values may be dropped without losing meaning,
- it does not determine task success,
- it should not produce an unbounded binary log, and
- it usually has no useful representation in a redirected text log.

MSBuild therefore needs a distinct semantic channel rather than a convention built from ordinary
messages.

## Goals

- Give tasks a small, discoverable API for determinate and indeterminate progress.
- Support one or more concurrent operations from a task.
- Correlate progress with the current task and its `BuildEventContext`.
- Keep `Report` nonblocking and safe to call from multiple threads.
- Permit aggressive coalescing before node IPC.
- Guarantee a terminal lifecycle transition for every accepted operation.
- Let Terminal Logger render progress without corrupting warnings, errors, prompts, or other
  immediate output.
- Preserve useful, bounded diagnostic evidence in binary logs.
- Degrade to a no-op when a host or logger does not support progress.
- Preserve existing behavior when no task reports progress.

## Non-goals

- Giving tasks direct cursor or terminal control.
- Providing arbitrary ANSI or host-console passthrough. Verbatim host output is a separate
  problem tracked by [dotnet/msbuild#15026](https://github.com/dotnet/msbuild/issues/15026).
- Replacing errors, warnings, messages, telemetry, or task return values.
- Defining a durable, lossless event stream for every intermediate update.
- Inferring progress from arbitrary child-process output.
- Combining unrelated operations into a mathematically meaningful build-wide percentage.
- Making task behavior depend on whether visual progress is available.

## Terminology

**Operation**
: One task-defined unit of long-running work, such as downloading one file or extracting one
  archive.

**Update**
: The latest known state of an operation. Updates are snapshots, not deltas.

**Reporter**
: The lifecycle-aware object a task uses to publish updates and finish an operation.

**Sink**
: The engine-side receiver that validates, coalesces, transports, and logs progress.

**Renderer**
: A logger that converts semantic progress into terminal or textual output.

## Ownership model

The ownership boundary is:

> Tasks report state; MSBuild transports and arbitrates it; loggers decide how to present it.

A task owns:

- the operation title,
- the current status text,
- completed and total work,
- the unit of work, and
- the terminal outcome it reports.

MSBuild owns:

- operation identity,
- task/build correlation,
- sequence numbers,
- lifecycle enforcement,
- validation and text bounds,
- coalescing and IPC,
- abandonment when execution ends unexpectedly, and
- routing to loggers.

A logger owns:

- whether progress is shown,
- terminal layout and cursor movement,
- update cadence at render time,
- width-aware truncation,
- aggregation or selection among concurrent operations,
- OSC integration, and
- noninteractive fallback behavior.

## Task-facing API

The proposed public surface is attached to `EngineServices`, which is available through
`IBuildEngine10`. `EngineServices` was designed to accept future task capabilities without adding
another mandatory member to `IBuildEngine`.

Conceptually:

```csharp
namespace Microsoft.Build.Framework;

public abstract class EngineServices
{
    public virtual ITaskProgressReporter CreateTaskProgressReporter(
        string title,
        TaskProgressUnit unit = TaskProgressUnit.Unspecified);
}

public interface ITaskProgressReporter :
    IProgress<TaskProgressUpdate>,
    IDisposable
{
    void Complete(string? summary = null);
    void Cancel(string? summary = null);
    void Fail(string? summary = null);
}

public readonly struct TaskProgressUpdate
{
    public TaskProgressUpdate(
        long completed,
        long? total = null,
        string? status = null);

    public long Completed { get; }
    public long? Total { get; }
    public string? Status { get; }
}

public enum TaskProgressUnit
{
    Unspecified,
    Items,
    Bytes,
}
```

The exact names and constructor shapes require API review. The important constraints are:

- updates use `IProgress<TaskProgressUpdate>`,
- operation creation and terminal transitions use MSBuild-owned APIs,
- the reporter is disposable,
- the operation title and unit are stable for the reporter lifetime, and
- unsupported hosts return a no-op reporter rather than forcing tasks to branch.

### Why `IProgress<T>`

`IProgress<T>` is the appropriate update abstraction because it:

- is in the BCL,
- is familiar to task and library authors,
- allows existing libraries to accept an MSBuild reporter,
- has a single contravariant `Report` method, and
- does not prescribe transport or rendering.

`IProgress<T>` is not a complete operation protocol. It has no begin, completion, cancellation,
failure, abandonment, or disposal semantics. The MSBuild-owned reporter adds those semantics.

MSBuild must not implement reporters by deriving from or wrapping `System.Progress<T>` in the hot
path. `System.Progress<T>` captures the current `SynchronizationContext` and can dispatch
asynchronously. A task report must instead be accepted synchronously into an inexpensive,
thread-safe state holder; logger and terminal code must never execute inline from `Report`.

### .NET Framework compatibility

No `IProgress<T>` polyfill is required. MSBuild's .NET Framework target is .NET Framework 4.7.2,
and `IProgress<T>` has been available since .NET Framework 4.5.

Defining another `System.IProgress<T>` for older frameworks is not viable. Type identity includes
the defining assembly, so a polyfilled type would not be assignment-compatible with the BCL type
and could cause ambiguous-type conflicts. If support for a pre-4.5 runtime were ever required,
MSBuild would need its own differently named interface rather than a `System` namespace polyfill.

## Data model

### Stable operation data

The engine assigns the following data when a reporter is created:

- `OperationId`: unique within the build,
- `BuildEventContext`: the current submission, project, target, task, and node correlation,
- `Title`: a short stable description,
- `Unit`: the meaning of `Completed` and `Total`,
- creation timestamp, and
- initial sequence number.

Tasks do not choose operation IDs. This avoids collisions and prevents a task from impersonating
another task's operation.

### Update data

Each accepted update contains:

- `Completed`: absolute work completed, not a delta,
- `Total`: optional current total,
- `Status`: optional transient description, and
- an engine-generated monotonically increasing sequence number.

Absolute values make updates idempotent and allow intermediate updates to be discarded. A
sequence number lets receivers reject stale packets without interpreting task values.

`Total = null` means indeterminate progress. A task may begin without a total and later supply one.
A total may change when work is discovered dynamically. Renderers derive percentage, transfer
rate, and estimated time; tasks should not report a floating-point percentage.

Initial units are:

- `Unspecified`,
- `Items`, and
- `Bytes`.

Units are intentionally closed in the first API. A later revision can add units when a concrete
renderer or task requires one.

### Validation

The initial implementation should apply these rules:

- `title` must not be null, empty, or whitespace,
- `completed` and `total`, when present, must be nonnegative,
- `completed` may exceed `total` because totals can be estimates,
- updates after a terminal transition are ignored,
- duplicate terminal calls are ignored,
- control characters and terminal escape sequences are sanitized before rendering, and
- excessively long text is truncated at the engine or renderer boundary.

Decreasing `Completed` values are accepted in the first version. Work can be retried or
re-estimated, and rejecting the value would make producer bugs affect build behavior. Renderers
may display the latest value without animation. The engine should collect diagnostic telemetry
for invalid or suspicious producer behavior rather than issue build warnings.

Suggested implementation bounds are 256 UTF-16 code units for `Title` and 1,024 for `Status` or a
terminal summary. These are defensive implementation limits, not API guarantees, unless partner
feedback demonstrates a need to document fixed limits.

## Lifecycle

The logical protocol is:

1. `BeginProgress`
2. zero or more `ReportProgress`
3. `EndProgress`

`EndProgress` has one of four outcomes:

- `Completed`: the operation finished normally,
- `Canceled`: the operation was canceled,
- `Failed`: the operation did not finish successfully,
- `Abandoned`: the reporter was disposed or task execution ended without an explicit outcome.

The outcome is presentation and diagnostic data. `Failed` does not fail the task, and `Completed`
does not make it succeed. Task return values and logged errors remain authoritative.

Reporter requirements:

- creation emits begin before any update,
- `Report` is thread-safe and nonblocking,
- concurrent reports use latest-accepted-state semantics,
- a terminal call atomically closes the reporter,
- the terminal call flushes the latest accepted update before end,
- reports after closure are ignored,
- disposal without a terminal call emits `Abandoned`,
- disposal after a terminal call is a no-op, and
- task teardown abandons any reporter the task leaked.

The reporter does not guarantee that every `Report` invocation is observed by a logger.

## Capability discovery and compatibility

`EngineServices` should gain a new version constant and increment its default `Version`. The new
virtual member must have a working no-op base implementation rather than throw
`NotImplementedException`; otherwise a task that probes the service can fail under an older or
third-party host implementation.

Tasks should use the capability as follows:

```csharp
ITaskProgressReporter progress =
    (BuildEngine as IBuildEngine10)?.EngineServices.CreateTaskProgressReporter(
        "Downloading package",
        TaskProgressUnit.Bytes)
    ?? TaskProgressReporter.None;
```

API review should decide whether a public singleton no-op reporter is necessary or whether the
base `EngineServices` implementation alone is sufficient. Built-in tasks must never change their
work, result, diagnostics, retry behavior, or cancellation behavior based on capability support.

This additive API does not by itself require a Change Wave. A Change Wave or explicit opt-out is
required if implementation changes existing message classification, text output, ordering, or
binary-log behavior in a way users can observe when no task reports progress.

## Engine implementation

The initial engine implementation should have these layers:

1. `TaskProgressReporter` implements the public lifecycle and stores the latest accepted state.
2. A task-scoped progress manager owns all reporters created during one task invocation.
3. The manager creates engine operation IDs and sequence numbers.
4. The manager forwards begin, selected updates, and end to the logging service.
5. Task teardown abandons remaining reporters.

The task-scoped manager must be connected to the existing `TaskHost` and
`TaskLoggingContext`, so callers cannot provide an arbitrary `BuildEventContext`.

### Threading

`Report` can be called by worker threads used inside a task. The implementation must not assume
thread affinity or a `SynchronizationContext`.

The hot path should:

- avoid LINQ,
- avoid allocating when an update is coalesced,
- use atomic state or a small lock scoped to one reporter,
- avoid taking Terminal Logger or global logging locks, and
- avoid synchronous IPC.

Terminal transitions are rare and may perform more work than updates, but must not deadlock with
task completion or cancellation.

## Coalescing and transport

Progress is a lossy latest-value-wins channel. Throttling only in Terminal Logger is insufficient:
unbounded reports would still allocate events, cross AppDomain or process boundaries, consume IPC,
and inflate centralized queues.

The proposed policy has two stages:

### Node-local coalescing

- Keep only the latest update for each operation.
- Forward no more than approximately 5-10 updates per second per active operation initially.
- Let begin and end bypass throttling.
- Flush the latest pending update immediately before end.
- Do not block the reporting task while waiting for the forwarding interval.

The exact rate is an implementation detail and can adapt to load.

### Central render coalescing

- Merge updates arriving faster than the terminal refresh cadence.
- Render all changes under Terminal Logger's existing synchronization.
- Bound the number of visible operations.
- Prefer recently updated or longest-running operations when not all fit.

### Node protocol

Progress generated on worker nodes must reach the central logging service. The final packet/event
shape must support:

- operation ID,
- sequence,
- lifecycle kind,
- outcome for end,
- task `BuildEventContext`,
- title and unit on begin,
- completed, total, and status on update,
- final summary on end, and
- protocol-version negotiation or graceful fallback for mixed-version nodes.

Unknown progress packets must not terminate a node connection. Until the node compatibility
strategy is proven, the distributed slice must remain separate from the public API slice.

## Logger behavior

### Terminal Logger

Terminal Logger is the primary renderer. It should own a bounded progress region integrated with
its existing node-status display.

Required behavior:

- all erase, write, and repaint operations use existing Terminal Logger synchronization,
- warnings, errors, prompts, and immediate messages temporarily clear and then repaint progress,
- terminal resizing and very narrow widths do not corrupt output,
- titles and status are sanitized and width-truncated,
- counts and unit names are localized, and match the wording used by the classic console logger,
- indeterminate operations use a nonnumeric activity representation,
- completed operations disappear promptly or remain briefly according to renderer policy,
- failed or canceled progress does not duplicate authoritative diagnostics, and
- redirected output receives no cursor-control sequences.

The renderer should not average unrelated operation percentages. "Three downloads at 50%" does
not imply that the build is 50% complete.

#### Placement

Each operation is drawn on its own line, indented below the node row for the project that
reported it. A wide build therefore keeps each operation next to the work it belongs to, instead
of collecting all operations in one block that the reader must match back to a project.

An operation whose project is no longer displayed is drawn after the node rows. The renderer
keeps such an operation rather than dropping it, so an outcome is never lost.

#### Frame updates

A frame is written as one terminal operation, between the codes that start and end a
synchronized update. A terminal that does not support those codes ignores them.

The renderer erases and repaints the whole block only when an operation starts or ends, because
those are the moments when a line changes its position. Every other frame keeps its lines and
writes only the text that is different, which keeps a frequently updated build free of flicker.
A progress line is erased to the end of the line before it is rewritten, because the text of an
operation shrinks as often as it grows.

### Windows Terminal progress

Windows Terminal supports OSC 9;4 taskbar/tab progress. This is a presentation enhancement, not
part of the task protocol.

The Terminal Logger may:

- publish determinate state when one meaningful aggregate or operation is selected,
- publish indeterminate state when unrelated concurrent operations are active,
- publish an error state only when consistent with authoritative build status, and
- clear the terminal-level indicator when the last operation ends.

The logger must capability-detect this feature and never expose OSC sequences through task APIs.

### Classic console and file loggers

The classic console and file loggers collapse an operation into a single message, written when the
operation ends. Intermediate updates produce no output at all, because the classic loggers write a
scrolling transcript and cannot revise a line that has already been written.

The rendered line reads `{Title} completed: {amount}`, for example
`Downloading README.md completed: 5,313 of 5,313 bytes`. The outcome word changes for cancelled,
failed, and abandoned operations, and the amount is omitted when the task reported no counts. The
title and unit arrive on the started event, so the logger keeps a small table of in-flight
operations keyed by operation id; if the started event is missing, the line falls back to the
sender name. The amount text is shared with Terminal Logger, so the two loggers name units
identically and translators see one set of strings.

The synthesized message is normal importance, so it appears at normal verbosity and above and is
suppressed at minimal and quiet. Because it is a new line rather than a changed one, log parsers
that match on existing text are unaffected.

The lifecycle events themselves are low importance, which means a forwarding logger would normally
drop them at anything below detailed verbosity and the central logger would never see them in a
multi-process build. `ConfigurableForwardingLogger` therefore forwards the started and finished
events whenever it forwards normal-importance messages. Updates keep the ordinary importance rules,
since the classic loggers ignore them and only a custom central logger would want them.

Third-party loggers should receive structured lifecycle events when the public logger event model
is defined. They must not need to parse Terminal Logger output.

## Binary logs

Binary logs are MSBuild's durable diagnostic record, but persisting every intermediate report
would conflict with coalescing and could make logs unbounded.

The intended policy is:

- preserve operation begin and end, or one equivalent bounded summary,
- preserve final outcome and duration,
- preserve the latest known completed, total, unit, and status/summary,
- optionally preserve a bounded number of sampled updates for diagnostics,
- never serialize ANSI sequences or cursor commands, and
- do not encode progress as `BuildMessageEventArgs`.

Before adding event records:

- define explicit `BuildEventArgs` types or another structured logging envelope,
- trace them through forwarding loggers and task-host forwarding,
- increment the binary-log version when required,
- prove how older readers skip or gracefully degrade on the new record type,
- make newer readers handle older logs,
- add serialization round-trip tests, and
- add a size-regression test for high-frequency reporting.

The first task-facing API slice should not write progress to binary logs until this compatibility
work is complete. During that slice, an internal test sink can validate the lifecycle without
claiming durable logging support.

## Security and robustness

Progress text is untrusted task output.

- Strip or escape control characters before terminal rendering.
- Reject embedded ANSI, OSC, and other terminal control sequences.
- Bound retained and transmitted text.
- Bound active operations per task and per build.
- Do not allow tasks to supply operation IDs, node IDs, or build contexts.
- Do not allow progress to reserve arbitrary terminal rows.
- Ensure a malicious reporter cannot bypass throttling with many short-lived operations.
- Ensure reporter failure never fails the build or hides an authoritative diagnostic.

Resource limits should use silent degradation plus internal diagnostic telemetry, not new build
warnings.

## First-party adoption

Adoption should begin only after lifecycle, coalescing, and at least one renderer or test sink work
end to end.

### 1. `DownloadFile`

`DownloadFile` is the best first adopter:

- it already uses `HttpCompletionOption.ResponseHeadersRead`,
- it can use `Content-Length` when available,
- bytes transferred are meaningful even when length is unknown,
- it already supports cancellation and retries, and
- users understand download progress.

Adoption requires replacing `Stream.CopyToAsync` with a buffered loop or counting stream.
Reporting must not allocate per buffer, must preserve cancellation and retry behavior, and must
not materially reduce throughput.

Each retry should either update one operation's status or cleanly end and start an attempt
operation. The preferred initial behavior is one logical operation whose completed count resets
when the transfer restarts; decreasing values are therefore permitted.

### 2. Archive tasks

`Unzip` can report archive entries processed out of `ZipArchive.Entries.Count`.

`Untar` should normally report indeterminate item progress. Pre-scanning a compressed archive only
to compute a total would duplicate decompression and I/O.

`TarDirectory` should report an indeterminate count unless its existing work already provides a
total. It must not enumerate a directory tree twice only to obtain a denominator.

### 3. `Copy` and `Move`

These tasks have complete item vectors and can report items completed out of total items. They are
high-volume and may use parallel workers, so they should adopt only after reporter coalescing has
been profiled. The task should use one atomic completed counter and avoid per-item formatting.

### 4. `Exec`

`Exec` cannot infer semantic progress from arbitrary standard output. A later adapter could offer:

- a user-supplied progress regular expression with documented named groups, or
- a documented child-process progress protocol.

A Boolean regex match is insufficient; the adapter needs fields such as completed, total, unit,
and status. Matched progress records must have clear rules for whether they are also logged as
ordinary output.

### 5. Lower-priority candidates

`GenerateResource` has known input counts and could report file-level progress.

`ResolveAssemblyReference` could report coarse phases, but it is performance-sensitive and its
work graph is dynamic. It should not report per-reference updates without evidence that the hot
path cost is negligible.

### Tasks that should not adopt

`MSBuild` and `CallTarget` should not duplicate engine project/target events as task progress.
Compiler wrappers should use a deliberate child-process protocol rather than guessed percentages.
Short metadata and property manipulation tasks have no useful long-running progress to report.

## Rollout plan

### Phase 1: task contract and lifecycle

- Add progress data types and reporter interface to `Microsoft.Build.Framework`.
- Add the `EngineServices` factory method and version.
- Implement a no-op base reporter for unsupported hosts.
- Implement a task-scoped in-process reporter and test sink.
- Test validation, concurrent reports, completion races, abandonment, and post-completion reports.
- Do not change default logger output.

### Phase 2: structured engine events

- Define begin, update, and end logging records.
- Connect the task-scoped manager to the logging service.
- Add forwarding within one process.
- Add logger-facing tests.

### Phase 3: distributed transport

- Add node packet/event serialization and compatibility handling.
- Coalesce before IPC.
- Test in-proc, `/m`, AppDomain task isolation, and out-of-proc task hosts.

  `/m` worker nodes and the out-of-process task host (`TaskHostFactory`, non-CLR2) both forward
  begin/update/end records through the existing `LogMessagePacket` channel, so the node
  packet/event serialization work from Phase 2 already covers both. `TaskProgressManager` and
  `TaskProgressReporter` live in `src/Shared` (rather than `Microsoft.Build`) and accept an
  `Action<BuildEventArgs>?` forwarding delegate instead of `ILoggingService` directly, so the same
  lifecycle state machine is shared, unmodified, between the in-process `TaskHost` (forwards
  through `ILoggingService.LogBuildEvent`) and `OutOfProcTaskHostNode` (forwards through
  `SendBuildEvent`, scoped to a per-task-execution `TaskProgressManager` on
  `TaskExecutionContext` so a leaked reporter is abandoned when the task's context is disposed).
  The legacy CLR2/.NET Framework 3.5 task host (`MSBuildTaskHost.exe`) does not implement
  `CreateTaskProgressReporter`, so it inherits the base no-op reporter from `EngineServices` and
  its restricted, separate `LogMessagePacketBase.cs` copy is intentionally left unmodified.

  `TaskProgressReporting_E2E_Tests` builds a real project, through `BuildManager`, with a task
  that creates two operations and reports on both, once in-process and once through an explicitly
  requested `TaskHostFactory` (a genuine separate `MSBuild.exe` process). This exercised the full
  round trip for the first time (prior tests only covered the reporter/manager state machine and
  packet serialization in isolation) and caught a real bug: `TaskProgressStartedEventArgs`,
  `TaskProgressUpdatedEventArgs`, and `TaskProgressFinishedEventArgs` were missing the
  `[Serializable]` attribute that `OutOfProcTaskHostNode.SendBuildEvent`'s `Type.IsSerializable`
  gate requires (matching the existing pattern of e.g. `CriticalBuildMessageEventArgs`); the
  attribute is not implicitly inherited from `BuildMessageEventArgs`, so every progress event a
  task host tried to forward across the node connection was silently dropped, replaced by an
  `ExpectedEventToBeSerializable` warning. All three classes now carry `[Serializable]`.

  The same fixture now forces a real `/m` worker-node build (`DisableInProcNode=true` and
  `MaxNodeCount=2`) and verifies that the forwarded event contexts identify a worker node rather
  than the in-process node. A framework-only variant runs the task in a separate AppDomain.
  That test found two additional remoting requirements: reporter creation must execute on the
  owning `TaskHost` rather than dereferencing its private state through a transparent proxy, and
  the `TaskProgressUpdate` value passed through `IProgress<TaskProgressUpdate>` must itself be
  serializable. `TaskHost.CreateTaskProgressReporter` and `[Serializable]` on
  `TaskProgressUpdate` address those boundaries.

  `ForwardingTerminalLogger` forwards the three progress event types independently of normal
  message-importance filtering. Progress events are low-importance structured records, so
  applying the ordinary message filter would otherwise prevent worker-node progress from
  reaching the central Terminal Logger.

### Phase 4: Terminal Logger

- Add a bounded progress region to the existing node frame. The frame owns a snapshot of active
  operations keyed by operation ID; progress rows are rendered after node rows and participate in
  the same cursor accounting and resize redraw logic.
- Update the snapshot under the Terminal Logger lock from structured lifecycle events. Starts add
  an operation, updates use latest-value-wins sequence ordering, and finishes remove it. The
  renderer is gated by `ITerminal.SupportsProgressReporting`, so redirected or unsupported
  terminals do not receive progress output.
- Sanitize control characters and truncate rows to the terminal layout width before writing.
- Render determinate progress rows with a bounded ten-character ASCII bar when a positive total
  is available; retain the numeric completed/total values for precise progress.
- Keep failed, canceled, and completed operations out of the next live frame; authoritative
  warnings and errors remain responsible for diagnostics.
- Integrate immediate output and resize behavior.
- Add OSC 9;4 where supported: one determinate operation publishes its bounded percentage,
  multiple active operations publish indeterminate state, and the last operation publishes the
  removal sequence. This is capability-gated and does not change authoritative build error state.
- Renderer unit tests cover sanitization/truncation and stale-update rejection. Terminal Logger
  integration tests cover lifecycle rendering, determinate and indeterminate OSC state, prompt
  removal on finish, bounded concurrent operations, resize, cancellation, immediate-message
  repaint, and capability-gated suppression for unsupported terminals.

### Phase 5: binary logs

- Add bounded durable progress records.
- Version the format.
- Add old/new reader compatibility, replay, and size tests.

Phase 5 implementation uses dedicated `TaskProgressStarted` and `TaskProgressFinished` records.
Intermediate `TaskProgressUpdated` events are intentionally omitted from binary logs: they are
too prevalent for durable logging and do not provide useful information in a static log view.
The terminal event carries the final completed/total values and summary. The records use the
existing length-delimited framing, and the new record kinds are appended to the record-kind enum
so forward-compatible readers can skip them, while strict older readers reject the newer format
version as they do for other format additions.

### Phase 6: first-party tasks

- Adopt `DownloadFile`.
- Measure throughput and allocation impact.
- Adopt archive tasks.
- Use `Copy` as the concurrency and high-volume stress test.

Phase 6 begins with `DownloadFile`. It reports one logical byte-count operation across retries,
uses the response content length when available, and reports indeterminate progress when the
server omits it. The transfer wraps the destination stream, reporting bytes after successful
writes while retaining the standard source-to-destination copy path. Up-to-date and
`FailIfNotIncremental` paths do not start a transfer operation. The operation title names the
file being downloaded.

The archive tasks follow the same rules with an item-count unit:

- `Unzip` reports `Extracting {archive}`. The entry count of a zip archive is known up front, so
  progress is determinate.
- `Untar` reports `Extracting {archive}`. A tar archive is read as a stream, so the entry count is
  not known up front and progress is indeterminate.
- `TarDirectory` reports `Creating {archive}`. The entry list is materialized before writing, so
  progress is determinate.

Every archive task creates its reporter lazily, on the first entry that actually requires work.
Archives that are entirely filtered out or up to date therefore start no operation at all, which
keeps incremental builds free of progress traffic. Cancellation ends the operation through
`Cancel` rather than `Complete`.

`Copy` is the concurrency and high-volume case. It reports a determinate item count over the
source-file list from both the single-threaded and the parallel copy paths. The parallel path
processes partitions on several threads, so the processed-file counter is incremented atomically
and the reporter's own lock serializes the updates. Single-file copies finish too quickly for
progress to be useful and start no operation.

`TaskProgressUpdate` is a readonly struct and the reporter allocates nothing on the throttled
path, so a per-file report site adds no allocation to a large copy. The reported status string is
the existing destination path rather than a formatted message, which keeps the hot path free of
string formatting.

### Retiring messages that progress replaces

Adoption makes some existing log messages redundant, but only some of them. The distinction is
whether the message is the *only* durable record of what happened.

Terminal Logger renders only messages of `MessageImportance.High`, and the binary logger drops
`TaskProgressUpdatedEventArgs` (see [Binary logs](#binary-logs)), persisting only the `Started`
and `Finished` events. So a per-*operation* high-importance message such as
`DownloadFile.Downloading` or `TarDirectory.Comment` announces exactly what the progress row
already shows, and the operation still survives in the binlog through the `Started` event. Those
two messages drop from `High` to `Normal` under change wave 18.13: they leave the Terminal Logger
view but remain in binary logs and in console output at normal verbosity, and their text is
unchanged.

The per-*item* messages — `Copy.FileComment`, `Unzip.FileComment`, `Untar.FileComment` — are not
redundant, even though progress now covers the same items. They are already `Normal` importance,
so they never appeared in the Terminal Logger view and duplicate nothing there; and because
per-item progress updates never reach the binary log, deleting them would leave no record at all
of which files were copied or extracted. They are deliberately retained.

## Measured cost

Adoption is only safe if a task can report without paying for it, so the cost is measured rather
than assumed. The benchmarks live in `src/MSBuild.Benchmarks` and can be re-run with
`dotnet run -c Release --filter '*TaskProgress*' --filter '*CopyTaskProgress*' --filter '*DownloadFileProgress*'`.

### Cost of one report

`TaskProgressReporterBenchmark` measures a single `Report` call against the three sinks a task
meets in practice.

| Sink | Time | Allocated |
| --- | --- | --- |
| Host without progress support | 1.5 ns | 0 B |
| Supporting host, no logging sink | 15.0 ns | 0 B |
| Supporting host, throttled (steady state) | 17.8 ns | 0 B |
| Forwarded update (survives the throttle) | 42.9 ns | 192 B |
| Create and complete one operation | 51.7 ns | 192 B |

The first row is the one that protects existing builds: a task that adopts progress costs a host
without support about a nanosecond and no allocation, because `EngineServices` returns a no-op
reporter. The steady-state row is the one that protects supporting builds, because the throttle
discards an update before any event is constructed. Only the fourth row allocates, and the 150 ms
throttle bounds it to a few hundred bytes per second per operation regardless of how often the
task reports.

### Cost to a high-volume task

`CopyTaskProgressBenchmark` copies 1000 files with the copy itself replaced by a delegate that
does no file system work. Removing the I/O is deliberate: it is the worst case, because the
overhead is measured against nothing instead of being diluted by a real copy.

| Path | Progress | Time | Allocated |
| --- | --- | --- | --- |
| Single-threaded | off | 1040.6 ± 20.5 µs | 2.02 MB |
| Single-threaded | on | 1051.1 ± 19.5 µs | 2.02 MB |
| Parallel | off | 554.3 ± 6.2 µs | 2.11 MB |
| Parallel | on | 548.3 ± 5.5 µs | 2.11 MB |

Both time differences fall inside the error bars, and allocation is identical to the reported
precision. The parallel path is the one that contends on the reporter lock, and it shows no
regression, so serializing updates across partitions is not a bottleneck at this volume.

### Cost to a byte-counting task

`DownloadFileProgressBenchmark` downloads 16 MiB from an in-memory handler, so the measurement
reflects the task rather than a network. `DownloadFile` reports once per buffer written rather
than once per item, which is the highest report rate of any adopter.

| Progress | Time | Allocated |
| --- | --- | --- |
| off | 3.683 ± 0.061 ms | 4.10 KB |
| on | 3.801 ± 0.076 ms | 4.77 KB |

A 16 MiB transfer allocating about 4 KB in total confirms that the transfer buffer is rented from
`ArrayPool<byte>` rather than allocated per download; a regression there would show up as roughly
80 KiB of extra allocation per row. Progress adds 0.67 KB for the whole transfer and about 3% of
the time of a download that has no network cost at all, which is an upper bound rather than a
realistic figure.

## Test plan

### Reporter unit tests

- begin occurs exactly once,
- updates receive increasing sequence numbers,
- latest update wins under coalescing,
- concurrent `Report` calls do not throw or deadlock,
- exactly one terminal transition wins a race,
- end flushes the latest accepted update,
- post-terminal reports are ignored,
- disposal without terminal state abandons,
- disposal after terminal state is harmless,
- invalid numeric values do not affect build success,
- text bounds and sanitization are enforced, and
- unsupported hosts provide a safe no-op.

### Engine tests

- multiple operations from one task remain distinct,
- leaked reporters are abandoned at task teardown,
- operations carry the correct `BuildEventContext`,
- task cancellation ends active operations,
- high-frequency reports stay within allocation and forwarding bounds,
- no-progress builds preserve existing event and output behavior, and
- progress cannot reorder warnings or errors.

### Distributed tests

- `/m` forwards lifecycle in order,
- stale or duplicate sequences are ignored,
- end flushes pending state,
- node shutdown abandons active operations,
- mixed-version nodes degrade safely, and
- out-of-proc task hosts cannot corrupt or lose task correlation.

### Logger tests

- determinate and indeterminate display,
- multiple concurrent operations,
- immediate warning/error repaint,
- terminal resize and narrow widths,
- redirected and non-TTY output,
- sanitization of malicious status text,
- no unrelated percentage aggregation, and
- terminal indicator cleanup.

Frame layout:

- an operation is drawn below the node row of the project that reported it,
- an operation whose project is no longer displayed is still drawn,
- a frame that only changes an amount keeps its lines and does not repaint the block, and
- a frame in which an operation starts or ends repaints the block.

For the classic console logger:

- one final line per operation, and nothing for updates,
- every outcome word,
- a finished event whose started event never arrived,
- suppression below normal verbosity, and
- forwarding of the started and finished events at normal verbosity but not at minimal.

### Binary-log tests

- write/replay round trip,
- new readers accept old logs,
- old readers degrade gracefully on new logs,
- final lifecycle data is retained,
- intermediate sampling is bounded, and
- report storms do not cause proportional log growth.

### First-adopter tests

- `DownloadFile` with and without `Content-Length`,
- cancellation and retry during download,
- no regression in downloaded bytes,
- progress disabled/no-op host behavior,
- archive operations with zero and many entries, and
- `Copy` with parallel workers and skipped/up-to-date files.

## Open questions

- Final names and constructor shapes for the public types.
- Whether reporter creation should return a public no-op singleton or use a `TryCreate` pattern.
- Whether invalid numeric updates should be ignored entirely or normalized for display.
- The maximum active operations allowed per task and per build.
- Whether begin/update/end should be public `BuildEventArgs` types.
- How new progress event types are represented so old binary-log readers degrade safely.
- The exact negotiation strategy for mixed-version node and task-host processes.
- Whether classic console/file loggers emit final summaries by default.
- Whether one operation can expose an optional parent/child relationship in a later version.
- Whether retry attempts should be child operations once hierarchy exists.

## Related work

- [dotnet/msbuild#6944](https://github.com/dotnet/msbuild/issues/6944) - task progress and cursor-control request
- [NuGet/Home#4346](https://github.com/NuGet/Home/issues/4346) - NuGet progress motivation
- [dotnet/msbuild#8878](https://github.com/dotnet/msbuild/issues/8878) - Terminal Logger output behavior
- [dotnet/msbuild#9378](https://github.com/dotnet/msbuild/issues/9378) - Terminal Logger rendering issue
- [dotnet/msbuild#10415](https://github.com/dotnet/msbuild/issues/10415) - Terminal Logger and interactive output
- [dotnet/msbuild#10416](https://github.com/dotnet/msbuild/issues/10416) - Terminal Logger progress behavior
- [dotnet/msbuild#15026](https://github.com/dotnet/msbuild/issues/15026) - verbatim host output
- [LSP work-done progress](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#workDoneProgress) - semantic lifecycle prior art
- [Windows Terminal progress indication](https://learn.microsoft.com/windows/terminal/tutorials/progress-bar-sequences) - OSC 9;4 renderer capability
