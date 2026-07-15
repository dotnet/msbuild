# MSBuild object model - Native AOT validation harness

A standalone **MSTest + Microsoft.Testing.Platform (MTP)** test project that runs **Native
AOT-published** and validates that the MSBuild object-model scenarios the .NET SDK CLI relies on
in-process actually work under AOT.

It is driven by [documentation/aot/sdk-msbuild-object-model-audit.md](../../documentation/aot/sdk-msbuild-object-model-audit.md),
which maps every in-process object-model usage in `dotnet/sdk` to the CLI command that exposes it.
This harness exercises the **evaluation** and **construction** tiers from that audit - the surface
that should be trim/AOT-safe (`dotnet new`/`run`/`test` discovery, `publish`/`pack` release
detection, `reference`/`solution` editing) - and now a **registered-task build** tier: with the host
task registry (`Microsoft.Build.Utilities.Task.RegisterTask`), a project whose tasks are registered
**builds** end to end under Native AOT (see [RegisteredTaskAotTests.cs](RegisteredTaskAotTests.cs)). The
broader **execution engine** (`BuildManager`) remains the AOT-hard path a host falls back to a
forwarded/JIT MSBuild for - its build entry points stay `[RequiresUnreferencedCode]` for reflective
logger/plugin loading; an in-process host that passes pre-constructed `ILogger` instances and no
project-cache plugins (as the harness does) does not hit that path.

> This project is **intentionally not part of the repository's Arcade build** (it is not in
> `MSBuild.slnx`). The local empty `Directory.Build.props` / `Directory.Build.targets` /
> `Directory.Packages.props` isolate it from the repo's test machinery so it can use a Native-AOT,
> MSTest-on-MTP configuration. Build and run it explicitly with the commands below.

## How to run

From the repository root, using the repo's pinned SDK (`.dotnet\dotnet.exe`):

```powershell
# Fast JIT pass (build + run the MTP test host)
.\.dotnet\dotnet.exe build src\aot-validation\Microsoft.Build.AotValidation.csproj
.\src\aot-validation\bin\Debug\net10.0\win-x64\Microsoft.Build.AotValidation.exe

# Native AOT pass (the real validation): publish, then run the native exe
.\.dotnet\dotnet.exe publish src\aot-validation\Microsoft.Build.AotValidation.csproj -r win-x64 -c Release
.\src\aot-validation\bin\Release\net10.0\win-x64\publish\Microsoft.Build.AotValidation.exe
```

Native AOT publishing requires the Visual Studio C++ toolchain (the MSVC linker). A run prints the
standard MTP summary and exits `0` when all tests pass.

## Why this MSTest/MTP configuration

MSTest has two MTP integrations, and only one is AOT-compatible:

- `MSTest.TestAdapter` routes discovery/execution through the reflection-based **VSTest bridge**,
  which throws `NotSupportedException: Running tests ... is not supported for the selected platform`
  under Native AOT.
- `MSTest.Engine` + `MSTest.SourceGeneration` is MSTest's **source-generated** runner. It registers
  tests and the MTP entry point with no runtime reflection, so it works under AOT. This harness uses
  it, with `Microsoft.Testing.Platform.MSBuild` generating the entry point. This is the same MTP
  family `dotnet test` uses in MTP mode.

## What it found

Vetting the object model under AOT surfaced two concrete blockers. Both are the classic single-file
problem: `Assembly.Location` returns an empty string in a single-file / Native AOT executable.

1. **Toolset discovery (`BuildEnvironmentHelper`).** With no `MSBuild.dll` on disk next to the app and
   an empty `Assembly.Location`, `new ProjectCollection()` throws
   `ArgumentException: The path is empty`. A real AOT host (the dotnet CLI) already knows where its
   SDK/MSBuild lives and points the engine at it via the `MSBUILD_EXE_PATH` environment variable - the
   SDK does exactly this today. The harness mirrors that contract: a `[ModuleInitializer]`
   ([HarnessEnvironment.cs](HarnessEnvironment.cs)) sets `MSBUILD_EXE_PATH` to the repository's
   bootstrap toolset before any object-model type initializes. This is a host responsibility, not an
   engine bug.

2. **`ProjectCollection.Version` (engine bug, fixed).** Evaluation reads `ProjectCollection.Version`
   (to stamp the built-in `MSBuildVersion` property), which called
   `FileVersionInfo.GetVersionInfo(Assembly.Location)` - empty under AOT, throwing
   `The path is empty` from `Path.GetFullPath`. Fixed in
   [src/Build/Definition/ProjectCollection.cs](../Build/Definition/ProjectCollection.cs) to read
   the `AssemblyFileVersionAttribute` directly (same value, no file path needed - also more robust
   under shadow-copy and single-file).

With those two in place, **all evaluation/construction scenarios pass under Native AOT.** Remaining
`Assembly.Location` usages in the engine are on the task-execution path (task/SDK/logger loading),
which is the AOT-hard tier this harness deliberately does not cover.

3. **SDK resolution (`<Project Sdk="...">`).** The evaluation entry points used to carry an
   SDK-resolution `[RequiresUnreferencedCode]` all the way up to the `Project`/`ProjectInstance`
   constructors, forcing this harness to `#pragma warning disable IL2026`. That suppression is gone:
   in-box SDK resolution is a reflection-free directory probe, so it stays trim-safe, and the
   reflective plugin-resolver load is now gated behind the
   `Microsoft.Build.EnableSdkResolverDynamicLoading` feature switch that fails observably (MSB4282)
   when disabled (see [documentation/aot/sdk-resolution.md](../../documentation/aot/sdk-resolution.md)).
   This harness bakes that switch **off** (a `RuntimeHostConfigurationOption` with `Trim="true"`, mirroring
   an AOT dotnet CLI), so ILC dead-strips the reflective branch and `Evaluation_InBoxSdkResolvesReflectionFree`
   proves a `<Project Sdk="...">` still resolves and imports its `Sdk.props`/`Sdk.targets` under Native AOT.

4. **`System.Configuration.ConfigurationManager` (transitive dependency, now trimmed).** The config-file
   toolset reader (`ToolsetConfigurationReader` -> `ToolsetElement` -> `System.Configuration`) is compiled
   into the .NET build, and the `locations & ConfigurationFile` selector is a runtime flag ILC cannot prove
   is never set, so it used to keep the whole subtree and surface an **IL2104** for the
   `System.Configuration.ConfigurationManager` assembly. That block is now gated behind the
   `Microsoft.Build.EnableConfigurationFileToolsets` feature switch (default **on**, so the JIT keeps reading
   `.exe.config` toolsets exactly as before). This harness bakes the switch **off** (another `Trim="true"`
   `RuntimeHostConfigurationOption`), so ILC folds the selector to `false`, dead-strips the
   `ToolsetConfigurationReader` subtree, and `System.Configuration` drops out of the closure entirely - the
   IL2104 is gone rather than suppressed. On .NET the config-file location is not in the default toolset set
   anyway (the matching tests are .NET Framework-only), so nothing this harness exercises regresses. A host
   that disables the switch and still asks for `ToolsetDefinitionLocations.ConfigurationFile` fails observably
   with an `ArgumentException` rather than silently returning no toolsets.

## Scenarios

[ObjectModelAotTests.cs](ObjectModelAotTests.cs) mirrors the audit tiers:

| Test | Audit scenario it represents |
| --- | --- |
| `Construction_CreateAndReadProjectRootElement` | `dotnet reference` / `solution add` editing project XML |
| `Construction_ParseSolutionFile` | `dotnet test` discovery / release locator (`SolutionFile.Parse`) |
| `Evaluation_InMemoryProject_PropertiesAndItems` | `dotnet new` capabilities, `run`, reference TFM checks |
| `Evaluation_ConditionsEvaluate` | core condition evaluation |
| `Evaluation_IntrinsicPropertyFunction` | AOT-friendly subset of property functions (`$([MSBuild]::...)`) |
| `Evaluation_LoadProjectFromDisk_MirrorsRunCommand` | `dotnet run` project property reads |
| `ProjectInstance_InMemory_PropertiesAndItems` | release locator / `run` / test discovery / `solution add` |
| `ProjectInstance_FromFile_MirrorsTestDiscoveryAndReleaseLocator` | `dotnet test` `ProjectInstance.FromFile`, `PublishRelease` detection |
| `Evaluation_InBoxSdkResolvesReflectionFree` | `<Project Sdk="...">` in-box SDK resolution - reflection-free directory probe, no resolver assembly loaded |
| `RegisteredResolver_ResolvesSdk_AndImportsItsPropsAndTargets` | `SdkResolver.Register` host registration - a reflection-free resolver baked in at startup resolves a `<Project Sdk="...">` the in-box probe can't, with no assembly loading (the workload-resolver injection seam) |
| `DotnetNew_Console_EvaluatesAsExecutableProject` | `dotnet new console` -> a real `Microsoft.NET.Sdk` project evaluates end to end (`OutputType=Exe`, output dir, TFM) |
| `DotnetNew_Classlib_EvaluatesAsLibraryProject` | `dotnet new classlib` -> a real `Microsoft.NET.Sdk` project evaluates end to end (`OutputType=Library`) |
| `PropertyFunctionAotTests.*` | a property function cannot reach a reflective `Type`-taking member (`Enum.GetValues(Type)`) - the author gets MSB4185/MSB4186, never an AOT crash |
| `RegisteredTaskAotTests.RegisteredBuiltInAndCustomTasks_Build_UnderAot` | a host registers built-in and custom task classes, then **builds** a project end to end - the tasks run under Native AOT with the reflective task-loading path off |
| `RegisteredTaskAotTests.IntrinsicCallTargetAndMSBuildTasks_Build_UnderAot` | the intrinsic `MSBuild` and `CallTarget` tasks (engine-internal, never host-registered) drive a child build and a target call under Native AOT with the reflective path off |
| `RegisteredTaskAotTests.UnregisteredTask_WithReflectionOff_FailsObservably` | an unregistered task fails the build with a reported error, never a reflection crash |
| `DotnetTemplateAotTests.DotnetNew_*_BuildUnderAot_RunsRegisteredTasksThenFailsObservably` | a real SDK template build evaluates and runs registered tasks, then fails observably at the first task from the SDK's own task assembly (`Microsoft.NET.Build.Tasks`, which is not part of `Microsoft.Build.Tasks.Core`) |
| `ToolchainSmokeTests.TestHostRunsUnderAot` | the MSTest+MTP+AOT host itself |

### Property-function reflective members under AOT

`Enum.GetValues(Type)` carries `[RequiresDynamicCode]` and is the source of an IL3050 the engine
suppresses. [PropertyFunctionAotTests.cs](PropertyFunctionAotTests.cs) confirms empirically that a
property function can **never** reach it (or any reflective method that takes a `System.Type`): there is
no way to produce a `Type` argument - `string` does not coerce to `Type` (**MSB4186** "method not found,
check that parameters are of the correct type"), and `[System.Type]::GetType(...)` is not an available
property function (**MSB4185**), even with `MSBUILDENABLEALLPROPERTYFUNCTIONS=1`. So an author always sees
a normal, AOT-independent property-function diagnostic pointing at their expression - never an AOT crash or
a `NotSupportedException` from the trimmed dynamic-code path - which is why the IL3050 suppression is a
static-reachability false positive and no special-casing is warranted.

### Evaluating real `dotnet new` templates under AOT

[DotnetTemplateAotTests.cs](DotnetTemplateAotTests.cs) goes past synthetic projects: it shells out to the
bootstrap `dotnet new` to create the stock `console` and `classlib` templates, then opens each real
`Microsoft.NET.Sdk` project with `new Project(...)`. Getting a full SDK project to evaluate under Native
AOT surfaced three host responsibilities a real AOT MSBuild host must take on - each mirrored in the `.csproj`:

- **Disable workload resolution.** `Microsoft.NET.Sdk` unconditionally imports the workload-locator SDKs
  (`Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` / `...WorkloadManifestTargetsLocator`), resolved by the
  dynamically-loaded NuGet/workload plugin resolver - the AOT-hard path baked off here, so reaching it fails
  observably with **MSB4282**. The SDK gates that whole import behind `MSBuildEnableWorkloadResolver`, so the
  test evaluates with that global property set to `false` (what an AOT host does); the project then resolves
  only through the in-box, reflection-free `Microsoft.NET.Sdk`.
- **Bundle `NuGet.Frameworks`.** SDK evaluation calls NuGet-backed property functions
  (`[MSBuild]::GetTargetFrameworkIdentifier` and friends). `Microsoft.Build` references `NuGet.Frameworks`
  with `PrivateAssets="all"` (in a real SDK it loads the copy next to `MSBuild.dll`), so it does not flow to
  the harness transitively; the harness adds its own reference to put it in the output and the AOT image.
- **Root `Microsoft.Build.Utilities.Core`.** The SDK invokes
  `[Microsoft.Build.Utilities.ToolLocationHelper]::...` property functions, and the allowlist resolves such
  cross-assembly receivers by assembly-qualified name via `Type.GetType` - which under AOT only succeeds if
  the type's metadata is preserved. The harness references the (`IsAotCompatible`) assembly and roots it with
  `TrimmerRootAssembly`.

With those in place both templates evaluate end to end under Native AOT, and the tests read back the derived
properties a host cares about: `OutputType`, the `bin`/`obj` output directories, `TargetFramework`, and
`AssemblyName`.

### Building a project under AOT with registered tasks

[RegisteredTaskAotTests.cs](RegisteredTaskAotTests.cs) goes past evaluation and actually **builds** under
Native AOT. The harness bakes `EnableReflectiveTaskExecution=false`, so the reflective task-loading path
(assembly probing, by-name type resolution) is trimmed away and an *un*registered task fails observably. A
host instead pre-registers its tasks with the host task registry (see
[task-class-registration-api.md](../../documentation/specs/task-class-registration-api.md)): the
common built-in tasks through `Microsoft.Build.Tasks.BuiltInTasks.RegisterAll()`, and its own tasks through
`Microsoft.Build.Utilities.Task.RegisterTask<T>(name)`. A registered task is constructed and bound with no
assembly loading or by-name type resolution, so `RegisteredBuiltInAndCustomTasks_Build_UnderAot` runs a real
in-process build of a hand-authored project - `MakeDir`/`WriteLinesToFile`/`Copy` produce files, and a
host-registered custom task's `[Output]` is bound back to a property - entirely under AOT.

The engine-internal intrinsic tasks `MSBuild` and `CallTarget` - which virtually every real build dispatches
through but no host registers - resolve the same reflection-free way (a direct `new`, no assembly probing), so
they stay available with the switch off too: `IntrinsicCallTargetAndMSBuildTasks_Build_UnderAot` builds a child
project through `<MSBuild>` and dispatches a target through `<CallTarget>` under Native AOT.

Making the in-process build trim-clean surfaced the build-execution path the rest of the harness deliberately
avoids. Three host responsibilities (all mirrored by the `.csproj` switches and engine guards):

- **Pass loggers as instances, not descriptions.** Creating a logger from a `LoggerDescription`
  (assembly/class name) reflects over the logger assembly. The new `EnableReflectiveLoggerLoading` switch
  (baked off here) drops that path - and with it `TypeLoader` and `System.Reflection.MetadataLoadContext` -
  so the harness logs through pre-constructed `ILogger` instances.
- **No custom plugins.** `EnableCustomPluginProbing` (baked off) drops the reflective build-check/plugin
  acquisition the build wires up.
- **The build entry point stays `[RequiresUnreferencedCode]`.** `Project.Build` can still load loggers and
  project-cache plugins by reflection in general, so the single call is isolated behind a documented
  suppression in [InProcBuild.cs](InProcBuild.cs) (the harness passes neither). In-process MSBuild builds
  use process-global state, so the harness runs serially (`[assembly: DoNotParallelize]`).

The `DotnetTemplateAotTests.DotnetNew_*_BuildUnderAot_*` tests build the real `console`/`classlib` templates
the same way: evaluation and the registered built-in tasks run, then the build fails observably at the first
task from the SDK's own task assembly (`Microsoft.NET.Build.Tasks` - for example `AllowEmptyTelemetry` - which
is not part of `Microsoft.Build.Tasks.Core` and so cannot be registered from this harness). That pins the
exact AOT boundary for a real SDK build: it degrades to a reported error, never a reflection crash.

### Host-registered SDK resolvers under AOT

[RegisteredSdkResolverAotTests.cs](RegisteredSdkResolverAotTests.cs) covers the `SdkResolver.Register`
host-registration API (see
[documentation/specs/sdk-resolver-host-registration-api.md](../../documentation/specs/sdk-resolver-host-registration-api.md)) -
the seam an AOT host (the .NET SDK CLI) uses to contribute a reflection-free SDK resolver without MSBuild
discovering and loading it from disk. It is what makes an AOT-ready workload resolver possible now that the
plugin-resolver load is baked off.

The test constructs a tiny resolver with `new` (no `Assembly.LoadFrom`, no reflection) and registers it from a
`[ModuleInitializer]` - mirroring how a host registers at startup, before the first evaluation, which is what
guarantees the resolver is present when the engine snapshots its default-resolver list on the first SDK
resolution. It then evaluates a `<Project Sdk="Harness.Registered.Sdk">` whose SDK deliberately does **not**
live under `MSBuildSDKsPath`, so the in-box probe cannot find it; only the registered resolver can. Because
plugin loading is baked off, an unresolved SDK would fail observably with **MSB4282** instead - so the project
evaluating, with the SDK's `Sdk.props`/`Sdk.targets` imported, is end-to-end proof that a host-registered,
reflection-free resolver participates in resolution by `Priority` alongside the built-in resolver under Native
AOT. The resolver claims only its own SDK name and defers for every other SDK, so it leaves the rest of the
harness's resolution unchanged.

## Warning gate

The harness builds with `TreatWarningsAsErrors=true`, so **any new trim/AOT/single-file warning fails the
publish** - the engine's AOT surface stays explicitly triaged. The harness keeps **no `WarningsNotAsErrors`
exemptions**: every warning is driven to zero, so a regression cannot slip in as a "known" warning.

- **IL3000** (`Assembly.Location` is empty in a single-file/AOT app) - the engine's on-disk self-discovery.
  Most reachable sites are **excluded** rather than suppressed, by guarding the `Assembly.Location` read on
  `RuntimeFeature.IsDynamicCodeSupported` (which ILC substitutes to `false` under Native AOT, then dead-strips
  the guarded branch - so the warning is gone with no suppression at all):
    - `AssemblyLoadsTracker` subscribes to `AppDomain.AssemblyLoad`, which never fires under Native AOT, so
      its entry point returns early and ILC proves the tracker is never instantiated, dropping
      `CurrentDomainOnAssemblyLoad` and its `Assembly.Location` read.
    - `BuildEnvironmentHelper.Initialize` and `GetProcessFromRunningProcess` fall straight to the running
      process path under AOT (an empty assembly location is meaningless there), so their `Assembly.Location`
      reads are dead-stripped.
    - `NativeMethods.FrameworkCurrentPath` reports an empty path under AOT. Its only consumers locate an
      installed .NET Framework (or Mono) - which a Native AOT process never has - and already treat an empty
      result as "framework not found", so the guard both keeps the behavior sensible and dead-strips the read.

  One site remains a documented `[UnconditionalSuppressMessage("SingleFile", "IL3000")]`:
  `TypeExtensions.GetAssemblyPath`, the generic self-discovery primitive that is correct in a hosted/JIT
  layout and cannot simply switch to the process path (it is also hardened to return the empty path rather
  than throw from `Path.GetFullPath`). The supported AOT contract is that the host supplies the toolset via
  `MSBUILD_EXE_PATH` (finding #1), so none of these paths are the source of truth here.

Everything else is driven to zero too: the SDK-resolution and property-function reflective paths are gated
behind feature switches baked off here, the `Enum.GetValues(Type)` IL3050 is a vetted false positive (see
above), and the property-function receiver `IL2078` was fixed by annotating the `FunctionBuilder` backing
field. The `System.Configuration.ConfigurationManager` dependency (previously an exempted **IL2104**) is
now trimmed out entirely: the config-file toolset reader is gated behind the
`Microsoft.Build.EnableConfigurationFileToolsets` feature switch, baked off here so ILC dead-strips the
`ToolsetConfigurationReader` subtree and `System.Configuration` leaves the closure.

## Files

- `Microsoft.Build.AotValidation.csproj` - the AOT, MSTest-on-MTP test project (ProjectReferences `Microsoft.Build`).
- `HarnessEnvironment.cs` - `[ModuleInitializer]` that supplies `MSBUILD_EXE_PATH` (finding #1).
- `ObjectModelAotTests.cs` - the object-model scenarios.
- `PropertyFunctionAotTests.cs` - property-function behavior under AOT (reflective `Type`-taking members are unreachable; allowlisted receivers work).
- `DotnetTemplateAotTests.cs` - evaluates real `dotnet new` console/classlib projects under AOT (see "Evaluating real `dotnet new` templates").
- `RegisteredSdkResolverAotTests.cs` - validates the `SdkResolver.Register` host-registration API under AOT (see "Host-registered SDK resolvers under AOT").
- `TempDirectory.cs` - a disposable temp directory, mirroring the test infrastructure's `TransientTestFolder`.
- `ToolchainSmokeTests.cs` - validates the MSTest/MTP/AOT host independent of the object model.
- `Directory.Build.props` / `Directory.Build.targets` / `Directory.Packages.props` - isolate the harness from the Arcade build.
