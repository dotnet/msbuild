# Experimental VS telemetry SDK reunification

This is an **isolated branch investigation**, stacked on the private telemetry NGEN registration fix.
The experimental official build of `exp/janprovaznik/telemetry-reunification-2` activates it
automatically; it must produce the newer SDK **or fail**, never silently publish the control.
It is not a proposed default SDK upgrade. Normal main/public builds, NuGet packages, .NET hosts and
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

`TelemetryReunification.targets` owns activation for the full-framework experimental official
build of `refs/heads/exp/janprovaznik/telemetry-reunification-2`. No source path or snapshot-hash
run parameters are required. Other branches, public CI and non-VS builds keep their defaults.
For a local experimental build, one explicit property suffices:

```powershell
$env:VsTelemetryExperiment = 'true'
.\build.cmd -msbuildEngine vs -c Release -v quiet -bl
Remove-Item Env:\VsTelemetryExperiment
```

The generator uses VS Installer `vswhere` to select the latest complete, launchable VS 18.6+
installation with MSBuild and CoreEditor. There is **no hardcoded IntPreview/agent path**.
The intended telemetry SDK is pinned in source to SHA-256
`E6059126F51A01EB6B20D0452D3138BC2E6F6295EA0E86E4829CAA9583AFBA75`
(file 18.4.50.56123). A missing/older/different SDK, missing OTel dependency, incomplete SAC
binding, or unavailable source is an error with provisioning guidance, not a control fallback.
It never downloads a private SDK or provisions VS implicitly.

The selected host's actual config/SAC/dependency hashes are captured in the packaged
`TelemetryReunification.json`. The sibling **build receipt** `TelemetryReunificationSource.json`
records the discovered source path, selection rule, SDK pin, snapshot hash, and CI source
branch/commit. The receipt pins subsequent package checks to this exact generated snapshot.
This is dynamic, recorded producer provenance, **not an assertion that the destination VS
has the same host/dependency snapshot**. Destination verification below remains mandatory.

Optional reproduction overrides remain MSBuild properties (or environment properties):

| Property | Purpose |
|---|---|
| `VsTelemetryExperimentSource` | Explicit fully installed source VS path instead of discovery; an unsuitable override fails rather than falling back |
| `VsTelemetryExperimentExpectedTelemetryHash` | Explicit alternative newer-SDK pin; the 18.4.50+ and complete closure checks still apply |
| `VsTelemetryExperimentExpectedSnapshotHash` | Require a previously inspected whole host/closure snapshot; a different snapshot is rejected |
| `VsTelemetryExperiment=false` | Explicitly select the optimized old-SDK control |

`MSBuild.VSSetup.csproj` consumes the selection and generates the SWR, replacing the old
private telemetry payload with the selected VS bytes and adding all required non-SAC assemblies.
Other build outputs, including `MSBuild.exe.config` outside the VSIX, are not rewritten.
The same timestamped SWR input is used for both modes, preventing a mode switch from reusing
the other variant's cached VSIX.

The experimental official pipeline is `azure-pipelines\.vsts-dotnet-exp-perf.yml`, not the
main/vs-only official pipeline. Its only experiment-specific run parameter is
`telemetryReunificationControl`: default **false** leaves the branch's automatic experiment
active; **true** passes the explicit old-SDK control opt-out. Keep `enableOptProf` and
`OptProfDropName` identical across comparison runs. The shared build job now explicitly defines
`SkipApplyOptimizationDataArg` for both OptProf states, so a disabled run receives
`/p:EnableNgenOptimization=false` rather than an unexpanded PowerShell expression.

`VerifyTelemetryReunificationPackage` is mandatory after SWIX (including incremental builds),
again in the post-signing phase **before insertion manifest generation**, and before artifact
publication. It requires the experiment manifest, newer telemetry/OTel payload, all recorded
dependency hashes/bindings, and NGEN/group-config metadata. Missing/old/corrupted packages fail
even when compilation/signing otherwise succeeds. No successful active experiment can publish
an old-SDK control through a stale package cache.

The original `exp/janprovaznik/telemetry-reunification` and draft #15429 remain unchanged
evidence: MSBuild-ExpPerf 15875635 (20261030.5) published the old SDK because both former run
parameters were disabled. VS insertion 801748 therefore did **not** test SDK reunification.
The separate `-2` branch corrects that activation error without rewriting the earlier evidence.
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
  -VsInstallPath '<actual destination VS installation>' `
  -ExpectedSnapshotHash '<published producer snapshot SHA-256>'
```

It verifies every selected payload hash and NGEN root, every required binding in all four
complete packaged config templates, preserved group-finalization metadata, and the destination's
exact host/config/SAC/dependency hashes. A mismatch is an error, not a fallback. Run this
preflight as a required deployment gate: the VS setup engine does not understand the experiment's
snapshot manifest and cannot enforce this producer/destination constraint by itself.

For a local producer check, use `-SourceReceiptPath` pointing at
`artifacts\obj\MSBuild.VSSetup\Release\net472\telemetry-reunification\TelemetryReunificationSource.json`
instead of supplying the source path and snapshot pin manually. Passing a destination path
with that receipt still verifies the **destination**, not the recorded source.

## Untimed diagnostic probe

`eng\experiments\TelemetryReunification\TelemetryReunificationProbe.cs` is deliberately not in a
shipping project. Compile it into a scratch directory with the selected VS compiler and framework:

```powershell
$bin = Join-Path '<selected VS installation>' 'MSBuild\Current\Bin'
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

## Automatic activation correction (2026-11-02)

The source-owned `-2` activation and gate checks were exercised with the production
`SetVsixProperties`, SWIX compiler and verification targets, using already-built architecture
inputs rather than recompiling another architecture. The scratch wrapper overrides only
`SetBinPaths`; the complete VSIX generation and postpackage checks are the repository's targets.

The local discovered producer is now VS **18.13.12330.35**, still with the pinned telemetry
**18.4.50.56123**. Its actual updated dependency/config snapshot is
`7585D1BB44BA7042EFDF192D2B00D3F885D9F3A87D97B52589E16D2B9CBA5799`:
13 private payloads (**3,191,456 bytes**), 14 existing SAC members and four complete configs.
This is a recorded new producer snapshot, not a claim that insertion 801748 has matching
dependencies or that CI has been provisioned.

The regression matrix passed **29 checks**, covering automatic activation without either
former run parameter, original/main/public defaults, explicit control, automatic/control/automatic
package round trips, incremental verification, missing discovery/source, old SDK, wrong SDK
and host snapshot pins, and missing/corrupted experiment payloads. Old controls and changed
payloads were blocked before post-signing Build, insertion manifest generation and publication
sentinels. Those sentinels replace only the final side effects in scratch imports of the actual
phase projects: **no real signing, network publication or pipeline run was performed**.
A separate Core/MSBuild query also confirmed `VsTelemetryExperiment=false` and
`ExcludeFromBuild=true` under the experimental branch context.

The initial individual-project build attempts could not use the existing x64 reference-output
layout, so a new full compilation was not claimed. The packaging-only harness avoids that
unrelated prerequisite and verifies the requested configuration/payload correction directly.
The prior runtime/lifecycle evidence above remains historical; no new runtime timing is claimed.

Normal experimental CI on `exp/janprovaznik/telemetry-reunification-2` will now discover the
specified SDK and validate the full generated/signed package, **or fail with provisioning/payload
errors**. Availability of that SDK on the actual CI agent, a matching destination VS setup,
native-image acceptance on both sides and fair timing remain unverified downstream work.
