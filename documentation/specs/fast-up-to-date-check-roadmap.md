# Fast Up-To-Date Checks in MSBuild CLI: Architecture & Roadmap

## Executive Summary

Today, Visual Studio achieves dramatically faster inner-loop build times through a **Fast Up-To-Date Check (FUTDC)** implemented in [dotnet/project-system](https://github.com/dotnet/project-system). This check runs entirely in-process in VS and can determine whether a project needs building in **1–10 ms**, compared to **100–500+ ms** for MSBuild's own no-op evaluation-and-target-execution cycle. The FUTDC is VS-only; CLI users running `dotnet build` get no benefit from it.

This document analyzes the FUTDC architecture, maps it to MSBuild's existing infrastructure, and proposes a roadmap for bringing equivalent fast up-to-date checking to `dotnet build` and other batch-build scenarios.

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [How VS Fast Up-To-Date Check Works](#2-how-vs-fast-up-to-date-check-works)
3. [How MSBuild Incremental Build Works Today](#3-how-msbuild-incremental-build-works-today)
4. [Gap Analysis](#4-gap-analysis)
5. [Design Principles for CLI Fast Up-To-Date Check](#5-design-principles-for-cli-fast-up-to-date-check)
6. [Roadmap: Phased Implementation](#6-roadmap-phased-implementation)
7. [Key Technical Challenges](#7-key-technical-challenges)
8. [References](#8-references)

---

## 1. Problem Statement

### The Cost of a No-Op Build

When a developer runs `dotnet build && dotnet build`, the second invocation should ideally take zero time. In practice, MSBuild must:

1. **Parse and evaluate** every project file in the build graph (property/item/import resolution)
2. **Walk every target** in the `BuildDependsOn` chain, performing timestamp comparisons
3. **Execute non-skippable targets** like `ResolveAssemblyReferences` (RAR) that lack concrete `Inputs`/`Outputs`
4. **Run Copy tasks** that check file-level incrementality even when targets cannot be skipped

> "Every [batch build] must evaluate every project in the scope of the build."
> — [`documentation/Persistent-Problems.md`](../../documentation/Persistent-Problems.md), Line 9

> "When build is invoked, most targets can be skipped as up to date, but `ResolveAssemblyReferences` (RAR) and some of its prerequisites like `ResolvePackageAssets` cannot, because their role is to produced data used within the build to compute the compiler command line."
> — [`documentation/Persistent-Problems.md`](../../documentation/Persistent-Problems.md), Lines 11–13

> "In the limit, a fully-up-to-date build is instructive for the MSBuild team, because an ideal fully up-to-date build would take no time to run."
> — [`documentation/Build-Scenarios.md`](../../documentation/Build-Scenarios.md), Lines 21–22

### What VS Does Differently

Visual Studio's project system implements `IBuildUpToDateCheckProvider` to perform a **project-level** up-to-date check *before* invoking MSBuild. If the project is up-to-date, MSBuild is never called at all. This eliminates the evaluation, target-walking, and task-execution overhead entirely.

> "Fast Up-To-Date Check is a system that is implemented by the Project System, that decides, if it needs to run MSBuild. MSBuild takes a non-trivial amount of time to load, evaluate, and run through each target and task. Fast Up-To-Date is faster, but can be less accurate, suitable for an IDE and a human interface."
> — [`documentation/specs/question.md`](../../documentation/specs/question.md), Lines 8–9

**This document proposes bringing this concept into MSBuild itself, so that `dotnet build` can skip entire projects without the overhead of evaluation and target execution.**

---

## 2. How VS Fast Up-To-Date Check Works

### 2.1 Architecture Overview

The FUTDC lives in the `dotnet/project-system` repository under:
```
src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/
```
— [GitHub directory](https://github.com/dotnet/project-system/tree/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate)

#### Data Flow

```
MSBuild Evaluation + Design-Time Build
         │
         ▼
UpToDateCheckImplicitConfiguredInputDataSource
   (Dataflow pipeline subscribed to evaluation/design-time build results)
         │
         ▼
UpToDateCheckImplicitConfiguredInput (immutable snapshot)
         │
         ▼
BuildUpToDateCheck.IsUpToDateAsync()
   ├─ Stage 1: CheckGlobalConditions()
   ├─ Stage 2: CheckInputsAndOutputs()     (per named set)
   ├─ Stage 3: CheckCopiedOutputFiles()     (1:1 transforms)
   ├─ Stage 4: CheckCopyToOutputDirectoryFiles()
   └─ Stage 5: Reference Assembly Markers
         │
         ▼
Decision: Up-to-date → Skip MSBuild entirely
          Not up-to-date → Invoke MSBuild
```

— Source: [`BuildUpToDateCheck.cs`](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/BuildUpToDateCheck.cs)

### 2.2 Key Components

| Component | File | Role |
|-----------|------|------|
| **Orchestrator** | `BuildUpToDateCheck.cs` | Main decision logic; implements `IBuildUpToDateCheckProvider2` |
| **State Snapshot** | `UpToDateCheckImplicitConfiguredInput.cs` | Immutable record of all inputs, outputs, references, copy items |
| **Data Pipeline** | `UpToDateCheckImplicitConfiguredInputDataSource.cs` | Subscribes to evaluation/DTB results; incrementally builds snapshots |
| **Timestamp Cache** | `BuildUpToDateCheck.TimestampCache.cs` | Caches `File.GetLastWriteTimeUtc()` within a single check |
| **Item Hashing** | `BuildUpToDateCheck.ItemHashing.cs` | Detects when the *set* of project items changes (add/remove) |
| **State Persistence** | `UpToDateCheckStatePersistence.cs` | Persists state to `.futdcache.v2` across VS sessions |
| **Copy Aggregator** | `CopyItemAggregator.cs` | Walks project reference graph for transitive copy items |

### 2.3 What the FUTDC Tracks

#### Inputs
- **Source items**: `Compile`, `EmbeddedResource`, `Content`, etc. (via evaluation data)
- **Resolved references**: Analyzer references, compilation references (from design-time build targets)
- **Import chain**: Newest entry in `MSBuildAllProjects` (the `.props`/`.targets` import graph)
- **Custom inputs**: `UpToDateCheckInput` items (user/SDK-declared)
- **Copy-to-output items**: Items with `CopyToOutputDirectory` metadata

#### Outputs
- **Primary output**: `TargetPath` property (e.g., `bin/Debug/net9.0/MyApp.dll`)
- **Custom outputs**: `UpToDateCheckOutput` items
- **Built outputs**: `UpToDateCheckBuilt` items (with optional `Original` metadata for 1:1 transforms)
- **Output directory**: `OutDir` / `OutputPath`

#### Grouping
- **Sets** (`Set` metadata): Partition inputs/outputs into independent groups
- **Kinds** (`Kind` metadata): Filter items via `FastUpToDateCheckIgnoresKinds`

— Source: [project-system docs/up-to-date-check.md](https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md)

### 2.4 Decision Algorithm

The check short-circuits on the first "not up-to-date" finding:

1. **Global conditions**: Is this the first run? Have items been added/removed since last successful build?
2. **Per-set timestamp comparison**: For each set, is any input newer than the earliest output? Is any input newer than the last successful build start time?
3. **Copied output files**: For 1:1 transforms (`UpToDateCheckBuilt` with `Original`), is source newer than destination?
4. **Copy-to-output-directory files**: For `PreserveNewest` items, is source newer? For `Always` items, compare size + timestamp.
5. **Reference assembly markers**: Have referenced project implementations changed?

### 2.5 Build Acceleration

Starting in VS 17.5, when the FUTDC determines that only file copies are needed (not recompilation), VS performs the copies directly without invoking MSBuild at all. In a chain `A → B → C → D`, changing D previously required 4 MSBuild invocations; with acceleration, only 1 MSBuild call + fast VS-driven copies for the rest.

— Source: [project-system docs/build-acceleration.md](https://github.com/dotnet/project-system/blob/main/docs/build-acceleration.md)

---

## 3. How MSBuild Incremental Build Works Today

### 3.1 Target-Level Up-To-Date Checking

MSBuild's primary incrementality mechanism operates at the **target level**. Each target can declare `Inputs` and `Outputs` attributes. Before executing a target, the engine runs [`TargetUpToDateChecker.PerformDependencyAnalysis()`](../../src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs) (Line 132) which returns one of:

```csharp
internal enum DependencyAnalysisResult
{
    SkipUpToDate,      // Target outputs are all newer than inputs
    SkipNoInputs,      // Target declared no inputs
    SkipNoOutputs,     // Target declared no outputs
    IncrementalBuild,  // Some outputs are out of date
    FullBuild          // All outputs need rebuilding
}
```
— Source: [`src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs`](../../src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs), Lines 37–44

The analysis uses `NativeMethodsShared.GetLastWriteFileUtcTime()` for timestamp comparisons (Lines 1197–1232) and supports two correlation strategies:
- **Correlated inputs/outputs**: Item vector correlation (e.g., `@(Compile)` → `@(IntermediateAssembly)`)
- **Discrete outputs**: All inputs compared against all outputs

— Source: [`src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs`](../../src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs), Lines 589–777

### 3.2 The `/question` Flag

MSBuild's `/question` switch runs the build but errors out at the first non-up-to-date target or task. This is a **diagnostic tool**, not an optimization—it still performs full evaluation and walks every target.

```cmd
msbuild /p:Configuration=Debug Project1.csproj /bl:incremental.binlog /question
```

Tasks can implement `IIncrementalTask` to participate in `/question` mode:

```csharp
public interface IIncrementalTask
{
    bool FailIfNotIncremental { set; }
}
```
— Source: [`src/Framework/IIncrementalTask.cs`](../../src/Framework/IIncrementalTask.cs)

— Source: [`documentation/specs/question.md`](../../documentation/specs/question.md)

### 3.3 The Build Target Chain

A standard `dotnet build` walks this target chain:

```xml
<BuildDependsOn>
    BeforeBuild;
    CoreBuild;
    AfterBuild
</BuildDependsOn>

<CoreBuildDependsOn>
    BuildOnlySettings;
    PrepareForBuild;
    PreBuildEvent;
    ResolveReferences;       ← Includes RAR (cannot be skipped)
    PrepareResources;
    ResolveKeySource;
    Compile;                 ← CoreCompile has Inputs/Outputs
    ExportWindowsMDFile;
    UnmanagedUnregistration;
    GenerateSerializationAssemblies;
    CreateSatelliteAssemblies;
    GenerateManifests;
    GetTargetPath;
    PrepareForRun;           ← Copy operations
    UnmanagedRegistration;
    IncrementalClean;
    PostBuildEvent
</CoreBuildDependsOn>
```
— Source: [`src/Tasks/Microsoft.Common.CurrentVersion.targets`](../../src/Tasks/Microsoft.Common.CurrentVersion.targets), Lines 902–960

### 3.4 Cost Centers in a No-Op Build

| Phase | Approximate Cost | Can Be Skipped? |
|-------|-----------------|-----------------|
| Project evaluation (parsing, property/item resolution) | 50–70% of no-op time | ❌ Always runs |
| ResolveAssemblyReferences + ResolvePackageAssets | 20–30% | ❌ No Inputs/Outputs |
| Target dependency analysis (timestamp comparisons) | 5–10% | ❌ Must check each target |
| Copy tasks (fine-grained incrementality) | 5–15% | ❌ Targets not incremental |

### 3.5 Copy Up-To-Date Marker

MSBuild does have a lightweight marker mechanism for copy operations:

```xml
<MSBuildCopyMarkerName>$(MSBuildProjectFile).Up2Date</MSBuildCopyMarkerName>
<CopyUpToDateMarker Include="$(IntermediateOutputPath)$(MSBuildCopyMarkerName)" />
```
— Source: [`src/Tasks/Microsoft.Common.CurrentVersion.targets`](../../src/Tasks/Microsoft.Common.CurrentVersion.targets), Lines 389–399

This marker is `Touch`ed after copy operations complete (Line 5118) and checked by referencing projects to skip copying when the referenced project hasn't changed. However, this is a narrow optimization that only covers copy operations, not the overall project build.

### 3.6 Results Cache

`BuildManager` maintains `IConfigCache` and `IResultsCache` that cache build results by `(project path, global properties, targets)`. Within a single `BeginBuild`/`EndBuild` session, previously-built projects can be served from cache. However, this cache does not persist across `dotnet build` invocations.

— Source: [`src/Build/BackEnd/BuildManager/BuildManager.cs`](../../src/Build/BackEnd/BuildManager/BuildManager.cs)

---

## 4. Gap Analysis

| Capability | VS FUTDC | MSBuild CLI |
|-----------|----------|-------------|
| **Skip MSBuild entirely for up-to-date projects** | ✅ Yes | ❌ No |
| **Project-level up-to-date check** | ✅ ~1–10 ms | ❌ Must evaluate + walk targets |
| **Persisted state across invocations** | ✅ `.futdcache.v2` | ❌ No persistence |
| **Item set change detection** (add/remove) | ✅ Hash-based | ❌ Only timestamp-based |
| **Reference graph-aware copy optimization** | ✅ Build Acceleration | ❌ Each project runs Copy targets |
| **Custom input/output declarations** | ✅ `UpToDateCheckInput`/`Output` | Partial (target `Inputs`/`Outputs`) |
| **Target-level incrementality** | N/A (skips MSBuild) | ✅ `TargetUpToDateChecker` |
| **Task-level incrementality** | N/A (skips MSBuild) | ✅ `IIncrementalTask`, Copy task |
| **Diagnostic mode** | ✅ Logging in Build Output | ✅ `/question` flag |
| **Handles non-timestamp changes** (config, env) | ✅ Detected by evaluation data | ❌ Only file timestamps |

### Key Architectural Difference

The VS FUTDC operates as a **higher-order build system** that sits above MSBuild. It receives continuously-updated project state from the CPS (Common Project System) Dataflow pipeline and can make fast decisions without invoking MSBuild at all.

For CLI scenarios, there is no long-lived process maintaining project state. A new `dotnet build` invocation starts from scratch. **The core challenge is: how do we get FUTDC-level speed without a persistent process?**

---

## 5. Design Principles for CLI Fast Up-To-Date Check

1. **Correctness first**: A CLI FUTDC must never produce false positives (claiming up-to-date when not). In contrast to the VS FUTDC which notes "It is not accurate enough for a CI" ([`documentation/specs/question.md`](../../documentation/specs/question.md), Line 8), a CLI version used in CI must be conservative.

2. **Persistence-based**: Since there's no long-lived process, the check must persist state to disk between invocations. The VS `.futdcache.v2` pattern is the model.

3. **Pre-evaluation**: The check must run *before* MSBuild evaluation to avoid the dominant cost center. This means reading persisted state and comparing it against the filesystem.

4. **Opt-in initially**: Like VS Build Acceleration, start opt-in to build confidence before making it default.

5. **Composable with existing mechanisms**: The CLI FUTDC should complement, not replace, target-level `Inputs`/`Outputs` and `IIncrementalTask`. If the CLI check says "not up-to-date," MSBuild's existing incrementality still minimizes work.

6. **Graph-aware**: For solution/multi-project builds, the check should understand project references to make graph-level skip decisions.

---

## 6. Roadmap: Phased Implementation

### Phase 1: Build State Persistence ("Build Fingerprint Cache")

**Goal**: After a successful build, persist a fingerprint of the project's build state. Before the next build, compare the fingerprint to determine if MSBuild needs to be invoked.

#### 1.1 Define the Fingerprint

A project build fingerprint should capture:

| Component | What to Hash/Store | Source |
|-----------|-------------------|--------|
| **Source items** | Sorted list of item include paths + timestamps | Evaluation: `@(Compile)`, `@(EmbeddedResource)`, etc. |
| **Reference assemblies** | Resolved reference paths + timestamps | RAR output from prior build |
| **Project file chain** | `MSBuildAllProjects` paths + timestamps | Evaluation property |
| **Global properties** | Sorted key-value pairs | `BuildParameters.GlobalProperties` |
| **Target framework** | `$(TargetFramework)` value | Evaluation property |
| **Primary output** | `$(TargetPath)` timestamp | Evaluation property |
| **Copy-to-output items** | Source/destination pairs + timestamps | Evaluation: items with `CopyToOutputDirectory` |
| **Build configuration** | `$(Configuration)`, `$(Platform)`, key switches | Evaluation properties |

This mirrors the VS FUTDC's `UpToDateCheckImplicitConfiguredInput` snapshot but stored on disk rather than in memory.

#### 1.2 Persistence Format

Store the fingerprint in the intermediate output directory (e.g., `obj/Debug/net9.0/.msbuild.futdc`). Use a binary or JSON format with:
- Schema version number for forward/backward compatibility
- Timestamp of last successful build start
- Hash of the item set (for add/remove detection, mirroring `BuildUpToDateCheck.ItemHashing.cs`)
- Per-file timestamps for all tracked inputs and outputs
- Hash of global properties and build configuration

This is analogous to `UpToDateCheckStatePersistence.cs` in the project-system, which writes `.futdcache.v2` files.

#### 1.3 Write Phase (Post-Build)

After a successful build, write the fingerprint. This could be implemented as:
- A new target (`_WriteBuildFingerprint`) that runs at the end of `CoreBuild`
- Or integrated into `BuildManager.EndBuild()` for solution-level state

#### 1.4 Read Phase (Pre-Build)

Before evaluation, read the fingerprint and compare against the filesystem. This is the most architecturally challenging part because it must run **before** MSBuild evaluation to avoid that cost.

**Options:**
- **Option A: MSBuild engine-level check** — Add a pre-evaluation hook in `BuildManager` or `RequestBuilder` that reads the fingerprint file and compares timestamps. If up-to-date, inject a cached `BuildResult` without evaluation.
- **Option B: SDK-level wrapper target** — A target that runs before evaluation-dependent targets, reads the fingerprint, and short-circuits. Less effective because evaluation still runs.
- **Option C: CLI-level check** — The `dotnet build` CLI (in dotnet/sdk) performs the check before invoking MSBuild, similar to how VS performs FUTDC before invoking MSBuild. Most analogous to the VS architecture.

**Recommendation**: Option A (engine-level) gives the best performance, but Option C (CLI-level) is the most architecturally clean and mirrors the VS pattern. A hybrid approach could work: the CLI reads a lightweight fingerprint, and the engine persists it.

### Phase 2: Project-Level Skip in Graph Builds

**Goal**: In `dotnet build` with `--graph` (or the default graph-based build), skip entire projects that are up-to-date.

#### 2.1 Graph Build Integration

MSBuild's graph build (`BuildManager` with `ProjectGraph`) already evaluates all projects up front to construct the dependency graph. After graph construction, before scheduling builds:

1. For each project node in topological order, check the build fingerprint
2. If a project and all its dependencies are up-to-date (fingerprints match), skip it entirely
3. If any dependency was rebuilt, mark the project as potentially not up-to-date

This mirrors the VS behavior where `CopyItemAggregator.cs` walks the project reference graph to make transitive up-to-date decisions.

— Source: [`CopyItemAggregator.cs`](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/CopyItemAggregator.cs)

#### 2.2 Integration with Project Caching

MSBuild's existing project caching plugin system (`ProjectCachePluginBase`) already supports graph-based builds with cache consultation. The fingerprint check could be implemented as a built-in cache plugin, or the fingerprint could feed into the existing `IResultsCache`.

— Source: [`src/Build/BackEnd/BuildManager/BuildManager.cs`](../../src/Build/BackEnd/BuildManager/BuildManager.cs)

### Phase 3: Lightweight Pre-Evaluation Check

**Goal**: Avoid MSBuild evaluation entirely for up-to-date projects.

#### 3.1 Pre-Evaluation Fingerprint Comparison

The dominant cost of a no-op build is evaluation (~50–70%). To eliminate this:

1. Before calling `Evaluator.Evaluate()`, read the fingerprint file from the expected `obj/` path
2. Compare key filesystem timestamps against the fingerprint:
   - Project file timestamp
   - `Directory.Build.props`/`Directory.Build.targets` timestamps
   - `global.json` timestamp
   - NuGet assets file timestamp
3. If all match, skip evaluation and inject the cached build result

This requires knowing the `obj/` path without evaluation, which is possible for SDK-style projects using convention (`obj/$(Configuration)/$(TargetFramework)/`). For non-standard layouts, fall back to full evaluation.

#### 3.2 Evaluation Caching (Alternative)

Instead of skipping evaluation, cache evaluation results:
- After evaluation, serialize the `ProjectInstance` to disk
- Before evaluation, check if the serialized state is still valid (import chain timestamps)
- If valid, deserialize instead of re-evaluating

This is a heavier mechanism but handles more edge cases. It's related to the existing `ProjectRootElementCache` which caches parsed XML but not evaluated state.

### Phase 4: Build Acceleration for CLI

**Goal**: When only file copies are needed, perform them without a full MSBuild invocation.

#### 4.1 Copy-Only Fast Path

Mirroring VS Build Acceleration:
1. The fingerprint records which files need copying (source → destination)
2. On the fast path, if only copy operations are needed (compilation outputs are up-to-date), perform the copies directly
3. This eliminates the need to invoke MSBuild for reference-chain propagation

#### 4.2 Reference Assembly Awareness

When a referenced project rebuilds but its **reference assembly** (`ref/` output) is unchanged:
1. The referencing project's compilation is still up-to-date
2. Only copy operations (copying the implementation assembly) are needed
3. The CLI FUTDC can detect this by checking the reference assembly timestamp separately from the implementation assembly

This mirrors the VS FUTDC's `CopyUpToDateMarker` checking logic and the `ProduceReferenceAssembly` mechanism.

— Source: [`src/Tasks/Microsoft.Common.CurrentVersion.targets`](../../src/Tasks/Microsoft.Common.CurrentVersion.targets), Lines 399–406

### Phase 5: MSBuild Server Mode Integration

**Goal**: In the long term, a persistent MSBuild server process can maintain in-memory state, eliminating the need for disk-based fingerprinting.

The MSBuild server mode (already partially implemented) keeps the MSBuild process alive between builds. With a persistent process:
1. Evaluation results can be cached in memory (like VS CPS)
2. File system watchers can detect changes incrementally (like VS Dataflow subscriptions)
3. The FUTDC can operate exactly like the VS version—in-memory, sub-millisecond decisions

This is the ultimate convergence point where CLI builds approach IDE build performance.

---

## 7. Key Technical Challenges

### 7.1 Correctness vs. Speed Tradeoff

The VS FUTDC explicitly acknowledges being "not accurate enough for a CI" ([`documentation/specs/question.md`](../../documentation/specs/question.md), Line 8). A CLI FUTDC must be more conservative because:
- CI builds must be reproducible
- Users expect `dotnet build` to always produce correct output
- Silent build skips that miss changes would severely damage trust

**Mitigation**: Start opt-in, maintain a conservative default, and provide detailed logging (like VS's `FastUpToDate:` messages) for diagnostics.

### 7.2 RAR and Non-Skippable Targets

`ResolveAssemblyReferences` cannot declare `Inputs`/`Outputs` because its inputs are the transitive closure of all referenced assemblies, which is expensive to compute. The FUTDC approach sidesteps this by checking at the project level—if no inputs changed, RAR's outputs won't change either.

> "ResolveAssemblyReferences (RAR) and some of its prerequisites like ResolvePackageAssets cannot [be skipped], because their role is to produce data used within the build to compute the compiler command line."
> — [`documentation/Persistent-Problems.md`](../../documentation/Persistent-Problems.md), Lines 11–13

**Mitigation**: The fingerprint must capture enough information to know that RAR's inputs haven't changed (reference assembly paths + timestamps, NuGet lock file, etc.).

### 7.3 Determining `obj/` Path Without Evaluation

To skip evaluation, we need to read the fingerprint file, but the fingerprint is stored in `obj/` whose path depends on evaluated properties. For SDK-style projects, the default is predictable (`obj/$(Configuration)/$(TargetFramework)/`), but users can customize `IntermediateOutputPath`.

**Mitigation**: Store a lightweight pointer file at a well-known location (e.g., `obj/.futdc-pointer`) that records the actual fingerprint path. Or use a convention-based path that only works for default layouts (covering the majority of projects).

### 7.4 Multi-Targeting

Multi-targeting projects (`<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`) dispatch to inner builds for each framework. Each inner build needs its own fingerprint. The VS FUTDC handles this via `UpToDateCheckConfiguredInputDataSource.cs` which aggregates across configurations.

**Mitigation**: Store per-TFM fingerprints and check all of them. The outer build is up-to-date only if all inner builds are up-to-date.

### 7.5 Custom Build Logic and Extensibility

NuGet packages, SDK extensions, and user targets can add arbitrary build logic that the FUTDC may not model. The VS FUTDC handles this partially via `UpToDateCheckInput`/`UpToDateCheckOutput`/`UpToDateCheckBuilt` items and `BuildAccelerationIncompatiblePackage` markers.

**Mitigation**: Support the same `UpToDateCheckInput`/`UpToDateCheckOutput`/`UpToDateCheckBuilt` item types. Provide a `BuildAccelerationIncompatiblePackage` escape hatch. When unknown custom targets are detected, disable the fast path.

### 7.6 Environment Variable and Tool Version Changes

Changes to environment variables, SDK versions, or tool versions can affect build output without changing any tracked file timestamps. The VS FUTDC detects some of these through evaluation data changes.

**Mitigation**: Include in the fingerprint:
- SDK version (`dotnet --version` or `global.json` timestamp)
- Key environment variables (`DOTNET_ROOT`, `MSBUILD_*`)
- Hash of the tools version

### 7.7 Concurrent and Distributed Builds

In CI environments with parallel builds, concurrent file system access could cause race conditions with fingerprint files.

**Mitigation**: Use atomic write patterns (write to temp file, then rename). Include a build ID in the fingerprint to detect stale data from concurrent builds.

---

## 8. References

### MSBuild Repository

| Document | Path | Description |
|----------|------|-------------|
| Persistent Problems | [`documentation/Persistent-Problems.md`](../../documentation/Persistent-Problems.md) | Documents known performance bottlenecks |
| Build Scenarios | [`documentation/Build-Scenarios.md`](../../documentation/Build-Scenarios.md) | Describes batch, incremental, and IDE build scenarios |
| Question Spec | [`documentation/specs/question.md`](../../documentation/specs/question.md) | `/question` flag and FUTDC relationship |
| TargetUpToDateChecker | [`src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs`](../../src/Build/BackEnd/Components/RequestBuilder/TargetUpToDateChecker.cs) | Target-level up-to-date analysis engine |
| TargetEntry | [`src/Build/BackEnd/Components/RequestBuilder/TargetEntry.cs`](../../src/Build/BackEnd/Components/RequestBuilder/TargetEntry.cs) | Target execution including skip decisions |
| IIncrementalTask | [`src/Framework/IIncrementalTask.cs`](../../src/Framework/IIncrementalTask.cs) | Task-level incrementality interface |
| BuildManager | [`src/Build/BackEnd/BuildManager/BuildManager.cs`](../../src/Build/BackEnd/BuildManager/BuildManager.cs) | Build orchestration and result caching |
| Common Targets | [`src/Tasks/Microsoft.Common.CurrentVersion.targets`](../../src/Tasks/Microsoft.Common.CurrentVersion.targets) | Build/CoreBuild target chain, CopyUpToDateMarker |
| Cross-Targeting Targets | [`src/Tasks/Microsoft.Common.CrossTargeting.targets`](../../src/Tasks/Microsoft.Common.CrossTargeting.targets) | Multi-TFM dispatch |

### dotnet/project-system Repository

| Document | URL | Description |
|----------|-----|-------------|
| FUTDC Documentation | [docs/up-to-date-check.md](https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md) | Official FUTDC documentation |
| Build Acceleration | [docs/build-acceleration.md](https://github.com/dotnet/project-system/blob/main/docs/build-acceleration.md) | Build Acceleration feature documentation |
| BuildUpToDateCheck.cs | [src/.../UpToDate/BuildUpToDateCheck.cs](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/BuildUpToDateCheck.cs) | Main FUTDC orchestrator |
| State Snapshot | [src/.../UpToDate/UpToDateCheckImplicitConfiguredInput.cs](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/UpToDateCheckImplicitConfiguredInput.cs) | Immutable project state for up-to-date checking |
| Data Source | [src/.../UpToDate/UpToDateCheckImplicitConfiguredInputDataSource.cs](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/UpToDateCheckImplicitConfiguredInputDataSource.cs) | Dataflow pipeline for FUTDC data |
| State Persistence | [src/.../UpToDate/UpToDateCheckStatePersistence.cs](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/UpToDateCheckStatePersistence.cs) | `.futdcache.v2` persistence |
| Copy Aggregator | [src/.../UpToDate/CopyItemAggregator.cs](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/UpToDate/CopyItemAggregator.cs) | Transitive copy item collection |

### Microsoft Learn

| Document | URL | Description |
|----------|-----|-------------|
| Build Incrementally | [learn.microsoft.com](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-build-incrementally) | MSBuild incremental build documentation |
| Up-to-date Check | [learn.microsoft.com](https://learn.microsoft.com/en-us/visualstudio/project-system/up-to-date-check) | Official VS FUTDC documentation |

---

## Appendix A: Concept Mapping — VS FUTDC → MSBuild CLI FUTDC

| VS FUTDC Concept | Proposed MSBuild CLI Equivalent |
|-----------------|-------------------------------|
| `IBuildUpToDateCheckProvider` | New `IProjectUpToDateCheck` interface in `Microsoft.Build.Framework` |
| `UpToDateCheckImplicitConfiguredInput` | `BuildFingerprint` class persisted to `obj/` |
| `UpToDateCheckImplicitConfiguredInputDataSource` | Post-build fingerprint writer target + pre-build reader |
| `BuildUpToDateCheck.TimestampCache` | Similar timestamp cache in fingerprint comparison logic |
| `BuildUpToDateCheck.ItemHashing` | Item set hashing included in fingerprint |
| `UpToDateCheckStatePersistence` (.futdcache.v2) | `.msbuild.futdc` file in intermediate output |
| `CopyItemAggregator` (BFS graph walk) | Graph build integration in `BuildManager`/`ProjectGraph` |
| Build Acceleration (copy-only fast path) | CLI copy-only fast path skipping MSBuild for copy operations |
| `UpToDateCheckInput` / `UpToDateCheckOutput` items | Same items, interpreted by MSBuild engine fingerprint writer |
| `FastUpToDateCheckIgnoresKinds` | Same property, interpreted by CLI FUTDC |
| `DisableFastUpToDateCheck` | Same property, additionally disables CLI FUTDC |
| Design-time build data subscription | N/A — use evaluation + last build's RAR/ResolvePackageAssets output |

## Appendix B: Decision Matrix — Where to Implement

| Implementation Location | Pros | Cons |
|------------------------|------|------|
| **dotnet/sdk CLI** (`dotnet build` command) | Mirrors VS architecture; clean separation; can use conventions for `obj/` path | Requires cross-repo work; duplicate logic if MSBuild also needs it |
| **MSBuild Engine** (`BuildManager` / `RequestBuilder`) | Single source of truth; works for all MSBuild invocations | Harder to skip evaluation (evaluation already started); increases engine complexity |
| **MSBuild Targets** (`.targets` files) | Easy to prototype; no engine changes | Cannot skip evaluation; limited performance benefit |
| **Project Cache Plugin** (`ProjectCachePluginBase`) | Uses existing extensibility; graph-aware | Plugin infrastructure overhead; requires graph build mode |
| **MSBuild Server** (persistent process) | Best long-term perf; mirrors VS architecture closely | Depends on MSBuild server maturity; complex state management |

**Recommended approach**: Start with SDK CLI-level check (Phase 1–2), integrate into MSBuild engine for graph builds (Phase 2–3), and converge with MSBuild Server long-term (Phase 5).
