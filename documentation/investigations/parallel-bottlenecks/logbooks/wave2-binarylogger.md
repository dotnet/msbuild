# Wave 2 logbook - BinaryLogger

## Focus

Canonical candidate: `Microsoft.Build.Logging.BinaryLogger.Write` / `Microsoft.Build.Logging.BuildEventArgsWriter`.

Deep-dive scope:

- single-writer path
- mutable writer state and dedup caches
- conditionality when `/bl` is enabled
- how likely it is to become a real bottleneck in one-process multi-project parallel builds

## Key findings

### 1. This path is conditional, not always present

The candidate matters only when the binary logger is explicitly configured:

- `/bl` enables the binary logger  
  - `documentation\wiki\Binary-Log.md:14-24`
- CLI processing adds one or more `BinaryLogger` instances only when binary logger parameters are present  
  - `src\MSBuild\XMake.cs:3513-3554`

Important nuance:

- common case with identical additional paths is optimized to **one** logger plus post-build file copies  
  - `src\MSBuild\XMake.cs:3539-3543`
- only distinct configurations create multiple logger instances  
  - `src\MSBuild\XMake.cs:3545-3552`

So for the usual `/bl` path, this is typically a single logger instance, not many competing writers.

### 2. `/bl` also increases event volume

CLI processing forces verbosity to diagnostic so task inputs are logged:

- `src\MSBuild\XMake.cs:3520-3523`

That does not itself prove a bottleneck, but it raises the ceiling on how much work `BinaryLogger.Write` may need to serialize.

### 3. The writer is intentionally stateful and single-lane

Runtime setup creates one writer stack per logger instance:

- `FileStream -> GZipStream -> BufferedStream -> BinaryWriter -> BuildEventArgsWriter`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:378-411`

The logger’s hot path is explicit:

- `eventSource.AnyEventRaised += EventSource_AnyEventRaised`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:455-468`
- `EventSource_AnyEventRaised -> Write(e)`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:584-607`
- write is guarded by `lock (eventArgsWriter)` with a TODO about queuing to avoid contention  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:603-606`

`BuildEventArgsWriter` keeps mutable shared state:

- staging `MemoryStream`s
- mutable `BinaryWriter` redirection
- string and name-value dedup dictionaries
- record ids
- reusable list buffers  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:29-45,50-62,75-114,125-159`

This all but requires serialized access.

### 4. The heavy part is not just “write bytes”

Per event, `BuildEventArgsWriter` may do all of the following:

- serialize event payload into `currentRecordStream`
- hash strings and look them up in `stringHashes`
- emit string records when first seen
- hash name/value lists, fill index buffers, and emit separate `NameValueList` records
- flush staged record bytes into the underlying compressed stream  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:144-159`
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:1168-1247`
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:1296-1337`

So the cost shape is: hashing + dictionary lookups + conditional extra records + compressed stream writes.

### 5. Import collection can add meaningful side work

By default, binary logging collects project/import sources:

- docs say default is `ProjectImports=Embed`  
  - `documentation\wiki\Binary-Log.md:30-35`
- logger creates `ProjectImportsCollector` unless `ProjectImports=None`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:380-383`

During event handling, `CollectImports(e)` can enqueue file/archive work:

- `src\Build\Logging\BinaryLogger\BinaryLogger.cs:593-596,616-638`

The collector itself serializes background work through one chained task:

- `_currentTask = _currentTask.ContinueWith(..., TaskScheduler.Default)` under `lock (_fileStream)`  
  - `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:34-38,122-130`
- shutdown waits for all pending work  
  - `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:283-287`

This means `/bl` cost is not just event serialization; default import capture can add file I/O and shutdown tail latency.

### 6. In one-process parallel builds, the main issue is throughput cost, not lock contention

For central loggers, MSBuild’s logging architecture already guarantees serialized event delivery:

- `documentation\wiki\Logging-Internals.md:55-61,125-134`

That means in a one-process multi-project build:

- `lock (eventArgsWriter)` is probably not the primary contention story
- the more important effect is that binary-log serialization work becomes part of the shared central logging lane and increases per-event service time

So the candidate is real, but mostly as a **slow single consumer / expensive logger** problem, not as many threads fighting over `eventArgsWriter`.

### 7. Replay/raw-event paths are less relevant to the target scenario

`BinaryLogger.Initialize` has replay-specific paths:

- raw record forwarding
- embedded-content replay
- string replay without dedup to preserve indexes  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:418-445`

These are important for binlog correctness, but they are not the main path for “one-process multi-project parallel build with `/bl` enabled”.

## Reasoned conclusion

### Is this a real bottleneck candidate?

Yes.

The path is intentionally single-writer and stateful. If `/bl` is enabled, every event routed to the binary logger pays serialization, dedup bookkeeping, compression, and sometimes import-capture overhead.

### How likely is it to matter in one-process multi-project parallel builds?

**Overall likelihood: medium-high when `/bl` is enabled; none when it is not.**

Why not simply “high” always:

- it is opt-in
- binary logging exists partly to be much cheaper than text diagnostic logs
- one-process central logging is already serialized, so this is usually additive cost on an existing single lane, not a brand-new lock convoy

Why above medium:

- `/bl` is common in diagnostics and CI investigations
- `/bl` forces richer event capture
- default import collection adds more work
- the code itself acknowledges contention risk with the TODO on queuing

### What would move it up?

- `/bl` with default import embedding
- diagnostic-heavy builds / lots of messages, items, metadata, task inputs
- large project graphs with repeated structured payloads
- slow compression or storage I/O

### What would move it down?

- no `/bl`
- `ProjectImports=None`
- builds with modest event volume
- cases where task/evaluation/compiler work dwarfs logger cost

## Escalation decision

Escalate to full report: **yes**.

Reason: the single-writer architecture and mutable writer state are clear from source, and the remaining uncertainty is practical magnitude under representative `/bl` workloads, not whether the shared lane exists.
