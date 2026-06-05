# BuildManager and Scheduler coordination

Canonical candidate: `BuildManager._syncLock` and the scheduler coordination path behind it.

## Why shared

This is shared build-wide state in the strongest possible sense:

- `BuildManager` has one `_syncLock`, and its comment explicitly says it protects `BuildManager` shared data **and the Scheduler** (`src\Build\BackEnd\BuildManager\BuildManager.cs:63-66`).
- `BuildManager` creates one `_workQueue` `ActionBlock<Action>` for its control plane (`src\Build\BackEnd\BuildManager\BuildManager.cs:679-681`).
- Node packets are routed into that queue through `INodePacketHandler.PacketReceived` (`src\Build\BackEnd\BuildManager\BuildManager.cs:1447-1449`).
- The scheduler holds global build coordination state in `SchedulingData`: executing, blocked, yielding, ready, and unscheduled requests; node assignments; configuration ownership; and build-event history (`src\Build\BackEnd\Components\Scheduler\SchedulingData.cs:20-109`).

So the shared object is effectively the entrypoint build coordinator: one queue, one lock, one scheduler, one global request graph.

## Why it might bottleneck

The candidate is plausible because frequent parallel-build control events all funnel through one serialized coordinator:

- submissions entering the build,
- worker nodes reporting blockers,
- worker nodes reporting results,
- resource releases,
- node creation/shutdown,
- submission/logging completion callbacks.

Those events do not just flip a bit. They can run full scheduler cycles, cache checks, request/result propagation, and node-creation logic before the next control event is processed.

The realistic bottleneck story is therefore:

1. many worker-side events arrive during a parallel build,
2. they queue behind one `BuildManager` work queue,
3. each queued action frequently executes under `_syncLock`,
4. scheduler work scans global request/node state,
5. queue delay grows, and worker nodes wait longer to get the next decision/result.

That is a credible throughput limiter for builds with many small or highly connected project requests.

## Evidence

### BuildManager serializes packet/control processing

- `_workQueue` is the control-plane queue (`src\Build\BackEnd\BuildManager\BuildManager.cs:679-681`).
- `PacketReceived` posts packet handling onto that queue (`src\Build\BackEnd\BuildManager\BuildManager.cs:1447-1449`).
- `ProcessWorkQueue` runs one queued action at a time (`src\Build\BackEnd\BuildManager\BuildManager.cs:1799-1819`).
- `ProcessPacket` takes `_syncLock` and dispatches blocker/result/resource/shutdown handling (`src\Build\BackEnd\BuildManager\BuildManager.cs:1851-1887`).

### Scheduler routing runs under the shared coordination path

- top-level build submission issuance is posted to `_workQueue`, then takes `_syncLock`, then calls `HandleNewRequest(Scheduler.VirtualNode, blocker)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2053-2096`)
- `HandleNewRequest` calls `_scheduler.ReportRequestBlocked(...)` and `PerformSchedulingActions(...)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2588-2617`)
- `HandleResult` calls `_scheduler.ReportResult(...)` and `PerformSchedulingActions(...)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2670-2726`)
- resource release also routes through `_scheduler.ReleaseCores(...)` and `PerformSchedulingActions(...)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2622-2642`)

### Scheduler reruns global scheduling passes after common events

- `ReportRequestBlocked` ends by calling `ScheduleUnassignedRequests(responses)` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:348-415`)
- `ReportResult` ends by calling `ScheduleUnassignedRequests(responses)` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:421-536`)
- `ReleaseCores` also calls `ScheduleUnassignedRequests` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:669-676`)
- `ReportNodesCreated` also calls back into scheduling (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:544-571`)

### Scheduler work is scan-heavy

The main scheduling pass:

- resumes ready work,
- scans available nodes to find idle nodes,
- scans unscheduled requests to assign work,
- may request new nodes,
- validates blocked-request situations.

See `ScheduleUnassignedRequests` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:735-845`).

Cost-bearing details include:

- explicit comment that one blocked-request path is `O(# nodes * closure of requests blocking current set of blocked requests)` (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:808-811`)
- plan-based heuristics scanning unscheduled requests for each idle node (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:993-1030`)
- in-proc traversal/proxy preference copying and scanning the unscheduled set (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1050-1075`)
- configuration-count levelling sorting nodes and then scanning schedulable requests (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1089-1122`)
- max-waiting-requests heuristics recursively computing waiting closure (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1212-1275,2220-2234`)
- result handling scanning unscheduled requests again to find other requests satisfied by the same result (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:478-535`)

### Additional lock amplifiers

These are secondary but real:

- `HandleResult` can synchronously wait for project-cache plugin result processing via `.Wait()` on the shared path (`src\Build\BackEnd\BuildManager\BuildManager.cs:2692-2722`)
- `PerformSchedulingActions` can create nodes while still in the coordination path, then immediately recurse into `_scheduler.ReportNodesCreated(...)` (`src\Build\BackEnd\BuildManager\BuildManager.cs:2899-2917`)
- `NodeManager.CreateNodes` delegates to provider node creation / acquisition logic rather than a trivial local counter update (`src\Build\BackEnd\Components\Communications\NodeManager.cs:91-113,308-338`)

## Likelihood

**Medium-high overall.**

More specifically:

- **High confidence** that this is a shared serialized coordination hotspot.
- **Medium confidence** that it is a dominant end-to-end bottleneck in typical builds.

Why not simply “high”? Because packet handling is already serialized by the `ActionBlock`, so the issue may show up more as queue backlog and coordinator saturation than as dramatic monitor contention on `_syncLock` itself.

## Expected contention mode

The expected contention mode is:

- **single-coordinator serialization**
- **queue backlog**
- **amortized scan cost per event**
- **occasional direct monitor contention from non-queue callers**

This is **not** primarily a “tiny hot lock hit by many threads at once” story. The more realistic failure mode is:

1. many worker events enqueue,
2. one coordinator thread drains them,
3. each event spends time in scheduler/cache/action-routing logic,
4. later events wait in the queue,
5. nodes can sit idle or blocked longer than necessary waiting for the next coordination decision.

`_syncLock` still matters, but mostly because it defines the exclusive region around that centralized coordinator work.

## Where it is used

Representative uses in `BuildManager`:

- build startup / initialization (`src\Build\BackEnd\BuildManager\BuildManager.cs:543-688`)
- cache reset (`src\Build\BackEnd\BuildManager\BuildManager.cs:902-917`)
- project-instance-for-build lookup (`src\Build\BackEnd\BuildManager\BuildManager.cs:926-937`)
- submission creation / registration (`src\Build\BackEnd\BuildManager\BuildManager.cs:966-980`)
- submission execution / configuration resolution (`src\Build\BackEnd\BuildManager\BuildManager.cs:1503-1585`)
- packet processing (`src\Build\BackEnd\BuildManager\BuildManager.cs:1851-1887`)
- submission issue to scheduler (`src\Build\BackEnd\BuildManager\BuildManager.cs:2053-2096`)
- cache result handling (`src\Build\BackEnd\BuildManager\BuildManager.cs:2524-2555`)
- scheduling actions and node creation (`src\Build\BackEnd\BuildManager\BuildManager.cs:2873-2944`)
- submission completion / SDK cache cleanup (`src\Build\BackEnd\BuildManager\BuildManager.cs:2951-3017`)

Representative scheduler entry points:

- blocker handling (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:348-415`)
- result handling (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:421-536`)
- node-created handling (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:544-571`)
- core release (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:669-676`)
- main reschedule pass (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:735-845`)

## Why it may or may not matter in practice

### Why it may matter

It is most likely to matter when the build has:

- many short-lived project requests,
- many child-project requests generated quickly,
- frequent cache hits / result propagation,
- many ready/blocked transitions,
- enough nodes that scheduler scans are repeated often,
- in-proc proxy/traversal work that keeps decisions on the main process,
- project-cache plugin activity that lengthens the critical path.

In those shapes, worker execution can become cheap relative to coordinator work, making the serialized control plane visible.

### Why it may not matter

It may not dominate when:

- project/task execution is long relative to coordination work,
- node count is modest,
- request graphs are shallow,
- most scheduler cycles see only a small unscheduled set,
- project-cache plugins are off,
- the build is bottlenecked elsewhere (evaluation, logging, task execution, filesystem).

Also, because the queue already serializes packet handling, runtime traces may show modest direct monitor contention even when the coordinator is still the throughput limit. The symptom may be queue delay rather than lock wait time.

## How to validate

1. **Measure queue delay**
   - instrument enqueue/dequeue timestamps around `_workQueue.Post(...)` / `ProcessWorkQueue(...)`
   - track `_workQueue.InputCount` over time

2. **Measure time spent in the serialized coordination region**
   - time `ProcessPacket`
   - time `HandleNewRequest`, `HandleResult`, `HandleResourceRequest`
   - time `_scheduler.ReportRequestBlocked`, `_scheduler.ReportResult`, `_scheduler.ReleaseCores`
   - time `PerformSchedulingActions`

3. **Measure scheduler cost as state size grows**
   - record unscheduled / blocked / ready / executing counts per scheduling cycle
   - correlate scheduler-cycle time with those counts and node count

4. **Correlate coordinator delay with worker idleness**
   - look for periods where nodes are idle or waiting while the BuildManager queue is non-empty

5. **Test build shapes likely to expose the issue**
   - many small projects
   - high fan-out `ProjectReference` graphs
   - cache-heavy builds
   - multi-threaded / in-proc parallel builds
   - project-cache-plugin-on vs off

6. **Try targeted experiments**
   - move feature-specific waits (for example project-cache result handling) out of the serialized region where correctness allows
   - batch or coalesce scheduler reruns
   - compare before/after queue depth, scheduler time, and total build time

## Bottom line

This candidate remains strong, but the refined claim is narrower and more actionable:

> The likely bottleneck is the serialized BuildManager scheduler control plane (`_workQueue` + `_syncLock` + `Scheduler`), with `_syncLock` serving as the exclusive coordination boundary around scan-heavy global scheduling work.

That makes it a good candidate for runtime instrumentation and validation in Wave 3.
