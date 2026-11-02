# Experimental standard-SAC telemetry unification

`exp/janprovaznik/telemetry-reunification-2` is the MSBuild consumer for the coordinated
**ordinary devenv SAC migration** in Visual Studio. VS owns the current telemetry SDK,
OpenTelemetry dependency closure, SAC versions, product dependencies, DLL ownership and
NGEN contexts. This branch does not copy a producer SDK or define a private binding group.

The experimental full-framework branch2 build activates automatically.
`telemetryReunificationControl=true` in ExpPerf, or `VsTelemetryExperiment=false`, selects
the explicit old-SDK control. Main/public/.NET/NuGet/outside-VS outputs keep their existing
private/public 17.14.18 behavior. Consent, session ownership, normal event saving and
shutdown budgets are unchanged.

## MSBuild-side changes

Only experimental `Microsoft.Build.vsix` and `Microsoft.Build.Arm64.vsix` authoring changes:

- Keep `vs.file.useGroupConfig="devenv"` and `vs.file.overwriteBindingRedirects=yes`.
- Remove private telemetry binding entries from the generated MSBuild/coordinator templates;
  the destination's ordinary SAC supplies the current SDK redirects/codeBase.
- Remove MSBuild's private `Microsoft.VisualStudio.Telemetry.dll` payload and its NGEN roots
  from the experimental package, without changing the default/control SWR.
- Make the ARM64 coordinator a normal devenv-group config template in the experiment.
  ARM64 MSBuild already used that group; leaving its coordinator as a raw config would be
  incomplete after removing the shared private DLL. Control ARM64 keeps its original raw config.
- Use one timestamped SWR input per package so switching experiment/control cannot reuse
  the other variant's cached VSIX.

No `msbuild-telemetry-experiment` group, custom provider dependency, SDK snapshot manifest,
exporter or copied OpenTelemetry/Microsoft.Extensions closure remains. Existing non-telemetry
config policy and the source app configs are not rewritten. A `/nodemode` worker uses the same
finalized MSBuild executable config.

## Build and package gates

For a local packaging candidate:

```powershell
$env:VsTelemetryExperiment = 'true'
.\build.cmd -msbuildEngine vs -c Release -v quiet -bl
Remove-Item Env:\VsTelemetryExperiment
```

No installed producer SDK or hash override is consulted. The shared package gate checks
ordinary devenv metadata, no private telemetry binding/payload/NGEN ownership, and no obsolete
custom-provider/snapshot assets. It runs after SWIX, before post-signing insertion manifest
generation, and before artifact publication. Both packages are checked in the publication
phase; a stale control package fails rather than being published as the candidate.

```powershell
& .\src\Package\MSBuild.VSSetup\Test-TelemetryReunification.ps1 `
  -VsixPath .\artifacts\VSSetup\Release\Insertion\Microsoft.Build.vsix
& .\src\Package\MSBuild.VSSetup\Test-TelemetryReunification.ps1 `
  -VsixPath .\artifacts\VSSetup\Release\Insertion\Microsoft.Build.Arm64.vsix
```

These checks prove **consumer authoring**, not that telemetry is already a SAC member in the
installed destination. The matching VS migration must supply the telemetry/current closure
in the ordinary `devenv` group, its owning files packages, required SKU dependencies and native
images. Without that coordinated VS change, this candidate is not a working insertion.
Do not interpret a successful MSBuild package build as a completed SAC migration.

## Validation scope

The finite local checks use production package generation and the actual SWIX compiler with
prebuilt architecture inputs: automatic activation, explicit controls, main/public/Core
defaults, all six MSBuild/coordinator template surfaces, absence of private SDK ownership,
and control/candidate/cache/publication gates. No new architecture compilation is needed
for this metadata/config-only change. Product lifecycle code is unchanged; previous targeted
lifecycle evidence is reused.

Prior local proofs of IDE-private routing and a custom provider are historical, not proof of
the standard-SAC destination. The coordinated VS producer/insertion and Heavy performance
run are owned by the parent investigation. This MSBuild task neither queues pipelines nor
writes VS authoring, and does not claim that the new SAC membership is already installed.

## Performance and compatibility limits

Validate the actual combined insertion: telemetry and the full current closure must bind to
the VS-owned SAC files, the normal owned/host sessions must initialize and save, and native
images must actually be accepted in standalone/worker/coordinator processes. Match native
coverage on both control and candidate before comparing timing; observe Heavy/perf results
from that insertion rather than treating an IL-versus-NI replay as an SDK win.

This cleanup does not fix unrelated unsigned/test-sign core behavior or GAC Utilities fallback
JIT. No NGEN-footprint reduction or performance improvement is claimed. VS-side SAC ownership,
BuildTools/product dependencies, native-context authoring and downstream results remain part
of the coordinated migration, not a producer-agent snapshot workaround.
