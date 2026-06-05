# Most impactful findings

This summary reflects the consolidated stopping point after three Wave 2 deep-dive batches.

## Entry / configuration setup

- **Main-node SDK resolution funnel** remains the strongest startup-side coordination risk in this stage. The refined story is bursty fan-in: many projects waiting on the same `Lazy<SdkResult>` for a small set of common SDK names, plus cold manifest/resolver loading under the service lock.

## Project load and evaluation

- **ProjectRootElementCache** remains one of the strongest candidates after Wave 2. The refined story is a **real but bounded** throughput limiter: strongest during startup bursts over shared imports, then milder ongoing pressure through the global hit-path lock and strong-cache maintenance.
- **ConditionEvaluator** is still real, but narrower than it first looked. The code definitely serializes full evaluation for identical condition keys, yet the impact is **per-key selective**, so it currently ranks below `ProjectRootElementCache`.

## Scheduling / execution coordination

- **BuildManager._syncLock + scheduler coordination** remains the strongest execution-side candidate. The refined model is a **serialized control-plane bottleneck**: one queue, one exclusive coordination region, and repeated scheduler passes whose cost can grow with request/node state.
- **ResultsCache** remains a real candidate, but it now looks **medium** and probably secondary to the control-plane bottleneck because much traffic is already serialized before it hits the cache.
- **In-proc BuildRequestEngine** is also real, but more localized than it first appeared: serial per in-proc node, yet weakened in MT builds because the process can host multiple in-proc engines.

## Logging / event forwarding

- **LoggingService** is a real serialization point, but its practical rank moved down slightly after Wave 2. It looks **medium** overall for one-process parallel builds: important when event volume and logger cost are high, less likely to dominate when logging stays light.
- **BinaryLogger** is a strong **conditional** candidate: **medium-high when `/bl` is enabled**, otherwise absent. Its practical effect is mostly extra work on the already-shared logging lane.
- **ProjectImportsCollector** is a secondary `/bl` cost center: medium when import embedding is on, mostly as serialized background archive work and a shutdown tail.
- **`BuildEventArgTransportSink` + `LogMessagePacket`** turned out to be the clearest example of a candidate that matters more for multi-node/OOP builds than for the user’s target in-proc scenario.

## Cross-stage top findings so far

1. `ProjectRootElementCache`
2. `BuildManager._syncLock` + scheduler coordination
3. `ConditionEvaluator`
4. `LoggingService`
5. `BinaryLogger` (conditional on `/bl`)
6. `ToolLocationHelper.s_locker`

## Overall take

The strongest recurring pattern is **shared coordination or cache layers that combine real work with serialization**:

1. **Evaluation/import reuse layers** such as `ProjectRootElementCache`
2. **Central execution control-plane coordination** via `BuildManager` and `Scheduler`
3. **Selective hot-key serialization** such as `ConditionEvaluator`
4. **Central logging lanes** that become more expensive under `/bl`, verbose logging, or import embedding

At this point the investigation has enough coverage to support the current findings set without needing the deferred lower-priority batch.
