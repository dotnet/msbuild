---
applyTo: "src/Build/BackEnd/**"
---

# Execution engine

## State and scheduling

- Establish thread/process ownership before adding synchronization. Distinguish in-process nodes, worker processes, multithreaded nodes, and external TaskHosts; they do not have the same isolation.
- Preserve `BuildManager` lifecycle invariants across initialization failure, cancellation, shutdown, and repeated builds. Invalid lifecycle calls should follow existing validation, not acquire a silent recovery path.
- For scheduler changes, construct the actual request/target sequence and check deadlock, starvation, yield/unyield, and cached-result behavior.
- Document lock order when introducing interacting locks. A mutable field is not itself proof of a race; trace all relevant owners and callbacks.

## Caching and IPC

- Trace configuration identity and target results through the actual caches rather than assuming a single `(path, properties, targets)` key. See [Results Cache](../../documentation/wiki/Results-Cache.md).
- Identify which peers can actually connect before changing packet layout. Inspect handshake/reuse compatibility and any negotiated version; do not assume arbitrary old/new workers support rolling updates.
- Preserve packet ordering and read/write symmetry for supported peers. Do not assume old streams contain optional fields or that a new version number alone makes them compatible.
- SDK resolver changes can affect cached state across evaluations/builds. Define its owner and invalidation boundary rather than banning all persistent state.

## Evidence and deeper context

Use representative in-proc, worker-process, MT, or TaskHost cases according to the changed boundary, not all modes for every edit. For an executable claim, use the [bootstrap skill](../skills/use-bootstrap-msbuild/SKILL.md).

Read [node orchestration](../../documentation/wiki/Nodes-Orchestration.md), [logging internals](../../documentation/wiki/Logging-Internals.md), or the [threading spec](../../documentation/specs/threading.md) only for the relevant investigation. Check design prose against current implementation.
