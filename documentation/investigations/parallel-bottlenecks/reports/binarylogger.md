# BinaryLogger

## Why shared

`BinaryLogger` is not always present, but when `/bl` (or equivalent binary-logger registration) is used, it becomes a shared central logger instance for the build.

- `/bl` enables the binary logger  
  - `documentation\wiki\Binary-Log.md:14-24`
- CLI processing instantiates `BinaryLogger` only when binary logger parameters are present  
  - `src\MSBuild\XMake.cs:3513-3554`
- in the common case of identical configuration plus extra output paths, CLI intentionally creates **one** logger instance and copies the finished binlog afterward instead of running multiple equivalent binary loggers  
  - `src\MSBuild\XMake.cs:3539-3543`
- runtime initialization wires that logger to the event stream with `eventSource.AnyEventRaised += EventSource_AnyEventRaised`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:455-468`

Per logger instance, there is one mutable writer stack:

`FileStream -> GZipStream -> BufferedStream -> BinaryWriter -> BuildEventArgsWriter`

- `src\Build\Logging\BinaryLogger\BinaryLogger.cs:378-411`

That makes the writer path effectively shared for all events consumed by that binary logger.

## Why it might bottleneck

The hot path is intentionally single-writer and stateful:

- `BinaryLogger.Write(BuildEventArgs e)` locks `eventArgsWriter` before writing  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:603-606`
- the code contains an explicit comment: `TODO: think about queuing to avoid contention`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:603`
- `BuildEventArgsWriter` carries mutable dedup state, record ids, staging streams, and reusable buffers  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:29-45,50-62,75-114`

So, if `/bl` is enabled, every event sent to the binary logger pays work on one shared writer lane:

- event serialization into a temporary record stream
- deduplicated string handling
- deduplicated name/value-list handling
- compressed stream output
- optionally, import/source capture side work

This can turn the binary logger into either:

1. a direct single-writer bottleneck, or
2. more commonly in MSBuild, an *expensive logger* that slows the already-serialized central logging lane.

## Evidence

### Opt-in / conditional nature

- `/bl` is a dedicated switch, not default behavior  
  - `documentation\wiki\Binary-Log.md:14-24`
- the binary logger can be used alongside console/file loggers, but it is independent  
  - `documentation\wiki\Binary-Log.md:26-29`
- CLI creates binary logger instances only when the switch is present  
  - `src\MSBuild\XMake.cs:3513-3554`

This matters because the candidate has **zero** cost when binary logging is not enabled.

### Event-volume amplification when enabled

When CLI sees `/bl`, it forces diagnostic verbosity because binary logs want task inputs:

- `src\MSBuild\XMake.cs:3520-3523`

That increases the amount of structured event data available to the binary logger and raises its potential cost.

### Single-writer hot path

- `BinaryLogger.Initialize(...)` creates the stream/writer stack and subscribes to `AnyEventRaised`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:343-411,455-468`
- every event reaches `EventSource_AnyEventRaised -> Write(e)`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:584-607`
- the actual writer call is serialized by `lock (eventArgsWriter)`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:603-606`

### Stateful serialization work

`BuildEventArgsWriter` is not a stateless byte dumper.

- it stages each record in `currentRecordStream` before flushing  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:32-38,144-159`
- it maintains a deduplicated string table via `stringHashes` and string record ids  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:65-80,98-103,1296-1337`
- it maintains deduplicated name/value-list records via `nameValueListHashes`, buffer lists, and record ids  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:77-80,105-114,1168-1247`
- it temporarily redirects writes between the current-record writer and the original writer so extra records can precede the current event record  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:57-62,153-159,258-288,1190-1220`
- large blobs bypass the per-event memory stream and copy directly to the underlying stream  
  - `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:258-272,1269-1275`

This means per-event cost includes hashing, dictionary lookups, extra record emission, stream copying, and compression.

### Import/source capture side work

By default, binary logging collects project/import source files:

- `ProjectImports=Embed` is the documented default  
  - `documentation\wiki\Binary-Log.md:30-35`
- unless disabled, `BinaryLogger.Initialize(...)` creates `ProjectImportsCollector`  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:380-383`
- event handling calls `CollectImports(e)` and may add imported project files, project files, metaproject XML, response files, and generated files  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:593-596,616-638`

`ProjectImportsCollector` itself is serialized:

- it holds `_processedFiles`, `_fileStream`, `_zipArchive`, and a single chained `_currentTask`  
  - `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:25-38`
- `AddFileHelper` uses `lock (_fileStream)` and appends work by chaining `_currentTask = _currentTask.ContinueWith(...)`  
  - `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:120-136`
- shutdown waits for pending collector work to finish  
  - `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:283-287`
- embed mode then re-reads the archive and writes it back through `eventArgsWriter.WriteBlob(...)` during shutdown  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:504-525`
  - `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:262-279`

So the default `/bl` path can include extra file I/O and shutdown-tail work beyond normal event serialization.

### Important nuance for one-process builds

MSBuild central logging already delivers events sequentially to central loggers:

- `documentation\wiki\Logging-Internals.md:55-61,125-134`

Because of that, in the target scenario (one-process multi-project parallel builds), the binary logger’s own `lock (eventArgsWriter)` is probably not the main *contention* story. Instead, the more important practical effect is that the binary logger adds substantial per-event work to an already shared, serialized logger-delivery lane.

## Likelihood

**Medium-high when `/bl` is enabled; none when it is not.**

Why not universally high:

- the logger is opt-in
- binary logging is designed to be relatively efficient compared with text diagnostic logging  
  - `documentation\wiki\Binary-Log.md:5-9`
- in one-process builds, the dominant issue is often additive logger service time, not direct lock fights on `eventArgsWriter`

Why above medium when enabled:

- every event goes through one mutable writer
- `/bl` increases captured detail
- default import collection adds more work
- the writer performs non-trivial dedup/stateful serialization
- the code itself acknowledges contention risk

Practical likelihood bands:

- **high** for large, chatty builds using `/bl` with default import embedding
- **medium-high** for ordinary parallel builds with `/bl`
- **medium or below** for smaller or less chatty builds, or with `ProjectImports=None`

## Expected contention mode

### Primary mode in the target scenario

- one shared binary-writer lane per binary logger instance
- serialized access to mutable writer state
- additive per-event service time

### Specific shapes

1. **Serialization bottleneck**
   - hashing strings / name-value lists
   - dictionary lookups / inserts
   - temporary record staging
   - compressed stream writes

2. **I/O bottleneck**
   - writing through `BufferedStream` + `GZipStream`
   - possible large blob writes
   - import-archive handling at shutdown

3. **Shutdown-tail bottleneck**
   - waiting for `ProjectImportsCollector` chained work
   - embedding archive back into the binlog in `Embed` mode

### Less likely mode in one-process builds

- classic lock convoy on `lock (eventArgsWriter)` from many producer threads

That is less convincing here because central logger delivery is already serialized upstream.

## Where it is used

- CLI `/bl` switch / binary logger registration  
  - `documentation\wiki\Binary-Log.md:14-24`
  - `src\MSBuild\XMake.cs:3513-3554`
- any build where `BinaryLogger.Initialize(IEventSource)` subscribes to the event stream  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:343-468`
- replay scenarios have additional raw-record / embedded-content paths, but those are less relevant to the target question of one-process parallel build execution  
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:418-445`

For the user’s target scenario, the main runtime path is:

`central logging event -> BinaryLogger.EventSource_AnyEventRaised -> BinaryLogger.Write -> BuildEventArgsWriter.Write`

## Why it may or may not matter in practice

### Why it may matter

- `/bl` is common when diagnosing CI and parallel-build issues
- binary logger serialization is richer than a trivial text append
- default import collection adds more I/O and shutdown work
- per-event cost compounds across the whole build because the logger sees the full event stream
- slower binary logging stretches central logging service time for every event

### Why it may not matter

- if `/bl` is absent, the candidate disappears
- binary logging was explicitly designed to be faster than text diagnostic/file logging  
  - `documentation\wiki\Binary-Log.md:5-9`
- many builds remain dominated by evaluation, compilation, tasks, or filesystem work
- in one-process builds, upstream central logging serialization may dominate the concurrency story; binary logger then becomes one contributor to that lane rather than an independent lock bottleneck
- `ProjectImports=None` materially reduces side work

Net: the candidate is real and stronger than a weak “maybe”, but its practical importance is highly conditional on `/bl`, event volume, and import-collection settings.

## How to validate

1. **A/B compare with and without `/bl`**
   - same one-process parallel build
   - compare wall-clock time, CPU samples, and event throughput

2. **A/B compare import modes**
   - `/bl:ProjectImports=None`
   - `/bl:ProjectImports=Embed`
   - `/bl:ProjectImports=ZipFile`
   - this isolates the import-capture component

3. **Profile the binary logger hot path**
   - sample:
     - `BinaryLogger.Write`
     - `BuildEventArgsWriter.Write`
     - `BuildEventArgsWriter.HashString`
     - `BuildEventArgsWriter.WriteNameValueList`
     - `BuildEventArgsWriter.FlushRecordToFinalStream`
   - check inclusive CPU in compression / stream write calls too

4. **Measure sensitivity to verbosity / task inputs**
   - compare builds with `/bl` under reduced event volume vs highly chatty builds
   - verify how much extra task-input / message volume changes cost

5. **Measure shutdown tail**
   - compare time spent after build completion with `ProjectImports=Embed` vs `None`
   - this should expose collector wait + archive embedding cost

6. **Check storage / compression sensitivity**
   - local fast disk vs slower storage
   - larger builds with many structured payloads

The strongest confirmation would be a measurable `/bl` regression accompanied by significant sampled time in `BinaryLogger` / `BuildEventArgsWriter` and, optionally, visible shutdown-tail cost from `ProjectImportsCollector`.
