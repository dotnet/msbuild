# In-proc BuildRequestEngine

## Why shared

`BuildRequestEngine` is the per-node coordinator for build requests, results, and configuration transactions. For in-proc nodes, `InProcNode` constructs one engine and routes build packets through it (`src\Build\BackEnd\Node\InProcNode.cs:111-123`, `src\Build\BackEnd\Node\InProcNode.cs:436-443`). The engine then serializes its own work through a single `ActionBlock<Action>` (`src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:231-235`).

Important scope note: this is shared **per in-proc node**, not once for the entire process.

## Why it might bottleneck

Two design choices create serialization pressure:

- the engine’s work queue is single-threaded
- the engine allows only one active `BuildRequestEntry` at a time

Additionally, the scheduler explicitly prefers some request classes (traversals and proxy builds) on in-proc nodes, which can concentrate certain work on these serial coordinators (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:871-875`, `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1041-1048`).

## Evidence

- single-threaded action queue: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:107-116`, `231-235`
- one active request per engine:
  - `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:784-793`
  - `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:837-853`
  - `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEntry.cs:29-40`
- request-entry global lock is used around multi-step state mutation:
  - engine-side lock regions: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:1107-1144`, `1172-1349`
  - entry-side lock regions: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEntry.cs:222-231`, `240-263`, `311-385`, `391-401`, `409-423`, `429-466`, `480-503`, `509-520`
- scheduler prefers in-proc assignment for traversals/proxy requests:
  - `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:871-875`
  - `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1041-1070`
  - `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:2253-2259`
- in MT mode, multiple in-proc nodes can exist:
  - `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1541-1544`
  - `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1619-1638`
  - `src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:346-384`
- expensive cleanup can run on the engine thread after request completion:
  - `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:829-833`
  - `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:883-965`

## Likelihood

**Medium**.

This is a real serialization mechanism, but it is weaker than a process-global choke point because:

- the engine is per-node rather than process-global
- MT mode can create multiple in-proc nodes
- most heavy execution work happens in request builders / targets / tasks, not in the engine loop itself

The candidate becomes stronger when the workload is dominated by control-plane coordination rather than task execution.

## Expected contention mode

- serialized request progression within one in-proc node
- especially for traversal/proxy-heavy work that the scheduler prefers to keep in-proc
- possible long-tail stalls if `CheckMemoryUsage()` runs on the engine thread

This is best thought of as a **control-plane serialization bottleneck**, not a general task-execution bottleneck.

## Where it is used

- in-proc node packet handling routes requests to the engine: `src\Build\BackEnd\Node\InProcNode.cs:436-443`
- each in-proc node owns one engine: `src\Build\BackEnd\Node\InProcNode.cs:111-123`
- in-proc nodes are created by `NodeProviderInProc`: `src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:346-384`
- scheduler preferentially sends traversal/proxy work to in-proc nodes:
  - `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:871-875`
  - `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1041-1070`

## Why it may or may not matter in practice

### Why it may matter

- only one active request runs per engine, so request-level continuation is serialized inside a node
- some request classes are intentionally funneled toward in-proc nodes
- memory-pressure cleanup on the engine thread can be expensive and stall progression

### Why it may not matter much

- MT mode can scale out to multiple in-proc nodes, each with its own serial engine
- heavy work generally happens below the engine in request builders, target builders, and task execution
- `BuildRequestEntry.GlobalLock` is per-entry, so it does not create one giant cross-engine lock

Overall judgment: this is more likely to matter as a **localized scheduling bottleneck** than as a dominant throughput limiter across the whole one-process build.

## How to validate

1. Instrument `BuildRequestEngine` to record:
   - queue length and queue wait time in `QueueAction`
   - time spent in `EvaluateRequestStates`
   - time spent in `IssueUnsubmittedRequests`
   - time spent in `IssueBuildRequests`
   - time spent in `CheckMemoryUsage`
2. Record per-engine metrics, not just process totals.
3. Run one-process MT builds with:
   - traversal-heavy graphs
   - proxy-build-heavy scenarios
   - normal SDK-style project graphs
4. Check whether:
   - one in-proc engine has persistent queue buildup
   - in-proc-preferred requests cluster disproportionately on a subset of nodes
   - `CheckMemoryUsage` causes visible request-progression stalls
5. Compare MT vs non-MT behavior to separate “single in-proc engine” effects from the multi-engine case.

This candidate should move up only if traces show meaningful time queued behind one in-proc engine or measurable throughput improvement from reducing in-proc request concentration.
