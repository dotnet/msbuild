# ProjectImportsCollector

## Why shared

`BinaryLogger` owns one `projectImportsCollector` instance per logger and routes all collected import-related files through it (`src\Build\Logging\BinaryLogger\BinaryLogger.cs:143`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:380-383`). The logger subscribes once to `eventSource.AnyEventRaised`, and `Write(...)` calls `CollectImports(...)` for all relevant events before writing the binlog record (`src\Build\Logging\BinaryLogger\BinaryLogger.cs:467`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:584-607`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:616-638`). That makes the collector a shared per-build path for all projects when `/bl` import collection is enabled.

## Why it might bottleneck

The collector deliberately serializes all file capture into one chain of background tasks. That avoids holding build threads during file reads and zip writes, but it also means import embedding throughput cannot scale with project parallelism. The likely cost is a combination of short foreground enqueue contention, one globally serialized background archive pipeline, and a shutdown tail when the logger waits for the queue to drain and then embeds the completed archive into the binlog.

## Evidence

- Shared collector owned by the binary logger:
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:143`
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:380-383`
- All build events flow through one logger `Write(...)` path:
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:467`
  - `src\Build\Logging\BinaryLogger\BinaryLogger.cs:584-607`
- Import-related events forwarded into the collector:
  - `ProjectImportedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:618-621`
  - `ProjectStartedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:622-625`
  - `MetaprojectGeneratedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:626-629`
  - `ResponseFileUsedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:630-633`
  - `GeneratedFileUsedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:634-637`
  - `EmbedInBinlog` item path: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:413-416`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:471-476`, `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:1065-1084`
- Serialized collector design:
  - one `_processedFiles` set, one `_zipArchive`, one `_fileStream`, one `_currentTask`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:25-38`
  - comment describing a chain of file-write tasks running sequentially on a background thread: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:36-37`
  - enqueue under `lock (_fileStream)`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:120-136`
  - continuation chaining on `TaskScheduler.Default`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:128-130`
  - actual work deferred to `TryAddFile()`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:139-152`
  - no extra locking needed only because work is chained linearly: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:155-159`, `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:181-184`
- Expensive work serialized on that chain:
  - duplicate/path checks / existence checks: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:214-237`
  - source file open: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:167-168`
  - zip entry creation: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:239-248`
  - stream copy into the archive: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:208-212`
- Default `/bl` behavior enables the path by default:
  - `CollectProjectImports` default is `Embed`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:298`
  - parsed parameters also default to `Embed`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:20-35`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:652-655`
  - unit tests verify default `Embed`: `src\Build.UnitTests\BinaryLogger_Tests.cs:727-739`
  - CLI test verifies default imports are included unless `ProjectImports=None` is specified: `src\MSBuild.UnitTests\XMake_Tests.cs:2901-2915`
- Shutdown tail:
  - binary logger closes the collector during shutdown: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:504-524`
  - `Close()` waits for `_currentTask`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:283-287`
  - default `Embed` mode then reads the completed archive and writes it into the binlog blob: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:514-520`, `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:262-280`
  - embed mode uses a temp archive rather than direct streaming because blob size must be known up front: `src\Build\Logging\BinaryLogger\BuildEventArgsReader.cs:394-397`
  - temp-archive handling is designed to survive shutdown cache cleanup: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:66-75`, `src\Build.UnitTests\BinaryLogger_Tests.cs:556-587`, `src\MSBuild\XMake.cs:1737-1740`

## Likelihood

**Medium.** This is a real shared serialization path, and it is on by default for `/bl` because project imports default to `Embed`. But the risk is more about globally serialized background work and end-of-build tail latency than about long foreground lock holds on project threads. So it is credible, but it is probably a secondary throughput limiter compared with candidates that directly serialize active evaluation or execution work.

## Expected contention mode

- **Foreground:** brief contention on `lock (_fileStream)` while callers append to `_currentTask`
- **Background:** one-at-a-time import capture work for duplicate checks, file opens, archive entry creation, and stream copies
- **Shutdown:** explicit wait for all queued work, followed by archive-to-binlog copy in `Embed` mode

In practice, the most likely symptom is a serialized import-capture pipeline and a noticeable shutdown tail, not a large critical section held during project execution.

## Where it is used

- Activated by the binary logger when project-import collection is enabled: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:380-383`
- Default `/bl` behavior uses `ProjectImports=Embed` unless explicitly overridden: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:298`, `src\Build.UnitTests\BinaryLogger_Tests.cs:727-739`
- Triggered from:
  - project import events during evaluation: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:618-621`, `src\Build\Evaluation\Evaluator.cs:640`, `src\Build\Evaluation\Evaluator.cs:2234-2244`, `src\Build\Evaluation\Evaluator.cs:2265-2277`, `src\Build\Evaluation\Evaluator.cs:2320-2332`
  - project started events: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:622-625`
  - metaproject generation: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:626-629`
  - response files and generated files: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:630-637`
  - explicit `EmbedInBinlog` items: `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:1065-1084`

## Why it may or may not matter in practice

Why it **may** matter:

- `/bl` enables the path by default through embedded imports
- every collected file funnels through one shared collector pipeline
- the collector serializes real file/archive work instead of allowing concurrent archive writes
- large multi-project builds can generate many import events and therefore many queued archive operations
- shutdown must wait for the queue to drain and, in `Embed` mode, copy the entire archive into the binlog

Why it **may not** matter:

- build threads usually do not perform the expensive collector I/O directly; they mostly enqueue work under a short lock
- the dominant cost may appear only at the end of the build rather than as steady-state project-thread contention
- builds with small import graphs or `ProjectImports=None` will not stress this path much
- other logging bottlenecks, such as central event writing itself, may dominate before collector enqueue contention does

Net: `ProjectImportsCollector` is a plausible `/bl` cost center, but mainly as serialized background I/O plus shutdown finalization, not as a classic long-held global lock on active worker threads.

## How to validate

1. Instrument `ProjectImportsCollector.AddFileHelper(...)` to measure:
   - time waiting to enter `lock (_fileStream)`
   - queue depth / pending continuation count
   - enqueue rate by event kind
2. Instrument the background chain to measure per-file time for:
   - duplicate/path checks
   - file open/read
   - zip entry creation
   - stream copy
3. Measure `BinaryLogger.Shutdown()` time separately, especially:
   - time spent in `ProjectImportsCollector.Close()`
   - time spent in `ProcessResult(...)`
   - size of the embedded archive blob
4. Compare:
   - `/bl`
   - `/bl;ProjectImports=None`
   - `/bl;ProjectImports=ZipFile`
5. For large parallel builds, correlate total shutdown tail with:
   - number of project/import events
   - number of unique collected files
   - final `.ProjectImports.zip` size

If most of the extra time appears in shutdown and archive embedding, this candidate should be understood primarily as tail serialization. If project threads spend meaningful time waiting on `lock (_fileStream)`, upgrade it as a live-build contention point.
