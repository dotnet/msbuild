# Wave 2 logbook - ToolLocationHelper

## Candidate under investigation

- Canonical candidate: `Microsoft.Build.Utilities.ToolLocationHelper.s_locker`
- Stage: cross-cutting framework services, with evaluation-time and build-time reach

## Shared lock shape

- `ToolLocationHelper` is a public static helper and explicitly notes that all of its public methods are available to MSBuild projects for use in functions during project evaluation: `src\Utilities\ToolLocationHelper.cs:208-210`
- The single global lock is `s_locker`: `src\Utilities\ToolLocationHelper.cs:219-223`
- That one lock guards many process-wide caches:
  - reference assembly paths: `src\Utilities\ToolLocationHelper.cs:225-227`
  - highest framework-name lookup: `src\Utilities\ToolLocationHelper.cs:230-233`
  - target platform SDK cache: `src\Utilities\ToolLocationHelper.cs:236-243`
  - display-name cache: `src\Utilities\ToolLocationHelper.cs:246-250`
  - platform reference cache: `src\Utilities\ToolLocationHelper.cs:253-258`
  - extension SDK reference cache: `src\Utilities\ToolLocationHelper.cs:260-263`
  - supported framework monikers: `src\Utilities\ToolLocationHelper.cs:266-268`
  - VS install folders: `src\Utilities\ToolLocationHelper.cs:270-273`

## Global lock coverage

- `s_locker` is used in both short critical sections and long discovery paths:
  - short cache-init / cache-store sites: `src\Utilities\ToolLocationHelper.cs:2095-2103`, `src\Utilities\ToolLocationHelper.cs:2227-2233`, `src\Utilities\ToolLocationHelper.cs:2265-2268`, `src\Utilities\ToolLocationHelper.cs:2281-2288`, `src\Utilities\ToolLocationHelper.cs:2293-2299`, `src\Utilities\ToolLocationHelper.cs:2319-2322`, `src\Utilities\ToolLocationHelper.cs:3096-3105`, `src\Utilities\ToolLocationHelper.cs:3121-3125`, `src\Utilities\ToolLocationHelper.cs:3182-3185`, `src\Utilities\ToolLocationHelper.cs:3215-3218`
  - long discovery under the lock:
    - `GetTargetPlatformReferences`: `src\Utilities\ToolLocationHelper.cs:883-909`
    - `GetPlatformOrFrameworkExtensionSdkReferences`: `src\Utilities\ToolLocationHelper.cs:944-968`
    - `GetFoldersInVSInstalls`: `src\Utilities\ToolLocationHelper.cs:1410-1433`
    - `RetrieveTargetPlatformList`: `src\Utilities\ToolLocationHelper.cs:2438-2492`
    - `GetSupportedTargetFrameworks`: `src\Utilities\ToolLocationHelper.cs:3628-3655`
    - `HighestVersionOfTargetFrameworkIdentifier`: `src\Utilities\ToolLocationHelper.cs:3661-3694`

## Most important finding

- The strongest issue is **not** that every `ToolLocationHelper` API holds the global lock for expensive work.
- The real picture is mixed:
  - **Higher-risk APIs** keep heavy discovery under `s_locker`.
  - **Reference-assembly APIs** are better than expected: they usually only lock for cache lookup/update, then do path discovery and redist parsing outside the lock.
- So the candidate remains real, but it is narrower than the initial Wave 1 broad-scan impression.

## Expensive work that happens while `s_locker` is held

### 1. SDK / platform discovery

- `GetTargetPlatformReferences` keeps the lock while calling:
  - `GetLegacyTargetPlatformReferences`: `src\Utilities\ToolLocationHelper.cs:899-905`, which can do `Directory.GetFiles(..., "*.winmd")`: `src\Utilities\ToolLocationHelper.cs:1031-1055`
  - `GetTargetPlatformReferencesFromManifest`: `src\Utilities\ToolLocationHelper.cs:904-905`, `src\Utilities\ToolLocationHelper.cs:1075-1105`
- `GetTargetPlatformReferencesFromManifest` can load `PlatformManifest`: `src\Utilities\ToolLocationHelper.cs:1091-1097`, `src\Utilities\ToolLocationHelper.cs:1173-1179`
- `PlatformManifest` reads `Platform.xml` via `XmlReader` / `XmlDocument.Load`: `src\Utilities\PlatformManifest.cs:30-35`, `src\Utilities\PlatformManifest.cs:95-106`
- `GetPlatformOrFrameworkExtensionSdkReferences` also keeps the lock while resolving matching SDKs: `src\Utilities\ToolLocationHelper.cs:944-968`
- `RetrieveTargetPlatformList` holds the same lock while:
  - enumerating SDK directories: `src\Utilities\ToolLocationHelper.cs:2464-2475`, `src\Utilities\ToolLocationHelper.cs:2579-2667`
  - scanning the registry for SDKs: `src\Utilities\ToolLocationHelper.cs:2469-2472`, `src\Utilities\ToolLocationHelper.cs:2673-2886`
  - gathering extension SDK directory content: `src\Utilities\ToolLocationHelper.cs:2478-2489`, `src\Utilities\ToolLocationHelper.cs:2498-2522`

### 2. Visual Studio setup discovery

- `GetFoldersInVSInstalls` holds `s_locker` while calling `VisualStudioLocationHelper.GetInstances()`: `src\Utilities\ToolLocationHelper.cs:1415-1433`
- `VisualStudioLocationHelper.GetInstances()` enumerates setup instances through COM setup APIs: `src\Framework\VisualStudioLocationHelper.cs:26-35`, `src\Framework\VisualStudioLocationHelper.cs:38-69`

### 3. Reference framework enumeration

- `GetSupportedTargetFrameworks` holds the lock while enumerating framework identifiers, versions, and profiles from disk: `src\Utilities\ToolLocationHelper.cs:3628-3655`
- Those helpers walk directories:
  - `GetFrameworkIdentifiers`: `src\Utilities\ToolLocationHelper.cs:3743-3806`
  - `GetFrameworkVersions`: `src\Utilities\ToolLocationHelper.cs:3812-3869`
  - `GetFrameworkProfiles`: `src\Utilities\ToolLocationHelper.cs:3879-...`
- `HighestVersionOfTargetFrameworkIdentifier` also holds the lock across `GetFrameworkVersions(...)`: `src\Utilities\ToolLocationHelper.cs:3661-3694`

## Lower-risk paths

- `GetPathToReferenceAssemblies` does a cache check under `s_locker`, then computes outside the lock, then stores back: `src\Utilities\ToolLocationHelper.cs:2217-2268`
- `GetDisplayNameForTargetFrameworkDirectory` uses the same split approach: `src\Utilities\ToolLocationHelper.cs:2278-2323`
- `ChainReferenceAssemblyPath` only uses `s_locker` for cache checks/updates; the redist XML read happens outside the lock: `src\Utilities\ToolLocationHelper.cs:3092-3220`

## Public APIs that can hit this during evaluation/build

- Evaluation/property-function reachable:
  - any public method, per class comment: `src\Utilities\ToolLocationHelper.cs:208-210`
  - concrete proof that property functions call into it during evaluation: `src\Build.UnitTests\Evaluation\Expander_Tests.cs:1993-2028`
- Particularly relevant public API families:
  - framework/reference-assembly lookup:
    - `GetPathToStandardLibraries`: `src\Utilities\ToolLocationHelper.cs:1757-1759`
    - `GetPathToReferenceAssemblies`: `src\Utilities\ToolLocationHelper.cs:1880-1945`, `src\Utilities\ToolLocationHelper.cs:2182-2217`
    - `GetSupportedTargetFrameworks`: `src\Utilities\ToolLocationHelper.cs:3628-3655`
    - `GetFoldersInVSInstallsAsString`: `src\Utilities\ToolLocationHelper.cs:1448-1470`
  - SDK/platform discovery:
    - `GetTargetPlatformReferences`: `src\Utilities\ToolLocationHelper.cs:877-909`
    - `GetPlatformOrFrameworkExtensionSdkReferences`: `src\Utilities\ToolLocationHelper.cs:944-968`
    - `GetPlatformExtensionSDKLocation`: `src\Utilities\ToolLocationHelper.cs:495-530`
    - `GetTargetPlatformSdks`: `src\Utilities\ToolLocationHelper.cs:766-777`
    - `GetPlatformSDKLocation`: `src\Utilities\ToolLocationHelper.cs:1263-1266`, `src\Utilities\ToolLocationHelper.cs:1288-1304`
    - `GetPlatformSDKDisplayName`: `src\Utilities\ToolLocationHelper.cs:1315-1318`
    - `GetPlatformsForSDK`: `src\Utilities\ToolLocationHelper.cs:1337-1343`

## Overall assessment

- Structural issue: **real**
- Best-supported contention source: public SDK/platform discovery APIs that keep `s_locker` held across directory enumeration, registry enumeration, and manifest/XML loading
- Counterweight: the probably most common framework/reference-assembly path (`GetPathToReferenceAssemblies`) does **not** hold the lock over its expensive work
- Likelihood in one-process multi-project parallel builds: **medium**
  - moves up when builds/property functions repeatedly hit platform-SDK or VS-install discovery
  - moves down when usage is mostly `GetPathToReferenceAssemblies` and those caches are already warm

## Escalation decision

- **Escalate: yes**
- Reason: one global lock fronts multiple heavy discovery families, and some of those families do meaningful filesystem / registry / XML work while holding the lock. But the report should explicitly note that not all `ToolLocationHelper` entry points are equally risky.
