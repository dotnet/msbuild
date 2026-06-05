# Candidate bottlenecks

Wave 1 broad scan is complete. The entries below are the normalized candidate list after consolidating the four scope logbooks.

This file will become the canonical catalog of all plausible shared-state bottlenecks found during the investigation.

## Stage buckets

### Entry / configuration setup

- Main-node SDK resolution funnel  
  Refined judgment: **medium**. Strongest as a bursty one-process shared wait via same-submission same-SDK `Lazy<SdkResult>` fan-in plus cold manifest/resolver loading under `SdkResolverService._lockObject`, not as a steady-state packet funnel. See `logbooks\wave1-execution.md`, `logbooks\wave2-sdkresolution.md`, and `reports\sdkresolution.md`.

- Project-cache plugin initialization / query path  
  Low-Medium candidate. Shared plugin initialization and query state may serialize followers, but this is feature-dependent. See `logbooks\wave1-execution.md`.

- `BuildEnvironmentHelper` singleton  
  Medium-likelihood candidate. First-touch environment and VS discovery can stall parallel callers during startup, but this looks more like a startup burst than a steady-state limiter. See `logbooks\wave1-framework.md`.

### Project load and evaluation

- `ConditionEvaluator.s_cachedExpressionTrees` / per-condition expression pool locks  
  Refined judgment: **medium**. Shared expression cache definitely serializes full evaluation for identical `(ParserOptions, condition string)` keys, but the impact is selective rather than universal because contention is per-key, not global. See `logbooks\wave1-evaluation.md`, `logbooks\wave2-conditionevaluator.md`, and `reports\conditionevaluator.md`.

- `ProjectRootElementCache` + `ProjectCollection.s_projectRootElementCache`  
  Refined judgment: **medium-high**. Shared XML/import cache is a real, bounded bottleneck candidate: strongest on startup bursts over common imports via per-file locks, with additional hit-path pressure through the global `_locker` and O(n) strong-cache maintenance. See `logbooks\wave1-evaluation.md`, `logbooks\wave2-projectrootelementcache.md`, and `reports\projectrootelementcache.md`.

- Shared `EvaluationContext` glob/filesystem caches  
  Medium-likelihood candidate. Shared evaluation contexts can funnel wildcard expansion and filesystem probes through common caches and per-key locking. See `logbooks\wave1-evaluation.md`.

- `FileMatcher` static glob-expansion cache  
  Medium-Low candidate. If process-wide enumeration caching is enabled, identical wildcard misses can serialize behind per-key expansion locks. See `logbooks\wave1-framework.md`.

### Scheduling / execution coordination

- `BuildEventArgTransportSink` + `LogMessagePacket` remote event path  
  Refined judgment: **low for pure one-process builds** and more like **medium** for multi-node/OOP builds. This is a real per-event transport tax, but it is largely bypassed in the in-proc-only scenario. See `logbooks\wave1-logging.md`, `logbooks\wave2-eventtransport.md`, and `reports\eventtransport.md`.

- `BuildManager._syncLock`  
  Refined judgment: **medium-high**. The stronger story is the serialized BuildManager control plane (`_workQueue` + `_syncLock` + `Scheduler`), where frequent control events can accumulate queue backlog and scan-heavy coordination cost. See `logbooks\wave1-execution.md`, `logbooks\wave2-buildmanager-scheduler.md`, and `reports\buildmanager-scheduler.md`.

- `Scheduler` / `SchedulingData`  
  Refined judgment: **medium-high** as part of the same serialized control-plane candidate. Centralized scan-heavy scheduling passes matter most when frequent worker events keep rerunning scheduling through the shared BuildManager coordinator. See `logbooks\wave1-execution.md`, `logbooks\wave2-buildmanager-scheduler.md`, and `reports\buildmanager-scheduler.md`.

- `ResultsCache` coarse locking around `_resultsByConfiguration`  
  Refined judgment: **medium**. Shared result-cache operations still use a coarse lock around add/merge/satisfaction work, but much traffic is already serialized before reaching the cache, making it likely secondary to the BuildManager/scheduler control plane. See `logbooks\wave1-execution.md`, `logbooks\wave2-resultscache.md`, and `reports\resultscache.md`.

- In-proc `BuildRequestEngine` funnel  
  Refined judgment: **medium**. Real localized serial coordinator per in-proc node, but weaker as a whole-process bottleneck because MT mode can host multiple in-proc nodes/engines. See `logbooks\wave1-execution.md`, `logbooks\wave2-buildrequestengine.md`, and `reports\buildrequestengine.md`.

- Graph-build coordination lock in `BuildManager`  
  Medium-Low candidate. Shared graph-build bookkeeping lock may serialize unblock/completion coordination on large graphs. See `logbooks\wave1-execution.md`.

### Logging / event forwarding

- `LoggingService` central fan-in and serialized delivery pipeline  
  Refined judgment: **medium**. `LoggingService` is a real single-lane serialization point in both sync and async modes, but practical impact in one-process parallel builds depends heavily on event volume, verbosity, and logger mix. See `logbooks\wave1-logging.md`, `logbooks\wave2-loggingservice.md`, and `reports\loggingservice.md`.

- `BinaryLogger.Write` / `BuildEventArgsWriter`  
  Refined judgment: **medium-high when `/bl` is enabled** and none otherwise. The main issue is additive expensive logger work on the shared logging lane rather than raw writer-lock contention alone. See `logbooks\wave1-logging.md`, `logbooks\wave2-binarylogger.md`, and `reports\binarylogger.md`.

- `ProjectImportsCollector`  
  Refined judgment: **medium when `/bl` import collection is enabled**. The real cost is a serialized background import-capture pipeline plus an explicit shutdown/embed tail, more than long foreground lock holds on worker threads. See `logbooks\wave1-logging.md`, `logbooks\wave2-projectimportscollector.md`, and `reports\projectimportscollector.md`.

- `TerminalLogger` live renderer  
  Low-Medium candidate. Shared UI lock and terminal output device can add rendering contention, but it currently looks weaker than the main logging fan-in paths. See `logbooks\wave1-logging.md`.

- `LogMessagePacketBase.s_writeMethodCache`  
  Medium candidate. Shared reflection-method cache uses one lock for packet serialization lookups on the logging/event path. See `logbooks\wave1-framework.md`.

### Cross-cutting framework services

- `ToolLocationHelper.s_locker`  
  Refined judgment: **medium**. One global lock still fronts several heavy discovery families, but the highest-risk paths are concentrated in SDK/platform/VS/framework-discovery APIs; prominent reference-assembly APIs are less concerning because they mostly use split-phase locking. See `logbooks\wave1-framework.md`, `logbooks\wave2-toollocationhelper.md`, and `reports\toollocationhelper.md`.

- `CoreClrAssemblyLoader._guard`  
  Refined judgment: **medium-low**. The lock is coarse but instance-scoped per subsystem loader, so it looks more like startup/plugin-initialization serialization than a top-tier steady-state build bottleneck. See `logbooks\wave1-framework.md`, `logbooks\wave2-coreclrassemblyloader.md`, and `reports\coreclrassemblyloader.md`.

## Wave 2 progress

Completed first deep-dive batch:

1. `ProjectRootElementCache` / `ProjectCollection.s_projectRootElementCache`
2. `ConditionEvaluator` expression cache / pool locking
3. `BuildManager._syncLock` and scheduler coordination
4. `LoggingService` central fan-in and serialized delivery

Selected second deep-dive batch:

1. `ToolLocationHelper.s_locker`
2. `ResultsCache`
3. `BinaryLogger.Write` / `BuildEventArgsWriter`
4. `CoreClrAssemblyLoader._guard`

Selected third deep-dive batch:

1. `Main-node SDK resolution funnel`
2. `BuildEventArgTransportSink` + `LogMessagePacket`
3. `ProjectImportsCollector`
4. `In-proc BuildRequestEngine`

Current stopping point:

- Three deep-dive batches completed.
- Strong report set now exists for the highest-impact and medium-impact candidates.
- Lower-priority follow-ups are deferred unless a later phase needs broader coverage.
