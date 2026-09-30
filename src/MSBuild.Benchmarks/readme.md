# MSBuild Benchmarks

This project contains performance benchmarks for MSBuild using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

### Run Benchmarks Across Supported TFMs

On Windows, `Run-Benchmarks.ps1` runs each selected benchmark on both `net472` and `net11.0`.
Artifacts are kept separate under `artifacts\BenchmarkDotNet\<TFM>`.

```powershell
cd src/MSBuild.Benchmarks
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*"
```

Use `-Set` to run named benchmark sets without writing filter patterns:

```powershell
.\Run-Benchmarks.ps1 -Set Expansion
.\Run-Benchmarks.ps1 -Set PropertyExpansion
.\Run-Benchmarks.ps1 -Set PropertyFunctions
```

Multiple sets are combined with OR. Some sets are umbrellas for narrower sets:

| Umbrella set | Included sets |
| --- | --- |
| `Expansion` | `PropertyExpansion`, `PropertyExpansionScaling`, `PropertyFunctions`, `ItemExpansion`, `ItemFunctions`, `MetadataExpansion`, `MetadataExpansionScaling`, `MixedExpansion` |
| `PropertyExpansion` | Regular property expansion and `PropertyFunctions` |
| `PropertyExpansionScaling` | Property-reference scaling and `PropertyBagCardinality` |
| `ItemExpansion` | Regular item expansion and `ItemFunctions` |
| `Conditions` | `ConditionParsing`, `ConditionEvaluation` |
| `ExpressionShredder` | `ExpressionShredderThroughput` |
| `Items` | `ItemEvaluation` |

Scaling sets remain separate. For example, `-Set MetadataExpansion` excludes
`MetadataExpansionScaling`. The cross-cutting `Scaling` set contains both property- and
metadata-expansion scaling benchmarks. `PropertyBagCardinality` varies the number of unreferenced
properties while keeping the referenced properties and expression shapes fixed.

`ExpressionShredderAllocations` is an opt-in cold-cache diagnostic for allocation-focused
shredder work and is not included in the broad `ExpressionShredder` set.

Common BenchmarkDotNet options are exposed directly:

```powershell
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Job short -DisableNGen
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -LaunchCount 3
```

Use `-CollectEtw`, `-DisableInlining`, or `-EnforcePowerPlan` for the other custom options.
Less common BenchmarkDotNet arguments can still be passed with `-BenchmarkDotNetArguments`.

Use `-All` to explicitly run every benchmark, or `-Framework` to override the target frameworks:

```powershell
.\Run-Benchmarks.ps1 -All
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Framework net11.0
```

### Choose a Run Mode

Use `-Job dry` or `-Job short` only to confirm that benchmarks build and execute. These jobs do
not collect enough data for performance conclusions.

For exploratory measurements, omit `-Job` and `-LaunchCount`. BenchmarkDotNet will adapt its
warmup and measurement iterations within one process launch. For a final comparison, use three
independent launches on the target framework:

```powershell
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" `
    -Framework net11.0 -LaunchCount 3
```

Each launch performs a complete benchmark run, so this approximately triples execution time.

The runner leaves the current OS power plan unchanged by default. Configure dedicated benchmark
machines with a stable performance-oriented power plan. Alternatively, use `-EnforcePowerPlan` to
allow BenchmarkDotNet to temporarily select the High Performance plan on Windows and restore the
previous plan when the run completes. If the process terminates abruptly, the plan may need to be
restored manually.

Compare results only when the target framework, architecture, runtime, and machine environment
match. In particular, absolute `net472` and `net11.0` results are not directly comparable.

### Run Benchmarks on a Specific TFM

```
cd src/MSBuild.Benchmarks
dotnet run -c Release -f net472
dotnet run -c Release -f net11.0
```

### Filter to a Specific Benchmark Class

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*"
```

### Filter to a Single Benchmark Method

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark.IncludeOnly"
```

## Evaluation Input Recording

The integrated evaluation-cache experiment uses
`MSBUILDEVALUATIONCACHEMODE` with one of four values:

| Mode | Behavior |
| --- | --- |
| `Disabled` | Normal evaluation with no recorder or snapshot cache. |
| `Record` | Records evaluation inputs but never looks up or stores snapshots. |
| `SnapshotUnsafe` | Records and caches successfully frozen snapshots, including those marked non-cacheable for validated reuse, and accepts hits without validation. This is an explicit benchmark ceiling only. |
| `SnapshotFileSystem` | Records and caches snapshots, validating recorded file and directory metadata before reuse. |

An explicitly supplied mode overrides the legacy
`MSBUILDRECORDEVALUATIONINPUTS` and
`MSBUILDENABLEPROJECTINSTANCESNAPSHOTCACHE` switches. Without the new variable,
the legacy recording switch retains observation behavior and the legacy
snapshot switch retains its reject-all validator. Invalid values fail closed to
`Disabled` and produce a configuration diagnostic.

In snapshot modes, requests carrying the `MSBuildRestoreSessionId` global property
bypass input recording, snapshot lookup, and snapshot admission. Restore generates a
new session ID on each invocation, so retaining these snapshots would consume the
cache budget and evict reusable build evaluations. Explicit `Record` mode still
records restore inputs. Normal build snapshots remain subject to validation after
restore; this does not ignore restore-generated input changes or alter cache keys.

The snapshot budget defaults to **1 GiB** in this measurement prototype. The former
256 MiB limit could not retain a large multi-targeting build's working set: as a
warm build admitted its first missing snapshots, LRU eviction removed the remaining
snapshots before their next lookup. Stable keys and inputs therefore still produced
almost no reuse. The larger bound provides measurement headroom without changing
activation, validation, admission accounting, or the eviction policy.
Set `MSBUILDPROJECTINSTANCESNAPSHOTCACHEMAXBYTES` to override the limit in bytes
(for example, `268435456` preserves 256 MiB on memory-constrained hosts).
This bounds estimated retained payload, not process RSS, and is not an up-front
allocation. Inspect eviction diagnostics before interpreting a low warm hit rate.

Each explicitly configured or legacy-opted-in build logs one low-importance, versioned
`EvaluationCacheExperimentStatus|` record. It contains the effective mode,
configuration validity, process and main-`BuildManager` identities, build
count, and—outside Disabled mode—cumulative evaluation, cache, validation,
materialization, eviction, and fallback counters. Consumers must compare
consecutive records from equivalent untimed submissions rather than infer a
hit by subtracting an extra diagnostic build.
With no explicit mode and neither legacy switch set, normal builds create no
experiment identity or status record. Explicit `Disabled` still emits the
baseline record required by the comparison harness.

### Diagnosing cache decisions

Set `MSBUILDEVALUATIONCACHEDIAGNOSTICS=1` for an **untimed diagnostic run**.
This switch does not enable recording or caching; select the mode separately.
For Hosted PerfStar, use the extra environment variables:

```text
MSBUILDEVALUATIONCACHEMODE=SnapshotFileSystem;MSBUILDEVALUATIONCACHEDIAGNOSTICS=1
```

The console retains compact High-importance timing summaries and bounded examples,
even with `diagnostics=none` and minimal verbosity. Select `diagnostics=binlog` when
the complete Low-importance decision trace is needed and artifacts can be retrieved.
Do not compare diagnostic-run timing against clean performance runs: the opt-in adds
timestamp reads, synchronized timing accumulation, environment-name hashing,
candidate comparisons, formatting, and buffered logging. Without it, these captures,
comparisons, buffers, and timestamp reads are not performed. Diagnostics never enable caching.

Search binlogs for `EvaluationCacheDiagnostic|Version=1|` and
`EvaluationCacheDiagnosticSummary|Version=1|`. Records identify process, owning
BuildManager, tracing session (`TraceId`), build ordinal, configuration, submission,
node, project, request, and an opaque key ID where available. Key IDs are HMACs
with a private per-tracing-session salt; compare them only within the same `TraceId`.
Turning diagnostics off discards diagnostic state, not snapshots. Re-enabling starts
a new tracing session. The existing aggregate status schema is unchanged; only while
diagnostics are active, `EvaluationCacheExperimentStatus|` is promoted to High importance
to expose cumulative counters, entry count, retained bytes, and configured capacity.

Search console logs for `EvaluationCacheTimingSummary|Version=1|`:

| Record | Fields |
| --- | --- |
| `Kind=Counts` (one per flush) | Process, BuildManager, TraceId, build ordinal and mode; `Requests`, `DroppedEvents`, `ForgottenHistory`, `SuppressedExamples`, `StopwatchFrequency`, and the same `Counts` reason totals as the original diagnostic summary. |
| `Kind=Phase` (at most eleven per flush, only entered phases) | The same owner identities; `Phase`, `NestedUnder`, `Count`, `TotalTicks`, `MaxTicks`, invariant `TotalMilliseconds`/`MaxMilliseconds`, and `SlowestProject`. Ticks use `Stopwatch.GetTimestamp`, not `TimeSpan` ticks. |

The nonoverlapping top-level phases within a request are:

| Phase | Scope |
| --- | --- |
| `RequestKey` | Open the requested root, select toolset, construct cache/input identities (including the existing environment fingerprint). |
| `CacheLookup` | Look up a candidate, including opt-in miss explanation. |
| `Validation` | Provenance checks and the selected validator. |
| `Materialization` | Materialize an accepted snapshot. |
| `FallbackPreparation` | After validation rejection or failed materialization, discard implicit XML-cache references when appropriate and prepare the fresh-evaluation SDK resolver. Excludes candidate removal (`CacheAdmission`) and reopening the root (`FreshEvaluation`). |
| `FreshEvaluation` | Open the root if still needed and evaluate a normal request, including recording when enabled. |
| `RestoreEvaluation` | The same work for a request carrying `MSBuildRestoreSessionId`, distinguished even in Disabled or Record mode. |
| `SnapshotCreation` | Check admission eligibility, freeze the snapshot, create validation data and calculate its retained-size admission estimate. Rejected eligibility checks count as attempts. |
| `CacheAdmission` | Add/replace/evict a snapshot, or remove a rejected candidate. A rejected candidate followed by a new admission counts twice. |

`ManifestValidation` (filesystem/environment manifest checks) and `SdkValidation`
(one scope around the SDK loop, only for SDK-bearing manifests reached after cheaper
checks) are nested within `Validation`, identified by `NestedUnder=Validation`.
SDK resolution and validators are not rerun for timing. Counts include unsuccessful
attempts; scopes end on success, early rejection, fallback, and exception unwinding.
The phases exclude some request setup/diagnostic/fallback plumbing and are not an
exhaustive build timeline. Nested durations are already included in their parent.
Different requests/nodes can run in parallel: **neither nested sums nor parallel
sums are additive wall time or CPU time**. These are elapsed operation durations
with diagnostic overhead, not synthetic project-evaluation events or clean benchmarks.

`EvaluationCacheTimingExample|Version=1|` contains owner/request/configuration/
submission/node identities, project, opaque key (if available), event, reason and
safe detail. It selects at most three examples per stable `Event.Reason`, and at most
96 overall per flush, for validation rejection, admission rejection, explained misses,
unavailable host cache and fallback. Routine restore bypass, cold misses and accepted
reuse have no High-importance per-project examples. Reason counts continue without
sampling; `SuppressedExamples` counts eligible examples omitted by either limit.
Project/detail fields and the slowest-project display are limited to 512 UTF-16 code
units (then delimiter-escaped), backing off by one rather than splitting a surrogate
pair, with `<truncated>` when shortened. The eleven timing
accumulators retain only one slowest-project path each, not per-project timing history.

`Event`/`Reason` distinguish intentional restore/record-only bypass, unavailable
host-owned caches, lookup candidates, successful materialization, admission,
eviction/clear/removal, validation rejection, and recoverable cache failures.
`KeyMismatch` compares the closest live same-project candidate (fewest different
fields, MRU tie-break); it is **not proof that this is the intended configuration**.
Its detail lists differing identity fields and changed global-property/environment
**names**, never their values. Validation reports the first failing path, environment
name, SDK name, or safe failure category; it does not repeat validation or SDK resolution.
`NoEntryOrHistory` means no live same-project candidate or remembered exact disposition,
not proof that the project has never been evaluated.

The trace retains at most 2,048 key dispositions and 10,000 detailed events between
flushes, including on hosts without snapshot storage. `ForgottenHistory` and `DroppedEvents` expose truncation; summary reason counts
continue after detailed-event truncation. Timing counts/totals/maxima, slowest paths,
example limits, and reason/event counters reset together on flush; key history survives.
An in-flight timer is counted in the flush after it finishes. Messages flush at EndBuild
(or worker build cleanup), with logging callbacks outside both cache and diagnostic
locks; abrupt termination may lose them. Lifecycle records between builds are included
in the next flush. Only backend configuration loads entering this cache integration
are traced, not all API evaluations or already-loaded caller instances. Worker loads
without the owning cache emit `CacheUnavailableOnHost` with a host-local trace identity
and no owning BuildManager ID.
With diagnostics enabled but caching disabled, `CacheDisabled` and aggregate mode
status explain that no cache was activated.

All these opt-in diagnostics omit raw property/environment values, SDK-result payloads,
and exception messages. Paths, names and exception types are visible, including in
console examples and slowest-project fields. This is not a privacy guarantee for the
whole console log or binlog: ordinary MSBuild logging can still include sensitive values.

`SnapshotFileSystem` remains experimental. Its metadata comparison does not
detect same-size/same-timestamp content changes. It revalidates direct
environment reads and immutable SDK-result observations; registry-dependent and
failed-SDK evaluations are recorded but conservatively ineligible. It rejects
reuse when the selected project or a recorded import is retained in the XML
cache with unsaved changes or without authoritative file provenance, and when a
recorded-missing path has any retained XML-cache entry.

Existence-only probes validate path kind rather than timestamp or length. File reads,
metadata requests, and direct filesystem enumeration remain metadata dependencies.
Glob expansion instead records its matching paths and directory change signals.
Unchanged directory stamps use the fast path; changed stamps trigger fresh expansion
with the recorded include/exclude patterns and matcher configuration. Reuse requires
the same engine-sorted results. A result replayed from a cache that may predate the
current evaluation is always revalidated. Isolated caches that started empty under
the current recorder keep the stamp fast path, including hits within that evaluation.
Thus an unrelated DLL copy need not invalidate a search for `.props` or `.targets`.
This is not a blanket `bin`/`obj` exemption: changed matching paths, direct metadata
reads, and generated imports still invalidate. Glob replay retains the original
positive/negative directory probes and never weakens a direct read dependency.

The backend constructs the request identity before evaluation for Record and
both snapshot modes. It uses the effective project-file/explicit toolset,
subtoolset, parser settings, directories, cultures, node count, environment,
global-property values, and command-line provenance. Snapshot materialization
retains the same recorded-input manifest for diagnostics; only
SnapshotFileSystem requires that manifest to be cacheable before admission.

Admission estimates include owned key and manifest strings, environment values,
registry arrays/strings, captured glob patterns/results, and immutable SDK payloads. They remain conservative
retained-payload accounting rather than a process-RSS limit.

`EvaluationInputRecordingBenchmark` measures what recording evaluation inputs
(`MSBUILDRECORDEVALUATIONINPUTS=1`) adds to an evaluation, in an isolated and in a shared evaluation
context. `EvaluationInputValidationBenchmark` separately measures checks of recorded files and
directories: unchanged, and after a project file, an import, or a matching glob member changed.
Its evaluation and input capture happen outside the timed operations.
The recording benchmark also compares the unconfigured and explicit-`Disabled`
control paths. That comparison is a same-build execution/allocation sanity
check; it is not a comparison with stock `main` and does not establish zero
overhead.

Both use the same synthetic project and any restored projects listed in
`MSBUILD_EVALUATION_INPUTS_BENCHMARK_PROJECTS` (path-separator
delimited). SDK-style projects also need `MSBUILD_EXE_PATH`, `MSBuildSDKsPath`, and
`DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` pointing at the bootstrap SDK, passed to the benchmark process
with `--envVars` as the shared fixture remarks describe. A project that is not cacheable fails setup with the
reason.

Run the observation cases or unchanged validation independently:

```powershell
.\src\MSBuild.Benchmarks\Run-Benchmarks.ps1 -Framework net11.0 -Filter '*EvaluationInputRecordingBenchmark.*'
.\src\MSBuild.Benchmarks\Run-Benchmarks.ps1 -Framework net11.0 -Filter '*EvaluationInputValidationBenchmark.ValidateUnchanged'
```

The two classes produce separate reports. To express validation cost relative to fresh evaluation,
compare `EvaluationInputValidationBenchmark.ValidateUnchanged` with
`EvaluationInputRecordingBenchmark.Evaluate` for the same project and run settings.
The stale-validation cases mutate files or directory membership; use synthetic or disposable workloads.
The glob case creates a name matching a recorded wildcard beside an existing match
under the project. Its setup verifies that validation actually rejects the mutation;
projects without a supported wildcard are not eligible for that case.

Validation compares path existence and file/directory kind for probes. Direct reads
and filesystem enumerations additionally compare last-write timestamp and length.
Glob-directory timestamps signal when matching results need fresh validation;
timestamp changes alone do not reject an otherwise unchanged glob result.
Direct environment reads are compared using platform environment-name casing,
and SDK results are re-resolved in their recorded context and compared with
immutable owned observations. Registry reads remain recorded but make the
evaluation ineligible for checked reuse. Request identity is checked separately;
a successful filesystem check alone does not establish that an evaluation
result can be reused. Incomplete/non-cacheable recording and failed checks still
reject validation.

Registry reads through `$(Registry:...)`, `[MSBuild]::GetRegistryValue`, and
`[MSBuild]::GetRegistryValueFromView` are retained in `EvaluationInputs.RegistryReads`.
Each observation contains the decoded key, value name (empty for the default value),
returned value, and the string view tokens the intrinsic considers (not ignored non-string arguments).
The key may be null for an accepted no-view request. Values are captured from the
existing call, not by rereading the registry; missing values remain `null`, while an intrinsic
that returns a supplied fallback records that fallback. Repeated reads are kept in order.
Binary, multi-string, and character arrays are copied into immutable arrays before further expansion.
Scalar string, primitive, enum, decimal, date/time, timespan, and GUID fallbacks are also supported.
Other fallback objects mark recording non-cacheable instead of retaining a mutable reference.
Registry access alone no longer stops recording. These are logical requests/results, not a
trace of each internal view probe or of resolver/toolset-internal registry accesses.
The comparison summary prints only the registry-read count, not potentially sensitive values.

To compare the recorded inputs with every path the process touched, build with
`-p:EnableEvaluationInputDetours=true` on Windows x64 and run the comparison instead of a benchmark.
It prints a summary line, then every touched path the recording does not explain: `DETOURS_ONLY|` for
probes and enumerations, `DETOURS_ONLY_READ|` for content reads, and `RECORDED_ONLY|` for recorded
paths the sandbox never saw.

```
dotnet run -c Release -f net11.0 -p:EnableEvaluationInputDetours=true -- --evaluation-input-detours --project <path> [--global-property Name=Value]
```

## Command-Line Options

### Custom Options

- `--collect-etw` - Enable ETW (Event Tracing for Windows) profiling diagnostics
- `--disable-ngen` - Disable NGEN/ReadyToRun to measure pure JIT performance
- `--disable-inlining` - Disable JIT inlining for more accurate method-level profiling
- `--enforce-power-plan` - Allow BenchmarkDotNet to select High Performance on Windows

These custom options can be combined with any BenchmarkDotNet options:

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*" --job short --disable-ngen
```
