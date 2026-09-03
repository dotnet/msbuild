# Evaluation observation layer prototype

Status: proposed, observation-only prototype

The prototype records inputs consumed during project evaluation. It is the dependency
manifest needed by a future evaluation cache, but it does not implement a cache.

It is off by default and enabled only with:

```text
MSBUILDPROTOTYPEEVALUATIONOBSERVATION=1
```

## Review guide

Review the change in this order:

1. [`EvaluationObservationModel.cs`](../../../src/Build/Evaluation/Context/EvaluationObservationModel.cs)
   defines the closed input categories and report records.
2. [`EvaluationObservationSession.cs`](../../../src/Build/Evaluation/Context/EvaluationObservationSession.cs)
   owns one report per evaluation and contains the filesystem recorder and fail-closed
   policy.
3. [`ProjectRootElement.cs`](../../../src/Build/Construction/ProjectRootElement.cs) and
   [`XmlReaderExtension.cs`](../../../src/Build/Xml/XmlReaderExtension.cs) capture root and
   import content while it is read.
4. [`Evaluator.cs`](../../../src/Build/Evaluation/Evaluator.cs) creates, scopes, and
   completes the session. The remaining evaluator, expander, filesystem, Registry, and
   task-registration changes connect existing semantic operations to it.
5. The SDK resolver changes record the request, result, cache hit, and cache-entry
   lifetime at the MSBuild/resolver boundary.
6. Focused tests cover disabled behavior, semantic equivalence, representative input
   categories, failure handling, source identity, and concurrent evaluation isolation.
   `EvaluationObservationBenchmark` measures total observation overhead and provides an
   opt-in Windows BuildXL Detours comparison.

## Required cache model

A future cache may reuse an evaluated project only when every result-affecting input is
handled in one of four ways:

1. included in the lookup key;
2. recorded as a dependency that can be validated;
3. covered by an authoritative provider generation or validity token; or
4. classified as unsupported for reuse.

Unknown, incomplete, conflicting, or unverifiable observations fail closed. Observation
continues to run the original evaluation operation and must not change its result,
exception, logging, or ordering.

Inputs known before evaluation belong in the future lookup key:

- project and source-provider identity;
- complete global properties;
- requested and effective `ToolsVersion`;
- project load settings and evaluation stage;
- interactive and Visual Studio modes;
- startup and working-directory semantics;
- culture, runtime, operating system, architecture, and path comparison semantics;
- evaluation-affecting traits, feature switches, escape hatches, and ChangeWaves; and
- filesystem, directory-cache, toolset, and other provider regimes.

A mismatch in any key field is a cache miss. The current prototype records this request
snapshot but does not construct or look up a cache key.

## Runtime flow

1. File-based project loading hashes the bytes consumed by the XML reader and captures
   encoding, timestamp, provider, and parse outcome. This starts before `Evaluator`
   exists so malformed roots can still produce a failed report.
2. `Evaluator` creates one `EvaluationObservationSession`. No session is created when the
   feature is disabled.
3. The active evaluation filesystem is wrapped by `RecordingFileSystem`; existing
   directory and context caches remain inside that wrapper.
4. The session is passed directly where practical. A thread-static current session and
   `EvaluationInputObserver` scope bridge existing static and Framework-layer seams only
   for the duration of the evaluation.
5. Semantic owners record the value or outcome already consumed by evaluation. The
   observer does not repeat filesystem, environment, Registry, or SDK operations.
6. Equivalent repeated observations are deduplicated. Different outcomes for the same
   identity add `ConflictingObservation`.
7. Completion is atomic. The resulting report owns read-only views of the collected
   records, and late callbacks cannot mutate it.
8. Observer failures are contained and set `ObservationIncomplete`; they cannot replace
   the evaluation result or exception.

The session is not stored on a shared `EvaluationContext`, `ProjectCollection`, or
process-global owner. Concurrent evaluations therefore produce independent reports.

## Inputs and future invalidation

The table describes what this prototype records and how a later cache can detect a
change. The validation mechanisms are design requirements, not code implemented by this
pull request.

| Category | Recorded value or outcome | Future change detection |
| --- | --- | --- |
| Request | Effective key inputs listed above | Exact key comparison |
| Project source | Root/import role, provider, source version, content hash, encoding, consumed timestamp, parse/load outcome | Compare the same provider version or content identity; reject unstable reads |
| File content | Canonical path, provider, hash domain, content hash or unverifiable marker | Compare an authoritative provider token or the same content hash |
| Path probe | Canonical path, requested file/directory kind, returned Boolean or failure | Repeat the same typed probe or compare a provider generation |
| File metadata | Canonical path, exact field/operation, provider, returned value or failure | Re-read the same field with the same semantics |
| Directory enumeration | Root, pattern, options, kind, completion, count, ordered fingerprint | Compare membership or an authoritative directory generation |
| Glob | Semantic root, include/excludes, lazy mode, result count and fingerprint | Compare semantic membership or an authoritative glob/directory generation |
| Search | Search kind, ordered candidates and ordered selected paths | Repeat ordered selection or compare an authoritative search-root generation |
| Imported environment | Name, present/missing state, exact effective imported value | Compare the next evaluation's effective imported environment-property table |
| Live environment | Exact named read, snapshot, or ambient result | Repeat the same operation against the next evaluation environment |
| Registry | Exact request, views/default where applicable, returned result or failure | Repeat the same typed request; notifications may only accelerate invalidation |
| Property function | Receiver, member, arguments, result/failure, and classified effects | Validate the typed owner above; volatile, side-effecting, or unknown members remain unsupported |
| Toolset | Effective toolset and provider inputs | Compare provider generation or replay its dependencies |
| SDK resolution | Complete request, returned `SdkResult`, hit/miss, and cache-entry identity | Require resolver dependency manifest or authoritative validity token |
| Task registration | Effective `UsingTask` registration | Recreate from validated project sources and supporting path decisions |
| Shared cache | Cache/provider identity and final semantic result when visible | Replay original dependencies or compare an authoritative generation |
| Custom provider | Provider identity and returned value when visible | Require a stable provider identity/version contract |
| Volatile or side effect | Invoked operation and result/side effect | Do not reuse unless a separate safe replay contract exists |
| Completion | Evaluation success, category states, typed failures, and blocking reasons | Reject any incomplete, unsupported, conflicting, or dropped observation |

Lexical path calculations such as `Directory.GetParent`, `FileInfo.FullName`, and built-in
path item metadata are ambient path-resolution inputs, not filesystem metadata. Their
effective base or instance path and result are recorded without inventing a disk
dependency.

Timestamps are captured because they are values consumed by existing MSBuild behavior
and are useful validation hints. Timestamp-only validation is not treated as proof that
file content is unchanged.

## Fail-closed boundaries

Every category has implementation coverage (`NotImplemented`, `Partial`, or `Complete`)
and a per-evaluation state (`NotExercised`, `Observed`, `Incomplete`, or `Unsupported`).
All non-completion categories remain `Partial` in this prototype, and there is no
admission predicate or cache-hit path.

Reuse must be rejected when the report contains, for example:

- a partial enumeration, failed observation, unstable source read, or unverifiable file
  read;
- different values for the same dependency identity;
- an unclassified property function, volatile value, or evaluation-time side effect;
- an unversioned shared cache, toolset input, directory cache, or custom provider;
- an in-memory or host source without authoritative identity and version; or
- an SDK resolution without a resolver dependency contract.

SDK cache-entry identity proves only that the same existing `SdkResult` entry remains
live in its owner-defined scope. It does not prove that resolver-internal files,
environment, Registry, workload manifests, network state, or host state are unchanged.
SDK-bearing evaluations therefore cannot be admitted to a correctness-capable evaluation
cache until resolvers provide a complete dependency manifest or authoritative validity
token.

The report contains exact environment and property-function values. It must not be
logged, placed in a binlog, sent through telemetry, or persisted without a separate
redaction and security design.

The benchmark project retains a Windows x64, .NET Framework Detours harness solely to
compare process-level filesystem paths with native observations. It is verification
infrastructure, not part of normal MSBuild evaluation or the future cache.

## Out of scope

This pull request does not implement:

- cache lookup, admission, hits, eviction, or evaluated-result materialization;
- dependency validation, invalidation, watchers, journals, or a reverse index;
- serialization, persistence, cross-process reuse, or cross-machine reuse;
- a public dependency-reporting API or production report sink;
- resolver-internal or arbitrary third-party dependency interception;
- solution (`.sln`, `.slnx`, `.slnf`) dependency manifests;
- target or task execution inputs, compiler inputs, build outputs, or execution caching;
  or
- BuildXL Detours as production infrastructure.

BuildXL comparison and benchmark results are review evidence and belong in the pull
request description, not in the long-lived design document.

## Acceptance criteria for this phase

- Observation disabled creates no report and preserves the normal fast path.
- Observation enabled does not change evaluated properties, items, metadata, imports, or
  failures.
- Successful and failed evaluations produce at most one immutable report.
- Representative filesystem, source, environment, Registry, SDK, toolset, task, and
  property-function inputs are typed and attributed to their semantic owner.
- Unsupported and incomplete inputs are explicit and silent to users.
- Shared-context concurrent evaluations do not share observation state.
- Total enabled overhead is measured separately from future lookup, validation,
  materialization, and persistence costs.
