# Wave 1 logbook - framework

## Scope searched

- Primary source scope: `src\Framework\**` and `src\Shared\**`.
- Additional non-task global helper inspected because it is process-wide and callable from many builds: `src\Utilities\ToolLocationHelper.cs`.
- Confirmed cross-stage usage with a few read-only call-site checks in `src\Build\**` and `src\MSBuild\**`.
- Explicitly ignored `src\Tasks\**` per scope.
- Search themes: static mutable state, singleton initialization, shared caches, `lock` usage, `Lazy<>`, registry/setup discovery, filesystem enumeration, assembly loading, and logging serialization.

## Candidate list grouped by build stage

### 1. Entry / configuration setup

#### Candidate: `BuildEnvironmentHelper.BuildEnvironmentHelperSingleton.s_instance`

- **Symbol:** `Microsoft.Build.Shared.BuildEnvironmentHelper.BuildEnvironmentHelperSingleton.s_instance`
- **Why shared:** Process-wide singleton; many evaluation, SDK-resolution, and command-line paths read `BuildEnvironmentHelper.Instance`.
- **Evidence:**
  - Singleton access and initialization: `src\Framework\BuildEnvironmentHelper.cs:39-45`, `src\Framework\BuildEnvironmentHelper.cs:71-90`, `src\Framework\BuildEnvironmentHelper.cs:497-505`
  - Expensive setup work in the initializer path: `src\Framework\BuildEnvironmentHelper.cs:283-295` (VS instance enumeration + directory existence checks)
  - Representative consumers across stages: `src\Build\Evaluation\IntrinsicFunctions.cs:700-733`, `src\Build\BackEnd\BuildManager\BuildParameters.cs:1079-1080`, `src\Build\Definition\ToolsetReader.cs:163-164`, `src\Build\BackEnd\Components\SdkResolution\DefaultSdkResolver.cs:31-34`
- **Why it may bottleneck:** First touch can block parallel callers behind type initialization while MSBuild probes environment variables, process paths, VS setup, and filesystem state. This looks like a startup-burst risk rather than a steady-state hot-path issue.
- **Likelihood:** Medium
- **Escalate to full report later:** Maybe - worth a deeper report only if startup traces show a visible first-touch stall before project fan-out.

### 2. Project load and evaluation

#### Candidate: `FileMatcher.s_cachedGlobExpansions` / `s_cachedGlobExpansionsLock`

- **Symbol:** `Microsoft.Build.Shared.FileMatcher.s_cachedGlobExpansions` and `Microsoft.Build.Shared.FileMatcher.s_cachedGlobExpansionsLock`
- **Why shared:** When `MsBuildCacheFileEnumerations` is enabled, `FileMatcher` switches to static process-wide glob caches instead of per-evaluation caches.
- **Evidence:**
  - Static global caches: `src\Framework\Utilities\FileMatcher.cs:51-57`
  - Feature gate declaring process-wide wildcard caching: `src\Framework\Traits.cs:63-65`
  - Global-cache hookup in constructor: `src\Framework\Utilities\FileMatcher.cs:105-145`
  - Same-key serialization for cache misses: `src\Framework\Utilities\FileMatcher.cs:1942-1975`
  - Evaluation and globbing consumers: `src\Build\Utilities\EngineFileUtilities.cs:64-72`, `src\Build\Evaluation\Context\EvaluationContext.cs:68-72`, `src\Build\Globbing\MSBuildGlob.cs:183-186`
- **Why it may bottleneck:** If the global enumeration cache is turned on and many projects hit the same wildcard at once, the per-key lock serializes the first miss and holds up identical evaluations behind one filesystem expansion. Repeated wildcard-heavy solutions could see bursty contention here.
- **Likelihood:** Medium-Low
- **Escalate to full report later:** Maybe - only if this escape hatch is known to be enabled in the target scenario.

### 3. Scheduling / execution coordination

#### Candidate: `CoreClrAssemblyLoader._guard`

- **Symbol:** `Microsoft.Build.Shared.CoreClrAssemblyLoader._guard`
- **Why shared:** The loader is reused as a singleton by higher-level components that are shared across builds in one process.
- **Evidence:**
  - Shared mutable state under one lock: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:24-29`
  - Locked load/resolve paths: `src\Framework\Loader\CoreCLRAssemblyLoader.cs:40-43`, `src\Framework\Loader\CoreCLRAssemblyLoader.cs:72-110`, `src\Framework\Loader\CoreCLRAssemblyLoader.cs:128-145`, `src\Framework\Loader\CoreCLRAssemblyLoader.cs:148-177`, `src\Framework\Loader\CoreCLRAssemblyLoader.cs:187-194`
  - Shared loader instances in non-task subsystems: `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:19-23`, `src\Build\BackEnd\Components\SdkResolution\SdkResolverLoader.cs:45-64`, `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:464-465`
- **Why it may bottleneck:** The same lock protects dependency-path registration, assembly caches, resolver hookup, and resolution. Some of that work includes filesystem probes and `AssemblyLoadContext` operations, so concurrent first-time loads of SDK resolvers or project-cache plugins can serialize.
- **Likelihood:** Medium
- **Escalate to full report later:** Yes - good candidate for a focused report if Wave 2 wants one execution-side framework helper to validate.

### 4. Logging / event forwarding

#### Candidate: `LogMessagePacketBase.s_writeMethodCache`

- **Symbol:** `Microsoft.Build.Shared.LogMessagePacketBase.s_writeMethodCache`
- **Why shared:** Static cache used by all log packet serialization in-process.
- **Evidence:**
  - Shared cache declaration: `src\Shared\LogMessagePacketBase.cs:263-270`
  - Every write takes the same lock and may reflect `WriteToStream`: `src\Shared\LogMessagePacketBase.cs:384-418`
  - Active packet type inherits this base on the logging path: `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:25-26`, `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:57-69`
- **Why it may bottleneck:** Even after warmup, every event serialization still locks the same dictionary for lookup. In a highly parallel build with heavy event fan-in, this is a plausible serialization point on the logging path.
- **Likelihood:** Medium
- **Escalate to full report later:** Yes - this is one of the cleaner shared-lock candidates in the framework/shared wave.

### 5. Cross-cutting framework services

#### Candidate: `ToolLocationHelper.s_locker`

- **Symbol:** `Microsoft.Build.Utilities.ToolLocationHelper.s_locker`
- **Why shared:** One global lock protects a large set of process-wide static caches in a public static helper. The file explicitly says its public methods must be safe during project evaluation.
- **Evidence:**
  - Public evaluation-facing role: `src\Utilities\ToolLocationHelper.cs:208-210`
  - Shared cache fields guarded by one lock: `src\Utilities\ToolLocationHelper.cs:217-273`
  - Expensive work performed while holding the global lock:
    - target-platform reference discovery: `src\Utilities\ToolLocationHelper.cs:885-909`
    - extension SDK reference discovery: `src\Utilities\ToolLocationHelper.cs:954-970`
    - VS instance enumeration under the lock: `src\Utilities\ToolLocationHelper.cs:1415-1432`
    - supported-framework enumeration under the lock: `src\Utilities\ToolLocationHelper.cs:3628-3655`
  - Additional shared cache access on reference-assembly paths: `src\Utilities\ToolLocationHelper.cs:2217-2268`, `src\Utilities\ToolLocationHelper.cs:3092-3218`
- **Why it may bottleneck:** This is the strongest framework/helper candidate from the scan. One lock fronts many unrelated caches, and several locked regions do real discovery work (filesystem, registry/setup, manifest/XML, framework enumeration). Parallel evaluation/property-function usage or repeated resolver/toolset discovery can pile onto the same lock.
- **Likelihood:** High
- **Escalate to full report later:** Yes - strongest escalation candidate from this wave.

## Weaker candidates / likely non-bottlenecks

- **`EscapingUtilities.s_escapedStringCache`** (`src\Framework\EscapingUtilities.cs:27-47`, `src\Framework\EscapingUtilities.cs:182-189`, `src\Framework\EscapingUtilities.cs:203-223`)  
  Real shared lock, but only used when callers opt into `cache: true`; the hot evaluation call sites mostly use uncached escaping. This looks too narrow for Wave 2 unless evidence appears that cached escaping is dominating task/item churn.

- **`FileUtilities.FileExistenceCache`** (`src\Framework\FileUtilities.cs:141-142`, `src\Framework\FileUtilities.cs:1244-1294`, `src\Framework\Traits.cs:55-65`)  
  Clearly process-wide and widely reachable, but it uses `ConcurrentDictionary` and is guarded by an opt-in trait. Plausible only in specialized environments that enable process-wide existence caching.

- **`TelemetryManager.s_lock`** (`src\Framework\Telemetry\TelemetryManager.cs:28-31`, `src\Framework\Telemetry\TelemetryManager.cs:53-70`, `src\Framework\Telemetry\TelemetryManager.cs:111-134`, `src\Build\BackEnd\BuildManager\BuildManager.cs:1219-1224`, `src\MSBuild\XMake.cs:301-304`)  
  Shared singleton with a lock, but the visible operations are build-start/build-end initialization and disposal, not per-project hot-path work. Looks like a one-time build-lifecycle cost, not a sustained parallel-build bottleneck.

- **`NativeMethods.MaxPathLock` / `SystemInformationLock`** (`src\Framework\NativeMethods.cs:262-293`, `src\Framework\NativeMethods.cs:643-664`)  
  Both are classic lazy-init locks, but they only protect one-time computation. Low steady-state risk.

- **`CommunicationsUtilities.s_traceLock`** (`src\Framework\BackEnd\CommunicationsUtilities.cs:50-55`, `src\Framework\BackEnd\CommunicationsUtilities.cs:744-754`, `src\Framework\Traits.cs:31-33`)  
  Fully serializes trace writes, but only when debug node communication tracing is enabled. Useful to note, but too debug-only to prioritize.

- **`Traits.Instance`** (`src\Framework\Traits.cs:13-25`, `src\Framework\Traits.cs:31-77`)  
  Read almost everywhere, but effectively immutable after construction and not lock-heavy. Important shared state, but not a likely throughput bottleneck by itself.
