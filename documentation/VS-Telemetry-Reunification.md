# Experimental destination-owned VS telemetry

`exp/janprovaznik/telemetry-reunification-2` now investigates **physical destination SDK
unification**, not a copy of the SDK on the MSBuild build agent. Its experimental full-framework
CI build activates automatically; `telemetryReunificationControl=true` or
`VsTelemetryExperiment=false` selects the explicit old-SDK control. Main/public/.NET/NuGet
outputs and the public 17.14.18 compile-time dependency are unchanged. Consent, owned/host
session behavior, saving, and shutdown budgets are unchanged.

## What failed before

The original automatic build 15875635 published the old SDK because its two snapshot parameters
were disabled. The corrected branch2 build 15892127 activated, but its producer's SDK
18.3.124 failed the pin for 18.4.50. That was a **source-provisioning rejection**, not a runtime
failure or performance result. Lowering the hash would still tie the runtime to the producer.
The original experiment/app refs and draft #15429 remain unchanged evidence.

The current consumer build does not inspect VS SDK files, invoke vswhere, copy IDE dependencies,
or require `VsTelemetryExperimentSource`/snapshot/hash properties. It generates four config
templates and a setup contract, even on a producer with a different SDK. Those retired
properties no longer select runtime policy.

## Supported setup mechanism and required companion

The verified VS `GroupConfigParser` accepts
`name,publickeytoken,culture,oldVersion,newVersion,version,href` CSV rows and resolves hrefs
relative to the CSV directory. `UpdateExeConfigFinalizerTask` overwrites overlapping
identity/version entries, rewrites codeBases relative to the executable, and rejects paths
outside the installation. It fails when the named group is missing.

The task reloads the original template **for each group**. Two groups on one template do
not compose: the second can overwrite the first. It does **not** infer private dependency
policy from `devenv.exe.config`. These constraints require a VS-owned companion provider:

| Owner | Responsibility |
|---|---|
| MSBuild consumer | Select only `msbuild-telemetry-experiment` on root/x86 and amd64 MSBuild/coordinator templates, with `overwriteBindingRedirects=yes`; require `Microsoft.VisualStudio.MSBuild.TelemetryBinding.Experimental` |
| VS companion | Generate one combined CSV from that VS build's unchanged devenv SAC rows plus its actual private telemetry dependency closure; publish it with `vs.file.groupConfig="msbuild-telemetry-experiment"` |
| Existing VS DLL owners | Keep owning telemetry, OpenTelemetry, Microsoft.Extensions and SAC DLLs; add required MSBuild application NGEN contexts where needed, without duplicating ownership |

The consumer includes a fail-directed telemetry codeBase to
`Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll` (CLR identity 16.0).
The provider supplies authoritative redirects/versions for the complete closure; it is not
three fixed codeBases. For example, the current destination has already advanced
`OpenTelemetry.Consolidated` from 1.12 to **1.15.3** while telemetry still has assembly version 16.
The four templates preserve the established ValueTuple facade-to-framework redirect, including
the coordinator.

The group is MSBuild-only. Telemetry is **not** re-added to global devenv SAC membership.
The provider owns only its CSV/provenance, never IDE DLLs. The legacy private telemetry file
remains solely for the unchanged ARM64 consumer, **without unused x86/x64 telemetry NGEN roots**.
This is explicitly not unification of ARM64, CLR 2 task hosts, or outside-VS outputs.

**The VS companion has not been integrated into the VS source build.** The MSBuild candidate
declares its dependency, so a real setup insertion without the provider cannot satisfy the
experiment. BuildTools without an IDE must not receive this IDE-only provider. Select the
companion only in the VS IDE product closure; do not silently install MinShell into BuildTools
or make its private file paths a production MSBuild default.

## Companion artifact

`src\Package\MSBuild.VSSetup\Export-DestinationTelemetryBinding.ps1` prepares the exact
binding-only artifact. Run it against the **matching VS-produced IDE layout**, after its
devenv configuration and shared group are finalized:

```powershell
& powershell.exe -NoProfile -NonInteractive -File `
  .\src\Package\MSBuild.VSSetup\Export-DestinationTelemetryBinding.ps1 `
  -VsInstallPath '<matching VS-produced IDE layout>' `
  -OutputDirectory '<companion output>' -PackageVersion '<VS build version>'
```

It recursively resolves actual assembly references through the destination host's redirects and
version-specific codeBases, checks strong-name identities/hashes, recognizes only the .NET
Framework platform contract for unconfigured references, and verifies canonical existing SAC
membership. Missing/ambiguous bindings, a legacy SDK, absent OTel dependencies, paths outside
the selected layout, or source changes fail explicitly. No SDK DLL is copied.

Outputs:

- `msbuild-telemetry-experiment.csv`: unchanged current SAC rows plus actual private closure rows.
- `DestinationTelemetryBinding.json`: versions, MVIDs, owner-relative paths and hashes of that
  destination closure, with host/SAC/CSV hashes.
- `companion.swr`: the unique configuration-only provider package.
- `NgenOwnerRequests.json`: required four application roots, architecture `all`, priority 2,
  and exact destination file identities/paths for the existing VS owners.

The precise original VS source file declarations for adding all NGEN contexts have not been
located/edited. Preserve their current owner roots, then add the requested MSBuild contexts
only in those owner packages. A configuration provider alone does not register native images.
Do not create foreign DLL entries in its SWR to manufacture NGEN coverage.

The producer must regenerate this provider when its private closure **or** devenv SAC changes
and include it in the same VS build/insertion as those files. Do not substitute a provider from
another VS build merely because telemetry's public CLR version is unchanged.

## Build and validation

The branch2 official experimental build requires no manual source/hash parameters. For a local
candidate, set one property:

```powershell
$env:VsTelemetryExperiment = 'true'
.\build.cmd -msbuildEngine vs -c Release -v quiet -bl
Remove-Item Env:\VsTelemetryExperiment
```

The mandatory package gate checks the single-group metadata on all four templates, the provider
dependency, destination SDK codeBase, preserved framework redirects, absence of copied OTel/
Microsoft.Extensions closure, and removal of unused private telemetry NGEN roots. It runs
after SWIX, before post-signing insertion manifest generation, and before artifact publication.
Default/control packages retain the original private SDK and roots.

Before running an installed candidate:

```powershell
& .\src\Package\MSBuild.VSSetup\Test-TelemetryReunification.ps1 `
  -VsixPath '<candidate Microsoft.Build.vsix>' -VsInstallPath '<destination VS>'
```

This also requires the installed matching companion CSV/provenance, unchanged destination host/
SAC/dependency hashes and correct final codeBases in all four finalized configs. Missing provider
or incomplete finalization fails; a successful consumer build is not proof of a complete VS
insertion.

## Actual local destination proof

A complete consumer VSIX built with an intentionally nonexistent producer SDK path. The
configuration-only companion SWIX also compiled with exactly two files and no foreign DLL/NGEN
ownership. The **actual installed VS finalizer's config-only task** processed that group and the
four candidate templates solely in scratch. Private/SAC directories were junctions to the actual
VS-owned files; no installed configs or NGEN registrations were changed.

| Check | Observed result |
|---|---|
| Missing companion | Destination preflight failed |
| Four finalized configs | Full destination closure codeBases verified |
| x64/x86 real C++/NMake replay | Both completed normal owned telemetry initialization/save |
| Actual telemetry location | `Common7\IDE\PrivateAssemblies`, resolving to the real VS-owned DLL, not MSBuild's fallback |
| SDK identity | File 18.4.50.56123, MVID `101331d0-098b-4945-9b5a-be8a890e1c67` |
| Current OTel closure | Consolidated **1.15.3.0** and bridge **1.0.0.0**, derived from destination policy |
| Untimed explicit closure run | All **27** `Assembly.Load` identities matched owner paths/MVIDs; all 27 corresponding native images mapped |
| Normal-policy transport | Legacy `aivortex`/`collector`; OTel disabled, no retained preparation failure; no flight/consent override |
| Rebuilt core native images | Zero mapped; not a matched optimized-core performance comparison |

The replay is the recorded Unreal-style NMake **echo fallback**, not full Unreal compilation.
The explicit closure test proves binding/native acceptance, not naturally activated OTel or
an Appx task-created worker session. The unchanged session probe is nonshipping and appears
only in untimed runs. The provider used here is a local artifact, not completed VS producer
integration.

## Remaining work and metrics

The fixed build-agent SDK pin is removed. **VS-owned companion production/IDE selection and
owner NGEN context authoring are still required**; no VS branch/insertion/pipeline was changed
or manually queued by this investigation. Coordinator binding policy is checked, but its
session lifecycle and reusable UWP workers were not re-benchmarked.

No performance or footprint win is claimed. Unsigned/test-sign core behavior and the unrelated
GAC Utilities fallback-JIT issue are not changed. New contexts may add footprint even though
the runtime now selects the same physical SDK. Before interpreting VS performance, verify
matched accepted core/SDK native coverage in control and candidate, then time the same real
workload without observers while including normal save/shutdown work.
