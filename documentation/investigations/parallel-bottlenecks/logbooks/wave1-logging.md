# Wave 1 logbook - logging / eventing

## 1) Scope searched

Broad Wave 1 scan over logging/event-flow code only:

- `src\Build\Logging\**`
  - focused on `BinaryLogger\**`, `ParallelLogger\**`, `TerminalLogger\**`, and `BaseConsoleLogger.cs`
- direct event-flow plumbing in `src\Build\BackEnd\**`
  - focused on `Components\Logging\**` and `Components\Communications\LogMessagePacket.cs`
- directly relevant host/doc context
  - `src\MSBuild\XMake.cs`
  - `documentation\wiki\Logging-Internals.md`

Search themes: shared mutable state, global/per-build fan-in points, queues, locks, single-thread pumps, per-event remoting/serialization, and output formatting paths. `src\Tasks\**` was intentionally not inspected.

## 2) Candidate list grouped by build stage

### Stage 3 - Scheduling / execution coordination

#### Candidate: `BuildEventArgTransportSink` + `LogMessagePacket` remote event path

- **Symbol:** `Microsoft.Build.BackEnd.Logging.BuildEventArgTransportSink`, `Microsoft.Build.BackEnd.LogMessagePacket`
- **Why shared:** worker-node forwarding loggers share one forwarding sink per build/node setup, and every forwarded event is turned into a remoted log packet on the same path.
- **Evidence:**
  - `documentation\wiki\Logging-Internals.md:50-53,98-102,113-115`
  - `src\Build\BackEnd\Components\Logging\BuildEventArgTransportSink.cs:18-28,133-149`
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1171-1177,1210-1219`
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:69-109,184-209`
- **Why it may bottleneck:** there is no batching here; each forwarded event becomes a `LogMessagePacket`, is serialized, and is sent individually. Heavy events (especially evaluation/property/item/profile payloads) can amplify per-event remoting cost before the scheduler even starts central dispatch.
- **Impact shape:** fan-in + serialization
- **Likelihood:** medium
- **Escalate later?:** yes

### Stage 4 - Logging / event forwarding

#### Candidate: `LoggingService` central fan-in and serialized delivery pipeline

- **Symbol:** `Microsoft.Build.BackEnd.Logging.LoggingService` (plus `EventSourceSink` dispatch)
- **Why shared:** the scheduler has a single `LoggingService` instance for the build; it owns the shared sink table, the shared filter source, and either a single lock-based synchronous path or a single async queue + pump thread.
- **Evidence:**
  - `documentation\wiki\Logging-Internals.md:26-31,55-61,125-134`
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:84-90,130-147,253-297,309-317`
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1302-1341,1412-1455,1541-1560,1763-1773`
  - `src\Build\BackEnd\Components\Logging\EventSourceSink.cs:234-355,403-410`
  - `src\MSBuild\XMake.cs:1503-1510`
- **Why it may bottleneck:** this is the clearest shared choke point in the scoped area. In synchronous mode, producers pay logger cost inline under `_lockObject`; in async mode, producers still feed one bounded queue and one consumer thread, and they block when the queue reaches capacity (`LoggingService.cs:1319-1323`). `EventSourceSink` then invokes logger handlers serially, so one slow logger stretches total delivery time for all others.
- **Impact shape:** fan-in/fan-out + repeated per-event serialization of logger work
- **Likelihood:** high
- **Escalate later?:** yes

#### Candidate: `BinaryLogger.Write` / `BuildEventArgsWriter`

- **Symbol:** `Microsoft.Build.Logging.BinaryLogger.Write`, `Microsoft.Build.Logging.BuildEventArgsWriter`
- **Why shared:** one binary logger instance writes one output stream for the whole build; all events handled by that logger pass through the same writer state (`MemoryStream`s, de-dup dictionaries, record ids).
- **Evidence:**
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:378-383,579-607`
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:32-80,125-159,1190-1220`
  - explicit comment: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:603-604` (`TODO: think about queuing to avoid contention`)
- **Why it may bottleneck:** when `/bl` is enabled, every event pays single-lane binary serialization and file I/O. The writer is intentionally mutable and protected by a lock, so event throughput cannot exceed one `eventArgsWriter.Write(e)` at a time. The de-dup tables and record staging streams also mean the hot path is not just a raw stream write.
- **Impact shape:** serialization + I/O serialization
- **Likelihood:** high when binary logging is enabled; otherwise none
- **Escalate later?:** yes

#### Candidate: `ProjectImportsCollector`

- **Symbol:** `Microsoft.Build.Logging.ProjectImportsCollector`
- **Why shared:** one collector hangs off the binary logger and owns the shared archive stream, zip archive, processed-file set, and a single chained background task.
- **Evidence:**
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:380-383,616-637`
  - `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:25-38,122-136,159-211,214-236,283-307`
- **Why it may bottleneck:** the “background” path is still deliberately serialized: `AddFileHelper` locks `_fileStream` and appends work onto `_currentTask` via `ContinueWith`, so file opens, reads, zip-entry creation, and `CopyTo` all happen in one chain. Shutdown/finalization then waits for completion (`ProjectImportsCollector.cs:283-287`).
- **Impact shape:** IO serialization + shutdown tail
- **Likelihood:** medium, but only when project-import collection is enabled
- **Escalate later?:** yes (conditional on `CollectProjectImports != None`)

#### Candidate: `TerminalLogger` live renderer

- **Symbol:** `Microsoft.Build.Logging.TerminalLogger`
- **Why shared:** all terminal-render state is shared inside one logger instance, protected by `_lock`, and a dedicated refresher thread also uses that same lock.
- **Evidence:**
  - `src\Build\Logging\TerminalLogger\TerminalLogger.cs:90-92`
  - `src\Build\Logging\TerminalLogger\TerminalLogger.cs:442-460`
  - `src\Build\Logging\TerminalLogger\TerminalLogger.cs:577`
  - `src\Build\Logging\TerminalLogger\TerminalLogger.cs:809-818,1105-1110,1472-1526,1593-1601`
- **Why it may bottleneck:** live UI refresh and immediate message rendering share one lock and one terminal output device. The refresher can run ~30Hz, and both refresh and immediate-message paths erase/redraw terminal state, so logger callback time can include UI work rather than just cheap bookkeeping.
- **Impact shape:** output serialization + rendering contention
- **Likelihood:** low-medium
- **Escalate later?:** no, unless a later report shows terminal logger specifically dominating wall-clock time

## 3) Weaker candidates / likely non-bottlenecks

- **`ParallelConsoleLogger` / `BuildEventManager`** (`src\Build\Logging\ParallelLogger\ParallelConsoleLogger.cs:67-69,1418-1423,1794-1806`; `src\Build\Logging\ParallelLogger\ParallelLoggerHelpers.cs:20-25,42-77`): there is local locking/state, but this mostly sits downstream of `LoggingService`'s already-serialized delivery. It looks more like per-event formatting cost than an additional cross-project choke point.
- **`BaseConsoleLogger` shared `StringBuilder` reuse** (`src\Build\Logging\BaseConsoleLogger.cs:1203-1215,1260-1267`): explicitly assumes messages are already processed serially, so it appears to be an optimization riding on upstream serialization, not an independent bottleneck.
- **Framework-side event types/serialization helpers**: within the narrow “direct event-flow plumbing only” scan, nothing in `src\Framework\**` stood out as a stronger shared-state bottleneck than the back-end/logging fan-in already identified above.

## 4) Short take

The strongest Wave 1 candidates in this scope are the **central `LoggingService` delivery path** and the **binary logger single-writer path**. The remote forwarding/remoting path (`BuildEventArgTransportSink` + `LogMessagePacket`) is also worth a deeper follow-up because it can multiply cost before events even reach central dispatch in multi-node builds.
