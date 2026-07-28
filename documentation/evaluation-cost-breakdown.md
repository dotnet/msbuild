# Evaluation performance: where the time goes and what to do about it

MSBuild Server is now enabled by default by recent .NET SDKs. It is not what introduced cross-build
caching — reused **worker nodes** have kept parsed project XML between builds for a long time, and
node reuse is on by default. What the server adds is that the **entry-point process** also survives,
so process startup, JIT, and the evaluation done in the entry node stop being paid on every
invocation.

That still changes the picture materially, and this document re-establishes the baseline under the
new default. It organises the remaining work into three pillars:

1. **[Cold builds](#pillar-1-cold-builds)** — before anything is warm. CI, fresh containers, a
   developer's first build of the day. Note this cost is paid **per node**, not once per build.
2. **[Warm builds](#pillar-2-warm-builds)** — every subsequent build in the session. The inner loop.
3. **[Caching between builds](#pillar-3-caching-between-builds)** — what is already reused, what is
   not, and the invalidation rules that decide what may safely be added.

Every number here is measured; [Method](#method) describes how, and
[Reproducing these numbers](#reproducing-these-numbers) shows how to re-run it. Absolute values are
machine specific (Release build, .NET 11 preview, i7-11370H, warm OS file cache); ratios transfer.

## Baseline

Subject: a restored `dotnet new console` (`net11.0`). One evaluation reads **116 project files
(1.4 MB of XML)** and produces 737 properties, 448 items, 4 item definitions, and 504 targets holding
1152 child elements. 179 `<Import>` elements resolve to those 116 files.

A no-op `dotnet msbuild -t:Build` of that project, MSBuild Server enabled:

| | Build wall clock | Evaluation | Evaluation share |
| --- | ---: | ---: | ---: |
| **Cold** (first build in the session) | ~1650 ms | **~368 ms** | 22% |
| **Warm** (second and later builds) | ~459 ms | **~49 ms** | 11% |

Evaluation drops **7.5x** between the first and later builds, because the server keeps the parsed XML
resident *and* keeps the entry-point process alive. Evaluation timings are `/profileevaluation`
figures corrected for that profiler's own measured overhead (88 ms warm, 178 ms cold — see
[Method](#method)).

For a single-project build the server is doing nearly all of that work, because the entry node
evaluates the project and, without the server, that process is new every time. Node reuse alone
cannot help there — but on a multi-project build it does, substantially. Total evaluation per build
across four consecutive `-m:4` builds of 12 projects, **server off**:

| `-nodeReuse` | Build 1 | Build 2 | Build 3 | Build 4 | Worker nodes left alive |
| --- | ---: | ---: | ---: | ---: | ---: |
| `true` (the default) | 3231 ms | 2443 ms | 2148 ms | **1559 ms** | 3 |
| `false` | 3312 ms | 3150 ms | 3162 ms | 3348 ms | 0 |

Node reuse alone recovers about half the evaluation cost by the fourth build, with no server
involved. So cross-build caching is not new; the server widens it and removes per-invocation startup.

Two framing facts for everything below:

* **Evaluation is a minority of a single-project build**, 11-22%. It matters at scale — large
  solutions, `ProjectGraph`, Visual Studio solution load and design-time builds — not on one small
  project. Size any investment against the scenario it targets.
* **Cost is fixed plus marginal.** There is a per-session fixed cost to warm the SDK import closure,
  then a per-project marginal cost. Work on the fixed term is capped per build; work on the marginal
  term multiplies by project count.

---

## Pillar 1: Cold builds

### What "cold" actually scopes to

"Cold" is not a property of the build, and not of each project — it is **per node**. The
`ProjectRootElementCache` is shared through `BuildParameters` within a node, so the first project
evaluated on a node pays to load and parse the whole SDK import closure and every later project on
that node reuses it. That is exactly the fixed-plus-marginal split measured in
[pillar 2](#how-warm-cost-scales-with-project-count): ~80-110 ms for the first project, ~16 ms for
each one after it.

The consequence is that a multi-proc build pays the fixed cost **once per node**, not once per build.
Twelve projects, `-nr:false` so every build starts fully cold, server off, varying only node count:

| Nodes | Wall clock | Total evaluation across the build |
| --- | ---: | ---: |
| `-m:1` | 4660 ms | 1338 ms |
| `-m:2` | 3011 ms | 1891 ms |
| `-m:4` | 3010 ms | 3096 ms |
| `-m:8` | 3755 ms | **6662 ms** |

Evaluation work grows about 5x for the same 12 projects. Each additional node adds roughly 760 ms of
duplicated work in this measurement — one more independent parse of the same ~116 SDK files. A
control with a **single** project shows node count alone changes nothing (499 ms at `-m:1` versus
533 ms at `-m:8`), which attributes the growth to per-node duplication rather than to parallelism
overhead.

Note also that wall clock stops improving after `-m:2` here and is *worse* at `-m:8`: for twelve
small projects the duplicated evaluation outweighs the parallelism. These absolute figures are
inflated by `/profileevaluation` and by cold JIT in each fresh node, so treat the ratios rather than
the milliseconds as the result.

### The server changes this, but only together with `-mt`

MSBuild Server on its own does not remove the duplication. Without `-mt` the server only orchestrates
and still delegates project work to separate worker nodes, so each of those nodes keeps its own
`ProjectRootElementCache`. What it does change is that the nodes and the entry process survive, so the
duplicated cost is paid on the first build of a session and amortized afterwards.

With `-mt` the server runs project work on **threads inside the single server process**. Each in-proc
node is a thread (`NodeProviderInProc` creates one `_inProcNodeThread` per node, up to `MaxNodeCount`
in multithreaded mode, and the scheduler sets out-of-proc affinity to zero), so there is exactly one
`ProjectRootElementCache` for the whole build and the SDK is parsed once. Twelve projects, three
consecutive builds, total evaluation per build:

| Configuration | Build 1 | Build 2 | Build 3 |
| --- | ---: | ---: | ---: |
| server off, `-m:8` | 7023 ms | 3541 ms | 2950 ms |
| server on, `-m:8` | 8593 ms | 2469 ms | 2099 ms |
| server on, `-mt` | **1123 ms** | **949 ms** | **1074 ms** |

`-mt` is **6-7x cheaper on the cold build** and 2-3x cheaper warm — and it is essentially flat,
because there is no per-node duplication left to amortize. Verified that the `-mt` run really does
the same work: all 12 projects build, exit code 0, in 2.29 s. Sampling the live process tree during a
build confirms the topology: `-m:8` shows worker processes (`/nodemode:1`) alongside the server, while
`-mt` shows **none at all**.

Two caveats that bound how much of this is bankable today:

* **`MaxNodeCount` defaults to 1**, so `-mt` on its own gives a single in-proc node and no
  parallelism. It needs `-m`/`-m:N` alongside it.
* **Only evaluation collapses into one process; task execution largely does not.** In multithreaded
  mode `TaskRouter` runs a task in-process only if its type carries
  `MSBuildMultiThreadableTaskAttribute`, and routes everything else to an out-of-proc sidecar TaskHost
  for isolation — necessary, because concurrent projects now share one process and a legacy task may
  mutate environment variables or the working directory. MSBuild's own tasks and the SDK's tasks are
  largely annotated, but `Microsoft.Build.Tasks.CodeAnalysis.dll` (Roslyn's `Csc`/`Vbc`) is not, so
  the compile step of every project still goes out of process. That is why the `-mt` run above still
  shows `/nodemode:2` TaskHost processes. **Maturing `-mt` is therefore substantially an annotation
  effort in other repositories**, and its measured win here is specific to the evaluation half of the
  build.

This makes `-mt` the single largest lever measured anywhere in this document for cold multi-proc
builds, which is exactly the CI shape. It is still experimental and opt-in; `-mt` implies the server
(`ShouldUseMSBuildServer` returns true when `MSBUILDUSESERVER` is unset and the build is
multithreaded).

Two practical consequences:

* **A CI build does not pay cold cost once.** It pays it once per node, so `-m:8` on a fresh machine
  parses the SDK up to eight times. How much that matters depends on project count: with 12 projects
  the fixed term dominates, while with 500 projects the marginal term does and the duplication is a
  smaller share.
* **Anything that reduces the fixed term is worth more on a multi-proc cold build than the
  single-project numbers below suggest**, because the saving is multiplied by node count. That
  applies to persisting the parsed construction model across processes (pillar 3), and it is what
  `-mt` already achieves by collapsing the node count to one.
* **Maturing `-mt` is the highest-leverage item for cold CI builds** on this evidence. It attacks the
  duplication directly rather than making each duplicate cheaper.

### Where the time goes in a cold evaluation

The first project on a node pays for acquiring and parsing all 116 files. Exclusive time per scope,
from MSBuild's own `Microsoft-Build` event source markers over 25 cold evaluations:

| Category | Share of cold evaluation |
| --- | ---: |
| **Acquiring and parsing project XML** (`LoadDocument` + `Parse`) | **~52%** |
| **SDK resolution** | **~21%** |
| Import expression expansion and probing | ~7% |
| Item evaluation including globbing | ~7% |
| Property and condition evaluation | ~6% |
| Target and task registration | ~6% |

A cold evaluation is dominated by *acquiring* project XML, not by evaluating it. All five evaluation
passes combined, excluding document loading, parsing and SDK resolution, are under 25%.

### Reading a file is the cheap part

From CPU profiles (`dotnet-trace`, stacks folded by category, two captures of 60 and 120 cold
evaluations), as a share of thread time inside evaluation:

| Category | Share |
| --- | ---: |
| Garbage collection | **27-30%** |
| File attribute queries (`GetFileAttributesEx`, `FillAttributeInfo`, `GetFileType`) | **18-21%** |
| Opening and closing file handles (`CreateFile`, `CloseHandle`) | **11-13%** |
| XML tokenizing | 9-11% |
| Reading bytes (`ReadFile`) | 6-7% |
| XML DOM construction | 3-4% |
| Path normalization | 3% |
| Directory enumeration | 3% |

Attribute queries plus handle open/close are roughly **30%** — four to five times the 6-7% spent
actually reading 1.4 MB. Isolating the layers over the same 117 files with a warm OS cache:

| Layer | Time for 117 files | Share |
| --- | ---: | ---: |
| Reading bytes (open, read, close) | 8.4 ms | 40% |
| Tokenizing XML | 5.7 ms | 27% |
| Building a stock `XmlDocument` | 4.2 ms | 20% |
| Attaching element locations (`XmlDocumentWithLocation`) | 2.9 ms | 14% |
| **Total** | **21.2 ms** | |
| Stat every file (`FileInfo.LastWriteTimeUtc`) | 4.8 ms | *additional* |

`XmlDocumentWithLocation` is **not** the villain it is often assumed to be: element locations add 14%
to document loading, not a multiple.

### Allocation drives the GC share

| Scenario | Allocated | Gen0 | Gen1 | Gen2 | GC pause |
| --- | ---: | ---: | ---: | ---: | ---: |
| Cold, full evaluation | **13.6 MB** | 2.60 | 1.08 | 0.32 | **19.0 ms** |
| Cold, properties only | 11.1 MB | 2.04 | 1.00 | 0.20 | 13.6 ms |
| Warm, full evaluation | 1.5 MB | 0.24 | 0.04 | 0.00 | 0.53 ms |

Per evaluation; BenchmarkDotNet's `MemoryDiagnoser` independently reports 13.9 MB cold and 1.5 MB
warm. Allocating roughly **10 MB per MB of project XML** is both the GC cost and the reason cold
evaluation cannot get much faster without changing what gets built.

### What to do

1. **Stop stat-ing files that cannot change.** `ProjectRootElement.LoadDocument` unconditionally
   calls `GetFileInfoNoThrow` to record `LastWriteTime` — about 4.8 ms per evaluation, ~5% of a cold
   evaluation, on top of the open the read already performs.
   `ProjectRootElementCache.IsInvalidEntry` already skips its staleness check for files that
   `FileClassifier.IsNonModifiable` recognises; `LoadDocument` does not apply the same reasoning.
   Small, self-contained, no invalidation risk (see [pillar 3](#pillar-3-caching-between-builds)).
2. **Mature `-mt`.** Collapsing project execution into one process removes the per-node duplication
   entirely and was measured at 6-7x on a cold 12-project build. Nothing else in this document
   attacks the cold multi-proc shape that directly. Note the win measured here is on the *evaluation*
   half: task execution still leaves the process for any task not marked
   `MSBuildMultiThreadableTaskAttribute`, which today includes Roslyn's `Csc`/`Vbc`. Annotating the
   remaining hot tasks — largely work in `dotnet/roslyn` and `dotnet/sdk` — is what converts this into
   a whole-build win.
3. **Reduce allocation in document loading.** 13.6 MB to read 1.4 MB is the direct cause of the
   27-30% GC share. This is also what a persisted construction model would attack (pillar 3), and its
   value is multiplied by node count for as long as builds stay multi-process.
4. **Reduce imports that resolve to nothing.** 179 import elements produce 116 loaded files; 79 are
   skipped on a false condition and 1 wildcard matches nothing. None loads a file, but each costs
   expression expansion and, for the `Exists(...)` conditions gating most of them, a file probe.
   Mostly an SDK authoring question rather than an engine one.

---

## Pillar 2: Warm builds

In a warm build the XML is already parsed, so the profile inverts. Per-pass share of a warm
evaluation, three consecutive server builds:

| Pass | Share of warm evaluation |
| --- | ---: |
| **Properties (pass 1)** — imports, property expansion, conditions | **~58%** |
| **Targets (pass 5)** — `ProjectTargetInstance` / `ProjectTaskInstance` construction | **~22%** |
| Items (pass 3) including lazy item operations | ~10% |
| Item definitions, using-tasks, initial properties | remainder |

### How warm cost scales with project count

A real solution evaluates *different* projects sharing the same SDK imports. Median marginal cost per
project over 12 distinct console projects, in four cache regimes (two independent runs):

| Regime | Each later project | 12 projects |
| --- | ---: | ---: |
| Shared `ProjectCollection` + fully shared `EvaluationContext` | **12-14 ms** | ~270 ms |
| Shared `ProjectCollection` + `SharedSDKCache` — **closest to the build path** | **14-18 ms** | ~300 ms |
| Shared `ProjectCollection` + fully isolated context per project | 32-35 ms | ~475 ms |
| Fresh collection and context per project | 94-98 ms | ~1150 ms |

**The build path is the second row.** `BuildRequestConfiguration.InitializeProject` constructs
`ProjectInstance` without an `EvaluationContext`, so `ProjectInstance.Initialize` creates an
`Isolated` one per project — but it *does* pass a shared `sdkResolverService` explicitly, and project
XML is shared through `BuildParameters.ProjectRootElementCache`. So SDK resolution and parsed XML are
already shared across projects within a build; only the file existence cache and glob cache are not.

Those two are worth very different amounts:

| What is not shared | Marginal cost added |
| --- | ---: |
| SDK resolution across projects (**already shared today**) | ~18 ms per project |
| File existence and glob caches (**not shared today**) | **~3 ms per project** |

So the headroom from sharing the `EvaluationContext` is ~3 ms per project, about 20% of the marginal
term — not the large win it first appears. `SharingPolicy.SharedSDKCache` specifically would add
**nothing**, because the build path already shares SDK resolution.

| Projects | Today (~16 ms marginal) | With shared file caches (~13 ms) |
| ---: | ---: | ---: |
| 12 | ~300 ms | ~270 ms |
| 100 | ~1.7 s | ~1.4 s |
| 500 | ~8 s | ~6.6 s |

### What to do

1. **Reduce the irreducible per-project cost — the largest warm item.** Of the ~16 ms a project costs
   today, only ~3 ms is missing cache sharing; the remaining ~13 ms is genuine re-evaluation. Every
   project re-evaluates all ~116 SDK files' property groups, item groups and targets even though the
   XML is parsed and the SDKs are resolved. Pass 1 (~58%) and pass 5 (~22%) are the hot code:
   `Expander`, `ConditionEvaluator`, `LazyItemEvaluator`, and `ProjectTargetInstance` construction.
   On a 500-project solution this is ~6.6 s of the ~8 s evaluation total.

   The structural idea: MSBuild's import order is project → `Sdk.props` → SDK props → project body →
   `Sdk.targets` → SDK targets. The prefix through `Sdk.props` depends only on global properties and
   the SDK, not on the project body, so for projects sharing global properties it produces identical
   results today and is recomputed per project. Snapshotting and reusing that prefix is the only idea
   here that attacks the marginal term structurally rather than by micro-optimization. Its
   invalidation story is in [pillar 3](#pillar-3-caching-between-builds).
2. **Share the file existence and glob caches across projects within a build.** ~3 ms per project,
   ~20% of the marginal term. The context is `Isolated` today for correctness reasons; the safe
   boundary is discussed under invalidation below.
3. **Adopt partial evaluation where targets are not needed.** `ProjectEvaluationStage.UsingTasks` and
   `.Items` already skip target and using-task registration — 22% of a warm evaluation and 1.7 MB of
   allocation. The win depends on callers adopting it; a build itself needs targets, so this applies
   to tooling and query scenarios rather than to builds.

---

## Pillar 3: Caching between builds

Cross-build caching is not new — reused worker nodes have kept parsed XML for a long time, and the
server extends that to the entry-point process. But exactly one artifact is cached this way, and
everything else is rebuilt from scratch on every build. The remaining opportunity is gated entirely
on invalidation.

### What is cached, where, and for how long

Three different lifetimes matter. They are easy to conflate, and the differences drive everything
below.

**Within one build, across projects:**

| Cache | Shared across projects? | Owner |
| --- | --- | --- |
| Parsed project XML (`ProjectRootElement`) | **Yes** | `BuildParameters.ProjectRootElementCache` |
| SDK resolution results | **Yes** | `CachingSdkResolverService`, keyed `(submissionId, SDK name)` |
| Evaluated `ProjectInstance` per (project, global properties) | **Yes** | `ConfigCache` |
| Target results per configuration | **Yes** | `ResultsCache` |
| Glob results and file existence probes | **No** | `EvaluationContext`, created `Isolated` per project |

**Between builds in a reused *worker* node** (node reuse is on by default, no server required):

`OutOfProcNode` holds `static ProjectRootElementCacheBase s_projectRootElementCacheBase`, constructed
as `new ProjectRootElementCache(true /* automatically reload any changes from disk */)`. So parsed
project XML has *already* survived across builds in worker nodes for a long time. Measured above:
with node reuse on and the server off, evaluation across four consecutive 12-project builds falls
from 3231 ms to 1559 ms, while with `-nr:false` it stays flat. **The server did not introduce
cross-build XML caching**; it extends the same mechanism to the entry-point process, which is what
makes it visible on single-project builds where the entry node does the evaluating.

**Between builds in a reused *server* node:**

| Artifact | Survives? | Why |
| --- | --- | --- |
| Parsed project XML | **Yes** | `reuseProjectRootElementCache: s_isServerNode` selects a static `ProjectRootElementCache`, and passes `autoReloadFromDisk: reuseProjectRootElementCache` |
| JIT-compiled code, loaded assemblies | **Yes** | same process |
| `FileMatcher` / `FileUtilities` file-existence statics | **Yes**, unless the caller passes `BuildRequestDataFlags.ClearCachesAfterBuild` | process-wide statics |
| SDK resolution results | **No** | `BuildManager` calls `SdkResolverService.ClearCache(submissionId)` when each submission completes |
| Evaluated `ProjectInstance` / target results | **No** | `BuildParameters.ResetCaches` defaults to `true`, so `ConfigCache` and `ResultsCache` are reset per build |
| Glob and file existence results | **No** | `EvaluationContext` is per project, never mind per build |
| `ChangeWaves` / `Traits` ambient state | **Yes — unintentionally** | see [invalidation](#invalidation-the-deciding-constraint) |

Verified for SDK resolution: three consecutive builds in one server session each emit the same SDK
resolution work — ~20 ms per build that no current cache recovers. Verified for evaluation results:
warm server builds report ~49 ms of evaluation on *every* build, so nothing about the evaluation
itself is reused.

### How the one cross-build cache invalidates

`ProjectRootElementCache` is the only artifact deliberately kept across builds, so its invalidation is
the whole of MSBuild's current cross-build correctness story. On every `Get`, `IsInvalidEntry` decides
whether the cached entry may be used:

1. **`!_autoReloadFromDisk` → entry is always considered valid.** This is the non-reused case, where
   the cache lives only as long as the build, so nothing can change underneath it.
2. **`FileClassifier.Shared.IsNonModifiable(path)` → assumed valid, and the file is never stat-ed.**
   Immutable locations are registered by `RequestBuilder.ConfigureKnownImmutableFolders` during a
   build and include `NetCoreRoot` (the entire .NET SDK), `NuGetPackageFolders`,
   `FrameworkPathOverride`, and Microsoft reference assemblies. `MSBUILDDONOTCACHEMODIFICATIONTIME=1`
   forces the timestamp check even for these.
3. **File does not exist on disk → valid.** An in-memory project that was never saved.
4. **`fileInfo.LastWriteTime != projectRootElement.LastWriteTimeWhenRead` → invalid, reload from
   disk.** This is the actual invalidation.
5. Optionally, a full content comparison, behind a test-only environment variable.

On top of that there is structural eviction: `_weakCache` is a `WeakValueDictionary`, so entries can
be collected, and `_strongCache` is a bounded MRU list. An entry can therefore disappear and be
re-read even when it was still valid.

So the rule is: **path plus last-write-time, except inside directories declared immutable, where the
timestamp is not consulted at all.**

That carve-out is a deliberate performance tradeoff — it is what avoids stat-ing the 114 SDK files on
every lookup — but it does mean that editing a file inside the SDK or the NuGet package cache is not
guaranteed to be observed by a reused node. In practice an edit to an SDK `.props` file *was* picked
up in a reused server session in testing, but that cannot be attributed to the timestamp check, since
a weakly-held entry may simply not have survived. **The carve-out should be treated as "staleness is
permitted here", not as "staleness cannot happen".** Anyone developing SDK content against a reused
node should set `MSBUILDDONOTCACHEMODIFICATIONTIME=1` or shut the server down between iterations.

### Invalidation: the deciding constraint

Cross-build caching is only as good as its invalidation. The measured evidence splits cleanly along
one axis.

**The file axis is sound, and this is proven.** Six mutations applied between builds in a single
server session, each probed by re-reading the affected property or item:

| # | Mutation between builds | Picked up? |
| --- | --- | --- |
| 1 | baseline (fresh server) | — |
| 2 | edit a property in the project file | **yes** |
| 3 | add a file matching a glob | **yes** |
| 4 | edit an imported `.props` file | **yes** |
| 5 | create a file that an `Exists(...)` import condition tests | **yes** |
| 6 | delete that file again | **yes** |

All six behave correctly, for the reasons in
[How the one cross-build cache invalidates](#how-the-one-cross-build-cache-invalidates): cases 2 and 4
are caught by the last-write-time check, and cases 3, 5 and 6 never involve the cross-build cache at
all, because glob results and `Exists(...)` probes live in the per-project `EvaluationContext`.

The deeper reason the timestamp key is *sound* rather than merely adequate: the construction model is
a pure function of a file's bytes. `ProjectParser.Parse` is purely syntactic — an `<Import>` keeps its
`Project` and `Condition` attributes as literal, unexpanded strings, and even the implicit
`Sdk.props`/`Sdk.targets` imports synthesized from an `Sdk="..."` attribute carry the SDK name
unresolved. Global properties decide *which* files get parsed, never *what* parsing a file produces,
which is why a cache keyed on path alone is shared safely across projects with different global
properties.

A corollary worth stating because it is easy to get wrong: **"conditionally imported" and "cacheable"
are orthogonal.** Conditionality is a property of the `<Import>` element; immutability is a property
of the file. `Microsoft.NET.Sdk.VisualBasic.props` is one of the 79 imports skipped on a false
condition here, yet it is the same immutable file a Visual Basic project loads unconditionally.
Conditions run exactly as they do today, before any cache lookup, so a cache never changes which
files an evaluation loads.

**The ambient-state axis is broken, and this is also proven.** Environment-derived static state is
*not* reset between builds. `ChangeWaves` caches its parsed wave on first use and never re-reads
`MSBUILDDISABLEFEATURESFROMVERSION` (`ShouldApplyChangeWave` is true only while
`ConversionState == NotConvertedYet || _cachedWave == null`):

| `MSBUILDDISABLEFEATURESFROMVERSION` | Without server | With server |
| --- | --- | --- |
| `17.0` | `17.10` | `17.10` |
| `99.0` | `18.10` | `17.10` (**stuck on the first build's value**) |
| `17.0` | `17.10` | `17.10` |

The worse variant needs no server at all. Out-of-proc worker nodes are reused by default while the
entry-point process is fresh each invocation, so **projects within a single build disagree** about
which change waves are enabled depending on which node evaluated them. With `-m:4` and the server
explicitly off, reproduced identically on three consecutive trials:

```
set=17.12  -> 1:17.12  2:17.12  3:17.12  4:17.12
set=18.5   -> 1:18.5   2:17.12  3:17.12  4:17.12
                       ^^^^^^^^^^^^^^^^^^^^^^^^^ stale, from the previous build
```

Tracked as [dotnet/msbuild#14547](https://github.com/dotnet/msbuild/issues/14547). It also undermines
the general refresh mechanism, because `Traits.UpdateFromEnvironment()` — which both `OutOfProcNode`
and `OutOfProcServerNode` already call per build — is itself gated on
`ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10)`, so stale change-wave state can freeze
`Traits` refresh too.

**The principle that falls out:** caches keyed on *file content identity* are safe and already work;
caches of *ambient process state* have no key at all, and the one place MSBuild caches ambient state
is already wrong. Any new cross-build cache must be keyed on file identity, or must explicitly
enumerate and key on the ambient inputs it depends on.

A third category is worth naming because it is neither: **caches that are deliberately not
invalidated**, like the immutable-directory carve-out above. Those are sound only as long as the
"immutable" declaration holds, and they trade correctness under SDK authoring for speed under normal
use. New caches should prefer an explicit key over an assumed-immutable declaration where the cost
allows it.

### What to do, in order of confidence

1. **Fix per-build state isolation.** This gates the entire pillar. If a reused process produces
   wrong results, reuse gets switched off and both the server's 7.5x and node reuse's ~50% are lost
   with it. Note this is not a server-only concern: the change-wave leak is *worse* on reused worker
   nodes, which are the default and predate the server. Reset `ChangeWaves` alongside the existing
   `Traits.UpdateFromEnvironment()` calls in `OutOfProcNode` and `OutOfProcServerNode`, ordered so
   `ChangeWaves` refreshes first; audit the remaining environment-derived statics for the same
   cached-on-first-use-with-no-reset pattern; give reused nodes one explicit "reset per request" path
   instead of today's scattered resets; add regression tests covering both the server and the
   multi-proc worker-node case.
2. **Cache SDK resolution across builds in a session.** ~20 ms per build, currently discarded by
   `ClearCache(submissionId)`. Invalidation is tractable: the result is a function of the SDK name and
   the resolver's own inputs (installed SDK, workload manifests on disk), all of which are files, so
   the sound-axis rule applies. The cache must be invalidated when those manifests change; a
   last-write-time check over the manifest set mirrors what `ProjectRootElementCache` already does.
   Self-contained within MSBuild; making it survive across *processes* additionally needs
   coordination with `dotnet/sdk`.
3. **Share the file existence and glob caches across projects within a build.** ~3 ms per project.
   The context is `Isolated` today because a shared existence cache can serve stale results if a build
   writes a file that a later project imports — a real risk, since builds generate files. The safe
   boundary already exists: `FileClassifier.IsNonModifiable` identifies the SDK install directory,
   where **114 of the 116 imported files** live. Sharing probe results only for paths that cannot
   change during a build captures most of the win with no staleness risk.
4. **Persist the parsed construction model across processes.** Sound by the file-axis rule, and its
   invalidation is already proven by the six tests above. The ceiling has moved for the *inner loop* —
   reused nodes already keep this resident within a session, so persisting it across *processes* helps
   the first build on each node rather than every build. But note that is not a small population: a
   cold `-m:8` build has eight such nodes, each re-parsing the same SDK files independently
   (see [what "cold" scopes to](#what-cold-actually-scopes-to)), so an on-disk cache read by every
   node saves the duplication as well as the first parse. Two open questions decide whether the prize
   is collectible: **deserialization is not free** (the win exists only if materializing a cached
   model beats 5.7 ms of tokenizing plus 4.2 ms of DOM construction), and **`ProjectRootElement` is
   mutable and owns an `XmlDocument`**, so a shared cache needs a read-only or copy-on-write form.
5. **Caching whole evaluation results — the largest prize and the hardest invalidation.** Warm
   evaluation is ~49 ms per build for one project and ~16 ms per project at scale; caching results
   across builds would remove nearly all of it. But a sound key must cover every input: every
   imported file's content, all global properties, the environment variables actually read, every
   `Exists(...)` result, every glob result, and every property function result. MSBuild cannot
   currently enumerate those, because the file system abstraction is not a complete choke point —
   `XmlReaderExtension` opens a `FileStream` directly and `GetFileInfoNoThrow` stats directly, both
   bypassing `IFileSystem`. **Closing that leak is the prerequisite**, and is worth doing on its own
   merits regardless of whether this cache is ever built.

   The `Sdk.props`-prefix idea from pillar 2 is the tractable subset of this: its inputs are just the
   global properties and the SDK, both of which are known before evaluation starts, so it can be keyed
   soundly without solving general input tracking.

---

## Method

Four independent lenses, cross-checked against each other:

| Lens | What it answers | Mechanism |
| --- | --- | --- |
| Event source markers | Wall clock per phase, including time blocked on I/O | In-process `EventListener` on `Microsoft-Build` |
| CPU profile | Where time goes below the markers, including GC and syscalls | `dotnet-trace`, stacks folded into categories |
| Injected file system | Logical versus real file operations, redundant probing | `MSBuildFileSystemBase` via `EvaluationContext` |
| BenchmarkDotNet | Statistically valid wall clock and allocation | `FullEvaluationBenchmark` with `MemoryDiagnoser` |

They agree: the marker-derived cold total (90 ms in-process) matches BenchmarkDotNet's mean (90.2 ms);
the harness's allocation figures match `MemoryDiagnoser`'s; and the marker breakdown's ~52% for
document loading and parsing is consistent with the CPU profile's syscall and XML shares.

End-to-end build numbers use `/profileevaluation` corrected for its own overhead, measured by running
the same build with and without the profiler: **88 ms warm, 178 ms cold**. Reported evaluation totals
of 137 ms warm and 546 ms cold therefore correspond to ~49 ms and ~368 ms.

Caveats:

* **The OS file cache is warm in all measurements.** A genuinely cold disk makes the I/O categories
  larger, not smaller, so the conclusions here are conservative.
* **`/profileevaluation` is not free**, hence the correction above. Per-element tracking also
  distorts *proportions* toward scopes with many elements, so pass-level shares from it are treated
  as approximate and the precise breakdowns come from the in-process lenses.
* **Steady state takes ~70 evaluations to reach.** Any evaluation benchmark warming up less than that
  is measuring tiered JIT compilation.
* **This is one small project.** More source files shift weight toward globbing and item evaluation;
  more `ProjectReference`s shift it toward the marginal term. The 116 SDK imports are the same for
  every SDK-style project, so the shape generalizes even where the absolute values do not.

## Reproducing these numbers

Build the repository in Release, then:

```powershell
# Marker, file system, allocation and XML breakdown for one project.
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --analyze --output breakdown.md

# Per-project marginal cost across many distinct projects, in four cache regimes.
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --analyze --multi-project <directory>

# Statistically valid wall clock and allocation.
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --filter *FullEvaluationBenchmark*

# CPU profile folded into categories (needs dotnet-trace and Python 3).
./src/MSBuild.Benchmarks/Analysis/Collect-EvaluationProfile.ps1 -Iterations 120
```

The harness creates and restores its own `dotnet new console` fixture. Pass `--project <path>` to
analyze a different project. See
[`src/MSBuild.Benchmarks/Analysis/README.md`](../src/MSBuild.Benchmarks/Analysis/README.md).

## Related documents

* [MSBuild Server](MSBuild-Server.md)
* [Partial (stop-after-pass) project evaluation](specs/proposed/partial-evaluation.md)
* [MSBuild evaluation profiling](evaluation-profiling.md) (`/profileevaluation`)
* [Event source markers](specs/event-source.md)
* [Evaluation performance investigations](specs/proposed/evaluation-perf.md)
* [dotnet/msbuild#14547](https://github.com/dotnet/msbuild/issues/14547) — ChangeWaves state is not
  reset between builds in reused nodes
