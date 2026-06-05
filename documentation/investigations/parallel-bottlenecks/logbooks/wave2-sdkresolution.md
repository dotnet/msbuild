# Wave 2 logbook - SDK resolution funnel

## Scope and files re-read

- `documentation\investigations\parallel-bottlenecks\plan.md`
- `documentation\specs\threading.md`
- `documentation\specs\sdk-resolvers-algorithm.md`
- `documentation\specs\event-source.md`
- `documentation\High-level-overview.md`
- `src\Build\BackEnd\BuildManager\BuildManager.cs`
- `src\Build\BackEnd\Shared\BuildRequestConfiguration.cs`
- `src\Build\Evaluation\Evaluator.cs`
- `src\Build\Evaluation\Context\EvaluationContext.cs`
- `src\Build\Construction\Solution\SolutionProjectGenerator.cs`
- `src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs`
- `src\Build\BackEnd\Components\SdkResolution\HostedSdkResolverServiceBase.cs`
- `src\Build\BackEnd\Components\SdkResolution\MainNodeSdkResolverService.cs`
- `src\Build\BackEnd\Components\SdkResolution\OutOfProcNodeSdkResolverService.cs`
- `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverService.cs`
- `src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs`
- `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs`
- `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs`
- `src\Shared\NodePacketFactory.cs`
- `src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs`
- `src\Build\BackEnd\Components\Communications\NodeProviderOutOfProcBase.cs`
- `src\Build\BackEnd\Node\OutOfProcNode.cs`

## Evidence log

### 1. The build-hosted SDK resolver really is shared in build execution

- `BuildComponentFactoryCollection` registers `BuildComponentType.SdkResolverService` as a singleton implemented by `MainNodeSdkResolverService` (`src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs:89-90`).
- `BuildRequestConfiguration` retrieves the hosted resolver service from the component host when creating a `ProjectInstance` for build evaluation (`src\Build\BackEnd\Shared\BuildRequestConfiguration.cs:492-493`).
- The evaluator calls `_sdkResolverService.ResolveSdk(...)` during SDK import evaluation (`src\Build\Evaluation\Evaluator.cs:1808-1818`).
- `BuildManager` also passes the hosted service into solution loading and project-instance creation (`src\Build\BackEnd\BuildManager\BuildManager.cs:1736-1746,2176-2188`).

Working conclusion: for build-managed evaluations in one process, SDK resolution is not per-project local state; it goes through a shared build-hosted resolver service.

### 2. The strongest “main-node funnel” is real for out-of-proc nodes, but that is not the main one-process story

- `BuildManager` registers `ResolveSdkRequest` packets to be handled by `SdkResolverService` (`src\Build\BackEnd\BuildManager\BuildManager.cs:669-669`).
- `MainNodeSdkResolverService.PacketReceived` handles the request and synchronously calls `ResolveSdk` before sending a response (`src\Build\BackEnd\Components\SdkResolution\MainNodeSdkResolverService.cs:58-95`).
- Packet routing is direct: `NodePacketFactory.DeserializeAndRoutePacket` deserializes and immediately invokes `_handler.PacketReceived(...)` on the receiving thread (`src\Shared\NodePacketFactory.cs:49-55,70-75,103-111`).
- Out-of-proc worker nodes use `OutOfProcNodeSdkResolverService`, which caches by SDK name but otherwise sends a request packet and blocks in `WaitHandle.WaitAny([_responseReceivedEvent, ShutdownEvent])` until the main node responds (`src\Build\BackEnd\Components\SdkResolution\OutOfProcNodeSdkResolverService.cs:67-98,123-145`).

But for **one-process** multi-project builds:

- in-proc nodes do not use packet routing for SDK resolution; `NodeProviderInProc` does not use packet-handler registration (`src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:263-284`)
- in-proc project evaluation reaches the hosted resolver directly through the component host (`BuildRequestConfiguration.cs:492-493`)

Refined conclusion: the literal packet funnel is mainly a multi-process concern. In one-process builds, the real shared waits are inside the singleton service and its caches/loader, not in a request-response queue.

### 3. The per-submission cache deliberately creates a single-flight wait per SDK name

- `MainNodeSdkResolverService` wraps a `CachingSdkResolverService` instance (`src\Build\BackEnd\Components\SdkResolution\MainNodeSdkResolverService.cs:27-30`).
- `CachingSdkResolverService` stores `_cache` as `ConcurrentDictionary<int, ConcurrentDictionary<string, Lazy<SdkResult>>>`, i.e. submission id -> SDK name -> lazy result (`src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverService.cs:20-23`).
- `ResolveSdk` uses `_cache.GetOrAdd(submissionId, ...)`, then `cached.GetOrAdd(sdk.Name, key => new Lazy<SdkResult>(...))`, and all callers read `resultLazy.Value` (`CachingSdkResolverService.cs:52-72`).
- The comment is explicit: multiple projects resolving the same SDK within one submission get the same `Lazy<SdkResult>`, so the SDK is resolved only once (`CachingSdkResolverService.cs:57-60`).
- `BuildManager` clears that cache when a submission completes (`src\Build\BackEnd\BuildManager\BuildManager.cs:2985-2993`).

This is a real shared wait point for one-process multi-project builds: if many projects in the same submission all need `Microsoft.NET.Sdk`, followers block behind the first resolver invocation for that SDK name.

### 4. First-use resolver discovery is serialized under `SdkResolverService._lockObject`

- `SdkResolverService` has `_lockObject` plus mutable resolver registries and manifest-to-resolvers map (`src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:34-46`).
- On the first path through `ResolveSdkUsingResolversWithPatternsFirst`, if manifest registries are still null, `RegisterResolversManifests` is called (`SdkResolverService.cs:173-178`).
- `RegisterResolversManifests` takes `lock (_lockObject)` and, while holding it:
  - calls `GetResolverManifests(location)` (`SdkResolverService.cs:479-489`)
  - initializes `_manifestToResolvers` (`SdkResolverService.cs:488-490`)
  - partitions manifests into specific/general lists (`SdkResolverService.cs:505-535`)
- `SdkResolverLoader.GetResolversManifests` can enumerate `SdkResolvers` directories and read resolver manifests from disk (`src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:74-88,104-135,175-205`).

So the first concurrent wave of SDK requests can be forced through one lock while filesystem enumeration and XML manifest loading happen.

### 5. Resolver assembly loading is also serialized by the same service lock on first use

- `SdkResolverService.GetResolvers` loops manifests and takes `lock (_lockObject)` around `_manifestToResolvers.TryGetValue(...)` and, on miss, `_sdkResolverLoader.LoadResolversFromManifest(...)` plus `_manifestToResolvers[manifest] = newResolvers` (`src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:274-295`).
- `SdkResolverLoader.LoadResolversFromManifest` calls `LoadResolvers`, which loads the resolver assembly and instantiates exported resolver types (`src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:281-314`).
- `LoadResolverAssembly` may do assembly load / load-from work for the resolver DLL (`SdkResolverLoader.cs:231-268`).

This is the other important startup burst: different SDK names do not necessarily wait on the same `Lazy<SdkResult>`, but they can still queue behind `_lockObject` while manifests and resolver assemblies are first materialized.

### 6. Loader singletons exist, but they mostly reduce repeated work instead of creating the main steady-state bottleneck

- `SdkResolverService` defaults `_sdkResolverLoader` to `CachingSdkResolverLoader.Instance` when the 17.10 changewave is enabled (`src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:61-67`).
- `CachingSdkResolverLoader` is a static singleton (`src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs:43-50`).
- It keeps cached default resolvers, cached manifests, and cached manifest->resolver lists, with a small lock protecting `_allResolvers` and `_resolversManifests` (`CachingSdkResolverLoader.cs:18-40,71-96`).
- `EvaluationContext` creates a new `CachingSdkResolverService` by default for non-hosted evaluations (`src\Build\Evaluation\Context\EvaluationContext.cs:63-69`), so not every evaluation shares the same service instance, but they still share the loader singleton underneath via `SdkResolverService`.

Refined conclusion: the loader singleton is shared process-wide, but after warmup it mostly memoizes expensive discovery. The stronger one-process contention story remains service-level first-use locking plus same-SDK `Lazy` waits inside the build-hosted resolver.

### 7. Steady-state cost is usually outside the global lock and often outside the shared path entirely

- Once manifests and resolvers are loaded, most later calls skip `RegisterResolversManifests`, and `GetResolvers` only does short dictionary lookups under the lock when the manifest is already populated (`SdkResolverService.cs:274-295,479-537`).
- `TryResolveSdkUsingSpecifiedResolvers` calls each resolver without holding `_lockObject`; the expensive resolver-specific work happens after resolver list construction (`SdkResolverService.cs:316-399`).
- The high-level docs explicitly note SDK resolvers may run arbitrary code, including network or package-download work (`documentation\High-level-overview.md:128-129`), which means the first-project latency can be high even if the central lock is not held for the whole resolution.
- `MainNodeSdkResolverService` even comments that requests for other SDKs can continue while one new SDK is being resolved (`src\Build\BackEnd\Components\SdkResolution\MainNodeSdkResolverService.cs:78-79`).

This pushes the candidate away from “steady-state central lock convoy” and toward “startup burst and same-SDK fan-in.”

### 8. The most realistic one-process contention story

Most realistic:

1. parallel project evaluations all request the same common SDK early in a submission
2. one thread runs the first `Lazy<SdkResult>` factory
3. the rest wait for `resultLazy.Value`
4. if the process is still cold, the first request may also pay manifest enumeration and resolver assembly load under `_lockObject`

Less realistic / weaker:

- sustained cross-project serialization on every SDK lookup after warm caches
- the out-of-proc packet funnel dominating one-process builds

## Final assessment for the report

- **Why shared:** clear yes for build-managed evaluations; the hosted resolver service is a singleton build component
- **Why it might bottleneck:** same-submission same-SDK single-flight waits and first-use manifest/assembly load under one service lock
- **Best realistic contention mode:** startup burst / fan-in on a small number of popular SDK names, amplified by cold resolver discovery
- **Likelihood:** medium
- **Escalation status:** yes, but as a bursty startup-side limiter, not a strong steady-state throughput convoy

## Weaker claims discarded during the deep dive

- “All one-process SDK resolution is serialized through a main-node packet queue” — rejected; in-proc build evaluations call the hosted singleton directly rather than sending packets.
- “`CachingSdkResolverLoader.Instance` is itself the main bottleneck” — weakened; it mostly saves repeated work after warmup.
- “Every SDK resolution stays under a global lock for the whole resolver execution” — rejected; resolver invocation happens after resolver-list construction and outside `_lockObject`.
