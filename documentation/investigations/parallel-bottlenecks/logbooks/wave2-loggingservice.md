# Wave 2 logbook - LoggingService

## Focus

Canonical candidate: `Microsoft.Build.BackEnd.Logging.LoggingService` central fan-in / serialized delivery.

Deep-dive scope:

- sync vs async delivery mode selection
- queue and backpressure behavior
- sink / logger dispatch path
- whether this is likely to be a real bottleneck in one-process multi-project parallel builds

## Key findings

### 1. The path is unquestionably shared

`LoggingService` is the single ingestion point for build events in the scoped area:

- direct engine logging helpers call `ProcessLoggingEvent(...)` for comments, errors, build start/finish, evaluation, project, target, and task events  
  - `src\Build\BackEnd\Components\Logging\LoggingServiceLogMethods.cs:72-79,132-143,345-363,379-401,494-537,584-586,670-680,740-756,769-782,836-850`
- remoted node events also enter through `ProcessLoggingEvent(...)` from `PacketReceived(...)`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:991-1014`

So, regardless of event source, this is the convergence point before loggers see anything.

### 2. Current code makes mode selection more nuanced than the logging wiki suggests

The wiki says the scheduler node is synchronous by default unless `MSBUILDLOGASYNC=1`:

- `documentation\wiki\Logging-Internals.md:129-132`

Current code is more specific:

- `BuildManager` uses **synchronous** logging only when `MaxNodeCount == 1 && UseSynchronousLogging`; otherwise it chooses **asynchronous**  
  - `src\Build\BackEnd\BuildManager\BuildManager.cs:3171-3178`
- CLI (`XMake`) sets `UseSynchronousLogging = true` unless `MSBUILDLOGASYNC=1`  
  - `src\MSBuild\XMake.cs:1503-1510`
- `ProjectCollection` defaults to whichever constructor argument sets, and many overloads default to `useAsynchronousLogging: false`  
  - `src\Build\Definition\ProjectCollection.cs:303-304,327-330,362-363,1825-1830`

Practical takeaway:

- CLI single-node / non-parallel builds default to sync.
- BuildManager-based parallel builds default to async.
- one-process OM / `ProjectCollection` usage can still be sync by default unless the caller opts into async.

### 3. Both modes still serialize delivery; async only moves the serialization point

The implementation has two modes:

- **sync mode:** producer thread enters `lock (_lockObject)` and runs `RouteBuildEvent(buildEvent)` inline  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1336-1341`
- **async mode:** producers enqueue into `_eventQueue`; one background thread runs `LoggingEventProcessor -> RouteBuildEvent`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:253-297,1302-1328,1412-1455,1541-1560`

This matches the wiki’s architectural statement:

- isolated delivery is guaranteed
- one slow logger blocks all others
- event consumption time is effectively additive across loggers  
  - `documentation\wiki\Logging-Internals.md:55-61,125-134`

So async reduces direct producer blocking until the queue fills, but it does **not** make logger delivery parallel.

### 4. Backpressure is explicit and can block producers

Async mode is not “fire and forget”:

- queue capacity defaults to `200000`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:84,289`
- capacity can be overridden by `MSBUILDLOGGINGQUEUECAPACITY`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:323-331`
- when full, producers loop on `eventQueue.Count >= _queueCapacity` and wait on `_dequeueEvent.WaitOne()` before enqueueing  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1319-1327`

Implication: if logger throughput falls behind badly enough, build threads stop merely “logging asynchronously” and begin blocking on logger progress anyway.

### 5. The hot path does more than a trivial enqueue/dispatch

Per-event serial work includes:

- warning promotion / demotion to message or error  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1598-1689`
- tracking submission-level errors and telemetry categorization  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1692-1707`
- warning-config map maintenance from `ProjectStarted` / `ProjectFinished`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1709-1723`
- routing to filter source or sink  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1725-1773`

This is not huge per event, but it means the single lane does some real bookkeeping before logger code even runs.

### 6. In-proc central loggers still go through an extra forwarding hop

`RegisterLogger(...)` does not attach central loggers directly to the original producer path. Instead it:

- creates a `CentralForwardingLogger`
- routes events through `_filterEventSource`
- forwards them through `EventRedirectorToSink`
- lands in an `EventSourceSink` that actual central loggers subscribe to  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1024-1075,1113-1165,1763-1773`
  - `src\Build\BackEnd\Components\Logging\CentralForwardingLogger.cs:78-121`
  - `src\Build\BackEnd\Components\Logging\EventRedirectorToSink.cs:51-55`

`EventSourceSink.Consume(...)` then invokes typed handlers synchronously and finally `AnyEventRaised`:

- `src\Build\BackEnd\Components\Logging\EventSourceSink.cs:234-355,403-410`

Meaning: even in one process, this remains a serialized multi-step dispatch path, not a direct logger callback.

### 7. Some upstream filtering reduces pressure

`LoggingService` tracks `MinimumRequiredMessageImportance` based on known logger types:

- `src\Build\BackEnd\Components\Logging\LoggingService.cs:856-862,1858-1923`

Several build paths consult that minimum importance before expanding / logging low-importance messages:

- `src\Build\BackEnd\Components\RequestBuilder\TaskBuilder.cs:638-648`
- `src\Build\BackEnd\Components\RequestBuilder\TargetEntry.cs:371-379`
- `src\Build\BackEnd\Components\RequestBuilder\TaskHost.cs:937-938`

This matters because it can substantially reduce event volume in common non-diagnostic builds.

## Reasoned conclusion

### Is this a real bottleneck candidate?

Yes, but with nuance.

The evidence clearly shows a **single serialized logging lane** shared by all producers. In sync mode that lane is directly on producer threads; in async mode it becomes one bounded queue plus one consumer thread. Either way, logger work is not parallelized.

### How likely is it to matter in one-process multi-project parallel builds?

**Overall likelihood: medium.**

Why not high by default:

- pure one-process builds avoid the extra cross-process packet transport / deserialization cost
- some low-importance chatter is suppressed before it reaches the service
- many builds will be dominated by evaluation / execution / task work rather than logger dispatch

Why not low:

- this path is shared across all producers
- the queue can back up and block producers
- one slow logger stretches everyone’s event latency
- the implementation intentionally preserves strict sequential delivery

### What would move it up?

- diagnostic / detailed verbosity
- slow central loggers (binary logger, terminal logger, custom loggers)
- heavy event storms from many concurrently building projects
- sustained queue growth / backpressure in async mode

### What would move it down?

- minimal/normal verbosity with light logger mix
- builds where project/task work dominates and logging is sparse
- evidence that queue depth stays near zero and logger CPU is negligible

## Escalation decision

Escalate to full report: **yes**.

Reason: the serialization is architectural and well-supported by code evidence; the remaining uncertainty is mainly **how often** it dominates wall-clock time in realistic one-process parallel builds, not whether the shared lane exists.
