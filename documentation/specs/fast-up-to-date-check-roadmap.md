# Fast Up-To-Date Check for CLI Builds

## Summary

Visual Studio determines whether a project needs building in 1–10 ms via a [Fast Up-To-Date Check (FUTDC)](https://github.com/dotnet/project-system/tree/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate) that runs *before* MSBuild is ever invoked. CLI users (`dotnet build`) get none of this — every no-op build pays full evaluation, RAR, and target-walking costs, taking seconds even when nothing changed.

This document is a deep investigation into what it would take to bring FUTDC-equivalent performance to CLI builds: what the VS FUTDC actually does, where exactly in the MSBuild pipeline a skip could be injected, what each candidate approach can and cannot do, and what the correctness risks are.

## Table of Contents

1. [What makes no-op CLI builds slow](#1-what-makes-no-op-cli-builds-slow)
2. [What VS FUTDC actually does](#2-what-vs-futdc-actually-does)
3. [MSBuild's current incremental mechanisms](#3-msbuilds-current-incremental-mechanisms)
4. [Candidate approaches — deep analysis](#4-candidate-approaches)
5. [The obj/ path discovery problem](#5-the-obj-path-discovery-problem)
6. [Correctness risk matrix](#6-correctness-risk-matrix)
7. [Why this wasn't done before](#7-why-this-wasnt-done-before)
8. [Recommended path forward](#8-recommended-path-forward)
9. [Related issues and prior art](#9-related-issues-and-prior-art)

---

## 1. What Makes No-Op CLI Builds Slow

When a developer runs `dotnet build && dotnet build`, the second invocation should ideally cost near-zero. In practice, MSBuild must:

1. **Parse and evaluate** every project file (property/item/import resolution)
2. **Walk every target** in the `BuildDependsOn` chain, performing timestamp comparisons
3. **Execute non-skippable targets** like `ResolveAssemblyReferences` (RAR) that lack concrete `Inputs`/`Outputs`
4. **Run Copy tasks** that check file-level incrementality

The measured cost breakdown for a no-op build:

| Phase | % of no-op time | Skippable by existing mechanisms? |
|-------|----------------|----------------------------------|
| Evaluation (parsing, property/item resolution, imports) | **50–70%** | ❌ Never — always runs |
| RAR + ResolvePackageAssets (no `Inputs`/`Outputs`) | **20–30%** | ❌ Targets don't declare incrementality |
| Target dependency analysis (timestamp comparisons) | **5–10%** | ❌ Must check each target |
| Copy tasks (fine-grained incrementality) | **5–15%** | ❌ Targets not fully incremental |

> "Every [batch build] must evaluate every project in the scope of the build."
> — [`Persistent-Problems.md`](../../documentation/Persistent-Problems.md)

> "RAR and some of its prerequisites like ResolvePackageAssets cannot [be skipped], because their role is to produce data used within the build to compute the compiler command line."
> — [`Persistent-Problems.md`](../../documentation/Persistent-Problems.md)

The VS FUTDC takes 1–10 ms per project because it **skips all four phases entirely**. Any CLI solution that runs inside MSBuild and doesn't skip evaluation addresses at most 30–50% of the problem.

---

## 2. What VS FUTDC Actually Does

Source: [`BuildUpToDateCheck.cs`](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/BuildUpToDateCheck.cs)

### 2.1 Architecture

The FUTDC is a layer *above* MSBuild. VS's Common Project System (CPS) subscribes to MSBuild evaluation and design-time build results via Dataflow pipelines. This produces an in-memory snapshot (`UpToDateCheckImplicitConfiguredInput`) that the FUTDC queries — no MSBuild invocation required.

```
MSBuild Evaluation + Design-Time Build
         │
         ▼
UpToDateCheckImplicitConfiguredInputDataSource (Dataflow subscription)
         │
         ▼
UpToDateCheckImplicitConfiguredInput (immutable snapshot)
         │
         ▼
BuildUpToDateCheck.IsUpToDateAsync() — 5-stage algorithm
         │
         ▼
Up-to-date → Skip MSBuild entirely (1–10 ms)
Not up-to-date → Invoke MSBuild
```

### 2.2 The 5-Stage Decision Algorithm

The check short-circuits on the first failure:

**Stage 1 — Global Conditions:** Is this the first build? Are there pending critical operations? Has the item set changed (add/remove) since last successful build?

**Stage 2 — Input/Output Timestamps:** For each named set, is any input newer than the earliest output? Is any input modified after the last successful build start time? Inputs include `@(Compile)`, `@(EmbeddedResource)`, resolved references, analyzer references, and the project import chain (`MSBuildAllProjects`).

**Stage 3 — Built-From Transforms:** For `UpToDateCheckBuilt` items with `Original` metadata (single-input → single-output transforms), is source newer than destination?

**Stage 4 — Copy Markers:** Is any referenced project's `CopyUpToDateMarker` newer than this project's marker? (Skipped when Build Acceleration is enabled.)

**Stage 5 — Copy-to-Output-Directory:** For items with `CopyToOutputDirectory` metadata, compare source vs destination timestamps. If Build Acceleration is on and only copies are needed, VS copies the files directly and reports "up to date."

### 2.3 Item Set Change Detection

Source: `BuildUpToDateCheck.ItemHashing.cs`

The FUTDC hashes all item include paths per item type using a stable, order-independent XOR hash. The hash is persisted — if it changes between checks (files added/removed), a rebuild is forced. This catches glob changes that timestamps alone miss.

The hash uses a stable hash function copied from .NET Framework's `string.GetHashCode()` (not the randomized .NET Core version) because the hash is persisted to disk across sessions.

### 2.4 State Persistence

Source: [`UpToDateCheckStatePersistence.cs`](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/UpToDateCheckStatePersistence.cs)

State is persisted to `.futdcache.v2` in the `.vs/` solution working folder using a custom binary format:

```
[int32: configuredProjectCount]
  for each:
    [string: projectPath]
    [int32: dimensionCount]           // e.g., Configuration=Debug, TargetFramework=net9.0
      for each: [string: name] [string: value]
    [int32: itemHash]                 // XOR hash of item sets
    [int64: itemsChangedAtUtc.Ticks]
    [int64: lastSuccessfulBuildStartedAtUtc.Ticks]
```

Typical size is ~20 KB for a 50-project, 2-config solution. Corruption is handled by discarding the entire file and forcing a full rebuild (fail-open design). Schema versioning is via the filename suffix (`.v2`).

### 2.5 Build Acceleration

Source: [docs/build-acceleration.md](https://github.com/dotnet/project-system/blob/main/docs/build-acceleration.md)

When the FUTDC determines that only file copies are needed (compilation is up-to-date), VS performs the copies directly without invoking MSBuild. In a chain A → B → C → D, changing D previously required 4 MSBuild invocations; with acceleration, only 1 MSBuild call + VS-driven copies for the rest.

Known incompatible packages are declared via `<BuildAccelerationIncompatiblePackage Include="..." />`. Known correctness bugs: wrong files copied in multi-target setups ([project-system#8908](https://github.com/dotnet/project-system/issues/8908)), duplicate output items ([project-system#9001](https://github.com/dotnet/project-system/issues/9001)).

### 2.6 Known FUTDC Bugs and Limitations

The VS FUTDC has had real correctness issues that any CLI version must learn from:

| Issue | Category | Description |
|-------|----------|-------------|
| [project-system#4261](https://github.com/dotnet/project-system/issues/4261) | **False positive** | Git branch switch caused FUTDC to wrongly skip builds |
| [project-system#7803](https://github.com/dotnet/project-system/issues/7803) | **Correctness** | Item change tracking doesn't respect `Set` metadata partitioning |
| [project-system#9477](https://github.com/dotnet/project-system/issues/9477) | **Gap** | T4 template changes not detected |
| [project-system#4100](https://github.com/dotnet/project-system/issues/4100) | **Gap** | Custom target `Inputs`/`Outputs` invisible to FUTDC |
| [project-system#6301](https://github.com/dotnet/project-system/issues/6301) | **False positive** | Copy tasks skipped when they shouldn't be |
| [msbuild#3762](https://github.com/dotnet/msbuild/issues/3762) | **Cascade** | `DisableFastUpToDateCheck` cascades incorrectly through P2P |
| [msbuild#5406](https://github.com/dotnet/msbuild/issues/5406) | **Correctness** | Broken with `GeneratePackageOnBuild` + binding redirects |

Source generators, `.editorconfig` changes, and custom build targets are all known gaps — the FUTDC cannot model arbitrary build logic.

---

## 3. MSBuild's Current Incremental Mechanisms

### 3.1 Target-Level `Inputs`/`Outputs`

Source: [`TargetUpToDateChecker.cs`](../../src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs)

Each target can declare `Inputs` and `Outputs`. Before execution, `TargetUpToDateChecker.PerformDependencyAnalysis()` compares timestamps and returns `SkipUpToDate`, `IncrementalBuild`, or `FullBuild`. This is purely timestamp-based — `DateTime.Compare(inputWriteTime, outputWriteTime)`.

The critical limitation: many important targets (RAR, ResolvePackageAssets) **cannot declare** `Inputs`/`Outputs` because their inputs are the transitive closure of all referenced assemblies, which is too expensive to enumerate without running the target itself.

### 3.2 The `/question` Flag

Source: [`documentation/specs/question.md`](../../documentation/specs/question.md), [`TargetEntry.cs:477`](../../src/Build/BackEnd/Components/RequestBuilder/TargetEntry.cs)

`/question` runs the build but errors at the first non-up-to-date target or task. It's a **diagnostic tool**, not an optimization — it still performs full evaluation and walks every target. Tasks implement `IIncrementalTask` to participate.

The spec explicitly notes: *"Fast Up-To-Date is faster, but can be less accurate, suitable for an IDE and a human interface."*

### 3.3 `CopyUpToDateMarker`

Source: [`Microsoft.Common.CurrentVersion.targets:389–399`](../../src/Tasks/Microsoft.Common.CurrentVersion.targets)

A lightweight marker (`.Up2Date` file) is `Touch`ed after copy operations and checked by downstream projects. This is a narrow optimization covering only copy operations across project references.

### 3.4 `CoreCompileInputs.cache`

The one exception to MSBuild's pure timestamp approach: `_GenerateCompileDependencyCache` hashes `@(Compile)`, `@(ReferencePath)`, `$(DefineConstants)`, and `$(LangVersion)` into a cache file to catch add/remove of glob-matched files. This is the closest existing thing to a build fingerprint, but it only covers compilation inputs.

### 3.5 Results Cache

`BuildManager` maintains `IConfigCache` and `IResultsCache` keyed by `(project path, global properties, targets)`. Results are cached within a single `BeginBuild`/`EndBuild` session but **not persisted** across `dotnet build` invocations.

---

## 4. Candidate Approaches

### Approach A: Pre-Evaluation Hook in MSBuild Engine ⭐

**Core idea:** Add a fingerprint check in `BuildManager.ExecuteSubmission()` *before* evaluation, short-circuiting with a cached `BuildResult`.

**Insertion point found:** `BuildManager.cs` line 1580 — the fork between cache path and build path:

```csharp
// BuildManager.cs:1580 — CURRENT fork point
if (_projectCacheService!.ShouldUseCache(resolvedConfiguration))
    IssueCacheRequestForBuildSubmission(...)   // evaluates, then queries plugin
else
    IssueBuildRequestForBuildSubmission(...)   // scheduler → node → evaluate → execute
```

At this point, we have:
- ✅ Project path
- ✅ Global properties (Configuration, Platform, TargetFramework)
- ✅ Target names, tools version
- ❌ No evaluated `ProjectInstance` (that's the whole point — we skip evaluation)

**Proposed insertion:**
```csharp
// NEW: Pre-evaluation FUTDC check
if (TryGetUpToDateResult(resolvedConfiguration, out BuildResult cachedResult))
{
    var result = new BuildResult(submission.BuildRequest!);
    foreach (var tr in cachedResult.ResultsByTarget)
        result.AddResultsForTarget(tr.Key, tr.Value);
    _resultsCache!.AddResult(result);
    ReportResultsToSubmission<BuildRequestData, BuildResult>(result);
    return;  // No evaluation, no execution.
}
```

This pattern already exists — `PostCacheResult()` at line 2566 does exactly this for cache hits.

**`TryGetUpToDateResult` would:**
1. Compute expected `obj/` path via convention (see [Section 5](#5-the-obj-path-discovery-problem))
2. Read `.futdc` fingerprint file
3. Compare project file, `Directory.Build.props/targets`, `project.assets.json`, `global.json` timestamps
4. Verify all stored output files still exist
5. If all match → return cached `BuildResult`

**How the fingerprint gets written:** A new `_WriteBuildFingerprint` target runs as the last step of `CoreBuild`. It has access to all evaluated properties and items and writes the fingerprint to `$(IntermediateOutputPath)/.futdc`.

| Aspect | Assessment |
|--------|-----------|
| Skips evaluation? | ✅ Yes — the dominant 50–70% cost |
| Skips execution? | ✅ Yes — entire pipeline bypassed |
| Requires engine changes? | Yes — moderate risk, needs MSBuild team buy-in |
| Works for all MSBuild invocations? | ✅ Yes — not limited to `dotnet build` |
| First build overhead? | None — fingerprint written post-build, no fingerprint → normal build |

**Flaws:**
- Requires MSBuild engine changes — high bar for acceptance
- `obj/` path discovery is convention-based (fails for ~2–3% of projects)
- Cached `BuildResult` must faithfully represent what MSBuild would have produced (target results, output items)
- The fingerprint target adds a small cost to every successful build

### Approach B: ProjectCachePlugin — ❌ Cannot Skip Evaluation

**Critical finding:** Cache plugins are consulted **after** evaluation.

From `ProjectCacheService.cs:531`:
```csharp
PostCacheRequest() {
    EvaluateProjectIfNecessary(...)    // ← FULL EVALUATION ALWAYS HAPPENS
    cacheResult = await GetCacheResultAsync(buildRequest, ...)  // ← Then plugin is asked
}
```

The plugin receives a fully-evaluated `ProjectInstance`. By that point, 50–70% of the no-op build cost has already been paid.

A `FutdcCachePlugin` as a NuGet package could skip execution (RAR, Compile, Copy targets), saving 30–50%. But it cannot address the dominant cost.

```
                     ┌───────────────────────────────────────┐
                     │        Build Pipeline Timeline        │
                     ├──────────┬────────────┬───────────────┤
                     │  Parse   │  Evaluate  │   Execute     │
                     │  XML     │  Props     │   Targets     │
                     │          │  Items     │   (Build,     │
                     │          │  Imports   │    RAR, etc.) │
                     ├──────────┴────────────┼───────────────┤
                     │ ◄── CANNOT SKIP ────► │ ◄─ CAN SKIP ─►│
                     │                       │               │
                     │   Plugin consulted ───┤               │
                     │   HERE (after eval)   │               │
                     └───────────────────────┴───────────────┘
```

**What would make this viable:** A new `PreEvaluationGetCacheResultAsync(string projectPath, IDictionary<string, string> globalProperties)` on `ProjectCachePluginBase` called before `EvaluateProjectIfNecessary()`. This is essentially Approach A exposed as a plugin API — more extensible but requires the same engine change.

Existing implementations (`microsoft/MSBuildCache`) confirm the limitation: they target CI distributed caching (skip compilation), not developer inner-loop FUTDC (skip evaluation). MSBuildCache explicitly states it doesn't support incremental developer builds.

### Approach C: SDK CLI Layer (`dotnet/sdk`) — ⚠️ Architecturally Clean but Impractical

**How `dotnet build` invokes MSBuild:**
```
dotnet build [args]
  → RestoringCommand → MSBuildForwardingApp
    → MSBuildForwardingAppWithoutLogging.ExecuteInProc()
      → MSBuildApp.Main(args)  ← IN-PROCESS method call
```

MSBuild is invoked **in-process** by default. The SDK is a pure pass-through — it has zero project evaluation capability. For solutions, the `.sln` path is passed directly to MSBuild; there is no per-project loop in the CLI.

**Fundamental problem:** The SDK doesn't know what projects are in the solution, what their `obj/` paths are, or what their inputs/outputs are. All of that requires MSBuild evaluation. To perform FUTDC at this layer, the SDK would need to:
1. Parse `.sln`/`.slnx` files independently
2. Discover `obj/` paths via convention
3. Read fingerprints and compare timestamps
4. Produce a synthetic "everything is up-to-date" result

This is building a mini build system on top of MSBuild. It mirrors the VS architecture (VS FUTDC also sits above MSBuild), but VS has CPS with live Dataflow subscriptions — the SDK has nothing.

**Where it could work:** Single-project builds with default `obj/` layout. Not viable for solution builds without duplicating substantial MSBuild logic.

### Approach D: MSBuild Targets — Early Short-Circuit — ❌ Cannot Skip Evaluation

A target running early in the build chain (e.g., `BeforeTargets="BeforeBuild"`) could read a fingerprint and try to short-circuit. But evaluation has already happened before any target runs, so this saves at most 30–50%.

Additionally, there's no clean mechanism for a target to say "skip all remaining targets." You'd need conditions on every target in the chain, which is fragile and breaks third-party target extensibility.

### Approach E: MSBuild Server Mode — 🔮 Long-Term Ideal

**Current state:** Server mode is opt-in (`MSBUILDUSESERVER=1`), in `Microsoft.Build.Experimental` namespace. The server process persists between builds and keeps a `ProjectRootElementCache` (parsed XML roots, not evaluated state).

What survives across builds in server mode today:
- ✅ `ProjectRootElementCache` — static singleton, auto-reloads on file timestamp changes
- ❌ No evaluation result cache
- ❌ No FUTDC state
- ❌ No file watchers

A persistent process is the natural host for FUTDC state — it can cache evaluation results, watch filesystems, and make sub-millisecond decisions from memory, just like VS CPS. But server mode was previously shelved due to state-leak bugs and is not yet production-ready.

### Approach Comparison

| Approach | Skips Evaluation? | Skips Execution? | Engine Changes? | Ships As |
|----------|:-:|:-:|:-:|---|
| **A: Engine pre-eval hook** | ✅ | ✅ | Yes | MSBuild change |
| **B: Cache plugin** | ❌ | ✅ | No (or small) | NuGet package |
| **B': Cache plugin + pre-eval API** | ✅ | ✅ | Yes | NuGet + MSBuild |
| **C: SDK CLI layer** | ✅ (single project) | ✅ | No MSBuild changes | SDK change |
| **D: Targets** | ❌ | Partially | No | `.targets` package |
| **E: Server mode** | ✅ | ✅ | Yes (substantial) | MSBuild change |

---

## 5. The obj/ Path Discovery Problem

To check a fingerprint *before* evaluation, we need to find the `obj/` directory — but `obj/` path depends on evaluated properties. This is the chicken-and-egg problem.

### 5.1 How `obj/` Path Is Determined

Source: [`Microsoft.Common.props:50`](../../src/Tasks/Microsoft.Common.props), [`Microsoft.Common.CurrentVersion.targets:146–163`](../../src/Tasks/Microsoft.Common.CurrentVersion.targets)

```
Microsoft.Common.props:50   → BaseIntermediateOutputPath = "obj\"
Microsoft.Common.props:54   → MSBuildProjectExtensionsPath = $(BaseIntermediateOutputPath)
CurrentVersion.targets:160  → IntermediateOutputPath = obj\$(Configuration)\
SDK targets                  → IntermediateOutputPath += $(TargetFramework)\
```

| Scenario | IntermediateOutputPath |
|---|---|
| Simple SDK project | `obj\Debug\net9.0\` |
| Non-SDK (.NET Framework) | `obj\Debug\` |
| Multi-target inner build | `obj\Debug\net8.0\` |
| Multi-target outer build | `obj\Debug\` (no TFM) |
| Non-AnyCPU platform | `obj\x64\Debug\net9.0\` |
| Artifacts layout (.NET 8+) | `artifacts\obj\ProjectName\debug\` |

### 5.2 Key Insight: `BaseIntermediateOutputPath` Is Stable

`BaseIntermediateOutputPath` defaults to `obj\` and is set in `Microsoft.Common.props` before project content is parsed. It doesn't vary by Configuration, Platform, or TFM. It can only be overridden in `Directory.Build.props` (which imports before the project body).

**Recommendation:** Store the fingerprint at `BaseIntermediateOutputPath` level (`obj/.futdc`), not at `IntermediateOutputPath` level. The fingerprint file itself stores per-config, per-TFM entries internally.

### 5.3 Convention-Based Discovery

```
1. Probe: {project_dir}/obj/.futdc              → ~90% of projects (default layout)
2. Probe: Walk up for Directory.Build.props
   with ArtifactsPath → {artifacts}/obj/{name}/ → ~5–8% more (artifacts layout)
3. Fallback: Run MSBuild evaluation              → remaining ~2–3% (custom layouts)
```

NuGet already uses this same convention — `project.assets.json` lives at `BaseIntermediateOutputPath`. If NuGet can find `obj/`, so can FUTDC.

### 5.4 Multi-Targeting

Multi-targeting projects dispatch to inner builds per TFM. The outer build (`TargetFramework` = empty) and each inner build (`TargetFramework` = `net8.0`, etc.) have different `IntermediateOutputPath` values but share the same `BaseIntermediateOutputPath`.

Store one fingerprint file at `obj/.futdc` with per-TFM sections:
```
obj/.futdc
  ├─ [Debug|net8.0]: {hash, timestamps, outputs}
  └─ [Debug|net9.0]: {hash, timestamps, outputs}
```

---

## 6. Correctness Risk Matrix

For each scenario: would a fingerprint-based FUTDC miss the change? What does VS do?

### Critical Risks

| Scenario | Would FUTDC Miss It? | Mitigation |
|----------|---------------------|------------|
| **Output file deleted** (DLL removed but fingerprint intact) | Yes | **Mandatory output existence check** — verify all expected outputs exist before declaring up-to-date. Non-negotiable. |
| **`global.json` SDK version change** | Yes — not in `MSBuildAllProjects`, no project timestamp changes | Hash resolved SDK version/path into fingerprint. VS doesn't handle this either (pins SDK at startup). |
| **SDK/workload update** (new targets installed) | Yes — SDK directory changes are invisible to project timestamps | Hash SDK root path or version marker file. |
| **Environment variable change** (`DOTNET_ROOT`, `PATH`, `MSBuild*`) | Yes — env vars injected as properties during evaluation | Hash a known set of critical env vars. Accept that custom env vars are a known gap (VS has the same gap). |

### High Risks

| Scenario | Would FUTDC Miss It? | Mitigation |
|----------|---------------------|------------|
| **Source generator DLL or `@(AdditionalFiles)` change** | Partially — not in `CoreCompileInputs.cache` | Include `@(Analyzer)` and `@(AdditionalFiles)` in fingerprint. VS has similar gaps. |
| **`.editorconfig` / `.globalconfig` change** | Yes — passed to compiler but not in cache hash | Include `@(EditorConfigFiles)` in fingerprint. VS FUTDC has this same bug. |
| **Custom targets with side effects** (pre/post build events) | Yes, by design — no `Inputs`/`Outputs` | Disable FUTDC for projects with Pre/PostBuildEvent. Honor `DisableFastUpToDateCheck`. |
| **P2P reference: impl assembly changed, ref assembly unchanged** | Partially — `CopyUpToDateMarker` doesn't always advance | Track full `@(ReferencePath)` timestamps, not just ref assemblies. |

### Medium Risks

| Scenario | Would FUTDC Miss It? | Mitigation |
|----------|---------------------|------------|
| **NuGet restore changes** | Partially — `project.assets.json` timestamp changes propagate, but edge cases exist | Include `project.assets.json` and `packages.lock.json` in fingerprint inputs. |
| **New `Directory.Build.props` added** | Possibly — discovery runs at eval time, but previous builds didn't have it in `MSBuildAllProjects` | Probe for existence at ancestor paths even when not currently imported. |
| **Git branch switch** | Maybe — git sets timestamps to checkout time, so all files look "newer" | Content hashing would fix this; timestamp-based checks may cause unnecessary rebuilds (safe but slow). Known VS bug: [project-system#4261](https://github.com/dotnet/project-system/issues/4261). |
| **Multi-targeting (only one TFM needs rebuild)** | Only if fingerprint is per-project, not per-TFM | Per-TFM fingerprint entries. |
| **Concurrent builds** | Race conditions on fingerprint file | Atomic writes (write-temp-rename). Document that concurrent builds of the same project are unsupported. |
| **File system edge cases** (FAT32 2s resolution, network clock skew, DST) | Yes for within-resolution edits | Use `>=` comparison. Document FAT32 limitations. |

### Non-Negotiable Requirements

1. **Output existence verification** before declaring up-to-date
2. **Fail-open design** — missing/corrupt fingerprint → always build
3. **Honor `DisableFastUpToDateCheck`** property
4. **Detailed logging** for diagnostics (like VS's `FastUpToDate:` messages)
5. **Opt-in initially** — build confidence before making it default

---

## 7. Why This Wasn't Done Before

From @rainersigwald (MSBuild team lead) in [#9122](https://github.com/dotnet/msbuild/issues/9122):

> *"MSBuild always builds projects. MSBuild's unit of incremental build is the Target... no hashing is involved — it's timestamp-based. This piece [a project-level build hash] doesn't exist. It was not part of the design of MSBuild. That's a major limitation of MSBuild compared to more modern build systems."*

### Prior Attempts

**RAR-as-a-Service** ([#3139](https://github.com/dotnet/msbuild/issues/3139), [#5536](https://github.com/dotnet/msbuild/issues/5536), [#6193](https://github.com/dotnet/msbuild/issues/6193)): The closest prior effort — tried to make RAR faster rather than skip it. Prototyped in 2020–2021, **abandoned** after VS 17.0. A new [spec revival](https://github.com/dotnet/msbuild/issues/11741) is underway.

**Static Graph Design** ([#3696](https://github.com/dotnet/msbuild/issues/3696)): Explicitly envisioned "persistent cross-build cache" and noted that with export targets, *"an incremental build of a particular project in the project graph would not require re-evaluation or any target execution in that project's dependencies."* This vision was partially realized via `ProjectCachePluginBase` but without the pre-evaluation skip.

**dotnet/sdk build performance complaints** ([sdk#7850](https://github.com/dotnet/sdk/issues/7850): 35 👍, 99 comments): The canonical user complaint, filed in 2017, demonstrating `dotnet build` wasting seconds on no-op builds.

### Key Blockers

| Blocker | Source |
|---------|--------|
| MSBuild was designed for target-level incrementality, not project-level skip | @rainersigwald in #9122 |
| Evaluation is load-bearing — RAR and other targets need evaluated state | `Persistent-Problems.md` |
| Correctness fear — VS FUTDC is explicitly "not accurate enough for CI" | `question.md` |
| No champion for cross-cutting work across engine + SDK + targets | Organizational |
| Server mode instability prevented persistent-process approach | MSBuild Server history |

---

## 8. Recommended Path Forward

### Phase 0: Prototype Cache Plugin (no engine changes)

Build a `FutdcCachePlugin` NuGet package that fingerprints projects post-build and returns `CacheHit` on subsequent builds. This saves ~30–50% (execution only, not evaluation). Ships as a NuGet package, requires `/graph` mode.

**Value:** Validates fingerprinting approach and correctness with real-world projects. Low risk, independent of MSBuild release cycle.

### Phase 1: Pre-Evaluation Engine Hook (the key change)

Add a pre-evaluation FUTDC check in `BuildManager.ExecuteSubmission()` before the cache/build fork. This is the highest-leverage change — it enables skipping evaluation, the dominant cost.

The fingerprint is written by a new `_WriteBuildFingerprint` target and read by the engine. Convention-based `obj/` path discovery covers ~95–98% of projects.

**Value:** CLI builds match VS FUTDC performance (1–10 ms) for up-to-date projects.

### Phase 2: Graph Build Integration

After constructing the `ProjectGraph` (which evaluates all projects upfront), check fingerprints for each node in topological order. Skip submission entirely for up-to-date projects whose dependencies are also up-to-date.

**Value:** Solution-level no-op builds drop from seconds to milliseconds.

### Phase 3: CLI Build Acceleration

When the fingerprint shows only copy operations are needed (compilation up-to-date, referenced project implementation assemblies changed), perform copies directly without full MSBuild invocation.

**Value:** In a chain A → B → C → D, changing D triggers 1 MSBuild build + fast copies for A, B, C.

### Phase 4: Server Mode Convergence

With MSBuild server mode maintaining in-memory state, FUTDC operates identically to VS: cached evaluation results, file watchers, sub-millisecond decisions. This is the ultimate convergence point.

### Storage Model: In-Memory with Disk Fallback

VS uses a dual-storage approach: FUTDC state lives in-memory during the session and is flushed to `.futdcache.v2` on solution close (`OnBeforeCloseSolution`). On next launch it restores from the file. The same pattern maps cleanly to MSBuild:

| Scenario | Storage | Check Cost | Write Cost |
|----------|---------|-----------|------------|
| **Server alive** | In-memory, next to existing `ProjectRootElementCache` singleton | Sub-millisecond | None (memory write) |
| **Server shutdown** | Flush to `obj/.futdc` via the existing `BuildCompleteReuse` shutdown path | N/A | Single disk write |
| **Server cold start** | Read from `obj/.futdc`, populate memory | ~1ms (disk read) | None |
| **No server (fallback)** | Read/write `obj/.futdc` per build | ~1–5ms per project | Small disk write post-build |

This means Phase 1 (disk-based fingerprints) and Phase 4 (server convergence) use the **same data format and logic** — just different storage backends. The server is an optimization over the disk path, not a different architecture. The disk format serves as the persistence layer for both paths and as the cold-start bootstrap.

### Where to implement: Engine vs SDK vs Plugin

The Phase 0 plugin and Phase 1 engine hook are not mutually exclusive. The plugin validates the approach; the engine hook delivers the full performance benefit. The key architectural decision is whether to expose the pre-evaluation check as a plugin API extension (`PreEvaluationGetCacheResultAsync`) or as built-in engine logic. The plugin API is more extensible; the built-in approach is simpler and avoids plugin packaging overhead.

---

## 9. Related Issues and Prior Art

### Core Problem

- [dotnet/sdk#7850](https://github.com/dotnet/sdk/issues/7850) — "Slow build with msbuild and dotnet cli" (35 👍, 99 comments)
- [dotnet/msbuild#2015](https://github.com/dotnet/msbuild/issues/2015) — "RAR is slow on .NET Core" (20 👍, 64 comments, open since 2017)
- [dotnet/msbuild#12954](https://github.com/dotnet/msbuild/issues/12954) — "blazor big project: build perf downgrade" (180-project solution)
- [dotnet/msbuild#1979](https://github.com/dotnet/msbuild/issues/1979) — "MSBuild in VS2017 & CLI rebuilds everything every time"

### RAR-as-a-Service (abandoned predecessor)

- [dotnet/msbuild#3139](https://github.com/dotnet/msbuild/issues/3139) — User story (abandoned)
- [dotnet/msbuild#5536](https://github.com/dotnet/msbuild/issues/5536) — Design doc
- [dotnet/msbuild#6193](https://github.com/dotnet/msbuild/issues/6193) — Prototype
- [dotnet/msbuild#11741](https://github.com/dotnet/msbuild/issues/11741) — Spec revival (active)

### Project Cache Plugin Infrastructure

- [dotnet/msbuild#5936](https://github.com/dotnet/msbuild/pull/5936) — Initial implementation
- [dotnet/msbuild#8726](https://github.com/dotnet/msbuild/pull/8726) — "Cache add" functionality
- [dotnet/msbuild#9329](https://github.com/dotnet/msbuild/pull/9329) — Updated docs
- [dotnet/msbuild#9122](https://github.com/dotnet/msbuild/issues/9122) — @rainersigwald: "MSBuild has no project-level skip"

### Static Graph

- [dotnet/msbuild#3696](https://github.com/dotnet/msbuild/issues/3696) — Static Graph Design (mentions persistent cross-build cache)

### VS FUTDC

- [dotnet/project-system#62](https://github.com/dotnet/project-system/issues/62) — Original tracking issue
- [dotnet/project-system#2380](https://github.com/dotnet/project-system/issues/2380) — UpToDateCheckInput/Output design

### Evaluation Performance

- [dotnet/msbuild#4025](https://github.com/dotnet/msbuild/issues/4025) — NuGetSdkResolver adds 180–400ms per evaluation

### Concurrency

- [dotnet/msbuild#9462](https://github.com/dotnet/msbuild/issues/9462) — Global mutex for concurrent builds

### Server Mode

- [dotnet/msbuild#10035](https://github.com/dotnet/msbuild/issues/10035) — Daemon lifetime management

### Design Documents

- [`Persistent-Problems.md`](../../documentation/Persistent-Problems.md) — Evaluation and RAR are the dominant no-op costs
- [`Build-Scenarios.md`](../../documentation/Build-Scenarios.md) — "An ideal fully up-to-date build would take no time"
- [`specs/question.md`](../../documentation/specs/question.md) — `/question` flag; acknowledges VS FUTDC is "not accurate enough for CI"
- [VS FUTDC docs](https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md)
- [Build Acceleration docs](https://github.com/dotnet/project-system/blob/main/docs/build-acceleration.md)
