# CoreClrAssemblyLoader

## Why shared

`CoreClrAssemblyLoader` owns shared mutable assembly-load state (`_pathsToAssemblies`, `_namesToAssemblies`, `_dependencyPaths`) protected by `_guard` (`src\Framework\Loader\CoreCLRAssemblyLoader.cs:24-29`). In practice, it is used through static subsystem-level singletons:

- SDK resolvers: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:21-23`
- project cache plugins: `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:464-466`
- build checks: `src\Build\BuildCheck\Acquisition\BuildCheckAcquisitionModule.cs:18-23`
- shared `TypeLoader`: `src\Shared\TypeLoader.cs:26-31`

Important nuance: this is **not one global lock for all assembly loading in the process**. Each singleton loader instance has its own `_guard`.

## Why it might bottleneck

The loader uses one coarse lock per instance, and that lock covers more than dictionary updates. Depending on the path, it also covers:

- handler hookup
- `MSBuildLoadContext` creation
- `AssemblyLoadContext.LoadFromAssemblyPath(...)`
- dependency-path search loops
- file existence checks
- `AssemblyLoadContext.GetAssemblyName(...)`

Evidence: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:70-110`, `src\Framework\Loader\CoreCLRAssemblyLoader.cs:128-181`.

So if many threads use the same subsystem loader concurrently, assembly loading is effectively serialized for that subsystem.

## Evidence

- instance-scoped guard and protected state: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:24-29`
- lock on dependency registration: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:33-43`
- lock on legacy default-context load path: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:70-87`
- lock on plugin-context load path: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:90-110`
- lock on resolving callback: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:128-145`
- dependency probing under the lock: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:148-181`
- `MSBuildLoadContext` constructor performs file checks and optional `AssemblyDependencyResolver` creation: `src\Framework\Loader\MSBuildLoadContext.cs:34-47`
- subsystem singleton usage:
  - SDK resolvers: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:21-23`, `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:231-268`
  - project cache plugins: `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:427-448`, `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:464-466`
  - build checks: `src\Build\BuildCheck\Acquisition\BuildCheckAcquisitionModule.cs:34-43`
  - `TypeLoader`: `src\Shared\TypeLoader.cs:232-237`
- SDK resolver loading is additionally cached process-wide through `CachingSdkResolverLoader.Instance`: `src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:61-67`, `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs:75-96`
- `TypeLoader` also caches loaded-type results by filter and assembly info: `src\Shared\TypeLoader.cs:56-61`, `src\Shared\TypeLoader.cs:338-348`

## Likelihood

**Medium-low**.

This is a credible contention mechanism, but the likely build impact is limited:

- contention is isolated per subsystem loader instance
- several major call sites are cold-start or initialization-style
- some callers add their own caches above the loader (`CachingSdkResolverLoader`, `TypeLoader`)

The strongest remaining concern is the `TypeLoader` path, because it is broader than the resolver/plugin-specific loaders.

## Expected contention mode

- **startup/plugin initialization serialization** inside one subsystem loader instance
- especially when multiple threads try to load different assemblies through the same static loader at roughly the same time

This looks more like a bursty lock during plugin/task/logger discovery than a steady-state per-project bottleneck.

## Where it is used

- SDK resolver assembly loading: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:296-328`
- project cache plugin assembly loading: `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:226-235`, `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:427-448`
- custom build-check assembly loading: `src\Build\BuildCheck\Acquisition\BuildCheckAcquisitionModule.cs:34-43`
- shared `TypeLoader` path for higher-level extensibility points such as loggers and task/task-factory related loading:
  - `src\Shared\TypeLoader.cs:232-237`
  - example logger usage: `src\Build\Logging\LoggerDescription.cs:197-218`

## Why it may or may not matter in practice

### Why it may matter

- `_guard` covers real assembly load work, not just cache bookkeeping.
- The legacy resolution path performs filesystem probing and assembly-name inspection under the lock.
- `TypeLoader` shares one static loader across many extensibility-oriented loads, so parallel plugin/task/logger discovery could pile onto one guard.

### Why it may not matter much

- subsystem separation means SDK resolver loads do not block project-cache loads, etc.
- SDK resolver loading is explicitly cached and described as process-stable.
- project cache plugin and build-check assembly loads are usually one-time setup events.
- much of the post-load reflection/type scanning happens outside the guard.

Overall judgment: this is a **real synchronization choke point**, but it is more likely to be a secondary startup cost than a top-tier parallel-build bottleneck.

## How to validate

1. Instrument `CoreClrAssemblyLoader` to record:
   - time waiting for `_guard`
   - time spent inside `LoadUsingPluginContext`
   - time spent inside `LoadUsingLegacyDefaultContext`
   - time spent in `TryResolveAssemblyFromPaths`
   - caller/subsystem identity
2. Run a one-process build that exercises:
   - SDK resolution
   - project cache plugin initialization
   - custom logger/task/task-factory loading
3. Distinguish:
   - lock wait on the same loader instance
   - total number of unique assemblies loaded through each subsystem loader
4. If possible, compare:
   - default behavior
   - an experiment that narrows the lock to cache mutation while leaving actual load work outside it

This candidate should move up only if traces show meaningful aggregate wait time on one subsystem loader during realistic parallel builds.
