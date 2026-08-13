# Evaluation observation layer design

Status: proposed

Prototype: [dotnet/msbuild#14689](https://github.com/dotnet/msbuild/pull/14689)

Detailed reference:
[Evaluation observation layer technical reference](evaluation-observation-layer-design-details.md)

## Purpose

An evaluation cache can reuse an evaluated project only when MSBuild knows every input
that affected the result.

The observation layer records those inputs while evaluation consumes them. It is not
itself a cache.

Every input must be:

1. part of the candidate key;
2. recorded as an observed dependency;
3. covered by an authoritative provider generation; or
4. classified non-cacheable.

Unknown or incomplete observation fails closed.

## Proposal at a glance

- Create one explicitly passed `EvaluationObservationSession` per evaluation.
- Start it before root project source acquisition.
- Reuse existing evaluator-native interception points.
- Keep per-evaluation state off shared `EvaluationContext` and process-global state.
- Use default-deny category coverage.
- Treat opaque third-party code and unclassified property functions as non-cacheable.
- Validate dependencies on lookup in the first in-memory cache.
- Add watchers and journals later only to avoid repeated validation work.
- Use Detours only to verify coverage on Windows.

## First milestone

The current prototype milestone is intentionally small:

- one isolated session per evaluation;
- outer recording filesystem;
- typed filesystem records;
- explicit incomplete/non-cacheable reasons;
- semantic, concurrency, and overhead tests;
- no cache hits.

The value of this milestone is proving the observation boundary: it shows that MSBuild
can collect isolated, typed dependencies without changing evaluation. Cache behavior is
deliberately deferred until the team trusts the coverage and cost model.

It remains off by default behind
`MSBUILDPROTOTYPEEVALUATIONOBSERVATION=1`.

The current PR creates the session in `Evaluator`. Moving session creation before root
source acquisition is Phase 2 work.

It does not yet promise complete filesystem, environment, Registry, SDK, toolset, or host
coverage.

## Existing MSBuild code to reuse

| Existing mechanism | Reuse |
| --- | --- |
| `EvaluationContext` and `ContextWithFileSystem` | Reuse the current filesystem and caches while installing a per-evaluation outer recorder. |
| Internal `IFileSystem` | File reads, probes, raw enumeration, and metadata. |
| `DirectoryCacheFileSystemWrapper` | Preserve host cache behavior; record the value returned by the wrapper. |
| `FileMatcher` | Record semantic glob requests and returned membership, including expansion-cache hits. |
| `ProjectRootElementCache` and `ProjectRootElement.Version` | Root/import and unsaved source identity and versions. |
| `PropertyTrackingEvaluatorDataWrapper` | Present environment-derived property reads, independently from logging. |
| `PropertiesUseTracker` | Undefined property reads that may become environment-derived properties later. |
| `Expander` and `Expander.Function` | Property-function classification and observation. |
| `PropertyExpander.ExpandRegistryValue` | Classic `$(Registry:...)` access. |
| `IntrinsicFunctions.GetRegistryValue*` | `[MSBuild]::GetRegistryValue*` access. |
| `ISdkResolverService`, `SdkReference`, `SdkResult` | SDK request, result, resolver identity, and cache provenance. |
| `BuildParameters` and `ProjectCollection.EnvironmentProperties` | Effective environment sources already consumed by evaluation. |
| Existing Detours reporting | Windows-only coverage comparison, not production semantics. |

No second public filesystem abstraction is proposed.

## Session ownership and transport

The session is created at the internal `Project`, `ProjectInstance`, and backend project
loading entry points.

It is passed explicitly to:

- project source acquisition;
- `Evaluator`;
- property tracking;
- `Expander` and property functions;
- SDK/toolset decorators;
- host source providers.

Static helpers receive an explicit observer argument.

The session is not stored on:

- shared or user-supplied `EvaluationContext`;
- `ProjectCollection`;
- a process-global singleton;
- production `AsyncLocal` state.

Observation completion is atomic. Observation failures never change evaluation behavior,
but they set `ObservationIncomplete`, which prevents reuse.

Repeated observations of the same identity must agree. Different values, outcomes, or
provider generations set `ConflictingObservation` and make the evaluation non-cacheable.

## Coverage model

Use a closed `EvaluationInputCategory` enum.

Static implementation coverage:

```text
NotImplemented   // default
Partial
Complete
```

Per-evaluation state:

```text
NotExercised
Observed
Incomplete
NonCacheable
```

The full category enum is required after explicit platform applicability is applied.
Adding a category fails a coverage test until it is classified.

Cache eligibility requires:

- successful evaluation;
- every applicable implementation category `Complete`;
- no per-report `Incomplete` or `NonCacheable` state;
- no dropped or conflicting observation.

The prototype always remains ineligible.

## Input ownership

| Class | Category | Primary observer |
| --- | --- | --- |
| Key | Project/provider identity, global properties | Evaluation entry point |
| Key | ToolsVersion, load settings, evaluation stage, interactive/VS mode | Evaluation entry point |
| Key | Culture, startup directory, node count, semantic feature identity | Evaluation entry point |
| Observed | Root and imported XML | Source/PRE provider |
| Observed | Non-PRE file reads, probes, metadata, raw enumeration | Per-evaluation filesystem |
| Observed | Upward/fallback searches | Search helper |
| Observed | Globs | `FileMatcher` semantic boundary |
| Observed | Imported environment properties | Property tracking |
| Observed | Live `System.Environment` calls | Property-function boundary |
| Observed | Registry | Classic Registry expansion, Registry intrinsics, typed built-in provider |
| Observed | SDK/toolset | Service/provider decorators |
| Observed | Stable machine/process values | Property-function/host observer |
| Observed | Unsaved IDE/object-model state | Host source provider |
| Non-cacheable | Opaque extensions, unclassified functions, unstable ambient input | Owning invocation boundary |
| Non-cacheable | Unversioned shared-cache result | Shared-cache boundary |
| Non-cacheable | Partial, failed, ambiguous, or unverifiable observation | Operation boundary |

Solution parsing (`.sln`, `.slnx`, `.slnf`) uses a separate key/report and feeds project
evaluation requests into this model.

## Filesystem strategy

`IFileSystem` is the primary seam, but it is not proof of complete coverage.

Every evaluation-affecting direct use of `FileSystems.Default`, `System.IO`, a
`*NoThrow` helper, or a process-wide cache must be:

1. routed through the per-evaluation filesystem;
2. observed explicitly at its semantic boundary; or
3. made non-cacheable.

Important semantic observers:

- source acquisition owns root/import XML identity;
- `FileMatcher` owns glob membership, including expansion-cache hits;
- search helpers own ordered upward/fallback probes;
- `Expander.Function` owns filesystem property-function classification.

Missing paths and missing nearer search candidates are dependencies.

Glob records retain a membership fingerprint and invalidation index data. Full member
lists are diagnostic-only.

## Environment strategy

Environment observation has several levels.

### Imported environment-derived properties

- A present `$(NAME)` read records the exact imported value.
- An undefined property read records a negative imported-environment dependency.
- Validation compares against the next effective imported environment-property table.
- If another property source overwrote the environment value before the read, it is not
  attributed to the original environment.
- Observation tracking is independent from existing environment-read log emission.

### Live `System.Environment`

| Operation | Policy |
| --- | --- |
| `GetEnvironmentVariable(name)` | Record name and exact returned value/missing. |
| `GetEnvironmentVariables()` | Record the exact returned environment snapshot. |
| `ExpandEnvironmentVariables(text)` | Non-cacheable until expansion executes against an immutable observed provider. |
| `CurrentDirectory` | Record the exact live value and repeat the same read during hit validation. |
| Stable properties such as `ProcessorCount` | Record as typed ambient values if policy allows. |
| Time, random, tick count, and similar unstable values | Non-cacheable. |

### Engine and SDK inputs

Engine-owned environment reads move behind request/provider snapshots or named providers.
SDK-injected environment values record resolver identity, name, value, and later reads.

Opaque third-party resolver or custom property-function code is non-cacheable. A full
environment snapshot is not sufficient because such code may also read files, Registry,
network, or private process state.

There is no portable notification for arbitrary process environment mutation. Known
engine mutations bump an environment generation; a generation mismatch makes the report
non-cacheable.

Raw environment values remain internal and must not appear in logs, binlogs, telemetry,
or diagnostic reports.

## Registry strategy

Two separate paths are observed:

- `$(Registry:...)` in `PropertyExpander.ExpandRegistryValue`;
- `[MSBuild]::GetRegistryValue*` through `Expander.Function` and
  `IntrinsicFunctions`.

Records contain the exact request, views/default where applicable, returned typed value
or string, and failure outcome.

Current APIs do not always distinguish missing key from missing value or a default value
equal to stored data. The observer records only what was authoritatively consumed until a
typed Registry provider is introduced.

Built-in Registry enumeration moves behind that provider. Opaque extension Registry
access is non-cacheable.

Registry notifications may later accelerate Windows invalidation; validation remains the
correctness mechanism.

## SDK and shared-cache strategy

An SDK observer records:

- complete SDK reference and project/solution context;
- resolver identity and version;
- success/failure;
- resolved paths/version/properties/items;
- dependency replay token or authoritative provider generation.

An inner shared cache can skip work that an outer observer would otherwise see.

Every shared cache must:

1. replay the original dependency set;
2. expose an authoritative generation; or
3. make the evaluation non-cacheable.

This applies to filesystem, glob, PRE/loaded-project, SDK, toolset, and host caches.

Any non-`Isolated` sharing policy, and any process-global cache used under any policy,
remains non-cacheable until every reused cache satisfies this contract.

## Validation and invalidation

The first in-memory cache validates candidate dependencies on lookup.

Examples:

- compare source/provider versions;
- hash or version file content;
- repeat typed probes and Registry operations;
- compare glob/search generations or membership;
- compare the next effective imported environment table;
- repeat named live environment reads;
- compare canonical full-environment snapshots;
- repeat recorded ambient reads such as live current directory;
- replay SDK/toolset dependencies.

The cached evaluation baseline is immutable. Each build receives a deep copy or
copy-on-write execution overlay.

Validation uses:

- a complete manifest check before materialization;
- provider epochs where available;
- a second complete check after materialization for dependencies without a stable epoch.

Any mismatch discards the hit. A dependency that cannot be rechecked or fenced is
non-cacheable.

Watchers, journals, and Registry/host notifications are later accelerators. Overflow,
event loss, or unsupported roots fall back to validation.

## Overhead

### Disabled

- No session, recorder, collections, or report allocations.
- Predictable null/feature checks at evaluation entry and instrumented static hot paths.
- No logging or semantic changes.

### Enabled

| Area | Main cost |
| --- | --- |
| Property/environment reads | Name lookup and deduplicated record update |
| Filesystem probes/metadata | Session gate, normalization, keyed update |
| File content | Hashing when no authoritative provider token exists |
| Globs/enumeration | Membership fingerprinting and optional diagnostics |
| Property functions | Classification and typed record/non-cacheable reason |
| Registry | Copy request and returned value |
| SDK/toolset | Canonical request/result and provenance replay |
| Completion | Freeze records and calculate counters |

Primary risks:

- property and property-function hot-path overhead;
- glob membership memory;
- duplicate content hashing;
- shared-cache dependency retention;
- observer lock contention;
- validation cost erasing cache benefit;
- sensitive value retention.

In practice, a small project that reads a few properties should add only keyed record
updates. A glob-heavy project can retain or hash thousands of paths, so glob membership
and validation are expected to dominate enabled-mode cost.

Implementation guidance:

- prefer ordinary dictionaries under a session gate unless profiling requires otherwise;
  the prototype deliberately uses concurrent dictionaries plus a gate until concurrency
  measurements justify simplifying it;
- use existing MSBuild comparers;
- keep hashing outside locks;
- store fingerprints rather than diagnostic member lists;
- avoid LINQ and closures in hot paths;
- share immutable request/provider snapshots.

## Measurement gate

Measure:

- observation disabled and enabled;
- cache miss;
- valid hit including validation and materialization;
- stale candidate followed by reevaluation.

Workloads:

- small SDK project;
- property-function-heavy project;
- glob-heavy project;
- large real solution;
- concurrent graph evaluation;
- repeated Server requests.

Metrics:

- wall-clock and CPU evaluation time;
- allocations, GC, and peak retained memory;
- lock contention;
- retained bytes by category;
- finalization, validation, and materialization time.

The team must approve CPU, allocation, and retained-memory budgets before default
enablement. Cache-hit validation and materialization must remain materially cheaper than
reevaluation.

## Verification

- Observation on/off produces identical evaluation results, errors, and log/binlog
  sequence.
- Coverage tests exercise every category and every known bypass.
- New categories default to incomplete.
- Conflicting repeated reads become non-cacheable.
- Shared-context concurrent evaluations remain isolated.
- Windows Detours compares process-level access with native observations where
  attribution is possible.

Detours is not the production architecture because it is Windows-specific, process-wide,
and cannot represent semantic inputs such as in-memory project versions.

## Phases

1. **Current prototype:** evaluator session and filesystem records; no hits.
2. **Source/filesystem completeness:** root/import, bypasses, property functions, globs,
   searches, shared-cache provenance.
3. **Environment/Registry/ambient:** property reads, live environment calls, both Registry
   syntaxes, stable/unstable ambient classification.
4. **SDK/toolset/host:** resolver decorator, built-in instrumentation, provenance replay,
   host document versions.
5. **Eligibility/performance:** complete coverage and accepted overhead.
6. **In-memory Server cache:** key, immutable baseline, validation, execution copy,
   eviction.
7. **Live invalidation/persistence:** reverse index, watchers/journals, overflow recovery,
   serialization and security.

## Decisions for the meeting

1. Confirm the process-local MSBuild Server as the first production target.
2. Confirm opaque third-party code and unclassified property functions are
   non-cacheable.
3. Approve the phase ordering.
4. Select benchmark workloads and the process for setting overhead budgets.
5. Decide when a redacted diagnostic report sink is needed.

## Compatibility

Observation-only remains internal, opt-in, and silent, so no ChangeWave is required.

Serving cache hits is a behavioral change. It must initially be opt-in behind a
reversible feature gate or ChangeWave, with opt-out, logging, resolver, mutation
isolation, and fallback tests.

No new warnings are proposed.
