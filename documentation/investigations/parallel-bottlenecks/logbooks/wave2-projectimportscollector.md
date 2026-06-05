# Wave 2 logbook - ProjectImportsCollector

## Candidate under investigation

- Canonical candidate: `Microsoft.Build.Logging.ProjectImportsCollector`
- Stage: logging / event forwarding, with evaluation-time reach when `/bl` enables import capture

## Shared path shape

- `BinaryLogger` owns a single `projectImportsCollector` field for the logger instance: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:143`
- It creates that collector during initialization when project-import collection is enabled and the logger is not replaying an existing binlog: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:380-383`
- The logger subscribes once to `eventSource.AnyEventRaised` and routes all build events through `Write(...)`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:467`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:584-607`
- `Write(...)` calls `CollectImports(...)` before writing the event record, and `CollectImports(...)` forwards multiple event kinds into the shared collector:
  - `ProjectImportedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:618-621`
  - `ProjectStartedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:622-625`
  - `MetaprojectGeneratedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:626-629`
  - `ResponseFileUsedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:630-633`
  - `GeneratedFileUsedEventArgs`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:634-637`
- `BuildEventArgsWriter` can also feed files into the same collector via `EmbedInBinlog` items: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:413-416`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:471-476`, `src\Build\Logging\BinaryLogger\BuildEventArgsWriter.cs:1065-1084`

## Import embedding defaults

- `BinaryLogger.CollectProjectImports` defaults to `ProjectImportsCollectionMode.Embed`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:298`
- Parsed logger parameters also default to `Embed` unless `ProjectImports=` is explicitly specified: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:20-35`, `src\Build\Logging\BinaryLogger\BinaryLogger.cs:652-655`
- Unit tests confirm plain `/bl`-style parameter strings use `Embed` by default: `src\Build.UnitTests\BinaryLogger_Tests.cs:727-739`
- CLI-level test coverage also assumes the default binlog includes imports unless `ProjectImports=None` is explicitly used: `src\MSBuild.UnitTests\XMake_Tests.cs:2901-2915`
- During initialization, the binary logger forces project-import logging on by setting `MSBUILDLOGIMPORTS=1` and `Traits.Instance.EscapeHatches.LogProjectImports = true`: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:345-355`
- Evaluation checks that trait to decide whether to emit `ProjectImportedEventArgs`: `src\Build\Evaluation\Evaluator.cs:640`

## Collector serialization model

- `ProjectImportsCollector` has one `_processedFiles` set, one `_zipArchive`, one `_fileStream`, and one `_currentTask` chain: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:25-38`
- The file comments explicitly describe the design as a linear chain of file-write tasks running sequentially on a background thread: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:36-37`
- `AddFileHelper(...)` takes `lock (_fileStream)` and, in the default background mode, appends a continuation onto `_currentTask`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:120-131`
- The continuation is scheduled on `TaskScheduler.Default`, so each add is serialized through task chaining rather than parallel archive writes: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:128-130`
- The actual work happens later in `TryAddFile()` / `addFileWorker(...)`, not while the caller holds the lock: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:139-152`
- The implementation remarks for `AddFileCore(...)` and `AddFileFromMemoryCore(...)` say they need no synchronization only because they are called from a task that is chained linearly: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:155-159`, `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:181-184`

## What is serialized

- Dedup / existence / normalization:
  - `_processedFiles.Contains(filePath)`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:217-220`
  - file existence check: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:222-226`
  - full-path normalization: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:230-233`
  - `_processedFiles.Add(filePath)`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:235-236`
- Archive creation / write:
  - open source file: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:167-168`
  - create zip entry: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:239-248`
  - copy data into zip entry: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:208-212`

So the design serializes not just archive mutation but also the pre-checks and file I/O that precede it.

## Background chaining consequences

- Good news: the build/event thread usually only pays for a short `lock (_fileStream)` plus continuation creation, not the full file read + zip write path: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:122-135`
- Less good: the collector deliberately limits all archive work to one chain, so import capture throughput cannot scale with project parallelism
- That means the likely symptom is **background lag and shutdown wait**, not long foreground lock holds on every project thread
- Because each add schedules a continuation, very import-heavy builds also pay per-file task-chaining overhead in addition to the actual I/O

## Shutdown tail / embed path

- `BinaryLogger.Shutdown()` closes the collector before finishing the binlog: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:504-524`
- `ProjectImportsCollector.Close()` waits for all pending background work to finish via `_currentTask.Wait()`: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:283-287`
- In the default `Embed` mode, shutdown then re-opens the resulting archive and writes it into the binlog as a blob: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:514-520`, `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:262-280`
- This means default `/bl` does:
  1. background file collection into a zip archive
  2. a mandatory wait for that queue at shutdown
  3. a second pass that copies the completed archive into the main binlog stream
- For `Embed`, the collector does **not** write directly into the binlog. It writes a temporary archive first because the embedded blob length must be known up front: `src\Build\Logging\BinaryLogger\BuildEventArgsReader.cs:394-397`
- The temp-archive choice is intentional: for non-zip-file mode, the archive is stored under `FileUtilities.TempFileDirectory` instead of the cache directory because shutdown clears the cache directory: `src\Build\Logging\BinaryLogger\ProjectImportsCollector.cs:66-75`
- Regression test coverage confirms the archive must survive `FileUtilities.ClearCacheDirectory()` long enough for `ProcessResult(...)` to embed it: `src\Build.UnitTests\BinaryLogger_Tests.cs:556-587`
- `XMake` clears the cache directory during final shutdown: `src\MSBuild\XMake.cs:1737-1740`

## How repeated import events arise

- Binary logger initialization forces project-import logging on: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:345-355`
- Evaluation checks that flag and emits import events: `src\Build\Evaluation\Evaluator.cs:640`
- `Evaluator` has multiple `ProjectImportedEventArgs` emission sites across different import outcomes / paths, not just one narrow case:
  - `src\Build\Evaluation\Evaluator.cs:1712`
  - `src\Build\Evaluation\Evaluator.cs:1831`
  - `src\Build\Evaluation\Evaluator.cs:2032`
  - `src\Build\Evaluation\Evaluator.cs:2093`
  - `src\Build\Evaluation\Evaluator.cs:2234-2244`
  - `src\Build\Evaluation\Evaluator.cs:2265-2277`
  - `src\Build\Evaluation\Evaluator.cs:2320-2332`
- Because `CollectImports(...)` also adds `ProjectStartedEventArgs`, every project contributes at least its main file even before counting imports: `src\Build\Logging\BinaryLogger\BinaryLogger.cs:622-625`

## Most important finding

- The collector is indeed a **single serialized pipeline** for import embedding
- But the strongest issue is **not** a wide hot lock that holds project threads during I/O
- Instead, the design trades off foreground latency for:
  - one shared enqueue lock on the event thread
  - one sequential background chain that performs all import file work
  - one shutdown wait / embedding tail at the end
- So the candidate is more credible as a **tail-latency and serialized background-work** issue than as a major steady-state foreground contention point

## Overall assessment

- Structural issue: **real**
- Primary contention mode:
  - shared enqueue lock on every collected file
  - globally serialized background file/archive work
  - shutdown tail when `_currentTask.Wait()` drains remaining work and embed mode copies the archive blob
- Likelihood for one-process multi-project parallel builds using default `/bl`: **medium**
  - moves up when builds have many imported files, many projects, or a large embedded archive
  - moves down when import sets are small or binlog collection is disabled / set to `ProjectImports=None`
- Important nuance:
  - likely less harmful during active execution than candidates that hold a global lock across expensive work on project threads
  - more likely to show up as finalization lag and background I/O serialization

## Escalation decision

- **Escalate: yes**
- Reason: `/bl` defaults to embedded imports, the collector is a single shared pipeline by design, and the shutdown tail is explicit in code. The report should make clear that this is probably more of an end-of-build serialization cost than a dominant active-build lock bottleneck.
