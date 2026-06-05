# Wave 2 logbook - CoreClrAssemblyLoader

## Candidate

- Canonical object: `Microsoft.Build.Shared.CoreClrAssemblyLoader._guard`
- Stage: cross-cutting framework services
- Investigated question: whether the loader lock can become a meaningful parallel-build bottleneck in one-process multi-project builds

## Shared-instance map

The important finding is that there is **not one process-global `CoreClrAssemblyLoader`**. The lock is instance-scoped, and each subsystem owns its own static loader singleton:

- SDK resolvers: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:21-23`
- project cache plugins: `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:464-466`
- build checks: `src\Build\BuildCheck\Acquisition\BuildCheckAcquisitionModule.cs:18-23`
- shared type loading (tasks/loggers/task factories and similar callers through `TypeLoader`): `src\Shared\TypeLoader.cs:26-31`

This immediately lowers the blast radius: contention in one subsystem does not directly serialize loads in the others.

## Lock coverage findings

`CoreClrAssemblyLoader` holds `_guard` around all of its mutable shared state:

- dependency-path mutation: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:33-43`
- legacy default-context load path: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:70-87`
- plugin-context load path: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:90-110`
- resolving callback path: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:128-145`

Protected state under that lock:

- `_pathsToAssemblies`: loaded-assembly cache by normalized path (`src\Framework\Loader\CoreCLRAssemblyLoader.cs:24`)
- `_namesToAssemblies`: loaded-assembly cache by full name (`src\Framework\Loader\CoreCLRAssemblyLoader.cs:25`)
- `_dependencyPaths`: shared resolver search roots (`src\Framework\Loader\CoreCLRAssemblyLoader.cs:26`)
- `_resolvingHandlerHookedUp`: one-time handler registration flag (`src\Framework\Loader\CoreCLRAssemblyLoader.cs:29`)

Conclusion: the lock is coarse for this object; cache lookup, context creation, assembly loading, and some dependency resolution all occur while it is held.

## Expensive work under the lock

### Plugin-context path

When `UseSingleLoadContext` is false, `LoadUsingPluginContext(...)` holds `_guard` while it:

- checks `_pathsToAssemblies`
- constructs a new `MSBuildLoadContext(fullPath)`
- calls `contextForAssemblyPath.LoadFromAssemblyPath(fullPath)`

Evidence: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:90-110`.

The `MSBuildLoadContext` constructor may:

- check for the target assembly and matching `.deps.json`
- create `AssemblyDependencyResolver`

Evidence: `src\Framework\Loader\MSBuildLoadContext.cs:34-47`.

So the guard covers filesystem checks, `AssemblyDependencyResolver` setup, PE/assembly loading, and any synchronous dependency resolution triggered by that load.

### Legacy default-context path

When `UseSingleLoadContext` is true, `LoadUsingLegacyDefaultContext(...)` holds `_guard` while it:

- hooks `AssemblyLoadContext.Default.Resolving`
- checks the path cache
- loads and caches the assembly in the default context

Evidence: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:70-87`, `src\Framework\Loader\CoreCLRAssemblyLoader.cs:187-195`.

The resolving callback itself also runs under `_guard` and can do:

- well-known-assembly handling
- `_namesToAssemblies` lookup
- dependency-path search loops
- `FileExists` checks
- `AssemblyLoadContext.GetAssemblyName(candidatePath)`
- version comparisons
- nested `LoadAndCache(...)`

Evidence: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:128-181`.

Conclusion: the legacy path puts more filesystem probing and assembly metadata inspection under the lock than the plugin-context path.

## Shared subsystems and likely frequency

### SDK resolvers

- `SdkResolverLoader` uses a static loader to load resolver assemblies: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:231-268`
- resolver discovery/instantiation loops over candidate resolvers: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:296-328`
- however, `SdkResolverService` defaults to `CachingSdkResolverLoader.Instance`, and the comments say the resolver set is expected to stay fixed for the process lifetime: `src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:61-67`, `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs:43-50`, `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs:75-96`

Assessment: real shared lock, but usually a cold-start/once-per-process or once-per-loader event rather than a repeated hot path.

### Project cache plugins

- `ProjectCacheService` uses a static loader when loading the plugin assembly: `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:427-448`, `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:464-466`
- this happens while creating the plugin instance, not on every cache request: `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:226-235`

Assessment: likely one plugin assembly (or a very small set), so low-frequency and not a strong parallel-build bottleneck by itself.

### Build checks

- `BuildCheckAcquisitionModule` uses a static loader for custom check assemblies: `src\Build\BuildCheck\Acquisition\BuildCheckAcquisitionModule.cs:18-23`, `src\Build\BuildCheck\Acquisition\BuildCheckAcquisitionModule.cs:34-43`
- the load is followed by `GetExportedTypes()` and check-type filtering, but those reflection scans happen outside the loader lock

Assessment: opt-in feature, potentially repeated for many custom assemblies, but not a default high-throughput path.

### Shared TypeLoader path

- `TypeLoader` uses a static `CoreClrAssemblyLoader`: `src\Shared\TypeLoader.cs:26-31`
- before loading, it adds the assembly directory as a dependency root, then loads from path: `src\Shared\TypeLoader.cs:232-237`
- `TypeLoader` also has process-wide caches keyed by filter and `AssemblyLoadInfo`: `src\Shared\TypeLoader.cs:56-61`, `src\Shared\TypeLoader.cs:338-348`
- callers include logger loading and task/task-factory related loading paths, for example logger creation: `src\Build\Logging\LoggerDescription.cs:197-218`

Assessment: this is the broadest subsystem using the loader, but its own caches reduce repeated assembly-load calls. It is the path most likely to surface real contention if many distinct plugin/task/logger assemblies are loaded concurrently.

## Why the candidate moves up or down

Evidence moving it **up**:

- `_guard` is coarse and covers real assembly-loading work, not just bookkeeping (`src\Framework\Loader\CoreCLRAssemblyLoader.cs:70-110`, `src\Framework\Loader\CoreCLRAssemblyLoader.cs:128-181`)
- the legacy default-context path performs filesystem probes and assembly-name inspection under the lock (`src\Framework\Loader\CoreCLRAssemblyLoader.cs:148-177`)
- `TypeLoader` shares one static loader across many higher-level type-loading callers (`src\Shared\TypeLoader.cs:26-31`)

Evidence moving it **down**:

- each subsystem has its own static loader instance rather than sharing one cross-process loader
- SDK resolver loading is explicitly cached at the loader/service level (`src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs:75-96`)
- project cache and build-check loads are initialization-style events, not per-project steady-state work
- reflection/type scanning after assembly load mostly happens outside `_guard`

## Final conclusion

`CoreClrAssemblyLoader._guard` is a **plausible but narrower** contention candidate than `ProjectRootElementCache`.

- The lock definitely covers expensive work, so contention would be real when many threads try to load assemblies through the same subsystem loader at the same time.
- But the candidate is weakened by subsystem isolation and by the fact that the main known callers are mostly cold-start/plugin initialization paths.

Current decision:

- **Likelihood**: medium-low
- **Most likely contention mode**: startup/plugin-initialization serialization within a single subsystem, especially if many distinct assemblies are loaded concurrently through `TypeLoader`
- **Escalation**: report justified, but this currently looks secondary to evaluation/import-cache candidates
