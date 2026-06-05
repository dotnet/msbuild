# SDK resolution funnel

## Why shared

`BuildComponentFactoryCollection` registers `BuildComponentType.SdkResolverService` as a singleton implemented by `MainNodeSdkResolverService` (`src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs:89-90`). Build-managed evaluation then retrieves that hosted service from the component host when creating `ProjectInstance`s (`src\Build\BackEnd\Shared\BuildRequestConfiguration.cs:492-493`), and `Evaluator` calls `_sdkResolverService.ResolveSdk(...)` during SDK import evaluation (`src\Build\Evaluation\Evaluator.cs:1808-1818`). `BuildManager` also passes the same hosted service into solution loading and project-instance creation (`src\Build\BackEnd\BuildManager\BuildManager.cs:1736-1746,2176-2188`).

For one-process multi-project builds, that means SDK resolution is routed through shared build-hosted state rather than each project owning an isolated resolver instance.

## Why it might bottleneck

There are two real shared wait points:

- `CachingSdkResolverService` keeps a per-submission cache keyed by SDK name, with `Lazy<SdkResult>` values (`src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverService.cs:20-23,52-72`). Multiple projects resolving the same SDK in the same submission block behind the same lazy computation.
- `SdkResolverService` uses `_lockObject` to protect first-time resolver manifest registration and manifest-to-resolver population (`src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:34-46,173-178,274-295,479-537`). That can serialize filesystem enumeration, manifest parsing, and resolver assembly loading during cold startup.

So the candidate is less about a permanent central queue and more about cold-start fan-in on a small number of common SDK names plus first-use resolver discovery.

## Evidence

- Shared hosted service:
  - singleton registration: `src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs:89-90`
  - build evaluation retrieves hosted resolver: `src\Build\BackEnd\Shared\BuildRequestConfiguration.cs:492-493`
  - evaluator call site: `src\Build\Evaluation\Evaluator.cs:1808-1818`
  - build-manager solution / project-instance paths: `src\Build\BackEnd\BuildManager\BuildManager.cs:1736-1746,2176-2188`
- Per-submission single-flight cache:
  - cache shape and `Lazy<SdkResult>` lookup: `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverService.cs:20-23,52-72`
  - per-submission cleanup on completion: `src\Build\BackEnd\BuildManager\BuildManager.cs:2985-2993`
- Service lock coverage:
  - `_lockObject` and mutable resolver registries: `src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:34-46`
  - first manifest registration under lock: `SdkResolverService.cs:173-178,479-537`
  - resolver loading under lock in `GetResolvers`: `SdkResolverService.cs:274-295`
- Expensive work behind those paths:
  - manifest discovery / directory enumeration / XML manifest load: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:74-88,104-135,175-205`
  - resolver assembly load / instantiation: `SdkResolverLoader.cs:231-268,281-314`
- Important limiting nuance:
  - resolver invocation itself runs after resolver-list construction, outside `_lockObject`: `src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:316-399`
  - `MainNodeSdkResolverService` explicitly notes other queued threads for different SDKs can continue while a new SDK is resolved: `src\Build\BackEnd\Components\SdkResolution\MainNodeSdkResolverService.cs:78-79`

## Likelihood

Medium.

This looks like a real startup-side coordination risk, especially in cold one-process builds evaluating many SDK-style projects at once. It does not look as strong as the BuildManager / scheduler control plane, and it does not look like a steady-state global-lock bottleneck once caches are warm.

## Expected contention mode

The most realistic contention mode is:

1. many projects in the same submission request the same SDK name early
2. one thread performs the first resolution
3. peer threads wait on the same `Lazy<SdkResult>`
4. if the process is cold, the first request also pays serialized manifest discovery and resolver assembly load

That is a startup burst / fan-in pattern, not a long-running convoy on every later lookup.

## Where it is used

- Build-managed `ProjectInstance` creation uses the hosted resolver service (`src\Build\BackEnd\Shared\BuildRequestConfiguration.cs:492-493`)
- `Evaluator` resolves SDK imports through that service (`src\Build\Evaluation\Evaluator.cs:1808-1818`)
- `BuildManager` passes the hosted service into solution evaluation / metaproject generation (`src\Build\BackEnd\BuildManager\BuildManager.cs:1736-1746,2176-2188`)
- Out-of-proc nodes proxy SDK resolution back to the main node (`src\Build\BackEnd\BuildManager\BuildManager.cs:669-669`; `src\Build\BackEnd\Components\SdkResolution\OutOfProcNodeSdkResolverService.cs:67-98,123-145`), though that is mainly a multi-process amplification path rather than the core one-process case
- Outside the hosted build path, `EvaluationContext` creates a new `CachingSdkResolverService` by default (`src\Build\Evaluation\Context\EvaluationContext.cs:63-69`), and non-build solution parsing can fall back to `SdkResolverService.Instance` (`src\Build\Construction\Solution\SolutionProjectGenerator.cs:186-187`)

## Why it may or may not matter in practice

Reasons it may matter:

- most SDK-style projects ask for the same few SDK names, so same-name fan-in is plausible
- cold startup can combine cache misses, directory enumeration, manifest parsing, and resolver assembly loading
- SDK resolvers may perform arbitrary expensive work, including network/package acquisition (`documentation\High-level-overview.md:128-129`)

Reasons it may not dominate:

- one-process in-proc builds do **not** need an out-of-proc packet roundtrip for each SDK lookup
- the global service lock is mainly around resolver discovery/materialization, not the full resolver execution path
- later hits in the same submission are served from the per-submission cache
- `CachingSdkResolverLoader.Instance` memoizes manifests and loaded resolvers across service instances (`src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:61-67`; `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs:43-50,71-96`)

## How to validate

- Measure the SDK-resolution event-source spans already documented in `documentation\specs\event-source.md`:
  - `CachedSdkResolverServiceResolveSdk`
  - `SdkResolverServiceFindResolversManifests`
  - `SdkResolverServiceLoadResolvers`
  - `SdkResolverResolveSdk`
- Add counters/timing around:
  - `CachingSdkResolverService` cache hits vs misses
  - time waiting for `Lazy<SdkResult>.Value` on cache misses
  - time spent inside `SdkResolverService.RegisterResolversManifests`
  - time spent inside the `GetResolvers` locked section
- Compare:
  - cold vs warm process
  - single-project vs many-project same-submission builds
  - one-process in-proc-heavy builds vs multi-proc builds
  - common repeated SDK name (`Microsoft.NET.Sdk`) vs diverse SDK-name mixes
- If most time concentrates in first-hit `Lazy` waits and early manifest/loading spans, this candidate is confirmed as a startup-side funnel. If those waits vanish after warmup and total build time does not move, the candidate should be downgraded.
