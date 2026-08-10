# Where a .NET build spends its time: cold, warm, no-op, incremental and clean

Measured 2026-08-10 on the daily .NET SDK **11.0.100-rc.1.26410.104** (MSBuild 18.11.0-1.26410.104),
Windows 11, 8 logical cores, warm OS file cache.

**Baseline for every number in this document: MSBuild Server plus `-mt`.** Server is on by default in
this SDK — verified, not assumed: a plain `dotnet build` produces a `nodemode:8` process with no
environment variable set. `-mt` is passed explicitly via `dotnet build -mt`, since it is scheduled to
become the default in .NET 11.0.2xx.

Subjects, chosen to span three orders of magnitude of project count:

| Subject | Projects | Notes |
| --- | ---: | --- |
| `dotnet new console` | 1 | floor case |
| synthetic solution | 11 | 10 libraries + app, 5 sources each, singular `TargetFramework` |
| [OrchardCore](https://github.com/OrchardCMS/OrchardCore) | 241 | real solution, heavy package graph, Razor |

## The matrix

Wall clock, median of 5 (cheap scenarios) or 3 (clean scenarios), `dotnet build --no-restore -mt`.
Restore is excluded throughout so that these are build numbers, not package-graph numbers.

"Cold" means **no MSBuild process state** — server and all nodes shut down before the timed build.
The OS file cache is warm in every case, so this measures MSBuild's own cold start, not a cold machine.

| Scenario | console | 11 projects | OrchardCore (241) |
| --- | ---: | ---: | ---: |
| warm no-op | 609 ms | 818 ms | **11 598 ms** |
| warm incremental (1 file touched) | 888 ms | 826 ms | 13 830 ms |
| cold no-op | 1 395 ms | 2 106 ms | 24 971 ms |
| warm clean | 733 ms | 2 016 ms | 64 727 ms |
| cold clean | 3 963 ms | 5 989 ms | 147 316 ms |

Three things fall straight out of this table.

## Finding 1: the cold tax is ~2.2x and it is remarkably size-independent

| Subject | no-op cold/warm | clean cold/warm |
| --- | ---: | ---: |
| console | 2.29x | 5.41x |
| 11 projects | 2.57x | 2.97x |
| OrchardCore | 2.15x | 2.28x |

Shutting the server down costs roughly **2.2–2.6x on a no-op build** at every scale from one project to
241. In absolute terms that is 0.8 s for a console app and 13.4 s for OrchardCore — the tax scales with
the work, not with a fixed process-start constant, which means it is dominated by re-parsing and
re-JITting rather than by spawning a process.

The console clean-build ratio of 5.41x is the outlier and the most striking single number here: a
trivial project takes **733 ms warm and 3 963 ms cold**, so 3.2 s of a 4 s build is pure cold start.
That is the number a developer experiences when they run their first build after lunch, and it is
almost entirely recoverable process state.

## Finding 2: evaluation work is completely invariant to what needs building

This is the finding with the largest headroom behind it.

Evaluation count, from `-profileevaluation`, over all five scenarios:

| Subject | warm no-op | warm inc | cold no-op | warm clean | cold clean |
| --- | ---: | ---: | ---: | ---: | ---: |
| 11 projects | 10 | 10 | 10 | 10 | 10 |
| OrchardCore | **463** | **463** | **463** | **463** | **463** |

A no-op build of OrchardCore — nothing changed on disk, every output up to date, the server already
warm — performs exactly the same 463 evaluations as a full clean build from an empty `obj`. Evaluation
is not merely uncached across builds; it is not even sensitive to whether there is anything to do.

463 evaluations for 241 projects is ~1.9 each, which is the outer/inner cross-targeting split
(`TargetFrameworks` with a single entry, filed as
[dotnet/sdk#55699](https://github.com/dotnet/sdk/issues/55699)).

The ceiling this implies: an 11.6 s warm no-op build of OrchardCore is spending a large share of its
time re-deriving property and item state that is bit-for-bit identical to the previous build's. The
inputs to that derivation are files (already keyed by `ProjectRootElementCache`), global properties
(trivially keyable), and environment reads (the hard part, and the thing to scope first).

## Finding 3: at scale, the dominant task in a no-op build is `ResolveAssemblyReference`

Task cost from binary logs, summed over all projects and all threads. OrchardCore, 232 executions:

| Scenario | `ResolveAssemblyReference` | avg/project | `Csc` |
| --- | ---: | ---: | ---: |
| warm no-op | **28 462 ms** | 122 ms | — |
| cold no-op | **34 879 ms** | 150 ms | — |
| warm incremental | 8 939 ms | 38 ms | 11 961 ms (2 projects) |
| warm clean | 5 514 ms | 23 ms | 204 319 ms |
| cold clean | not in top 5 | — | 449 424 ms |

In a build where **nothing is compiled at all**, RAR is the single most expensive task by a wide margin
— more than 28 s of CPU, roughly 122 ms per project. The target view agrees:
`ResolveAssemblyReferences` 28 705 ms, then `FindReferenceAssembliesForReferences` 7 724 ms,
`CopyFilesToOutputDirectory` 2 914 ms, `GenerateMSBuildEditorConfigFileCore` 2 661 ms.

The counter-intuitive part, which reproduces on both cold and warm runs, is that **RAR is ~5x more
expensive in a no-op build than in a clean build** (122 ms versus 23 ms per project). I have measured
this consistently but have *not* established the cause, so this is a finding to investigate rather than
a conclusion. The obvious hypothesis — that a populated `bin` directory gives RAR far more candidate
files to probe than an empty one — is plausible and untested.

This is scale-dependent, and the small subject shows why: on the 11-project solution RAR is only
11 ms per project, because those libraries have almost no references. RAR cost tracks the size of the
*reference graph*, not the project count. It is a large-solution problem.

## Finding 4: `-mt` is worth 1.4–1.9x over classic multiproc, and needs no `-m`

Warm no-op, same subject, same SDK:

| Regime | 11 projects | OrchardCore | node processes |
| --- | ---: | ---: | --- |
| `-mt` | 961 ms | 13 439 ms | 8x TaskHost, 1x server |
| `-mt -m:8` | 940 ms | 10 390 ms | 8x TaskHost, 1x server |
| classic `-m:8` | 1 492 ms | 19 123 ms | 7x worker, 1x server |
| classic (default) | 1 810 ms | 17 903 ms | 7x worker, 1x server |

`-mt` and `-mt -m:8` are the same within noise on the small subject, which settles a real question: in
this SDK `-mt` already parallelises through `dotnet build` without an explicit `-m`. (An earlier
revision of this document warned that `MaxNodeCount` defaults to 1 and that `-mt` alone might be
serial; that is not what the shipped path does.)

Note that `-mt` does **not** collapse everything into one process: 8 TaskHosts still appear, because
Roslyn's `Csc` is not annotated as multithread-safe. What it removes is the *worker node* layer — and
with it the per-node duplication of XML parsing and JIT, which is exactly where the win comes from.

On clean builds the advantage narrows (1 760 ms versus 2 282 ms on the small subject, 1.3x) because
compilation dominates and that is out-of-process in both regimes.

## Finding 5: clean builds are Roslyn, and MSBuild is not the lever

For completeness, so the above is not read out of proportion: on a clean OrchardCore build `Csc`
accounts for 204 s (warm) to 449 s (cold) of CPU across 232 projects, against 5.5 s of RAR and ~33 s of
evaluation. Clean-build time is a compiler-throughput problem. The MSBuild-shaped opportunities live in
the no-op and incremental columns — which is fortunate, because those are the columns a developer hits
dozens of times an hour.

## What to do, in order of measured confidence

1. **Cache evaluation results across builds.** Finding 2 shows a no-op build doing 463 full
   evaluations of unchanged projects. This is the largest identified block of provably redundant work
   in the inner loop. The file half of the key already exists; scope the environment-read half first,
   because it decides feasibility.
2. **Investigate why RAR is 5x more expensive in no-op builds than clean ones.** Finding 3 is a
   measured anomaly in the exact scenario developers repeat most, worth 28 s of CPU on a 241-project
   solution. Establish the cause before designing a fix.
3. **Ship `-mt`.** Finding 4 measures 1.4–1.9x on no-op builds at both scales, with no `-m` needed.
4. **Attack cold start.** Finding 1 says 2.2x on every no-op, and Finding 1's console case says 3.2 s
   of a 4 s cold clean build is recoverable state. The server already addresses the entry process;
   what remains is what the server cannot keep — JIT for freshly started TaskHosts, and anything
   discarded per submission.
5. **Fix the single-entry `TargetFrameworks` split** ([dotnet/sdk#55699](https://github.com/dotnet/sdk/issues/55699)).
   It is ~8–9 ms per project per build — small, but it is the reason 241 projects produce 463
   evaluations, and it therefore doubles the size of whatever prize item 1 eventually collects.

## Method, and what these numbers are not

* **Wall clock** comes from uninstrumented runs (`dotnet build --no-restore -mt`, no binlog, no
  profiler). **Attribution** comes from separate instrumented runs. The instrumented runs are roughly
  2x slower (OrchardCore warm no-op: 11.6 s uninstrumented, 26.9 s with `-bl` plus
  `-profileevaluation`), so task and evaluation figures should be read as **ratios and rankings, not
  as absolute times**.
* **Task durations are summed across parallel threads and nodes**, so they routinely exceed wall clock.
  `MSBuild` task totals are inclusive of everything they schedule and are excluded from the analysis
  above for that reason.
* `-profileevaluation` likewise reports evaluation summed over all projects and nodes; it measures
  work, not elapsed time.
* Restore is excluded from every timing. Clean scenarios delete `bin` and `obj`, restore untimed, then
  time the build.
* "Cold" is process-state cold, not machine cold. A genuinely cold OS file cache would be worse.
* Medians over 3–5 samples on a machine with background activity. Differences under ~15% on the
  multi-project subjects should not be treated as real; the ratios reported above are all larger than
  that, except where the text says otherwise.

One methodology bug is worth recording because it silently corrupted an earlier pass: in PowerShell
`[int](3/2)` is **2**, not 1, because .NET uses banker's rounding. A median helper written as
`$sorted[[int]($n/2)]` therefore returns the **maximum** for 3 samples, not the median. Every clean-build
figure in the first run of this matrix was a maximum until that was caught and the values recomputed
from the raw samples.

## Reproducing

```powershell
# Wall clock matrix
./Measure-BuildMatrix.ps1 -Name oc -ProjectOrSln <sln> -TouchFile <a .cs file> `
    -CleanRoots @('<src>','<test>') -CheapReps 5 -ExpensiveReps 3

# Attribution (run separately, never alongside the matrix)
./Capture-Binlogs.ps1 -Name oc -ProjectOrSln <sln> -TouchFile <a .cs file> -CleanRoots @('<src>','<test>')

# Parallelism regimes
./Compare-Parallelism.ps1 -ProjectOrSln <sln> -CleanRoots @('<src>') -Reps 5 -IncludeClean
```

## Related

* [dotnet/sdk#55699](https://github.com/dotnet/sdk/issues/55699) — single-entry `TargetFrameworks`
  evaluates every project twice
* [dotnet/msbuild#14556](https://github.com/dotnet/msbuild/issues/14556) /
  [#14558](https://github.com/dotnet/msbuild/pull/14558) — restore no longer discards the server's
  cross-build XML cache
* [`documentation/MSBuild-Server.md`](MSBuild-Server.md)
