# Wave 2 logbook - BuildManager and Scheduler

## Scope and question

Deep dive on the canonical execution candidate formed by `BuildManager._syncLock` plus the scheduler coordination path behind it:

- what code paths hit the shared state
- whether scheduler work is routed through one shared coordinator
- whether scheduler scans / cache checks / scheduling actions can become a throughput limiter
- what the most realistic contention story is in one-process multi-project parallel builds

## Key source findings

### 1. The shared object is broader than the monitor alone

The core shared coordinator is not just the `_syncLock` monitor; it is:

1. one `BuildManager` instance,
2. one `ActionBlock<Action>` work queue,
3. one `_syncLock` protecting `BuildManager` shared state **and the Scheduler**, and
4. one `Scheduler` / `SchedulingData` instance holding global request/node/configuration state.

Evidence:

- `_syncLock` comment explicitly says it protects `BuildManager` shared data and the Scheduler (`src\Build\BackEnd\BuildManager\BuildManager.cs:63-66`)
- `_workQueue` is created as a single `ActionBlock<Action>` (`src\Build\BackEnd\BuildManager\BuildManager.cs:679-681`)
- node packets are posted into that queue (`src\Build\BackEnd\BuildManager\BuildManager.cs:1447-1449`)
- packet processing then enters `_syncLock` and dispatches to scheduler-affecting handlers (`src\Build\BackEnd\BuildManager\BuildManager.cs:1851-1887`)
- scheduler state is centralized in `SchedulingData` dictionaries/sets for executing, blocked, ready, unscheduled requests, node assignments, and configuration ownership (`src\Build\BackEnd\Components\Scheduler\SchedulingData.cs:20-109`)

### 2. Runtime scheduler work is funneled through the BuildManager control plane

The steady-state control path is:

`node packet / submission / cache result / logging callback -> BuildManager work queue -> _syncLock -> scheduler method -> PerformSchedulingActions`

Evidence:

- top-level submissions are posted to `_workQueue`, then take `_syncLock` and call `HandleNewRequest(Scheduler.VirtualNode, blocker)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2053-2096`)
- cache-result completions are posted to `_workQueue`, then take `_syncLock` and either issue a real build request or complete from cache (`src\Build\BackEnd\BuildManager\BuildManager.cs:2524-2555`)
- project-started / project-finished logging callbacks also post back into `_workQueue` before taking `_syncLock` (`src\Build\BackEnd\BuildManager\BuildManager.cs:3110-3146`)
- `HandleNewRequest` calls `_scheduler.ReportRequestBlocked(...)` under the shared coordination path and immediately runs `PerformSchedulingActions(...)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2588-2617`)
- `HandleResult` calls `_scheduler.ReportResult(...)` and then `PerformSchedulingActions(...)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2670-2726`)
- resource-release packets call `_scheduler.ReleaseCores(...)` and then `PerformSchedulingActions(...)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2622-2642`)

### 3. Scheduler cycles are triggered often and can do non-trivial global work

The scheduler reruns its main scheduling pass after the events that naturally happen often in a parallel build:

- request blocked / yielded / generated child requests
- result received
- resource release
- node created

Evidence:

- `ReportRequestBlocked` ends with `ScheduleUnassignedRequests(responses)` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:348-415`)
- `ReportResult` ends with `ScheduleUnassignedRequests(responses)` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:421-536`)
- `ReleaseCores` also reruns `ScheduleUnassignedRequests` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:669-676`)
- `ReportNodesCreated` also reruns scheduling (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:544-571`)

Inside `ScheduleUnassignedRequests`, the scheduler:

- resumes ready work,
- scans all available nodes to find idle nodes,
- scans unscheduled requests to assign new work,
- may request new nodes,
- may do additional blocked-request validation.

Evidence:

- central loop in `ScheduleUnassignedRequests` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:735-845`)
- explicit comment that one blocked-request validation path is `O(# nodes * closure of requests blocking current set of blocked requests)` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:808-811`)

### 4. The scheduler’s hot operations are mostly scan-heavy, not lock-free point lookups

Important scheduler heuristics repeatedly enumerate the global request sets:

- plan-based scheduling scans unscheduled requests for each idle node (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:993-1030`)
- in-proc traversal / proxy preference copies and scans the unscheduled set (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1050-1075`)
- configuration-count levelling sorts nodes and then scans schedulable requests (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1089-1122`)
- max-waiting-requests heuristics recursively compute transitive waiting closure (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1212-1275,2220-2234`)
- result handling scans unscheduled requests to find other requests satisfied by the same result (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:478-535`)

This looks like amortized control-plane work that grows with request/node counts, rather than a constant-time routing layer.

### 5. The main practical limiter is coordinator serialization, not pure monitor convoying

Important nuance: packet handlers are already serialized by the `ActionBlock` before they hit `_syncLock` (`src\Build\BackEnd\BuildManager\BuildManager.cs:679-681,1447-1449,1799-1819`). That weakens the simple story of “many node threads pile up directly on `_syncLock`”.

The more realistic story is:

- many worker-side events enqueue onto one coordinator queue,
- queued actions then execute scheduler/cache/routing work under `_syncLock`,
- if a scheduling cycle is expensive, later packets/results/submissions wait in the queue,
- external callers that still use `_syncLock` directly can additionally block behind the queued coordinator work.

So the bottleneck shape is more “single control-plane funnel / queue backlog / serialized coordination” than “high-contention micro-lock in a hot inner loop”.

### 6. There are a few lock amplifiers inside the coordinated path

These are not the core story, but they can lengthen `_syncLock` hold times:

- `HandleResult` may synchronously wait for project-cache plugin result processing via `.Wait()` while still on the shared path (`src\Build\BackEnd\BuildManager\BuildManager.cs:2692-2722`)
- `PerformSchedulingActions` can create nodes under the lock, then immediately recurse back into scheduler handling for `ReportNodesCreated` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2899-2917`)
- `NodeManager.CreateNodes` delegates to provider creation / acquisition logic, so this startup work is not obviously trivial (`src\Build\BackEnd\Components\Communications\NodeManager.cs:91-113,308-338`)

These look more like startup-burst or feature-specific amplifiers than the default steady-state issue.

## Answer to the deep-dive questions

### What code paths hit this shared state?

- build startup and cache initialization (`BeginBuild`)
- submission creation / configuration resolution / request issuance
- node packet processing for blockers, results, resource requests, node shutdowns
- cache result completion callbacks
- logging callbacks that complete submission logging state
- submission completion / SDK cache cleanup

Primary references:

- `src\Build\BackEnd\BuildManager\BuildManager.cs:543-688,966-980,1503-1585,1851-1887,2053-2132,2524-2555,2873-3048,3110-3146`

### How often is it likely to be touched?

Very often during a parallel build:

- every new top-level request,
- every child-project request,
- every result packet,
- every yield/reacquire/block transition,
- every resource release that can unblock more work,
- every node creation event.

This is enough to make scheduler cycle cost and queue latency matter even if individual cycles are modest.

### Is the shared state read-mostly, write-heavy, or mixed?

Mixed, but biased toward mutation-heavy coordination:

- scheduler state moves requests between unscheduled / ready / executing / blocked / completed sets
- node/configuration ownership is updated as work moves
- results/config caches are consulted and updated
- BuildManager submission / node / logging state is updated on packet handling and completion

### Is synchronization narrow and cheap, or could it serialize meaningful work?

It can serialize meaningful work. The scheduler does global scans, cache lookups, request/result propagation, and action emission in the same serialized control path. It is not just guarding a small dictionary mutation.

### What evidence moves the candidate up or down?

**Moves it up**

- work-queue depth spikes during builds with many small projects
- long time in `ProcessPacket -> Handle* -> Scheduler -> PerformSchedulingActions`
- worker nodes becoming idle while the entrypoint queue still has pending work
- scheduler time scaling with unscheduled-request count or node count

**Moves it down**

- queue stays shallow even in highly parallel builds
- most build time remains inside long worker-node task execution
- scheduling cycles remain tiny relative to task/project execution time

## Deep-dive conclusion

This candidate remains strong, but the refined claim is:

> The most realistic throughput limiter is the serialized BuildManager scheduler control plane (`_workQueue` + `_syncLock` + `Scheduler`), not `_syncLock` viewed in isolation.

`_syncLock` is still important because it defines the exclusive coordination region, but the main performance risk is that frequent scheduler-triggering events funnel through one queue and one scheduler state machine whose work scales with active request graph size. That makes this candidate worth a full report and later runtime validation.
