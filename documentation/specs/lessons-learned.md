# Lessons Learned: `dotnet build --superfast` Prototype

## What We Built

A working `--superfast` switch for MSBuild that enables graph build mode with:
1. **`ProjectInstanceCache`** — server-persistent evaluation cache validated by `MSBuildAllProjects` timestamps
2. **`ProjectGraphCache`** — server-persistent graph cache validated by project file timestamps
3. **`IsNodeUpToDate`** — per-node skip in `ExecuteGraphBuildScheduler` comparing input timestamps vs output timestamps
4. **`--superfast` / `-sf` switch** in XMake.cs that implies `--graph` mode

## Results

| Scenario | Result | Time |
|----------|--------|------|
| First build (2 projects) | Normal build | ~12s |
| No-op rebuild with `--superfast` | 2/2 projects skipped | **~2.2s** |
| Touch leaf source, rebuild | Both rebuild correctly | ~4.8s |
| Touch app source, rebuild | Lib skipped, App rebuilds (1/2) | ~4.6s |
| No-op after partial rebuild | 2/2 skipped again | ~2.2s |

The ~2.2s is dominated by graph construction (~0.9s) + MSBuild startup. The actual skip check is sub-millisecond per project.

## What Works

- ✅ `--superfast` switch recognized, implies `--graph`
- ✅ Evaluation cache correctly returns cached `ProjectInstance` when no imports changed
- ✅ Graph cache skips graph reconstruction when project files unchanged  
- ✅ `IsNodeUpToDate` correctly identifies unchanged projects
- ✅ Dependency cascade: touching a leaf file rebuilds the leaf + all dependents
- ✅ Partial skip: only unchanged subtrees are skipped
- ✅ Build succeeds with 0 errors after all changes

## Known Gaps (from Expert Review)

### Blocking

1. **Synthetic `BuildResult` is incomplete** — currently creates empty `TargetResult` items. Downstream P2P targets (`GetTargetPath`, `GetCopyToOutputDirectoryItems`) need real items with metadata. For the simple test case this works because both projects build independently, but multi-targeting and P2P output propagation will break.

2. **`IsNodeUpToDate` input coverage is incomplete** — checks `Compile`, `EmbeddedResource`, `Content`, `ReferencePath`, `Analyzer`, and `MSBuildAllProjects`. Missing: `AdditionalFiles`, `EditorConfigFiles`, source generator inputs, glob-added files (new `.cs` files not in previous evaluation).

3. **`ReferencePath` is empty at evaluation time** — it's populated by RAR during execution. Cached `ProjectInstance` won't have it. Currently checking it anyway (harmless if empty — just a weaker check).

### Major

4. **No env var / SDK version tracking** — changing `DOTNET_ROOT` or SDK version won't invalidate the evaluation cache.

5. **No glob change detection** — adding a new `.cs` file to a directory won't trigger rebuild because no existing file timestamp changed and the evaluation cache returns the old glob results.

6. **Unbounded memory** — `ProjectInstanceCache` grows without limit. Need LRU eviction.

7. **No server mode enforcement** — `--superfast` doesn't force server mode on. Without server, caches are per-process only (still useful for graph build within one invocation, but no cross-invocation benefit).

8. **Graph load is still ~1s** — even with caching, the `ProjectGraph` constructor takes ~1s for 2 projects. For large solutions this will be significant. The graph cache helps on subsequent builds but the first build pays full cost.

## Architectural Decisions Made

### Decision 1: Engine-level implementation, not ProjectCachePlugin
**Why:** ProjectCachePlugin is consulted AFTER evaluation. We wanted to skip evaluation via the cache. A plugin can't do that.
**Tradeoff:** We had to create synthetic `BuildResult` objects (see Gap #1). A plugin approach would avoid this but can't skip evaluation.
**For the future:** Consider a hybrid — evaluation cache in the engine, skip logic as a built-in cache plugin with a new pre-evaluation API.

### Decision 2: Timestamp-based invalidation, not content hashing
**Why:** `ProjectRootElementCache` already uses timestamps, proven reliable. Content hashing adds I/O cost.
**Tradeoff:** Can't detect same-second changes or files that changed and changed back. Known limitation shared with VS FUTDC.

### Decision 3: Static caches on `ProjectInstanceCache` / `ProjectGraphCache`
**Why:** Need to survive across `BeginBuild`/`EndBuild` cycles in server mode. Static fields on these utility classes work like `ProjectRootElementCache`.
**Tradeoff:** Global state, thread safety via `ConcurrentDictionary`. No eviction policy yet.

### Decision 4: Per-node skip in `BuildGraph()` loop
**Why:** Simplest insertion point. The loop already has a skip mechanism for empty target lists.
**Tradeoff:** Tightly coupled to graph build mode. Non-graph builds don't benefit. Synthetic `BuildResult` needs work.

## What Would Need to Change for Production

1. **Persisted `TargetResult` outputs** — store the previous build's actual `TargetResult` items per target per project. Replay them on skip instead of empty results. This is the #1 gap.

2. **Glob change detection** — hash the set of files matching each glob, not just individual file timestamps. Store in the evaluation cache entry.

3. **Environment fingerprinting** — hash `DOTNET_ROOT`, `PATH`, SDK version, key `MSBuild*` env vars as part of the evaluation cache key.

4. **Memory management** — bounded LRU cache with configurable size limit.

5. **Server mode integration** — `--superfast` should set `MSBUILDUSESERVER=1` so caches persist across invocations.

6. **Terminal logger integration** — show "⚡ accelerated" next to skipped projects in the terminal logger output.

7. **Fallback path** — degrade gracefully when server mode unavailable (warn + fall back to normal graph build).

## Lines of Code

| File | Action | Lines Changed |
|------|--------|--------------|
| `src/Build/Evaluation/ProjectInstanceCache.cs` | New | 208 |
| `src/Build/Graph/ProjectGraphCache.cs` | New | 148 |
| `src/Build/BackEnd/BuildManager/BuildManager.cs` | Modified | +188 |
| `src/Build/BackEnd/BuildManager/BuildParameters.cs` | Modified | +8 |
| `src/MSBuild/XMake.cs` | Modified | +16 |
| `src/MSBuild/CommandLine/CommandLineSwitches.cs` | Modified | +2 |
| `src/MSBuild.UnitTests/CommandLineSwitches_Tests.cs` | Modified | +1 |
| `documentation/specs/impl-plan.md` | New | 477 |
| **Total** | | **~1048** |

## Comparison to VS FUTDC

| Aspect | VS FUTDC | Our `--superfast` |
|--------|----------|-------------------|
| Time to skip (per project) | 1-10ms | Sub-millisecond |
| Overhead | None (VS already running) | ~1s graph load + MSBuild startup |
| Evaluation caching | CPS keeps snapshots warm | `ProjectInstanceCache` (timestamp-validated) |
| Graph caching | SBM `DependencyGraph` | `ProjectGraphCache` (timestamp-validated) |
| Change detection | Proactive (file watchers) | Reactive (timestamp check at build time) |
| Correctness | Known bugs (branch switch, T4, etc.) | Same class of issues + additional gaps |
| Skip mechanism | Don't call `StartBuild()` | Skip submission in graph scheduler |
| `BuildResult` fidelity | N/A (MSBuild never called) | Synthetic — incomplete (needs work) |
