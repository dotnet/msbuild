# Experimental VS telemetry SDK reunification

This is an **opt-in investigation**, stacked on the private telemetry NGEN registration fix.
It is not a proposed default SDK upgrade. Normal builds, NuGet packages, .NET hosts and
MSBuild outside VS keep the public `Microsoft.VisualStudio.Telemetry` 17.14.18 dependency.
Consent, session ownership, pending-event persistence and shutdown budgets are unchanged.

## What is unified

An experimental `Microsoft.Build.vsix` snapshots the **actual current VS telemetry SDK**
and its non-framework dependency closure from a selected, fully installed VS instance.
MSBuild continues compiling against the public SDK; only the VS setup payload and executable
configuration templates change. No private NuGet feed or package-version substitution is needed.

The experiment deliberately unifies **SDK bytes and dependency binding**, not the physical
location of every DLL. Telemetry and non-SAC dependencies are byte-identical private copies
of the host's runtime implementations. Existing devenv SAC members remain shared at their
canonical paths. This avoids claiming ownership of IDE files or changing global SAC membership:
VS explicitly removed telemetry from the devenv group because of its OpenTelemetry dependency.

The snapshot tool follows the selected `devenv.exe.config` binding redirects and version-specific
codeBases recursively, verifies strong-name identities and hashes, and accepts unconfigured
dependencies only from the .NET Framework 4.7.2 platform contract. It fails on missing or ambiguous bindings,
an unexpected SAC path, or source changes during generation. It does not execute the SDK.
The installed `TelemetryReunification.json` records versions, MVIDs, relative source paths,
SHA-256 hashes, framework references and source configuration hashes.

Explicit redirects/codeBases are generated for root/x86 and amd64 `MSBuild.exe` **and**
`MSBuild.Coordinator.exe`. A `/nodemode` reusable worker uses its own `MSBuild.exe.config`;
serialized VS session settings do not substitute for binding policy. This also covers SDK
telemetry created by task libraries in those workers. The engine hosted inside devenv already
uses the host's SDK/session and is not changed. The CLR 2 task host and ARM64 package are not
part of this x86/x64 experiment.

## Build and setup

Use the VS instance whose SDK/configuration is to be measured; finish setup and close it first.
The source installation is read-only. Inspect a snapshot before opting into packaging:

```powershell
$vs = 'C:\Program Files\Microsoft Visual Studio\18\IntPreview'
$bin = Join-Path $vs 'MSBuild\Current\Bin'
& powershell.exe -NoProfile -File .\src\Package\MSBuild.VSSetup\Prepare-TelemetryReunification.ps1 `
  -VsInstallPath $vs -OutputDirectory "$env:TEMP\MSBuildTelemetrySnapshot" `
  -X86Config "$bin\MSBuild.exe.config" -X64Config "$bin\amd64\MSBuild.exe.config" `
  -CoordinatorConfig "$bin\MSBuild.Coordinator.exe.config" `
  -CoordinatorX64Config "$bin\amd64\MSBuild.Coordinator.exe.config"
```

The tool reports the SHA-256 of `TelemetryReunification.json`. Inspect its SDK, dependency and
host version/hash records, then explicitly pin that producer snapshot for the build. Environment
properties avoid nested command-line quoting of a VS path containing spaces:

```powershell
$env:VsTelemetryExperimentSource = $vs
$env:VsTelemetryExperimentExpectedSnapshotHash = '<inspected snapshot SHA-256>'
.\build.cmd -msbuildEngine vs -c Release -v quiet -bl
Remove-Item Env:\VsTelemetryExperimentSource, Env:\VsTelemetryExperimentExpectedSnapshotHash
```

Only `MSBuild.VSSetup.csproj` consumes these opt-in properties. Its generated SWR replaces the old
private telemetry payload with the selected VS bytes and adds all required non-SAC assemblies.
Other build outputs, including `MSBuild.exe.config` outside the VSIX, are not rewritten.
Omit the properties and rebuild to obtain the optimized old-SDK control package. The build
rejects an absent pin or a source snapshot that differs from the pin.

The experimental official pipeline is `azure-pipelines\.vsts-dotnet-exp-perf.yml`, not the
main/vs-only official pipeline. Its explicit runtime parameters are:

| Parameter | Unified-SDK run | Old-SDK native control |
|---|---|---|
| `vsTelemetryExperimentSource` | Fully installed source VS path on the Windows build agent | `disabled` |
| `vsTelemetryExperimentSnapshotHash` | Inspected snapshot SHA-256 from the intended destination host | `disabled` |
| `enableOptProf` | Match the control | Match the experiment |
| `OptProfDropName` | Match the control | Match the experiment |

Select `refs/heads/exp/janprovaznik/telemetry-reunification` explicitly. The source instance must
be provisioned to match the intended destination snapshot; do not use whichever SDK happens to
be on the build agent. These parameters are wired into the Windows build as environment
properties and a mismatched producer fails the package build. Default/automatic branch CI has
the experiment **disabled**; pushing this ref alone does not produce a unified-SDK insertion.
This investigation does not queue that pipeline or a downstream VS/perf run.

The four config templates retain `vs.file.useGroupConfig="devenv"` and
`vs.file.overwriteBindingRedirects=yes`. Normal VS setup finalization therefore continues to
apply the existing shared group. No telemetry row is added to `devenv.csv` and no other
group-config consumer changes. Snapshot-specific SAC redirects are already in the templates,
but setup remains authoritative for shared dependencies. Use the same VS source snapshot
for generation and the test insertion; re-snapshot after a VS update.

Every newly selected private assembly has `vs.file.ngenArchitecture=all`, priority 2 and explicit
NGEN application roots for all four executables. Thus `/NoDependencies` setup processing covers
the OpenTelemetry/Microsoft.Extensions closure too, not just telemetry itself. The unused old
telemetry payload is not registered or shipped in this experimental VSIX. The default package
retains its original payload and NGEN metadata.

Install the experimental package through an isolated VS test insertion/setup. Do not copy it over
a production VS installation or assume that unpacking a VSIX runs config finalization/NGEN.
Setup must finalize configs **before** compiling the images. Do not extend the IDE-owned
telemetry file's NGEN roots or re-add it to SAC globally just to run this experiment.

Before deployment, run the package/destination preflight against the actual target VS instance:

```powershell
& .\src\Package\MSBuild.VSSetup\Test-TelemetryReunification.ps1 `
  -VsixPath .\artifacts\VSSetup\Release\Insertion\Microsoft.Build.vsix `
  -VsInstallPath $vs -ExpectedSnapshotHash '<inspected snapshot SHA-256>'
```

It verifies every selected payload hash and NGEN root, every required binding in all four
complete packaged config templates, preserved group-finalization metadata, and the destination's
exact host/config/SAC/dependency hashes. A mismatch is an error, not a fallback. Run this
preflight as a required deployment gate: the VS setup engine does not understand the experiment's
snapshot manifest and cannot enforce this producer/destination constraint by itself.

## Untimed diagnostic probe

`eng\experiments\TelemetryReunification\TelemetryReunificationProbe.cs` is deliberately not in a
shipping project. Compile it into a scratch directory with the selected VS compiler and framework:

```powershell
& "$bin\Roslyn\csc.exe" /nologo /target:library /langversion:preview /nullable:enable /warnaserror+ `
  "/out:$env:TEMP\TelemetryReunificationProbe.dll" `
  "/reference:$bin\Microsoft.Build.Framework.dll" `
  "/reference:${env:ProgramFiles(x86)}\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\System.Web.Extensions.dll" `
  .\eng\experiments\TelemetryReunification\TelemetryReunificationProbe.cs
```

In an **untimed** standalone diagnostic run add
`"-logger:TelemetryReunification.SessionProbe,$env:TEMP\TelemetryReunificationProbe.dll"`.
It reads the already-existing internal default session, never the public `DefaultSession`
getter that can create one. It captures initialization/start/consent booleans, default channel IDs,
the actual router channel IDs/types, the OpenTelemetry-enabled flag, and only the
specific retained OpenTelemetry preparation-failure property. It fails on a preparation failure
or a no-op wrapper; an absent failure property is explicitly **not** proof of requested/ready
OpenTelemetry. It does not read identifiers or force any setting.

The same diagnostic assembly exposes `TelemetryReunification.BindingProbe`, an `ITask` with a
required `SnapshotPath` parameter. Invoke it in child projects using `BuildInParallel="true"`
and repeat the build with node reuse to verify the selected assembly MVIDs in reusable workers.
It explicitly calls `Assembly.Load` for the manifest identities, not `LoadFrom`, and reports its
PID and loaded paths/MVIDs. This is **explicit dependency-binding coverage**, not proof that an
ordinary Appx task activated the transport or that those modules are naturally used in a timed
workload. Remove both probes for timing.

## Required evidence before interpreting performance

Compare **old-native SDK versus unified-native SDK**, with the optimized NGEN fix present on
both sides. An old IL-only versus new native comparison conflates two changes and is invalid
as evidence for SDK reunification. Merely finding files or NGEN registrations is insufficient.

For both configurations, use separate untimed CLR loader/Binding/Fusion/JIT captures to establish:

- The selected telemetry MVID and all loaded non-framework dependency paths match the manifest
  and final SAC policy; no initialization/shutdown failure silently disables telemetry.
- Capture the session's active channel/preparation result as well as successful initialization.
  The new SDK can keep legacy channels after an OpenTelemetry preparation failure, or create
  `openTelemetryNoopChannel`. `IsOpenTelemetryEnabled` is true only for the accepted
  `openTelemetry` channel. A policy/flight-disabled `NotRequested` result is distinct from an
  assembly-load/configuration failure. Do not force flights, serialized settings or consent.
- Telemetry and exercised OpenTelemetry dependencies actually map accepted native images.
  Record ordinary versus runtime-generated dynamic JIT work, including task-created worker sessions.
- Core MSBuild images are also accepted, rather than losing their native coverage when binding
  changes. All existing task APIs and normal save/upload semantics still work.
- Root/x86, amd64, standalone/server/coordinator and reused `/nodemode` processes are covered
  separately. A passing standalone process is not proof of worker or coordinator coverage.

Timed runs must omit observers and ETW controllers. Keep consent, token/elevation, workload,
toolset, configuration and core bytes fixed. Measure full process/build wall time and the
normal telemetry shutdown tail; do not omit a longer save interval or truncate pending events.
Use the same real C++/NMake replay or original VS perf scenario, not an empty library benchmark.

Targeted NGEN registration and Binding/Fusion tracing require elevation. If elevation or a
fresh setup insertion is unavailable, report compatibility/package evidence and the missing
native-vs-native comparison explicitly. Existing measurements of the old NGEN defect are
control context only, **not results for this newer SDK**. Do not queue VS/perf pipelines as
part of this investigation.

## Observed local result (2026-10-30)

The inspected producer was VS **18.13.12328.28**, with telemetry file **18.4.50.56123**,
MVID `101331d0-098b-4945-9b5a-be8a890e1c67`. The selected snapshot SHA-256 is
`89425C6B4610530014C51E3E195369D488DD2977C05C131271F9BDB7C2209FEA`.

| Evidence | Result |
|---|---|
| Current private SDK closure | 13 assemblies, 3,159,592 bytes; replaces the old 1,026,088-byte telemetry payload, not the entire MSBuild package |
| Existing shared dependencies | 14 devenv SAC assemblies; no new SAC members |
| Complete VSIX verification | All 27 selected dependencies in all four config templates, exact payload hashes, and four NGEN application roots per private assembly |
| Opt-in/out round trip | Both full builds pass; opting out restores the old SDK, its two NGEN roots, all four unmodified build-output configs, and no experimental payload/manifest |
| Mismatch guards | Wrong producer pin, wrong destination config and wrong build snapshot each fail explicitly |
| Lifecycle regression tests | 25 net472 + 16 net11.0 = 41 passed, zero failed/skipped |
| Real CLI compatibility | Old/new x86/x64 C++/NMake replay completes ordinary owned-session initialization and saving; this is the recorded echo fallback, not Unreal compilation |
| Out-of-process binding | Two real `/nodemode` workers across two builds each return the expected MVIDs for all 27 explicit `Assembly.Load` requests |

The runtime smoke used scratch-deployed bytes/config templates from the **complete built VSIX**,
with existing SAC files exposed read-only through a directory junction. It did not run VS setup
finalization. The source VS installation, its configs, and NGEN registrations were not changed.
The second entry process used a different worker PID, so **cross-build worker reuse was not
demonstrated**. The worker probe exercises dependency binding, not a real Appx-created session.

The new SDK initializes and saves normally. Its observed default channel IDs are `aivortex`
and `collector`; `IsOpenTelemetryEnabled` is false and no retained OpenTelemetry preparation
failure is present. This is **not proof that the OpenTelemetry transport was requested or
activated**. No flights, consent, serialized session settings or shutdown budget were overridden.

The x64 loader observer sees an existing native image mapped for the new telemetry SDK.
However, the rebuilt experimental core maps **zero** core native images, while the installed
old-SDK control maps **three** core images and **no** telemetry image. Those are different
optimization states: **no SDK-performance timing or speedup is reported**. A fair old-native
versus unified-native comparison still requires setup/elevated registration covering both
cores and SDK closures, then independent Binding/Fusion/JIT confirmation and observer-free
timing. Coordinator runtime, ARM64, naturally activated OpenTelemetry, and a fresh VS/perf
insertion remain separate follow-up coverage.
