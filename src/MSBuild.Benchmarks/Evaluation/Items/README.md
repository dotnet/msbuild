# Lazy item evaluation benchmarks

These suites measure project-level item evaluation: collecting Include, Update, and Remove
operations, computing item states, and reading cached results. Most use
[`LazyItemBenchmarkScenario`](../../Infrastructure/LazyItemBenchmarkScenario.cs) and
[`LazyItemBenchmarkFixture`](../../Infrastructure/LazyItemBenchmarkFixture.cs) to separate evaluator
work from project setup. `LazyItemEvaluationBenchmark` supplies whole-project controls.

See the [benchmark runner guide](../../readme.md) for general options.

## Terminology

- **Collect operations:** create an evaluator and collect declarations in order. Conditions or
  metadata Remove construction can demand items during collection; collection is not generally
  independent of evaluation.
- **Bookmark:** a fixed position in an item type's operation sequence. Creating one does not compute
  or copy items.
- **Snapshot:** immutable ordered computed item data. Intermediate snapshots may omit glob results
  because of later Remove glob patterns; requested final states must still be complete.
- **Filtered items:** items whose originating Include condition was true, not items selected by
  specification matching.
- **Re-evaluate:** apply previously collected operations again, rather than read an already cached
  result.
- **Later Remove glob patterns:** parsed glob patterns from applicable ordinary Removes later in the
  requested sequence. These are distinct from an Include's explicit Exclude patterns.

## Suites and scenarios

| Benchmark | Coverage and scale |
| --- | --- |
| `LazyItemOperationBenchmark` | Bulk/individual Includes; literal, sparse/dense wildcard, and per-item-metadata Updates; indexed, duplicate-path, bulk, and metadata Removes. `ItemCount`: 500 or 5,000. |
| `LazyItemSnapshotBenchmark` | Append, sparse Update, and dense Update sequences. `ItemCount`: 500 or 5,000; `BookmarkCount`: 0, 4, or 32. |
| `LazyItemReadBenchmark` | Repeated final item collection and filtered-item reads from a setup-primed evaluator. `ItemCount`: 500 or 5,000. |
| `LazyItemCurrentReadBenchmark` | Sparse/every-step growing-state reads, a read after every Update, or 100 reads of one current state within each fresh evaluation. `ItemCount`: 100 or 1,000. |
| `LazyItemQualifiedUpdateBenchmark` | Qualified metadata using literal or wildcard-transformed referenced identities. `SourceCount` and `TargetCount` independently vary between 100 and 1,000. |
| `LazyItemSequenceBenchmark` | Individual Includes, 32 interleaved item types, and disjoint/overlapping Updates; collection-only and fully evaluated methods. `OperationCount`: 100 or 10,000. |
| `LazyItemGlobBenchmark` | Recursive Include; identical, subtree, or many Removes; reinclusion; earlier current reads or bookmarks. `FileCount`: 100 or 500. |
| `LazyItemExcludeBenchmark` | A fixed 100-item literal Include with 0, 4, 128, or 16,384 nonmatching Exclude patterns. |
| `LazyItemEvaluationBenchmark` | Fresh whole-project evaluation for wildcard Include, semicolon Include, Update, Remove, and combined operations. `FileCount`: 500. |
| `LazyItemUpdateReevaluationBenchmark` | Multi-fragment literal Updates with disjoint keys, late overlap, or duplicate normalized keys. `ItemCount`: 500 or 5,000; `ReevaluationCount`: 0 or 8. |
| `LazyItemRemoveCollectionBenchmark` | Collecting 100 or 10,000 nonmatching literal/glob Removes without requesting final items. |
| `LazyItemMetadataCollectionBenchmark` | Collecting 100 or 10,000 Includes/Updates with 0, 1, or 4 metadata declarations. |
| `LazyItemRemoveGlobPatternBenchmark` | Equivalent versus growing later Remove patterns, with current reads or bookmarks, over a fixed 100-file tree. `RemoveCount`: 4 or 128. |
| `LazyItemMetadataRemoveBenchmark` | Three-name metadata tuples from 16 or 128 referenced items, shared/distinct leading values, and 512 targets split equally between matches and misses. |

## Setup and measurement boundaries

### Shared evaluator fixture

The shared fixture uses editable `ProjectItem` data and the collector-based evaluator API. Global
setup creates the project collection, initializes an empty project, parses the declarations, creates
any files, and constructs an independent expected-result model.

Setup validation checks ordered item identities, `Trace` metadata, counts, originating Include XML,
and defining-project metadata. Count-checking conditions produce an unexpected `ReadMismatch` item
if a read is wrong, so an incorrectly skipped condition cannot silently pass validation.

The project's final item table remains empty. Timed evaluation adds results to a counting collector,
not back into the project. Its count includes all final item types, including lookup items and small
outputs obtained through bookmarks.

| Timed method | Work included |
| --- | --- |
| Fixture-backed `Evaluate` | Create a fresh evaluator, collect operations, compute required states, and enumerate all final output into the counting collector. |
| `CollectOperations` | Create a fresh evaluator and collect operations; return it without requesting final output. |
| `LazyItemReadBenchmark.CollectItems` | Enumerate final items from an evaluator whose snapshots were computed during setup. |
| `LazyItemReadBenchmark.ReadFilteredItems` | Evaluate an item-group count condition using that primed evaluator's current filtered items; successful reads add no operations. |

The collection-only suites contain no condition reads or metadata Removes. Their timed methods
therefore do not apply the collected operations. Setup validates the fully evaluated result
separately. Other suites can evaluate items during collection when a condition demands them.

CPU-only scenarios create an isolated `EvaluationContext` for each fresh evaluator. Glob scenarios
own a bounded temporary file tree and reuse a shared context. Setup validation warms its filesystem
caches wherever the scenario actually enumerates files; these are not cold-filesystem measurements.

### Whole-project controls

`LazyItemEvaluationBenchmark` does not use the shared fixture. Each invocation creates and disposes
a `ProjectCollection`, loads XML from disk, evaluates a `ProjectInstance`, and counts its `Compile`
and `None` items. Project and source files are created during setup.

This is a fresh-project measurement, including XML parsing and outer evaluation, not just the lazy
evaluator. Files are normally served from the operating system's cache. Its absolute timings are not
interchangeable with the focused counting-collector measurements.

## Interpreting specific workloads

### Operation collection and sequence length

For `LazyItemSequenceBenchmark`, Include workloads have `OperationCount` individual declarations;
interleaved workloads spread them across 32 types. Update workloads start with bulk item creation,
then append `OperationCount` Updates. `Evaluate` includes both collection and final consumption,
whereas `CollectOperations` stops after collection.

`LazyItemRemoveCollectionBenchmark` contrasts literal and glob Remove syntax without filesystem
expansion. It includes construction of any glob-planning state maintained during collection.

`LazyItemMetadataCollectionBenchmark` has no metadata in its zero-entry control. Its single-entry
case uses constant metadata. Four entries combine an item vector, per-item identity, and metadata
references. Differences reflect expression content as well as declaration count.

### Current reads, bookmarks, and cached reads

`LazyItemCurrentReadBenchmark` starts a fresh evaluation each time. Conditions request states while
operations are being collected. Sparse growing reads occur every 100 Includes; every-step reads
occur after each Include. Repeated-current reads request the same state 100 times within that fresh
evaluation, unlike the evaluator reused across invocations by `LazyItemReadBenchmark`.

`LazyItemSnapshotBenchmark` creates 0, 4, or 32 bookmarks distributed across 32 operation phases.
Each bookmark selects one tagged item, limiting output size while still requiring its earlier state.
The benchmark consumes those bookmark outputs as well as the final items.

`ManyRemovesAfterBookmark` in `LazyItemGlobBenchmark` instead copies the entire original file list.
Comparisons with its no-bookmark counterpart include that additional output and consumption.

### Update admission and re-evaluation

`LazyItemUpdateReevaluationBenchmark` creates 32 pairs of Updates: a pending single-key Update and a
two-fragment candidate. Candidates have disjoint keys, overlap the pending Update at their final
fragment, or repeat one normalized key using different spellings.

Nonmatching glob Removes followed by condition reads introduce different later Remove pattern sets.
These requests exercise re-evaluation rather than only repeated cached reads. `ReevaluationCount=0`
is the single-computation control. A bookmark also validates the earlier complete item state.

Candidates use absolute paths in the fixture's project directory while initial item identities are
relative. These cases exercise batch admission and matching; they do not cover every path spelling
or isolate key-classification time. The timed method includes operation collection and consumption.

`LazyItemQualifiedUpdateBenchmark` varies referenced and updated item counts independently.
`EvaluateWildcardSources` appends `*` through an item transform and obtains the same targets and
qualified metadata as `Evaluate`, but requires wildcard matching rather than literal lookup.

### Later Remove pattern sets

`LazyItemRemoveGlobPatternBenchmark` keeps the file tree fixed. Equivalent cases reorder and repeat
the same two nonmatching patterns; growing cases add a distinct nonmatching pattern per Remove.
Each phase either reads the current state or creates a bookmark selecting one tagged item.

All cases create an initial bookmark before the Removes. Bookmark variants add one after each
Remove: `RemoveCount=128` retains 129 bookmarked positions, not 128. Read variants keep the initial
bookmark but do not add another at each phase. File count, pattern count, evaluation demand, and
output count are separate dimensions.

### Metadata Remove tuples

`LazyItemMetadataRemoveBenchmark` matches ordered `(Group, Key, Trace)` tuples. Shared-first cases
reuse the first value across referenced items; distinct-first cases put the distinguishing value
first. Each evaluation constructs the stored tuples and tests both matching and nonmatching targets.

The referenced-item count varies independently of the fixed 512 targets; half the targets survive.
Metadata-name order stays fixed, and the referenced items also remain in the final output.

## Running comparable measurements

Use the same fixture definitions, parameter values, and result consumption on the baseline and
candidate revisions. Choose a baseline that contains the benchmarks, or apply identical fixtures to
both revisions. Record exact engine and fixture revisions with the results rather than embedding
revision-specific comparisons in this reference.

Run on the dedicated benchmark machine with matching target framework, architecture, runtime,
environment settings, and file-cache policy. From the repository root:

```powershell
Set-Location src\MSBuild.Benchmarks
.\Run-Benchmarks.ps1 -Filter "*LazyItem*" -Framework net11.0 -LaunchCount 3
.\Run-Benchmarks.ps1 -Filter "*LazyItem*" -Framework net472 -LaunchCount 3
```

`-Set ItemEvaluation` also selects these suites. For targeted runs, use a class filter such as
`"*LazyItemQualifiedUpdateBenchmark*"`. Keep relevant collection-only, cached-read, snapshot,
large-Exclude, and whole-project controls when assessing an optimization.

Leave `MSBuildDoNotExpandQualifiedMetadataInUpdateOperation` unset for qualified Update
measurements. Indexed Remove cases require `MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD` to be unset
or no greater than the final `Source` count. Setup rejects incompatible settings and incorrect
fixture results.

Report elapsed time, allocated bytes, and GC counts together, including run-to-run variation and
control regressions. Allocation and GC counts do not measure peak retained memory. Removing a
snapshot from a cache does not prove that shared ancestors or their buffers were released; retained
memory requires separate profiling.

These suites explain costs within evaluation. A focused speedup does not by itself establish an
improvement in total build time.
