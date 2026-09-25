# Evaluation cache acceptance contract

## Status

This document is the **required acceptance contract** for an opt-in evaluation cache under the
timestamp-and-length metadata model described below. The current prototype remains opt-in and makes
no claim of production readiness. Requirements below are normative within that model; the final
section separately describes present implementation evidence and known gaps.

The cache reuses a full, file-based project evaluation. It is not a build-output cache: normal target
scheduling and incremental-build checks still apply after a hit. A feature that works correctly may
conservatively miss for many or all projects; this contract promises correctness when enabled, not a
universal hit rate or speedup.

## Activation

`MSBUILDEVALUATIONCACHEMODE` is the controlling opt-in and currently recognizes these values:

| Value | Contract |
| --- | --- |
| `Disabled` | No recording, lookup, or reuse. Explicit `Disabled` overrides legacy switches; explicit-mode diagnostic status may still be emitted. |
| `Record` | Record evaluation inputs for investigation only; never reuse an evaluation. |
| `SnapshotUnsafe` | Benchmark-only reuse without the safety contract. It is nonconforming and is not an intended supported user opt-in. |
| `SnapshotFileSystem` | Prototype checked reuse. It may become supported only after satisfying this contract. |

With the controlling variable and both legacy switches unset, the feature MUST be off. While the
prototype retains `MSBUILDRECORDEVALUATIONINPUTS` and
`MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE`, their existing behavior remains compatibility-only; an explicit
controlling value takes precedence. An invalid controlling value MUST fail closed to `Disabled` and
produce a clear diagnostic without enabling recording or reuse. Activation handling is unchanged by
this specification. The fully unconfigured path remains quiet; explicitly selecting a comparison mode
is distinct from leaving the feature unconfigured.

"Checked mode" means the eventual conforming checked opt-in in this document. It does
not rename `SnapshotFileSystem` or any public API.

Restore-scoped requests carrying the `MSBuildRestoreSessionId` global property bypass
recording, snapshot lookup, and admission in snapshot modes. Their per-invocation
identity prevents cross-restore reuse, so retaining them would evict reusable build
snapshots. Explicit `Record` mode continues to record these evaluations. Subsequent
normal builds still validate retained snapshots against current inputs, including
restore-generated imports; no part of the request key is ignored.

## Correctness invariant

A cached evaluation MUST be accepted only when it is semantically equivalent to a fresh, current,
full evaluation for the same request and all evaluation inputs. Equivalence includes every relevant:

- property, item, item definition, metadata value, target, using-task registration, and default or
  initial target;
- diagnostic or failure that can affect build correctness; and
- evaluation-dependent state later consumed while executing targets.

A key or matching hash identifies a candidate; it is never permission to reuse it. Validation MUST
fail closed whenever equivalence cannot be established. Reuse MUST NOT change unsupported modes,
bypass normal target scheduling or incrementality, mask a real build error, or fabricate evaluation
events or durations for a hit. Cache telemetry MUST describe reuse rather than pretending a new
evaluation occurred.

The retained snapshot is canonical cache state. Materialization MUST isolate it from later mutations:
changes made while building one `ProjectInstance` MUST NOT flow back into the snapshot or into any
other request. Concurrent materializations MUST preserve the same isolation.

## Initially supported requests

Initial eligibility is deliberately narrow: full, file-based project requests for which every input
effect can be captured and validated. The following MUST bypass this snapshot cache unless their complete
semantics have been explicitly proven and covered by the acceptance matrix:

- unsaved or in-memory project XML and unverifiable cached `ProjectRootElement` provenance;
- caller-supplied or transferred `ProjectInstance` state and partial evaluations;
- custom or hosted file systems whose observations cannot be replayed by the validator; and
- unknown, volatile, process-wide, or otherwise unclassified evaluation effects.

For file-based requests, fallback performs the normal fresh evaluation. For caller-supplied XML,
already-evaluated instances, transferred state, or partial evaluation, bypass preserves the existing
uncached API behavior and supplied state; it MUST NOT discard that state and reread a disk project.
Fallback is valid enabled behavior. "Works" does not imply that every project is eligible, hits the
cache, or becomes faster.

## Candidate identity

Candidate identity MUST distinguish every request value that can affect evaluation, including project
path, global and command-line property provenance, effective and explicit toolset selection, engine
version, load settings, evaluation stage/profile flags, logical startup and working directories,
culture/UI culture, parser/change-wave configuration, and relevant environment state.

The working directory is the context actually observed by evaluation; it MUST NOT be normalized into
a preferred directory merely to increase hits. In the normal strict multithreaded path,
`TaskEnvironment` supplies the project directory. Direct or project-cache paths without that context
can observe a per-build sentinel and conservatively miss. That is an effectiveness gap, not evidence
of a default multithreaded correctness bug.

## Input validity

Before accepting an entry, checked mode MUST establish that all evaluation inputs remain valid:

- root project and imported file contents;
- positive and negative existence probes;
- directories and search sets used by globs, including additions, deletions, and renames;
- environment and registry reads;
- SDK/resolver selection, results, resolver state, and effective toolsets; and
- restore-generated projects, props, targets, assets, and other generated evaluation inputs.

An SDK result retained for validation MUST be immutable, copied into immutable owned data, or make the
entry non-reusable. Checked mode validates file and directory kind, last-write timestamp, and file
length. Its supported operating model assumes relevant edits change timestamp or length and that
inputs are not concurrently written during validation and materialization. Same-size edits that
preserve timestamps, timestamp aliasing, and concurrent writers are outside this guarantee. Content
hashing, file-system watchers, detours, and an atomic file-system snapshot are not requirements of
this prototype.

If an input is unknown, unstable, or unprovable, the request MUST receive a fresh evaluation and an
observable bypass reason. That reason belongs in opt-in diagnostic/status output and MUST NOT add
noise to the default, disabled path.

Conflicting observations detected while recording still make an evaluation non-reusable. The cache
does not claim correctness for concurrent writers outside the operating assumptions above.

## Failure behavior

Failed validation, unverifiable cached `ProjectRootElement` provenance, changed restore inputs,
recording failure, or a non-critical cache error MUST make the request evaluate fresh; the old entry
MUST NOT serve that request. If the fresh evaluation fails, its original failure and diagnostics MUST
be preserved. Cache fallback MUST NOT convert failure into success or suppress normal build errors.

Cancellation and critical failures, including out-of-memory conditions, MUST retain normal MSBuild
semantics and MUST NOT be swallowed by cache fallback. Routine misses and validation rejection MUST
not become warnings or errors.

## Ownership, lifecycle, and the disabled path

Cache ownership MUST remain within its `BuildManager` component host and process. Reuse across builds
served by that manager, server reuse, in-proc execution, and out-of-proc nodes MUST have explicit,
tested boundaries; entries and mode state MUST NOT leak across hosts or processes. Mode changes
between builds MUST not reuse state created under an incompatible policy.

`BuildManager.ResetCaches`, component shutdown/disposal, and equivalent host reset paths MUST clear or
invalidate all owned entries consistently. Out-of-proc transfer MUST not implicitly transfer cache
ownership.

When the feature is unconfigured or `Disabled`, no recorder or snapshot cache may be created for it.
The unconfigured path MUST NOT emit cache status. Explicit `Disabled` may emit truthful opt-in status,
without doing recording or cache work. Shared changes to project provenance, globbing, or evaluation
seams still require compatibility validation; this contract does not claim literally zero total overhead.

## Memory contract

Admission accounting MUST include the retained snapshot, validation manifest, and all owned payloads.
Retention MUST be bounded, with defined, concurrency-safe oversize rejection and eviction that can
reduce hit rate but never change build results. Entries MUST NOT keep unbounded shared mutable
aliases alive.

The prototype's 256 MiB default is an internal configured cache budget, not a promise that process RSS
is capped at 256 MiB. Conservative estimates cover cache-owned keys, snapshots, and validation
payloads; allocator, collection-capacity, and general runtime overhead are not an RSS accounting model.

## Acceptance matrix

Each row is a correctness gate, primarily a fresh-versus-cached differential test. Tests MUST run with
the feature enabled and disabled, and on supported runtimes where behavior differs. Existing
performance measurements are not proof of any row.

| Scenario | Required result |
| --- | --- |
| Unchanged eligible full file project | Hit is equivalent in properties, items/metadata, targets, task registrations, and build result. |
| Root or import changes normally | Old entry is rejected; fresh evaluation observes the change. |
| Root or import changes under the supported metadata model | Timestamp or length changes reject the old entry; same-metadata edits are outside the stated guarantee. |
| Glob set changes or a missing probe appears | Add/delete/rename and missing-now-exists cases reject; fresh evaluation sees the new search set. |
| Global/command-line properties, load/profile flags, directory, culture, or environment changes | Candidate differs or validation rejects; result matches fresh. |
| Relevant registry read changes | Validation rejects or the request is explicitly non-reusable. |
| SDK/resolver result, resolver state, or toolset changes | Validation rejects or the request is explicitly non-reusable. |
| Restore-generated import or asset changes | Old entry is not used, including restore/build sequences in one process. |
| Built-instance mutation and concurrent requests | Canonical state remains unchanged and materializations are isolated. |
| In-memory/unsaved/transferred/partial/custom-host request | Bypass preserves uncached API semantics and supplied state; no replacement from the file-based snapshot cache without separate proof. |
| Logger, evaluation profile, and diagnostics | Hit/miss diagnostics remain truthful; no synthetic evaluation timing/events; build diagnostics remain equivalent. |
| Strict multithreaded, direct, and project-cache/plugin contexts | Actual context is keyed; unsupported context falls back rather than receiving a wrong hit. |
| Budget, eviction, oversize, reset, disposal, and server reuse | Lifetime and accounting stay bounded; misses caused by cleanup remain correct. |
| Unset, explicit disabled, invalid, and legacy activation | Unconfigured is quiet/off; explicit disabled wins and may report status without caching; invalid fails closed with a diagnostic; legacy behavior does not leak into explicit modes. |

## Current prototype evidence and blockers

This table records present behavior; it does not weaken the requirements above.

| Area | Present evidence and gap |
| --- | --- |
| Activation | [`Traits.cs`](../../../src/Framework/Traits.cs) implements the four modes, explicit precedence, legacy mapping, and fail-closed invalid parsing. [`BuildManager.cs`](../../../src/Build/BackEnd/BuildManager/BuildManager.cs) intentionally reports explicit `Disabled` status; the unconfigured path is quiet. Activation is not evidence of complete enabled-path correctness. |
| Identity | [`ProjectInstanceSnapshotCacheKey.cs`](../../../src/Build/BackEnd/Components/Caching/ProjectInstanceSnapshotCacheKey.cs) covers many request, toolset, parser, directory, culture, and environment fields. Key equality is intentionally not reuse permission, and complete effect coverage is not established. |
| Recorded inputs | [`EvaluationInputs.cs`](../../../src/Build/Evaluation/Context/EvaluationInputs.cs) and [`EvaluationInputRecorder.cs`](../../../src/Build/Evaluation/Context/EvaluationInputRecorder.cs) represent paths, environment/registry observations, SDK results, and non-cacheable reasons. Full changed-input coverage has not been demonstrated. |
| Validation | [`EvaluationInputValidator.cs`](../../../src/Build/Evaluation/Context/EvaluationInputValidator.cs) checks cacheability, direct environment reads using platform environment-name casing, and recorded path kind, timestamp, and length after key agreement. Registry-dependent evaluations are recorded completely but conservatively ineligible. Successful SDK dependencies are re-resolved in their recorded context after cheaper checks and compared before acceptance; failed SDK observations are ineligible because ignored failures emit import diagnostics. |
| SDK ownership | The recorder copies path, version, additional paths, properties, items and metadata, environment additions, warnings, and errors into immutable owned observations. A rejected SDK validation result is fed to the immediate fresh evaluation so resolver diagnostics are deferred and replayed once rather than emitted during validation or resolved twice. |
| Diagnostics | Evaluations that emit warnings, errors, or custom SDK logger messages are non-reusable. This avoids silently dropping evaluation diagnostics on a hit without introducing a general diagnostic replay framework. |
| Integration and provenance | [`BuildRequestConfiguration.cs`](../../../src/Build/BackEnd/Shared/BuildRequestConfiguration.cs) rejects unsaved/unknown-length cached roots for recorded files and rejects any cached root for a recorded-missing path, preserves caller/transferred/in-memory fallback behavior, and propagates cancellation and build aborts. Complete coverage for every host and restore workflow remains an acceptance-matrix gap; the agreed same-metadata and concurrent-writer cases are operating-model exclusions rather than release blockers. |
| Snapshot isolation | [`ProjectInstanceSnapshotCacheEntry.cs`](../../../src/Build/BackEnd/Components/Caching/ProjectInstanceSnapshotCacheEntry.cs) stores a snapshot plus validation data. Snapshot clone/mutation tests and checked-versus-fresh concurrent request tests cover materialization isolation. |
| Memory and lifecycle | [`ProjectInstanceSnapshotCache.cs`](../../../src/Build/BackEnd/Components/Caching/ProjectInstanceSnapshotCache.cs) has LRU eviction, oversize rejection, clear, and shutdown paths. Admission includes the owned key, snapshot, file/environment manifest, SDK payload, and registry payload with overflow-safe conservative estimates. The configured byte budget remains retained-payload accounting, not an RSS cap. |
| Test coverage | [`ProjectInstanceSnapshotCache_Tests.cs`](../../../src/Build.UnitTests/BackEnd/ProjectInstanceSnapshotCache_Tests.cs) exercises prototype modes, environment and SDK revalidation, registry ineligibility, diagnostic admission, cancellation, memory accounting, and selected cache behavior. [`ProjectInstanceSnapshotCacheAcceptance_Tests.cs`](../../../src/Build.UnitTests/BackEnd/ProjectInstanceSnapshotCacheAcceptance_Tests.cs) compares checked reuse and fallback with independent current-input controls across rich project state/build output, input mutations, invalid inputs, restore-generated imports, request shapes, concurrency, lifecycle, activation, registry, and local default SDK resolution. This evidence does not imply that all projects or hosts are eligible or achieve hits. |
