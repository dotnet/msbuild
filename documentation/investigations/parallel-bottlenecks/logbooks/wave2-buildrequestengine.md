# Wave 2 logbook - In-proc BuildRequestEngine

## Candidate

- Canonical object: in-proc `BuildRequestEngine`
- Stage: scheduling / execution coordination
- Investigated concerns:
  - single-threaded `ActionBlock<Action>`
  - `BuildRequestEntry.GlobalLock` interactions
  - scheduler preference for in-proc work
  - whether this meaningfully limits one-process multi-project parallel builds

## Core structural findings

### The engine is intentionally single-threaded per node

`BuildRequestEngine` explicitly assumes one-thread-at-a-time execution:

- class comment: it runs asynchronously on its own thread and manages request state centrally: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:29-40`
- work queue is a plain `ActionBlock<Action>(action => action.Invoke())`: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:231-235`
- unresolved config maps are documented as safe without extra synchronization because of the ActionBlock single-threaded context: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:107-116`

Conclusion: one engine processes control-plane state transitions serially by design.

### Only one active request can exist per engine at a time

`EvaluateRequestStates()` enforces a single active request per engine:

- exactly one `BuildRequestEntryState.Active` entry is allowed: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:784-793`
- when no active request exists, only the first ready entry is resumed/activated: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:837-853`
- `BuildRequestEntry` state comments also define `Active` as singular: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEntry.cs:29-40`

Conclusion: build execution inside one engine is serialized at request-entry granularity.

## In-proc topology findings

### One engine per in-proc node, not one engine per process

- `InProcNode` constructs its own `BuildRequestEngine` instance: `src\Build\BackEnd\Node\InProcNode.cs:111-123`
- `NodeProviderInProc` creates a new `InProcNode` on a dedicated thread: `src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:346-384`
- `BuildComponentFactoryCollection` marks `BuildRequestEngine` as singleton **within a component host**, not process-global: `src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs:76`

Conclusion: the candidate is shared per node, not globally across all in-proc nodes.

### Multi-threaded mode weakens the bottleneck substantially

The scheduler can create multiple in-proc nodes in MT mode:

- `maxInProcNodeCount = MaxNodeCount` when `MultiThreaded` is true, otherwise 1: `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1541-1544`
- scheduler may request creation of multiple in-proc nodes: `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1619-1638`

Conclusion: in the target one-process multi-project build scenario, the process can host multiple serial engines in parallel, which reduces the risk that one engine becomes the whole-process bottleneck.

## In-proc preference findings

### Scheduler explicitly prefers some work on in-proc nodes

- traversal requests are assigned to the in-proc node first if possible: `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:871-875`, `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1034-1070`
- proxy build requests are also preferentially assigned to the in-proc node: `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1041-1048`, `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:2253-2259`
- comments say proxy builds are cheap and not worth IPC/re-evaluation out of proc: `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1042-1044`

Conclusion: even if multiple in-proc nodes can exist, certain request classes are intentionally funneled toward them.

### Each scheduling pass assigns at most one request to a given idle in-proc node in the “prefer in-proc” helper

`AssignUnscheduledRequestsToInProcNode(...)`:

- checks whether the in-proc node is idle
- scans schedulable requests
- assigns the first matching one
- removes the in-proc node from the idle set and breaks

Evidence: `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1050-1070`.

Conclusion: this is not itself a bug, but it reinforces the “single active request per engine” shape.

## Request-entry/global-lock findings

### `GlobalLock` protects long multi-step mutations, but the engine already serializes most control flow

- each `BuildRequestEntry` has its own `GlobalLock`: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEntry.cs:131-149`
- most state-mutating entry methods lock it: `WaitForBlockingRequest`, `ResolveConfigurationRequest`, `ReportResult`, `Unblock`, `Continue`, `BeginCancel`, `Complete`, private `WaitForResult`: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEntry.cs:222-231`, `240-263`, `311-385`, `391-401`, `409-423`, `429-466`, `480-503`, `509-520`
- the engine also explicitly takes `issuingEntry.GlobalLock` around multi-step request issuance / waiting transitions: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:1107-1144`, `1172-1349`

Conclusion: the extra locking exists because request builders can callback into the engine asynchronously, but the lock itself does not look like the primary bottleneck. The stronger serialization point is the engine’s own one-at-a-time request activation.

## What expensive work is inside the serial path?

Inside the single-threaded engine path:

- request bookkeeping and state transitions
- cache lookups against config/results caches
- potential synchronous memory-pressure cleanup with disk writes and GC on completed-entry paths: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:829-833`, `883-965`

This last point is notable: memory-pressure handling can be very expensive, and it runs in the engine thread after request completion.

Conclusion: normal control-plane work is modest, but `CheckMemoryUsage()` is a genuine long-tail stall source if triggered.

## Overall judgment

Evidence moving the candidate **up**:

- each engine is single-threaded and admits only one active request at a time
- scheduler intentionally prefers traversal/proxy work to in-proc nodes
- expensive cache write / GC behavior can run on the engine thread

Evidence moving it **down**:

- MT mode can create multiple in-proc nodes, each with its own engine
- the engine mostly coordinates control flow; heavy target/task execution happens in request builders, not in the engine loop
- `GlobalLock` contention appears secondary to the action-block serialization

## Final conclusion

`In-proc BuildRequestEngine` is a **real scheduling-serialization point, but not obviously a top-tier whole-process bottleneck** in MT builds.

- In non-MT mode it is inherently serial and can clearly bottleneck.
- In MT mode, the risk becomes narrower: workloads heavy in traversal/proxy requests or memory-pressure cleanup may still pile onto in-proc engines, but the process can host multiple such engines.

Current decision:

- **Likelihood**: medium
- **Most likely contention mode**: serialized control-plane/request progression on one in-proc engine, especially for traversal/proxy-heavy work or if memory-pressure cleanup fires
- **Escalation**: justified for a report, but weaker than a truly process-global shared cache
