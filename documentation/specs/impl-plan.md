# Implementation Plan: `dotnet build --superfast`

## Overview

This implements a server-cached graph build mode with per-node project skip. When MSBuild server is alive, evaluations and the project graph are cached in-memory. On subsequent builds, only changed projects are re-evaluated and rebuilt. Up-to-date projects are skipped entirely — no evaluation, no target execution.

## Repository: `dotnet/msbuild` (Q:\m2)

All changes are in the MSBuild repo. Zero SDK changes — the SDK is a transparent wrapper calling `MSBuildApp.Main()`.

---

## Layer 0: The `--superfast` Switch

### File: `src/MSBuild/XMake.cs`

Add a new command-line switch `--superfast` (alias: `-sf`) that implies:
- `--graph` (graph build mode)
- Server mode enabled (`MSBUILDUSESERVER=1`)  
- `BuildParameters.SuperFast = true` (new property)

**Changes:**
1. `XMake.cs` — add `ref bool superFast` parameter to `ProcessCommandLineSwitches`. Parse `--superfast`/`-sf`. When true, set `graphBuildOptions = new GraphBuildOptions { Build = true }` and set the env var for server mode.
2. `CommandLineSwitches.cs` — add `SuperFast` to the switch enum.

### File: `src/Build/BackEnd/BuildManager/BuildParameters.cs`

Add `public bool SuperFast { get; set; }` property. Wire in copy constructor, `ITranslatable` serialization.

---

## Layer 1: Evaluation Cache (`ProjectInstanceCache`)

### New File: `src/Build/Evaluation/ProjectInstanceCache.cs`

A static in-memory cache of `ProjectInstance` objects, keyed by `(string projectFullPath, Dictionary<string,string> globalProperties)`. Each entry stores:
- The `ProjectInstance`
- The `MSBuildAllProjects` list (all imported files) with their `LastWriteTimeUtc` at evaluation time
- The global properties dictionary hash

**Invalidation:** On cache lookup, stat every file in the stored `MSBuildAllProjects` list. If any file's `LastWriteTimeUtc` differs from the stored value, invalidate the entry and return cache miss. This is the same pattern as `ProjectRootElementCache.IsInvalidEntry()` (`src/Build/Evaluation/ProjectRootElementCache.cs:167-223`).

**API:**
```csharp
internal static class ProjectInstanceCache
{
    // Returns cached ProjectInstance if still valid, null if stale/missing
    static ProjectInstance? TryGet(string projectFullPath, IDictionary<string,string> globalProperties);
    
    // Cache a freshly evaluated ProjectInstance
    static void Store(string projectFullPath, IDictionary<string,string> globalProperties, ProjectInstance instance, IList<string> allImportedFiles);
    
    // Clear entire cache
    static void Clear();
    
    // Write all cached entries to disk for cold-start recovery
    static void FlushToDisk(string cacheDirectory);
    
    // Load entries from disk
    static void WarmFromDisk(string cacheDirectory);
}
```

**Thread safety:** Use `ConcurrentDictionary<ProjectInstanceCacheKey, ProjectInstanceCacheEntry>`.

### Integration: `src/Build/BackEnd/BuildManager/BuildManager.cs`

In `ExecuteGraphBuildScheduler()` around line 2158-2191, the `ProjectGraph` constructor takes a `projectInstanceFactory` lambda. Replace the lambda to check `ProjectInstanceCache` first:

```csharp
(path, properties, collection) =>
{
    if (_buildParameters!.SuperFast)
    {
        var cached = ProjectInstanceCache.TryGet(path, properties);
        if (cached != null)
        {
            return cached;
        }
    }
    
    var instance = new ProjectInstance(path, properties, null, _buildParameters, ...);
    
    if (_buildParameters!.SuperFast)
    {
        ProjectInstanceCache.Store(path, properties, instance, 
            instance.GetPropertyValue("MSBuildAllProjects").Split(';'));
    }
    
    return instance;
}
```

### Integration: `src/Build/BackEnd/Node/OutOfProcNode.cs`

In the constructor (line ~170), alongside `s_projectRootElementCacheBase`:
```csharp
// Already exists:
if (s_projectRootElementCacheBase == null)
    s_projectRootElementCacheBase = new ProjectRootElementCache(true);

// NEW: evaluation cache survives across server builds just like XML cache
// (no explicit init needed — ProjectInstanceCache is static)
```

In `CleanupCaches()` (line ~552): do NOT clear `ProjectInstanceCache` — it should survive across builds like `ProjectRootElementCache` does.

### Integration: `src/Build/BackEnd/Node/OutOfProcServerNode.cs`

In `HandleShutdown()` (line ~249): flush `ProjectInstanceCache` to disk.
In `Run()` (line ~111): on server startup, warm from disk if available.

---

## Layer 2: Graph Cache

### New File: `src/Build/Graph/ProjectGraphCache.cs`

Cache the `ProjectGraph` object across builds in server mode.

**API:**
```csharp
internal static class ProjectGraphCache
{
    // Returns cached graph if still valid (solution file + all project files unchanged)
    static ProjectGraph? TryGet(IReadOnlyCollection<ProjectGraphEntryPoint> entryPoints);
    
    // Cache a freshly constructed graph
    static void Store(IReadOnlyCollection<ProjectGraphEntryPoint> entryPoints, ProjectGraph graph);
    
    static void Clear();
}
```

**Cache key:** Hash of entry point paths + global properties.
**Invalidation:** Store the solution file timestamp + all `ProjectGraphNode.ProjectInstance.FullPath` timestamps. On lookup, check if any changed. If yes, reconstruct (using evaluation cache from Layer 1 for speed).

### Integration: `src/Build/BackEnd/BuildManager/BuildManager.cs`

In `ExecuteGraphBuildScheduler()` around line 2155-2191:

```csharp
var projectGraph = submission.BuildRequestData.ProjectGraph;
if (projectGraph == null)
{
    // NEW: try cached graph first
    if (_buildParameters!.SuperFast)
    {
        projectGraph = ProjectGraphCache.TryGet(submission.BuildRequestData.ProjectGraphEntryPoints);
    }
    
    if (projectGraph == null)
    {
        projectGraph = new ProjectGraph(...); // existing code
        
        if (_buildParameters!.SuperFast)
        {
            ProjectGraphCache.Store(submission.BuildRequestData.ProjectGraphEntryPoints, projectGraph);
        }
    }
}
```

---

## Layer 3: Per-Node Project Skip

### File: `src/Build/BackEnd/BuildManager/BuildManager.cs`

In `BuildGraph()` method (the loop at line ~2268), before submitting each node:

```csharp
var unblockedNodes = blockedNodes
    .Where(node => node.ProjectReferences.All(projectReference => finishedNodes.Contains(projectReference)))
    .ToList();
    
foreach (var node in unblockedNodes)
{
    var targetList = targetsPerNode[node];
    if (targetList.Count == 0)
    {
        finishedNodes.Add(node);
        blockedNodes.Remove(node);
        waitHandle.Set();
        continue;
    }

    // NEW: SuperFast up-to-date check
    if (_buildParameters!.SuperFast && IsNodeUpToDate(node, finishedNodes, skippedNodes))
    {
        skippedNodes.Add(node);
        finishedNodes.Add(node);
        blockedNodes.Remove(node);
        
        // Log skip message
        LogComment(buildEventContext, MessageImportance.Normal,
            "SuperFast: Skipping project '{0}' — up-to-date", node.ProjectInstance.FullPath);
        
        // Create a synthetic BuildResult with Success and empty target results
        var skipResult = new BuildResult(/* ... */);
        resultsPerNode.Add(node, skipResult);
        
        waitHandle.Set();
        continue;
    }
    
    // existing submission code...
}
```

### New Method: `IsNodeUpToDate`

```csharp
private bool IsNodeUpToDate(ProjectGraphNode node, HashSet<ProjectGraphNode> finishedNodes, HashSet<ProjectGraphNode> skippedNodes)
{
    // If any dependency was rebuilt (not skipped), this node might need rebuilding
    foreach (var dep in node.ProjectReferences)
    {
        if (!skippedNodes.Contains(dep))
            return false; // dependency was rebuilt — we might need to rebuild too
    }
    
    var project = node.ProjectInstance;
    
    // Check DisableFastUpToDateCheck
    if (project.GetPropertyValue("DisableFastUpToDateCheck").Equals("true", StringComparison.OrdinalIgnoreCase))
        return false;
    
    // Get the primary output path
    string targetPath = project.GetPropertyValue("TargetPath");
    if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
        return false;
    
    DateTime outputTime = File.GetLastWriteTimeUtc(targetPath);
    
    // Check all source items
    foreach (string itemType in new[] { "Compile", "EmbeddedResource", "Content", "None" })
    {
        foreach (var item in project.GetItems(itemType))
        {
            string fullPath = item.GetMetadataValue("FullPath");
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
            {
                if (File.GetLastWriteTimeUtc(fullPath) > outputTime)
                    return false;
            }
        }
    }
    
    // Check all references
    foreach (var item in project.GetItems("ReferencePath"))
    {
        string fullPath = item.EvaluatedInclude;
        if (File.Exists(fullPath) && File.GetLastWriteTimeUtc(fullPath) > outputTime)
            return false;
    }
    
    // Check project file and imports
    string allProjects = project.GetPropertyValue("MSBuildAllProjects");
    if (!string.IsNullOrEmpty(allProjects))
    {
        foreach (string importPath in allProjects.Split(';'))
        {
            string trimmed = importPath.Trim();
            if (!string.IsNullOrEmpty(trimmed) && File.Exists(trimmed))
            {
                if (File.GetLastWriteTimeUtc(trimmed) > outputTime)
                    return false;
            }
        }
    }
    
    // Check NuGet assets file
    string assetsPath = Path.Combine(project.GetPropertyValue("MSBuildProjectExtensionsPath"), "project.assets.json");
    if (File.Exists(assetsPath) && File.GetLastWriteTimeUtc(assetsPath) > outputTime)
        return false;
    
    // All checks passed — project is up-to-date
    return true;
}
```

**Note on synthetic BuildResult:** For skipped nodes, we need a `BuildResult` with `OverallResult = Success` and target results for all requested targets. Since the project was already evaluated (from cache), we can populate `GetTargetPath` output from `$(TargetPath)`. For other protocol targets, we populate from the evaluated `ProjectInstance` properties.

---

## Layer 4: Terminal Logger Integration

### File: `src/Build/Logging/TerminalLogger/TerminalLogger.cs`

Add handling for the skip log message. When a project is skipped via SuperFast, show it in the terminal logger output with a distinctive indicator:

```
  MySolution.slnx
    ✓ Library1 (net9.0) — accelerated
    ✓ Library2 (net9.0) — accelerated  
    ✓ WebApp (net9.0) — accelerated
  Build succeeded in 0.1s
```

This requires either:
- A new `BuildEventArgs` subclass for SuperFast skip events, or
- Using `BuildMessageEventArgs` with a specific message code that terminal logger recognizes

**Simpler approach:** Use existing `ProjectStartedEventArgs` / `ProjectFinishedEventArgs` with a flag. Or just emit a high-importance message with a recognizable prefix like `"SuperFast:"`.

---

## Layer 5: Cold-Start Disk Persistence

### New File: `src/Build/Evaluation/ProjectInstanceCachePersistence.cs`

Serialize/deserialize cache entries to JSON files in `obj/` for cold-start recovery.

**Format:** Per-project file at `$(BaseIntermediateOutputPath)/.superfastcache`:
```json
{
  "version": 1,
  "projectPath": "/path/to/project.csproj",
  "globalPropertiesHash": "abc123",
  "allImportedFiles": {
    "path/to/file.props": "2026-04-13T10:00:00Z",
    "path/to/file.targets": "2026-04-13T10:00:00Z"
  },
  "outputFiles": {
    "bin/Debug/net9.0/MyApp.dll": "2026-04-13T10:00:00Z"
  },
  "sourceItemHashes": {
    "Compile": 12345678,
    "EmbeddedResource": 87654321
  },
  "lastSuccessfulBuildUtc": "2026-04-13T10:00:00Z"
}
```

### New Target: `_WriteSuperFastCache`

In `src/Tasks/Microsoft.Common.CurrentVersion.targets`, after `CoreBuild`:

```xml
<Target Name="_WriteSuperFastCache" 
        AfterTargets="CoreBuild" 
        Condition="'$(BuildingSuperFast)' == 'true'">
  <WriteSuperFastCache
    ProjectPath="$(MSBuildProjectFullPath)"
    IntermediateOutputPath="$(BaseIntermediateOutputPath)"
    AllProjects="$(MSBuildAllProjects)"
    TargetPath="$(TargetPath)"
    CompileItems="@(Compile)"
    ReferenceItems="@(ReferencePath)"
    AnalyzerItems="@(Analyzer)"
    EditorConfigItems="@(EditorConfigFiles)"
    AdditionalFileItems="@(AdditionalFiles)"
  />
</Target>
```

### New Task: `WriteSuperFastCache`

In `src/Tasks/WriteSuperFastCache.cs` — serializes the fingerprint to disk.

---

## Build & Test Plan

### Test Projects to Create

Under `src/Build.UnitTests/TestData/SuperFast/`:

1. **SingleProject/** — simple console app with 3 source files
2. **MultiTarget/** — library targeting `net8.0;net9.0`  
3. **ThreeLayerSolution/** — `App → Lib1 → Lib2` with solution file
4. **ComplexSolution/** — 10 projects, diamond dependencies, multi-targeting, shared project
5. **GlobHeavy/** — project with `**/*.cs` matching 1000+ generated source files
6. **CustomTargets/** — project with pre/post build events
7. **SourceGenerator/** — project with a source generator

### E2E Test Script: `eng/superfast-e2e.ps1`

```powershell
param([string]$MSBuildPath)

function Test-NoOpBuild($solutionPath, $name) {
    # First build (warm)
    & $MSBuildPath $solutionPath --superfast
    
    # Second build (should be instant)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $MSBuildPath $solutionPath --superfast
    $sw.Stop()
    
    $elapsed = $sw.ElapsedMilliseconds
    $status = if ($elapsed -lt 1000) { "PASS" } else { "FAIL" }
    Write-Host "$status: $name — no-op rebuild in ${elapsed}ms (target: <1000ms)"
    return $elapsed
}

function Test-SingleFileChange($solutionPath, $fileToTouch, $name) {
    # Warm build
    & $MSBuildPath $solutionPath --superfast
    
    # Touch one file
    (Get-Item $fileToTouch).LastWriteTimeUtc = [DateTime]::UtcNow
    
    # Rebuild — should only rebuild affected project(s)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $output = & $MSBuildPath $solutionPath --superfast 2>&1 | Out-String
    $sw.Stop()
    
    # Verify accelerated projects are mentioned
    $accelerated = ($output | Select-String "accelerated").Count
    $rebuilt = ($output | Select-String "Build succeeded").Count
    
    Write-Host "REGRESSION: $name — rebuilt in $($sw.ElapsedMilliseconds)ms, $accelerated projects accelerated"
}

# Run tests
Test-NoOpBuild "TestData/SingleProject/SingleProject.csproj" "Single project"
Test-NoOpBuild "TestData/ThreeLayerSolution/ThreeLayer.slnx" "Three layer solution"
Test-NoOpBuild "TestData/ComplexSolution/Complex.slnx" "Complex 10-project solution"

Test-SingleFileChange "TestData/ThreeLayerSolution/ThreeLayer.slnx" "TestData/ThreeLayerSolution/Lib2/Class1.cs" "Leaf library change"
Test-SingleFileChange "TestData/ThreeLayerSolution/ThreeLayer.slnx" "TestData/ThreeLayerSolution/App/Program.cs" "App layer change"
```

### Regression Test Cases

| Scenario | Expected | Verify |
|----------|----------|--------|
| No-op build (nothing changed) | All projects skipped | Output contains "accelerated" for each |
| Source file touched | Only that project + dependents rebuild | Correct subset rebuilt |
| Project file changed | That project + dependents rebuild | Correct subset |
| `Directory.Build.props` changed | All projects rebuild | None skipped |
| New file added to directory (glob) | Affected project rebuilds | Item hash changed |
| Reference assembly changed | Downstream projects rebuild | Timestamp comparison |
| `dotnet clean` then build | All projects build normally | No stale cache |
| Output DLL deleted | That project rebuilds | Output existence check |
| Environment variable change | Appropriate rebuild | Hash of key env vars |
| First build (no cache) | Normal full build | Graceful degradation |
| Non-superfast build after superfast | Works normally | No interference |

---

## File Summary

| File | Action | Lines (est.) |
|------|--------|-------------|
| `src/MSBuild/XMake.cs` | Modify — add `--superfast` switch | ~30 |
| `src/MSBuild/CommandLine/CommandLineSwitches.cs` | Modify — add switch enum | ~5 |
| `src/Build/BackEnd/BuildManager/BuildParameters.cs` | Modify — add `SuperFast` property | ~15 |
| `src/Build/Evaluation/ProjectInstanceCache.cs` | **New** — evaluation cache | ~300 |
| `src/Build/Graph/ProjectGraphCache.cs` | **New** — graph cache | ~200 |
| `src/Build/BackEnd/BuildManager/BuildManager.cs` | Modify — integrate caches + skip logic | ~150 |
| `src/Build/BackEnd/Node/OutOfProcNode.cs` | Modify — cache lifetime | ~10 |
| `src/Build/BackEnd/Node/OutOfProcServerNode.cs` | Modify — flush/warm cache | ~20 |
| `src/Build/Evaluation/ProjectInstanceCachePersistence.cs` | **New** — disk serialization | ~200 |
| `src/Tasks/WriteSuperFastCache.cs` | **New** — fingerprint writer task | ~150 |
| `src/Tasks/Microsoft.Common.CurrentVersion.targets` | Modify — add `_WriteSuperFastCache` target | ~15 |
| `src/Build/Logging/TerminalLogger/TerminalLogger.cs` | Modify — show skip status | ~30 |
| Test data projects | **New** — 7 test solutions | ~500 |
| `eng/superfast-e2e.ps1` | **New** — e2e benchmark script | ~100 |
| Unit tests | **New** — cache + skip logic tests | ~800 |
| **Total** | | **~2525** |

---

## Risks and Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Evaluation cache returns stale ProjectInstance | Critical | Validate ALL files in MSBuildAllProjects list on every cache hit |
| Graph cache topology is stale | High | Compare all project file timestamps; reconstruct on any change |
| Synthetic BuildResult for skipped nodes missing target results | High | Populate from evaluated ProjectInstance properties |
| Server mode instability / state leaks | High | --superfast implies server but is opt-in; easy to disable |
| Memory pressure from cached ProjectInstances | Medium | LRU eviction when memory exceeds threshold |
| Thread safety in static caches | Medium | ConcurrentDictionary + immutable entries |
| MSBuildAllProjects doesn't cover env vars | Medium | Hash DOTNET_ROOT, PATH, key MSBuild env vars |
