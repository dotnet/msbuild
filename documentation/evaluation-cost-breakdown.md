# Evaluation performance: where the time goes and what to do about it

Measured on `dotnet/msbuild` at `f08c806268` (18.11.0), .NET 11.0.100-preview.7, Windows, 8 logical
cores, Release. Subjects: a `dotnet new console` project for continuity with the previous revision of
this document, and [OrchardCore](https://github.com/OrchardCMS/OrchardCore) (241 projects) as the
real-world subject. Absolute numbers are machine specific; ratios and shares transfer.

Every claim below is measured. Where a measurement failed to separate an effect from noise, it says
so rather than reporting the number.

## What changed since the previous revision

Three things moved, and together they invalidate the previous framing.

1. **The restore flush is fixed.** [#14558](https://github.com/dotnet/msbuild/pull/14558) landed as
   `ProjectRootElementCacheBase.ClearCachesAfterBuildIfNeeded`, gated on ChangeWave 18.11. A cache
   that reloads from disk now survives restore instead of being discarded wholesale.
2. **MSBuild Server is on by default** — but not from MSBuild. `ShouldUseMSBuildServer` is unchanged
   and still returns true only for `MSBUILDUSESERVER=1` or `-mt`. The default lives in `dotnet/sdk`
   (`MSBuildForwardingAppWithoutLogging`), which sets `MSBUILDUSESERVER` when it is unset.
3. **`-mt` is scheduled for .NET 11.0.2xx.** It turns out to matter far more than the server does.

### A measurement trap worth stating first

**This repository's own bootstrap does not reproduce shipping default behaviour.** The pinned SDK
(`11.0.100-preview.7.26360.111`) predates the `dotnet/sdk` change; none of `MSBUILDUSESERVER`,
`DOTNET_CLI_USE_MSBUILD_SERVER` or `DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER` appear anywhere in its
binaries. Verified empirically: a plain `dotnet build` through the bootstrap produces **no**
`/nodemode:8` process, while `MSBUILDUSESERVER=1` produces one.

Anyone benchmarking MSBuild with the bootstrap and no environment variable is measuring a
configuration that no longer ships. Every server number in this document sets `MSBUILDUSESERVER=1`
explicitly, which is a faithful emulation because that variable is the only lever the SDK has.

## Baseline

One evaluation of each subject, cold (fresh `ProjectCollection`, isolated `EvaluationContext` — what
a command line build does per project) and warm (both shared — what design-time and project graph
re-evaluation do).

| | console | OrchardCore.Contents |
| --- | ---: | ---: |
| Imported files | 116 | 90 |
| Properties / items / targets | 735 / 448 / 504 | 443 / 546 / 174 |
| Cold evaluation | 110.7 ms | 72.9 ms |
| Warm evaluation | 9.0 ms | 4.9 ms |
| Allocated, cold | 13.5 MB | 8.1 MB |

Where a cold evaluation goes, by exclusive time:

| Scope | console | OrchardCore.Contents |
| --- | ---: | ---: |
| `LoadDocument` (read + parse + locate XML) | 47.2% | 33.8% |
| `SdkResolverResolveSdk` | 20.5% | 21.9% |
| `ExpandGlob` | 3.7% | **15.1%** |
| `ApplyLazyItemOperations` | 3.7% | 17.5% |
| `ReadTargetElements` | 5.9% | 4.2% |
| `Parse` (XML tokenize/DOM, inside `LoadDocument`) | 4.7% | 4.5% |

The console project understates globbing by a factor of four. It has 27 files in its cone;
`OrchardCore.Contents` has 357. Any conclusion about glob cost drawn from a `dotnet new console`
fixture is wrong, which is why this revision uses a real solution as the primary subject.

`LoadDocument` decomposes as: reading bytes 25.5%, tokenizing XML 28.5%, building a stock DOM 12.2%,
**attaching element locations 33.9%**. Location tracking — the line/column information behind error
messages — is the single largest layer of XML loading, larger than the parse itself.

## Finding 1: a single target framework spelled in the plural doubles evaluation

This is the largest cheap win found in this pass.

A project that declares `<TargetFrameworks>net11.0</TargetFrameworks>` is evaluated **twice** per
build: once with no `TargetFramework` global property (the outer, framework-negotiating evaluation)
and once with `TargetFramework=net11.0` (the inner build). A project that declares
`<TargetFramework>net11.0</TargetFramework>` is evaluated once. The two evaluations differ in exactly
one global property, and the second repeats all five passes of the first.

Minimal fixture, one library plus one referencing app:

| Library declares | Evaluations in the build |
| --- | ---: |
| `<TargetFrameworks>net11.0</TargetFrameworks>` | 3 |
| `<TargetFramework>net11.0</TargetFramework>` | 2 |

Scaled to 20 libraries plus an app, identical in every other respect, both configurations building
successfully, server enabled, warm, median of 5:

| Library declares | Evaluations / build | Evaluation CPU | Wall clock |
| --- | ---: | ---: | ---: |
| `TargetFrameworks` (plural, one value) | 40 | 12 042 ms | 12 525 ms |
| `TargetFramework` (singular) | 20 | 8 048 ms | **4 748 ms** |

**2.6x wall clock, from changing the name of a property.** The plural runs were also far noisier
(6 368–26 560 ms, versus 4 328–5 466 ms singular); even comparing the *fastest* plural run against the
median singular run, singular wins by 25%.

This shape is extremely common: repositories set `TargetFrameworks` from a shared property so that
adding a second framework later is a one-line change, and pay for cross-targeting machinery they do
not use. OrchardCore is exactly this — `CommonTargetFrameworks` is `net10.0`, assigned to
`<TargetFrameworks>` in three `Directory.Build.props` files — and a warm inner-loop build of one of
its modules performs **82 evaluations for 41 projects**, every pair differing only by
`TargetFramework=net10.0`.

Two possible fixes, in increasing order of blast radius:

* **In the SDK**: when `TargetFrameworks` resolves to exactly one framework, skip the outer/inner
  split and build it as a single-framework project. This benefits every repository using the idiom
  without any of them changing anything. It is a behavioural change — `$(TargetFrameworks)` being set
  is observable, and cross-targeting also changes output layout and packaging — so it wants a change
  wave and a careful audit of what keys off "is this a cross-targeting build".
* **In repositories**: use the singular property when there is one framework.

Honest limitation: patching OrchardCore's three `Directory.Build.props` to the singular property did
halve its evaluations from 82 to 41, but the resulting build failed in my constrained setup (I was
using `-p:BuildProjectReferences=false`, and a source generator dependency went unbuilt). The
controlled 20-project measurement above is the one to trust; the OrchardCore run establishes only the
evaluation count, not a timing result, and does not establish that OrchardCore can simply make this
change.

## Finding 2: `-mt` is worth far more than the server, because it collapses per-node duplication

Cold evaluation cost is paid **per node**, not per build. A default `-m` build starts up to eight
worker nodes, each with its own `ProjectRootElementCache`, each parsing the same SDK independently.
`-mt` collapses all of them into one process with one cache.

Inner loop, `OrchardCore.Contents` with `-p:BuildProjectReferences=false`, warm median of 7:

| Regime | Cold | Warm |
| --- | ---: | ---: |
| no server | 6 228 ms | 4 425 ms |
| server | 6 102 ms | 4 560 ms |
| server + `MsBuildCacheFileEnumerations` | 6 337 ms | 4 210 ms |
| **server + `-mt`** | **4 195 ms** | **1 796 ms** |

**The server on its own buys nothing measurable here** (4 560 ms versus 4 425 ms without it — the
difference is inside the noise, and the wrong way round). `-mt` is 2.5x.

The reason is visible in evaluation cost across consecutive builds in one session:

| Run | server: evaluation CPU | server + `-mt`: evaluation CPU |
| ---: | ---: | ---: |
| 1 | 16 960 ms | 10 633 ms |
| 2 | 16 063 ms | 10 608 ms |
| 3 | 13 635 ms | 6 594 ms |
| 4 | 12 948 ms | 4 751 ms |
| 5 | 13 727 ms | **4 628 ms** |

Under the plain server, evaluation CPU flattens out around 13 s and stops improving. Under `-mt` it
falls to 4.6 s — **3x less evaluation work for the same build** — because the XML is parsed once
instead of once per node, and the JIT warms once instead of once per node.

This reframes the server's value. The server saves entry-process startup and keeps the entry node's
XML cache warm, but in a multi-project build the entry node is not where evaluation happens: worker
nodes are, and those already survived between builds via node reuse. `-mt` is the change that
actually removes duplicated evaluation work.

## Finding 3: evaluation results are never reused between builds

In every one of the consecutive runs above, the profiler reports **81 evaluations**. Not 80, not 40 —
the same 81, every build, in a warm server session with nothing changed on disk.

Only *XML* is cached across builds (and only in the entry node, and now only correctly since
[#14558](https://github.com/dotnet/msbuild/pull/14558)). Everything downstream of the parse — the five
evaluation passes, property and item computation, glob expansion, target registration — is redone in
full for every project on every build.

So the ceiling on cross-build caching is large: under `-mt` steady state, a build that changes nothing
still spends 4.6 s of CPU re-deriving evaluation results that are bit-for-bit identical to the
previous build's.

What stands between here and there is invalidation, and the shape of the key is known:

* **Project file and its entire import closure**, by path and last-write-time. `ProjectRootElementCache`
  already does exactly this, so the mechanism exists and is proven.
* **Global properties**, which is what makes the outer and inner evaluations of Finding 1 distinct.
* **Environment variables read during evaluation**, which is the hard part — evaluation can read
  arbitrary environment state through property functions, and there is no record of which variables a
  given evaluation actually consulted. Making this tractable probably means recording the set of
  environment reads during evaluation and keying on it, the same way the file closure is keyed.

The immutable-directory carve-out below is what makes the file half cheap: files under the SDK
install and the NuGet package folder are not stat-ed at all.

## Finding 4: the optimized `FileMatcher` regresses evaluation-time globbing

[PR #14663](https://github.com/dotnet/msbuild/pull/14663) introduces an optimized wildcard matcher
behind ChangeWave 18.11. Measured from the evaluation side, same binary, wave on versus off, median of
3 alternating runs:

| Project | files / dirs | optimized | legacy | ratio |
| --- | ---: | ---: | ---: | ---: |
| `dotnet new console` | 27 / 8 | 1.02 ms | 3.70 ms | 0.28x (faster) |
| `OrchardCore.OpenId` | 100 / 27 | 7.46 ms | 6.41 ms | 1.16x |
| `OrchardCore.Contents` | 357 / 62 | 16.61 ms | 10.91 ms | **1.52x** |

The three `OrchardCore.Contents` pairs do not overlap (optimized 16.0–17.3 ms, legacy 9.7–11.4 ms).
The regression grows with tree size, which is backwards: large trees are where glob cost matters. For
that project it is +5.7 ms on a ~73 ms evaluation, so **roughly +8% of the whole cold evaluation**.

Two structural notes, both established by reading the code rather than by measurement:

* `CanUseDirectEnumeration` requires `!_usesFileSystemEntryCache`, and `EvaluationContext` always
  constructs its `FileMatcher` with a `FileEntryExpansionCache`. The direct `FileSystemEnumerator`
  traversal that produces the PR's headline numbers is therefore **unreachable from evaluation**;
  evaluation always takes the cached-callback path.
* Warm, shared-context re-evaluation drops `ExpandGlob` to ~1% of evaluation because glob results are
  cached in the `EvaluationContext`. The regression is specific to cold, isolated-context evaluation —
  which is what a command line build does for every project.

The independent review on that PR attributes the regression to the optimized drivers being
single-threaded where the legacy path fans out with `Parallel.ForEach`, demonstrated by the ratios
collapsing to ~1.0x when pinned to one core. That is a better-supported root cause than anything
measurable from the evaluation side, and it predicts the regression worsens with core count.

## What is cached, where, and for how long

Between builds in a reused **server** node:

| Artifact | Survives? | Why |
| --- | --- | --- |
| Parsed project XML | **Yes** | static `ProjectRootElementCache` with `autoReloadFromDisk`, and since #14558 restore no longer discards it |
| JIT-compiled code, loaded assemblies | **Yes** | same process |
| Glob expansions | **No** (opt-in) | `s_cachedGlobExpansions` is process-wide but only used when `MsBuildCacheFileEnumerations` is set; otherwise the cache lives on the per-build `EvaluationContext` |
| SDK resolution results | **No** | `BuildManager` calls `SdkResolverService.ClearCache(submissionId)` as each submission completes |
| Evaluated `ProjectInstance` / target results | **No** | `BuildParameters.ResetCaches` defaults to true |
| `FileMatcher` / file-existence statics | **No** after restore | deliberately cleared; they hold negative results no timestamp can invalidate |

SDK resolution is worth calling out: it is 20–22% of a cold evaluation, dominated by
`Microsoft.DotNet.MSBuildWorkloadSdkResolver` (16.7–22.4 ms), and it is discarded at the end of every
submission. Within a build the cache is shared, so this is a fixed per-build cost rather than a
per-project one — modest next to Findings 1 and 2, but it is pure repetition, and the inputs
(installed SDKs, workload manifests) are files, so the invalidation story is the tractable one.

### How the surviving cache invalidates

`IsInvalidEntry`, on every `Get`:

1. `!_autoReloadFromDisk` → always valid (the non-reused case; the cache dies with the build).
2. `FileClassifier.Shared.IsNonModifiable(path)` → assumed valid, **the file is never stat-ed**.
   Registered immutable locations include the entire .NET SDK install and the NuGet package folders.
3. File missing on disk → valid (an in-memory project never saved).
4. `LastWriteTime != LastWriteTimeWhenRead` → invalid, reload.

Step 2 returns before step 4 ever runs, so for anything under an immutable root the timestamp is
never consulted. Editing a file inside the SDK is therefore **not guaranteed** to be observed by a
reused node; if an edit does appear it is structural eviction from the weak cache, not invalidation.
Anyone authoring SDK content against a reused node should set `MSBUILDDONOTCACHEMODIFICATIONTIME=1`
or shut the server down between iterations.

## What to do, in order of confidence

1. **Make a one-value `TargetFrameworks` stop meaning "cross-target".** Measured 2.6x wall clock on a
   controlled 20-project tree and a halving of evaluation count on OrchardCore. Cheapest measured win
   available, and it needs no new caching or invalidation machinery. Needs a change wave and an audit
   of what keys off cross-targeting.
2. **Ship `-mt`.** Measured 2.5x on the inner loop and 3x less evaluation CPU, because it removes the
   per-node duplication that node reuse never addressed. Nothing else in this document comes close for
   a multi-project build.
3. **Do not default ChangeWave 18.11 to the optimized `FileMatcher` until wall-clock parity is
   demonstrated** on a real, SDK-shaped tree at >= 8 cores. As measured it is a regression for
   evaluation-time globs, worsening with tree size, and the fast path it was benchmarked on cannot be
   reached from evaluation.
4. **Cache evaluation results across builds.** The prize is the whole 4.6 s of steady-state evaluation
   CPU that a no-op `-mt` build still spends. The file half of the key is already solved by
   `ProjectRootElementCache`; global properties are easy; environment reads are the open problem and
   should be scoped first, because they decide whether this is feasible at all.
5. **Cache SDK resolution across builds in a session.** ~17–22 ms per build, currently discarded per
   submission. Small, but it is pure repetition and its inputs are files.
6. **Attack XML location tracking.** 33.9% of `LoadDocument`, which is itself 34–47% of a cold
   evaluation — so roughly 12–16% of cold evaluation is spent recording line and column numbers.
   Worth an experiment in storing locations more compactly, or lazily for files that never produce a
   diagnostic. This only pays where XML is still being parsed, so it shrinks as Findings 2 and 4 land.

## Method

Numbers come from four lenses, described in
[`src/MSBuild.Benchmarks/Analysis/README.md`](../src/MSBuild.Benchmarks/Analysis/README.md):

* **Event source markers** (`Microsoft-Build`), reconstructed into an inclusive/exclusive tree. This
  is what produces the per-scope tables. Listener overhead is measured and reported alongside.
* **An injected `MSBuildFileSystemBase`** that counts logical versus real file system operations.
* **Allocation and GC** via `GC.GetTotalAllocatedBytes` and `GC.CollectionCount`.
* **Binary logs**, read with the binlog tooling, for evaluation counts and global properties per
  evaluation in real builds.

Cautions learned the hard way in this pass:

* `-profileevaluation` reports evaluation summed over all projects and all nodes, so it routinely
  exceeds wall clock on a parallel build. It is a measure of *work*, not of elapsed time.
* Toggling a change wave to A/B a feature is only valid once you have enumerated everything else that
  wave gates. Wave 18.11 gates the optimized matcher, the restore-flush fix, two out-of-proc node
  changes and three `Xml*` task defaults; in an in-process, evaluation-only harness only the first is
  reachable, which is what makes the toggle a clean control *there* and nowhere else.
* Wall clock on multi-project builds was too noisy on this machine to separate effects smaller than
  about 20%; evaluation counts and marker times were stable enough to use. Where only the noisy lens
  was available, this document says so instead of reporting a number.
* Reaching evaluation steady state takes roughly 60–80 evaluations.

## Reproducing these numbers

```powershell
./build.cmd -configuration Release

# Marker, file system, allocation and XML breakdown for one project.
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --analyze --project <path>

# Per-project marginal cost across many distinct projects, in four cache regimes.
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --analyze --multi-project <dir>

# Statistically valid wall clock and allocation.
dotnet artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll --filter *FullEvaluationBenchmark*
```

Set `MSBUILDUSESERVER=1` for any server measurement; the bootstrap will not do it for you.

## Related documents

* [`documentation/MSBuild-Server.md`](MSBuild-Server.md)
* [`documentation/specs/proposed/evaluation-perf.md`](specs/proposed/evaluation-perf.md)
* [`documentation/wiki/ChangeWaves.md`](wiki/ChangeWaves.md)
