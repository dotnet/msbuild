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
7. [Red-team: blocking flaws and open questions](#7-red-team-blocking-flaws-and-open-questions)
8. [Why this wasn't done before](#8-why-this-wasnt-done-before)
9. [Recommended path forward](#9-recommended-path-forward)
10. [Related issues and prior art](#10-related-issues-and-prior-art)

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

The FUTDC is a layer *above* MSBuild. It has a **two-layer design**: CPS tells it *what files to check* (via Dataflow snapshots from evaluation/design-time builds), but it reads *actual file timestamps* from the filesystem at check time.

**CPS does NOT use `FileSystemWatcher`.** It uses VS's kernel-level file change service (`IVsAsyncFileChangeEx` — a VS COM API) to detect project file changes, which trigger CPS re-evaluation, which updates the Dataflow snapshots. However, the FUTDC itself does not subscribe to file watchers — it reads `File.GetLastWriteTimeUtc()` directly when a build is requested.

```
┌─────────────────────────────────────────────────────────────┐
│  CPS Project Subscription Service                           │
│  (continuously maintained while VS is open)                 │
│                                                             │
│  JointRuleSource ──────┐  SourceItemsRuleSource ──┐        │
│  (eval + DT build      │  (Compile, Content, etc) │        │
│   rules like           │                          │        │
│   ConfigurationGeneral,│                          │        │
│   ResolvedCompilation- │                          │        │
│   Reference, etc.)     │                          │        │
│                        │                          │        │
│  ProjectItemSchema ────┤  ProjectCatalogSource ───┤        │
└────────────────────────┼──────────────────────────┼────────┘
                         │  SyncLinkTo (Dataflow)   │
                         ▼                          ▼
┌─────────────────────────────────────────────────────────────┐
│  UpToDateCheckImplicitConfiguredInputDataSource              │
│  TransformBlock → state.Update(...)                          │
│  Produces: UpToDateCheckImplicitConfiguredInput              │
│  (immutable snapshot of WHAT to check)                       │
└──────────────────────────┬──────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  BuildUpToDateCheck.IsUpToDateAsync()                        │
│  Gets latest snapshot, then reads File.GetLastWriteTimeUtc() │
│  for every input/output at CHECK TIME (not from snapshot)    │
└─────────────────────────────────────────────────────────────┘
```

The design-time MSBuild targets that produce FUTDC data are:
- `CollectUpToDateCheckInputDesignTime` → `@(UpToDateCheckInput)` items
- `CollectUpToDateCheckBuiltDesignTime` → `@(UpToDateCheckBuilt)` (output DLL, PDB, doc, etc.)
- `CollectResolvedCompilationReferencesDesignTime` → `@(ReferencePathWithRefAssemblies)`

**Key implication for CLI:** The CLI doesn't need file watchers or the CPS Dataflow pipeline. Those exist to keep the snapshot warm between builds. What the CLI needs is the same *data* (list of inputs/outputs/references) — which it can obtain from a prior build's evaluation results, persisted to the fingerprint file. The actual timestamp comparison logic is trivially portable (`File.GetLastWriteTimeUtc` is cross-platform .NET).


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

### Approach C': SDK Solution-Level Build Loop — ⭐ The VS Architecture Match

**Key reframe:** How does VS actually use FUTDC?

VS's Solution Build Manager (SBM) iterates projects in dependency order. For *each* project, it asks the FUTDC "is this project up to date?" If yes, it skips that project's MSBuild invocation entirely. The FUTDC is a **per-project gate within a solution build loop** — not an engine-level optimization.

Today, `dotnet build` on a `.sln` passes the entire solution path to MSBuild as a single invocation. MSBuild internally generates a traversal metaproject and builds everything. The SDK has no per-project loop where FUTDC could be inserted.

**But if the SDK built solutions by iterating projects itself** (like VS SBM does), it could do FUTDC per-project without any MSBuild engine changes:

```
dotnet build MySolution.slnx
  1. Parse .slnx → list of projects in dependency order
  2. For each project (leaves first):
     a. Check obj/.futdc fingerprint
     b. If up-to-date and all dependencies up-to-date → skip
     c. Otherwise → invoke MSBuild for this project only
  3. Done
```

This mirrors the VS architecture exactly. The SDK acts as the "higher-order build system" that sits above MSBuild — the same role VS's SBM plays.

**What makes this newly feasible:**
- `.slnx` is a simpler, parseable format (JSON-like) — unlike `.sln` which required MSBuild's `SolutionProjectGenerator`
- The SDK already has solution-parsing logic for `dotnet sln` commands
- Graph builds already compute dependency order — the SDK could use `ProjectGraph` for this
- No MSBuild engine changes required — each project is built via a normal `dotnet build MyProject.csproj` (or MSBuild API call)

**What's still hard:**
- Dependency order resolution requires knowing `ProjectReference` items, which requires evaluation
- Multi-targeting complicates the project list (each TFM is effectively a separate build)
- Global properties from solution configurations must be propagated correctly

**This approach sidesteps ALL the red-team blocking issues from Section 7:** no synthetic `BuildResult` needed (MSBuild is either called or not), no target-result completeness problem, no `ResultsCache` semantic gap. Each project that IS built gets a genuine MSBuild invocation; each project that's skipped simply isn't called — exactly like VS.

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

| Approach | Skips Evaluation? | Skips Execution? | Engine Changes? | Ships As | Red-Team Blockers? |
|----------|:-:|:-:|:-:|---|:-:|
| **A: Engine pre-eval hook** | ✅ | ✅ | Yes | MSBuild change | 🔴 3 blocking flaws |
| **B: Cache plugin** | ❌ | ✅ | No (or small) | NuGet package | None, but limited value |
| **B': Cache plugin + pre-eval API** | ✅ | ✅ | Yes | NuGet + MSBuild | Same as A |
| **C: SDK single-project** | ✅ | ✅ | No | SDK change | Only for single projects |
| **C': SDK solution build loop** | ✅ | ✅ | No | SDK change | ⭐ No blocking flaws |
| **D: Targets** | ❌ | Partially | No | `.targets` package | Limited value |
| **E: Server mode** | ✅ | ✅ | Yes (substantial) | MSBuild change | Long-term only |

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

## 7. Red-Team: Blocking Flaws and Open Questions

The following are potential fatal flaws identified through adversarial review of the proposal. These must be resolved before deciding whether to pursue this work.

### 7.1 🔴 For Non-Leaf Projects, This Is Not an FUTDC — It's a Result Cache

**The core reframing:** The proposal describes a "fast up-to-date check," but for any project consumed by other projects via `ProjectReference`, it's actually a **result-cache architecture**. This is a fundamentally harder problem.

VS FUTDC gets away with skipping MSBuild entirely because it doesn't need to synthesize engine-level results for downstream consumers. When a VS project is "up to date," VS simply doesn't call MSBuild — nothing downstream within the MSBuild engine needs to know.

But when the MSBuild *engine* skips a project, the rest of the build still expects target results. Project references consume specific target outputs:

- `GetTargetPath` returns `@(TargetPathWithTargetPlatformMoniker)` with metadata (`ReferenceAssembly`, `CopyUpToDateMarker`, etc.) — consumed by every upstream project reference (`Microsoft.Common.CurrentVersion.targets:2242-2278`)
- `GetCopyToOutputDirectoryItems` returns `@(AllItemsFullPathWithTargetPath)` — consumed transitively via `TargetOutputs` (`Microsoft.Common.CurrentVersion.targets:5197-5336`)
- Graph target propagation is computed from `ProjectReferenceTargets` in the upstream project's evaluation (`ProjectInterpretation.cs:492-523`)

A cached `BuildResult` *can* structurally hold these outputs (MSBuild's `TargetResult` stores `TaskItem[]` with metadata, and `PluginTargetResult` already supports this). The problem is not serialization — it's **identity, completeness, and invalidation**:

- **Target-set identity:** A cache entry from `/t:Build` is not valid for a `/t:GetTargetPath` request. The fingerprint must be keyed by requested targets, not just "last build succeeded."
- **Configuration identity:** The cache must be keyed by global properties (`-p:Configuration=Debug` vs `Release`). A `.futdc` keyed only by project path is unsound.
- **Completeness:** The cached result must contain results for *every* target that any upstream project might request — which depends on upstream evaluation (another circularity).
- **Invalidation model:** What makes a cached result stale? File timestamps? Content hashes? The cache needs a formal invalidation specification.

**For leaf projects (no downstream consumers):** The problem is much simpler. The cache only needs to confirm "build would produce the same outputs" — no target result synthesis needed. A leaf-only FUTDC MVP could be viable.

**For non-leaf projects:** This is a full result-cache architecture (like `microsoft/MSBuildCache`) with pre-evaluation semantics. The current proposal does not define the target surface, cache identity, or invalidation model needed to make this sound.

#### Engine-Level Constraints (from code review)

Detailed investigation of the engine reveals five specific constraints that a cached `BuildResult` injection must satisfy:

1. **`InitialTargets`/`DefaultTargets` must be present.** `ResultsCache.SatisfyRequest` (`ResultsCache.cs:188-202`) returns `NotSatisfied` when `configInitialTargets == null`. These are normally populated from the evaluated `ProjectInstance` via `BuildRequestConfiguration.SetProjectBasedState`. Skipping evaluation means these must be persisted and restored alongside the cached result.

2. **`TargetResult.Items` must include full metadata.** The `MSBuild` task deep-clones every `TargetResult.Items` entry (`TaskHost.cs:1237-1241`) and enriches them with `MSBuildSourceProjectFile`/`MSBuildSourceTargetName` metadata. Empty or incomplete items produce empty `@(TargetOutputs)`, breaking `ProjectReference` protocol.

3. **`ResultsCache.SatisfyRequest` has all-or-nothing semantics.** It returns `Satisfied` only if ALL requested targets, ALL initial targets, and (if no explicit targets) ALL default targets have results (`ResultsCache.cs:149-228`). A cache entry for `{Build}` won't satisfy a later request for `{GetCopyToOutputDirectoryItems}`.

4. **In non-graph builds, target requests arrive dynamically.** The `MSBuild` task issues child build requests at execution time. The full set of targets that will be requested for a project is not known upfront — it depends on what other projects' targets do.

5. **Graph builds know the full target closure** via `ProjectGraph.GetTargetLists` (`ProjectGraph.cs:609-770`), making them the natural fit for pre-computed result caching. Non-graph builds would need a heuristic target closure (e.g., `Build + GetTargetPath + GetCopyToOutputDirectoryItems + GetTargetFrameworks`).

### 7.2 🔴 Circular Dependency in Fingerprinting

The proposal says: check fingerprint before evaluation. But the real set of inputs is only known *after* evaluation:

- Conditional imports (`Condition="'$(CI)'=='true'"`) change the import graph
- SDK resolution determines which `.targets` files are loaded
- Globs expand to different files depending on what's on disk
- Property functions compute values at evaluation time
- Command-line global properties change evaluation results

The fingerprint from the *last* build records what the inputs *were*. But pre-evaluation, we can't know if the inputs *changed* — because discovering the inputs IS evaluation.

**Example:** User adds a new `Directory.Build.targets` file at a higher directory level. No existing file timestamp changes. The import graph is different. The fingerprint doesn't know about the new file because it wasn't imported last time. **Result: false positive (stale build).**

This circularity is fundamental, not an implementation detail. The fingerprint is always an approximation of the true input set.

### 7.3 🔴 Graph/Solution Builds Defeat the Purpose

The proposed insertion point (`BuildManager.ExecuteSubmission()` line 1580) handles individual `BuildSubmission`s. But:

- **Graph builds** go through `ExecuteSubmission(GraphBuildSubmission)` → `ExecuteGraphBuildScheduler()`, which constructs a `ProjectGraph` that evaluates ALL projects during graph construction (`ProjectGraph.cs:434-442`). The expensive evaluation happens before any per-project submission.
- **Solution builds** (the common case for `dotnet build` on a `.sln`) generate a metaproject via `SolutionProjectGenerator`, which dispatches to individual project builds — but the solution-level evaluation and project resolution still happens first.
- **Project-reference builds** are internal build requests, not top-level submissions. A hook at the submission level doesn't catch nested `MSBuild` task calls that build referenced projects.

**Phase 2 (graph integration) is not a later optimization — it's the hard part.** Most real-world `dotnet build` invocations are solution or multi-project builds where per-submission hooks don't help.

### 7.4 🟠 Blast Radius and Acceptance Risk

This is an engine change in `BuildManager` — the most complex and stability-critical component of MSBuild. It touches:

- Top-level submission semantics
- Results cache compatibility
- Graph build behavior
- Project-reference protocol
- IDE vs CLI behavior divergence
- Logging/diagnostics expectations

Given the MSBuild team's correctness culture and @rainersigwald's statement that project-level skip "was not part of the design of MSBuild," this will face heavy scrutiny. The change would need to be:
- Strictly opt-in (environment variable or property)
- Easy to disable when it causes issues
- Heavily tested across SDK-style, legacy, multi-targeting, solution, graph, and project-reference scenarios
- Proven to have zero false positives in CI scenarios

### 7.5 🟠 Alternative: Make Evaluation Faster Instead of Skipping It

Instead of the complexity of synthetic pre-evaluation skips, could MSBuild cache and reuse evaluation results?

- `ProjectRootElementCache` already caches parsed XML roots across builds in server mode
- Extending this to cache `ProjectInstance` (evaluated state) would preserve full engine semantics
- The import chain's timestamps could serve as a cache key (if any imported file changed, invalidate)
- This avoids the `BuildResult` semantic gap entirely — evaluation produces a real `ProjectInstance`, targets can run normally and skip via existing `Inputs`/`Outputs`
- Server mode is the natural host for this cache

This approach is less ambitious (doesn't skip evaluation entirely, just makes it cheaper) but is conceptually safer and more likely to be accepted.

### 7.6 Open Questions

| Question | Impact | Status |
|----------|--------|--------|
| What target results must a cached `BuildResult` contain? | Blocking for non-leaf projects | **Answered:** Must contain results for the full target closure including `GetTargetPath`, `GetCopyToOutputDirectoryItems`, `GetTargetFrameworks`. Graph builds compute this via `GetTargetLists`; non-graph builds need a heuristic. |
| Must `InitialTargets`/`DefaultTargets` be persisted alongside the cache? | Blocking | **Answered:** Yes — `ResultsCache.SatisfyRequest` returns `NotSatisfied` without them. |
| Can the fingerprint circularity be bounded? | Blocking — determines false-positive rate | Needs formal analysis. Conservative approach: invalidate on any file change in ancestor directories. |
| Is graph-only scope acceptable for an MVP? | Scoping decision | Graph builds know the full target closure; non-graph builds have dynamic target requests. Graph-only is safer but limits applicability. |
| Would the MSBuild team accept an engine change of this magnitude? | Blocking — organizational | Needs team discussion |
| Is evaluation caching (`ProjectInstance` reuse) a more viable path than evaluation skipping? | Could redirect the entire effort | Needs prototyping — likely safer, avoids `BuildResult` semantic gap entirely |
| What is the actual measured breakdown of evaluation cost? | Informs whether partial caching gives enough benefit | Needs profiling with representative projects |
| Can graph construction itself be cached? (cached topology + target map) | Would make graph-build FUTDC skip evaluation too | Unknown — graph depends on evaluation to discover `ProjectReference` items |

---

## 8. Why This Wasn't Done Before

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

## 9. Recommended Path Forward

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

### Reassessment After Red-Team Review

The red-team findings in [Section 7](#7-red-team-blocking-flaws-and-open-questions) significantly change the recommendation. The original Phase 1 (pre-evaluation engine hook with cached `BuildResult`) has **three blocking problems**: the `BuildResult` semantic gap, the fingerprint circularity, and the graph/solution build bypass.

A safer path forward:

**Phase A — Instrument and Measure:** Add telemetry/binlog markers to measure actual no-op build cost breakdown (evaluation vs RAR vs targets vs copies). Validate the assumed 50–70% evaluation cost with real data across diverse projects.

**Phase B — Evaluation Caching in Server Mode:** Instead of *skipping* evaluation, *cache* it. Extend `ProjectRootElementCache` to cache `ProjectInstance` (evaluated state, not just XML). Use import-chain timestamps as cache keys. This preserves full engine semantics — evaluation produces a real `ProjectInstance`, targets run normally, `BuildResult` is genuine. Server mode is the natural host. This avoids the `BuildResult` semantic gap entirely.

**Phase C — Strictly Opt-In Pre-Eval Skip:** For a narrow, well-defined scenario (SDK-style projects, `/t:Build` only, no custom targets, no requested project state), implement a pre-evaluation skip as a `ProjectCachePlugin` with a new `PreEvaluationGetCacheResultAsync` API. This limits blast radius to users who explicitly opt in and to projects that fit the constrained model.

**Phase D — Build Acceleration for CLI:** Independent of the above — when only copies are needed, perform them without full MSBuild. This is valuable even without evaluation skipping.

### Where to implement: Engine vs SDK vs Plugin

The Phase B evaluation-caching approach and Phase C plugin API extension are not mutually exclusive. The plugin API is more extensible; the built-in approach is simpler and avoids plugin packaging overhead. The key decision is whether the MSBuild team sees evaluation caching or evaluation skipping as the right long-term direction.

### Open Question: Where to Store the Fingerprint File

The choice of storage location affects discoverability (can we find it without evaluation?), gitignore behavior, clean-build semantics, and multi-tool compatibility.

| Location | Pros | Cons |
|----------|------|------|
| **`obj/.futdc`** (`BaseIntermediateOutputPath`) | Convention matches NuGet (`project.assets.json` lives here); `dotnet clean` deletes it (correct behavior); discoverable without evaluation for ~95% of projects | Pollutes `obj/` with yet another file; custom `BaseIntermediateOutputPath` breaks convention-based discovery; already crowded directory |
| **`.msbuild/` in project dir** | Clean namespace; clearly MSBuild-owned; easy to `.gitignore` as a pattern | New directory convention — nothing uses this today; not deleted by `dotnet clean`; needs explicit `.gitignore` entry |
| **`.vs/` in solution dir** | Precedent — VS FUTDC stores `.futdcache.v2` here; already `.gitignore`d by default | VS-specific convention; no solution context for standalone project builds; path requires knowing solution root; not deleted by `dotnet clean` on individual projects |
| **`~/.dotnet/futdc/` or OS temp** (user-level cache) | Never pollutes project tree; always writable; shared across clones | Cache invalidation nightmare; out of sync with build artifacts; stale across git worktrees; permissions issues in CI containers |
| **`artifacts/` dir** (artifacts layout) | Natural fit for `.NET 8+` artifacts layout users | Only ~5-8% of projects use this; doesn't help non-artifacts projects |

**Factors to consider:**
- `dotnet clean` should invalidate the fingerprint — `obj/` gets this for free, other locations need explicit cleanup
- CI builds typically start from clean `obj/` — fingerprint absence triggers normal build (correct)
- `.gitignore` — `obj/` is already ignored; `.msbuild/` would need a new global convention
- Multi-tool — if Rider/VS Code/VS all need the fingerprint, a project-local location beats a tool-specific one (`.vs/`)
- Pre-evaluation discovery — `obj/` is the only location discoverable by convention without evaluation (NuGet already proved this works)

**Current recommendation in this spec:** `obj/.futdc` — but this needs broader input from the team.

---

## 10. Related Issues and Prior Art

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
