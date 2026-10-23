# Lazy item evaluation benchmarks

The focused suites consolidate the workloads from `origin/lazy-item-evaluator-performance-refactor`
with the local sparse-edit and retained-snapshot benchmarks. They share `LazyItemBenchmarkScenario`
and `LazyItemBenchmarkFixture`, rather than maintaining a separate instance-model harness.

| Benchmark | Coverage |
| --- | --- |
| `LazyItemOperationBenchmark` | Bulk and individual Includes; disjoint, overlapping, sparse/dense, and per-item-metadata Updates; indexed, duplicate-path, bulk-fragment, and metadata-based Removes |
| `LazyItemSnapshotBenchmark` | Append, sparse-edit, and dense-edit histories with 0, 4, or 32 genuinely retained prefix captures |
| `LazyItemReadBenchmark` | Repeated final publication and matched-item reads from a setup-primed evaluator |
| `LazyItemCheckpointBenchmark` | Sparse/every-step growing-prefix condition reads, a condition read after every Update, and 100 reads of one current state within each fresh evaluation |
| `LazyItemQualifiedUpdateBenchmark` | Qualified metadata matching with source and target counts independently varied between 100 and 1,000; literal identities and wildcard-valued transformed sources |
| `LazyItemHistoryBenchmark` | Recording-only and fully consumed histories with 100 or 10,000 operations; individual Includes, 32 interleaved item types, and disjoint/overlapping Updates |
| `LazyItemGlobBenchmark` | Recursive Include, identical/subtree/many Removes, reinclusion, and earlier reads or deferred captures over 100 or 500 files |
| `LazyItemExcludeBenchmark` | A fixed 100-item literal Include with 0, 4, 128, or 16,384 nonmatching Exclude patterns |
| `LazyItemEvaluationBenchmark` | Cold whole-project controls for wildcard Include, semicolon Include, Update, Remove, and combined operations |
| `LazyItemUpdateReplayBenchmark` | Multi-fragment literal Updates with disjoint keys, late overlap, or duplicate normalized keys, computed once or replayed under eight changing exclusion contexts |
| `LazyItemRemoveRecordingBenchmark` | Recording 100 or 10,000 nonmatching literal/glob Removes without applying them |
| `LazyItemMetadataRecordingBenchmark` | Recording 100 or 10,000 Includes/Updates with zero, one, or four metadata declarations |
| `LazyItemGlobContextBenchmark` | Reordered/duplicate equivalent exclusions versus growing exclusions, with current reads or deferred captures, over a fixed 100-file tree |
| `LazyItemMetadataRemoveBenchmark` | Three-name metadata tuples from 16 or 128 sources, shared/distinct leading values, and 512 targets split equally between matches and misses |

The remote's single/bulk Includes, literal Updates, wildcard metadata Update, literal/duplicate Removes,
and metadata Remove are covered by the operation/history suites. Its read, qualified-Update, glob,
large-history, and Exclude cases are represented by the corresponding specialized suites above.
The local snapshot and setup-primed read suites remain separate because they measure different
ownership and consumption patterns.

## Measurement boundaries

All focused suites use editable `ProjectItem` data and the collector-based evaluator API. XML parsing,
outer project initialization, file creation, and expected-result modeling happen during global setup.
Each `Evaluate` creates a fresh evaluator, records declarations, and publishes every final and captured
item to a counting collector. Setup separately checks ordered identities, `Trace` metadata, item counts,
and Include/defining-project provenance against an independent scenario model. Incorrect condition
counts produce an unexpected `ReadMismatch` item, so a failed probe cannot silently pass validation.

CPU-only workloads create an isolated evaluation context per invocation. Glob workloads own a bounded
temporary tree and reuse a shared evaluation context, warmed by setup validation wherever the scenario
actually enumerates files. They measure evaluator/glob planning, not cold filesystem traversal.
`ManyRemovesAfterCapture` also publishes the full original file list through a deferred capture; account
for that additional output when comparing it with the no-capture case.

`LazyItemHistoryBenchmark.Record` returns the newly recorded evaluator without requesting final items.
Its histories contain no condition reads or metadata-Remove source materialization. Update histories
have an initial bulk Include followed by `OperationCount` Updates; Include histories have exactly
`OperationCount` declarations. `Evaluate` includes recording and full publication, not just replay.

`LazyItemEvaluationBenchmark` remains the complementary whole-project instance-model control. It
includes collection construction, disk XML loading, and outer evaluation, with files already created
and normally served from the OS cache. Neither its timings nor the remote fixture's checksum-based
`ProjectItemInstance` measurements are interchangeable with focused counting-collector timings.

Allocated bytes and GC counts do not measure peak retained memory. Use separate memory profiling to
evaluate checkpoint/projection retention. In particular, retiring cached snapshots does not prove
that piece-list ancestors or their buffers have been released.

## Replay, recording, and tuple workloads

A **replay** applies the same recorded operations again to compute an item state, rather than reading
an already cached final result. `LazyItemUpdateReplayBenchmark` uses nonmatching glob Removes followed
by condition reads to change the exclusion context. Its zero-replay control computes the history once.
Each history has 32 pairs of Updates: a pending single-key Update and a two-fragment candidate.
Candidates are disjoint, overlap the pending Update at their final fragment, or repeat one normalized
path with different spellings. A deferred capture also validates the earlier unpruned item state.
Candidates use absolute paths rooted in the fixture's temporary project directory, while the items
remain relative. This keeps the admission workload comparable with evaluators whose rollback path
requires canonical keys; it is not coverage of every spelling-related batching bug.
The benchmark includes recording and materialization; it is not an isolated key-classification timer.

`LazyItemRemoveRecordingBenchmark` records only: its timed method does not enumerate final items.
Setup validates the fully evaluated history separately. The literal/glob pair measures the recording
cost of removal syntax and any recorded glob-planning state without filesystem expansion.

`LazyItemMetadataRecordingBenchmark` likewise records without condition reads. The zero-entry control
has no operation metadata; the single entry is constant. Four entries combine an item-vector reference,
per-item identity, and metadata references. Their final `Trace` value validates those dependencies in
setup. Differences reflect both declaration cardinality and expression content, not cardinality alone.

An **exclusion context** is the set of later Remove glob patterns that an earlier Include may omit.
`LazyItemGlobContextBenchmark` keeps file count fixed and varies the number of Remove declarations.
Equivalent contexts reorder and repeat the same two nonmatching patterns; growing contexts add a
distinct pattern per declaration. Each phase demands a current read or records a deferred capture.
Captures select one tagged item, keeping output size small without avoiding historical materialization.
Expected items, metadata, ordering, and condition-read counts are validated outside timing.

`LazyItemMetadataRemoveBenchmark` matches ordered `(Group, Key, Trace)` tuples. Shared-first cases
reuse the first value across sources; distinct-first cases put the distinguishing value first.
Each run builds the source representation and tests both matching and nonmatching target tuples.
Source count varies independently of the fixed target count; metadata-name order stays the same.

`LazyItemQualifiedUpdateBenchmark.EvaluateWildcardSources` appends `*` to each captured source identity
through an item transform. It selects the same targets and qualified metadata as the literal workload,
but requires wildcard matching rather than assuming a literal-identity lookup can resolve it.

### Qualified-Update indexing

Qualified Updates prepare matching lazily and index literal transformed source identities once per
operation. This removes the source-by-target search for literal sources without adding analysis or
index construction during recording. Wildcard-valued sources retain ordered matching, and path keys
use the Update fragment's directory rather than blindly reusing an item's cached project-relative key.

Compare `LazyItemQualifiedUpdateBenchmark` against pre-transfer tip `948c8ff64c`, using the same
fixtures and all source/target count combinations. Keep recording-only histories and ordinary
literal/wildcard Updates as controls. No performance results for this indexing change are recorded yet.

## Comparable baseline on the benchmark VM

Run benchmarks on the dedicated benchmark VM, not on the development machine, including smoke runs.
Apply the same consolidated benchmark fixtures to the unoptimized evaluator and each optimization
revision. The pre-transfer local evaluator was `3ce231d865`; if the benchmark commit is rewritten,
record its replacement baseline revision together with the exact benchmark patch/revision. Do not use
the remote branch's published speedups as the baseline for the local evaluator.

Use matching TFM, architecture, runtime, environment settings, file-cache policy, and result consumption.
Run each framework separately, with three independent launches for final comparisons:

```powershell
cd src\MSBuild.Benchmarks
.\Run-Benchmarks.ps1 -Filter "*LazyItem*" -Framework net11.0 -LaunchCount 3
.\Run-Benchmarks.ps1 -Filter "*LazyItem*" -Framework net472 -LaunchCount 3
```

For targeted iterations, replace the filter with the relevant class, such as
`"*LazyItemQualifiedUpdateBenchmark*"` or `"*LazyItemGlobBenchmark*"`. The broad filter includes the
cold whole-project controls and the scaling histories. `-Set ItemEvaluation` also selects these suites.

Leave `MSBuildDoNotExpandQualifiedMetadataInUpdateOperation` unset for qualified-Update measurements.
Indexed Remove cases require `MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD` to be unset or no greater than
the final Source count. Setup fails explicitly if these assumptions or scenario results do not hold.

Compare source and target scaling independently, report allocation and GC changes alongside elapsed
time, and keep the recording, stable-read, retained-snapshot, large-Exclude, and cold-project controls.
Do not publish an improvement until it exceeds measured run-to-run noise without meaningful
regressions in those controls. No measurements for the consolidated suite are recorded here yet.
