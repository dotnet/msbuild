# CI Logging Build Event State

**Status:** Proposed

**Related:** #14649, #14651, PR #14642

## Context

Terminal Logger currently combines correlated build state with terminal presentation state.
PR #14642 introduces `BuildEventTracker` to separate these responsibilities.

The current PR still stores some facts in both `BuildEventTracker` and
`TerminalProjectInfo`.
Both types independently store project success and diagnostic counts.
This duplication creates two possible sources of truth.

The current callback contract also creates a complete `ProjectSnapshot` for each
tracked event.
Terminal Logger discards many message events after it receives the snapshot.
This order adds unnecessary work to a high-volume logging path.

Terminal Logger has presentation rules that do not change the underlying build
facts.
For example, Terminal Logger displays authentication-provider warnings
immediately but excludes them from project and build summaries.

## Decision

`BuildEventTracker` is the only mutable owner of correlated build facts.
`TerminalProjectInfo` owns only Terminal Logger presentation state and policy.

### Tracker-owned state

`BuildEventTracker` owns:

- evaluation and project correlation;
- the composite project context key;
- project identity and evaluated dimensions;
- current target;
- project completion and success;
- raw correlated error and warning counts; and
- immutable point-in-time project snapshots.

Raw diagnostic counts include all correlated warning and error events.
The tracker does not apply logger-specific display or summary rules.

### Terminal-owned state

`TerminalProjectInfo` owns:

- the latest immutable project snapshot;
- elapsed, yielded, and resumed time;
- output paths and source roots used for display;
- test-project and cache-plugin display flags;
- formatted terminal messages; and
- the number of warnings excluded by Terminal Logger summary policy.

`TerminalProjectInfo` does not store mutable project success or raw diagnostic
counts.
It does not copy mutable lifecycle state from the tracker.

Terminal Logger derives summary counts from tracker facts and presentation
policy:

```text
summary error count = tracker error count

summary warning count =
    tracker warning count
    - warnings excluded by Terminal Logger policy
```

Authentication-provider warnings remain tracker facts.
Terminal Logger records only their exclusion from its summaries.

### Callback contract

Tracker callbacks use two payload forms.

Project lifecycle callbacks receive complete immutable snapshots:

- project started;
- project finished.

Other callbacks receive a nullable `ProjectContextKey` and the original event:

- target started and finished;
- task started and finished;
- message raised;
- warning raised;
- error raised.

`BuildEventTracker` provides an on-demand lookup:

```csharp
internal bool TryGetProjectSnapshot(
    ProjectContextKey contextKey,
    out ProjectSnapshot snapshot);
```

The tracker updates its state before it raises the related callback.
Consumers can query the latest state only when they need it.

The callback contract does not use lazy snapshot delegates.
Lazy delegates add complexity and can introduce capture allocations.
Their validity after callback completion would also be unclear.

### State lifetime

Tracked project and evaluation state is build-scoped.
It remains available while `BuildFinishedTracked` callbacks run so consumers can
obtain final authoritative snapshots.
The tracker clears the state after those callbacks complete.

Cleanup occurs in a `finally` block.
The tracker therefore releases build state even if a consumer callback throws.
`Detach` also clears state so cancellation, incomplete builds, and logger
shutdown do not retain project or evaluation data.

Consumers that require data after build completion retain immutable snapshots.
They do not depend on the tracker's mutable dictionaries remaining populated.

### Message filtering

`BuildEventTracker` does not apply Terminal Logger importance or verbosity
rules.
Future consumers can require events that Terminal Logger ignores.

The tracker extracts a lightweight context key directly from the message event.
It does not validate the key against tracked project state or perform a project
dictionary lookup.
Terminal Logger applies its filters before it performs project lookups or
snapshot queries.

No complete `ProjectSnapshot` is created for message, warning, error, target,
or task callbacks.
Snapshots are created at lifecycle boundaries or through explicit lookup.

The message context key identifies the event context, not the availability of a
tracked project snapshot.
After a consumer accepts a message, it can call `TryGetProjectSnapshot` or use
its own context-keyed state.
This distinction keeps the high-volume message path lightweight and lets future
CI loggers apply their own filtering before requesting project state.

### Build totals

The tracker owns correlated diagnostic counts for each project.
Terminal Logger separately counts only uncorrelated diagnostics that affect its
summary.

At build completion, Terminal Logger calculates totals from:

```text
uncorrelated summary diagnostics
    + presentation-adjusted counts from final project snapshots
```

Terminal Logger does not increment a second aggregate for correlated project
diagnostics.
This rule prevents drift and duplicate counting.

### Project context identity

`ProjectContextKey` contains `NodeId` and `ProjectContextId`.
MSBuild defines `ProjectContextId` as unique within a node.
The combined values identify a project request across the event stream.

Two nodes can report the same project context ID:

```text
(NodeId: 1, ProjectContextId: 7) -> A.csproj
(NodeId: 2, ProjectContextId: 7) -> B.csproj
```

The composite key prevents events for these projects from being combined.

### MSBuild task-name comparison

Terminal and forwarding loggers compare the `MSBuild` task name with
`StringComparison.OrdinalIgnoreCase`.
MSBuild task names are case-insensitive identifiers.

The comparison is allocation-free and operates on a short constant.
Correct identifier handling takes precedence over a case-sensitive
micro-optimization.

## Consequences

### Benefits

- Correlated project state has one mutable source of truth.
- Immutable snapshots provide stable observations for logger consumers.
- Terminal-specific filtering does not affect provider-independent build facts.
- Ignored message events do not create or copy complete snapshots.
- Ignored message events do not perform tracker project dictionary lookups.
- Final diagnostic totals cannot drift from tracker project state.
- Future CI loggers can reuse correlation without inheriting Terminal Logger
  policy.
- Long-lived logger hosts do not retain project and evaluation state after a
  build or detachment.

### Costs

- Terminal Logger must retain and refresh the latest immutable snapshot.
- Some selected callbacks require an on-demand tracker lookup.
- Build totals require one pass over tracked projects at build completion.
- Authentication-warning exclusions remain additional terminal presentation
  state.

The final project pass is proportional to the number of tracked projects.
It replaces duplicated mutable aggregates and does not affect message-event
hot paths.

## Rejected alternatives

### Keep diagnostic counts in both types

This design permits the counts to diverge after filtering or callback changes.
It also makes ownership unclear.

### Move Terminal Logger filtering into the tracker

This design couples provider-independent event tracking to one presentation.
It can also discard events required by future consumers.

### Pass complete snapshots for every event

This design copies project state before consumers determine whether they need
it.
The cost occurs on high-volume message paths.

### Validate every message context against tracker state

This design performs a project dictionary lookup before consumers apply their
message filters.
The event already contains the context identity, so validation can occur later
through explicit snapshot lookup when a consumer needs tracked state.

### Let Terminal Logger store only included warning counts

This design creates another mutable representation of correlated warnings.
An exclusion count represents terminal policy without duplicating raw facts.

### Remove `NodeId` from the project key

This design relies on an implementation detail instead of the documented
node-local identifier contract.
It can correlate events to the wrong project.

## Implementation plan

1. Change non-lifecycle tracker callbacks to pass project context keys.
2. Add on-demand immutable snapshot lookup to `BuildEventTracker`.
3. Remove duplicate lifecycle and diagnostic state from `TerminalProjectInfo`.
4. Derive terminal summary counts from snapshots and warning exclusions.
5. Replace correlated build aggregates with computed final totals.
6. Keep the composite project context key and clarify its documentation.
7. Apply case-insensitive task-name matching to central and forwarding loggers.
8. Update tests for state ownership, filtering, counts, and multi-node identity.

## Required tests

1. Earlier snapshots remain unchanged after tracker state changes.
2. Tracker state is updated before each callback.
3. Non-lifecycle callbacks report keys without creating complete snapshots.
4. Message callbacks report event context keys without requiring tracked
   project state.
5. Correlated warnings and errors are counted exactly once.
6. Uncorrelated diagnostics are counted exactly once.
7. Authentication warnings appear immediately but do not affect summaries.
8. Mixed authentication and ordinary warnings produce the correct total.
9. Duplicate project context IDs on different nodes remain separate.
10. Central and forwarded `MSBuild` task events accept casing variants.
11. Existing Terminal Logger approved output remains unchanged.
12. Final tracker state is available during build-finished callbacks and is
    cleared afterward, including when a callback throws.
13. Detachment clears project and evaluation state for incomplete builds.

## Acceptance criteria

- `BuildEventTracker` is the only mutable owner of correlated lifecycle state.
- `BuildEventTracker` is the only mutable owner of raw correlated counts.
- `TerminalProjectInfo` contains only snapshots and presentation state.
- Ignored messages require no complete project snapshot.
- Ignored messages require no tracker project dictionary lookup.
- Terminal summary policy does not change tracker facts.
- Existing console and binary logging behavior remains unchanged.
