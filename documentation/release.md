# MSBuild release process

This is a description of the steps required to release MSBuild. The complete operational checklist is in [release-checklist.md](./release-checklist.md).

MSBuild ships in both Visual Studio (monthly) and the .NET SDK (quarterly). Each monthly VS release gets its own `vs*` branch, final branding, and VS insertion. The checklist is organized into 6 timeline-gated phases (0–5) with explicit triggers and ordering.

## How MSBuild releases flow into VS

MSBuild is a **component** inserted into Visual Studio. VS ships monthly; MSBuild must branch and prepare its bits **before** VS is ready to take them.

* BRANCH_SNAP_DATE — Phases 1–3"
  - Create vs* branch from main
  - Bump main to NEXT version
  - DARC channel & subscription setup
* Insertion → VS main
  - vs* builds auto-insert into VS main
* Before INSIDERS_SNAP_DATE — Phase 4
  - OptProf bootstrap
  - Release branch bits land in VS main
* VS snaps
  - VS snaps main → rel/insiders
  - VS promotes rel/insiders → rel/stable
* VS_SHIP_DATE — Phase 5
  - Publish packages to nuget.org
  - Tag release, update docs

The [VS insertion pipeline](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=24295) controls the routing:
- MSBuild `main` → VS `main` (daily canary builds)
- MSBuild `vs*` release branch → VS `main` (replaces `main` → `main` after branch snap)

VS handles the progression from `main` → `rel/insiders` → `rel/stable` on its own schedule. MSBuild's responsibility is to have final-branded bits in VS `main` before `INSIDERS_SNAP_DATE`.

In [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml), the `TargetBranch` parameter sets `InsertTargetBranch`. The default `auto` currently resolves to VS `main`; verify the branch-local pipeline when servicing an older release. For a one-off servicing insertion, select `rel/insiders` or `rel/stable` explicitly and select the intended official MSBuild resource build. Changes to automatic routing or OptProf routing require a separate reviewed pipeline change.

## Official-build artifact contract

The official pipeline, [`.vsts-dotnet.yml`](../.vsts-dotnet.yml), and the
[experimental pipeline](../azure-pipelines/.vsts-dotnet-exp-perf.yml) share the
[Windows build job](../azure-pipelines/.vsts-dotnet-build-jobs.yml). Package production
and signing are unchanged: Arcade repacks shipping packages into `Release` and
`PreRelease` variants and signs packages recursively before publication.

| Payload / artifact | Publication owner | Consumer / purpose |
|---|---|---|
| Flat per-build packages in `PackageArtifacts` | Arcade V3 `Publish` / `PushToBuildStorage` | Manifest-listed shipping and nonshipping assets for Darc; normal and experimental VS insertion download only the flat `Microsoft.Build.*`, `Microsoft.NET.StringTools*`, and `VS.ExternalAPIs.*` packages. Nonshipping insertion packages are not replaced by final release packages. |
| `ReleasePackages` (pipeline artifact) | Windows job's 1ES output, from the existing signed `packages/<configuration>/Release` folder | Release-team handoff for nonexperimental, real-signed builds only. Publishes the whole folder with relative paths intact, without rebuilding or repacking. Not emitted for experimental builds, even if real signing is selected, or for test/unsigned builds. |
| `AssetManifests`, `BlobArtifacts`, `PdbArtifacts` | Arcade V3 `Publish` | Asset registry registration, Darc asset/symbol publishing, and provenance. Arcade uploads each manifest directly to `AssetManifests`; there is no second Windows-job manifest upload. |
| `ReleaseConfigs` and `BlobArtifacts/MergedManifest.xml` | Arcade asset-registry job | BAR build ID, default channels, stability, and merged asset metadata linking source/build to published assets. Keep these for audit and downstream publishing. |
| VS payload drop (`Upload VSTS Drop`) and `xsd` | MicroBuild drop task and Windows job's 1ES output | VS insertion payload and schema-update script. The drop task's display name is a consumer contract. |
| OptProf profiling-input drop, `MicroBuildOutputs`, `VSSetup` | Windows job's 1ES outputs, only with OptProf enabled | DartLab consumes bootstrapper information, runsettings, metadata, and profiling inputs. Each hook downloads only its required files, not all official artifacts. |
| `logs` (pipeline artifact), post-build logs, and source-build outputs | Windows job and Arcade source-build/post-build jobs | Build/signing/publishing diagnostics and source-build dependency assets; retained. The `logs` name and relative paths are unchanged, and publication runs even on failure. |

### Validation and compatibility

The Windows job checks that the final package folder is nonempty and invokes
Arcade's `SigningValidation` task against that folder **before** staging and
publishing `ReleasePackages`. A failure fails the build job and prevents release
publication, BAR registration, and downstream publishing. The existing
`enableSigningValidation` switch controls signature validation here and in the
unchanged post-build check of flat `PackageArtifacts`; the nonempty-payload check
always runs. Validation logs remain in `logs`.
The payload check also records each final package's relative path and SHA-256 in
`logs/ReleasePackages.sha256` before staging; release retrieval can compare the
downloaded bytes with this build-time inventory.

`ReleasePackages` is a retrieval contract, not approval to release: downstream
validation and publishing must also succeed. Require the intended shipped source
commit, successful **non-skipped** final-package validation, and release-team
approval before publishing to nuget.org. The release checklist records
build/artifact URLs, source SHA, BAR build ID, and package hashes rather than
relying on payload copies for traceability.

The old full-tree `PackageArtifacts` upload is removed. There are no nested
`Shipping`, `NonShipping`, `Release`, or `PreRelease` package copies. Per-build
packages remain at the artifact root; final packages move to `ReleasePackages`.
The generated `PreRelease` repacks are not published: they are neither
manifest-listed dependency assets nor the final release handoff, and the
investigated insertion/OptProf paths do not use them. Consumers of the old nested
layout must migrate; main does not retain a compatibility archive for speculative
external consumers. Package production/signing itself is unchanged.

V3 remains the official-build publishing protocol. V4 would change manifest
locations, package staging, source-build registration, and post-build/insertion
downloads without eliminating the need to validate the distinct final-package
payload. A repo-owned validation step is simpler than that migration or a fork of
mirrored `eng/common` templates. Arcade's major version is independent of its
publishing protocol: the interfaces used here also exist in the Arcade 12 update
in [#15289](https://github.com/dotnet/msbuild/pull/15289); no SDK pin, downgrade, or
mirrored-template modification is required.

### OptProf dependencies

In [`.opt-prof.yml`](../.opt-prof.yml), machine configuration needs
`MicroBuildOutputs/BootstrapperInfo.json` and `VSSetup` runsettings; test execution
needs its own local copy of the runsettings; finalization needs `VSSetup` metadata.
These hooks may run on different agents, so the targeted runsettings downloads
are intentional, not redundant.

The external OptProf and DartLab single-runsettings templates forward execution
to DartLab's `Deploy-TestRun.ps1`. It reads the runsettings, then resolves test
containers from its `TestStores`. Arcade's `GetRunSettingsSessionConfiguration`
generates stores for the MSBuild profiling-input drop and the VS test drop
identified by the bootstrapper, not local official-build package artifacts.
Consequently the execution hook downloads only `VSSetup` runsettings. Keep the
profiling-input drop, bootstrapper, metadata, and VS payload generation intact.

### Evidence and service acceptance

The publication overlap was observed in successful real-signed official
[build 15712807](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=15712807)
(source `b879db1322ae7b3553e96d1490a19eee16a81e8a`, BAR build `336915`):
build log 129 uploads flat packages and `Windows_NT.xml` through Arcade; logs
176 and 178 upload the full package tree and the same manifest through YAML.
That run predates this change and is not acceptance evidence for the new layout.

Before accepting the new contract, run a real-signed official build and record
package identities against the Windows asset manifest, relative paths, and
SHA-256 equality between `logs/ReleasePackages.sha256` and downloaded `ReleasePackages`;
successful final-package signing validation;
VS insertion using the flat packages; BAR registration, Darc publishing and
downstream flow; and release-team retrieval. Check experimental/test-signing
absence of `ReleasePackages` and OptProf enabled/disabled behavior as applicable.
Local YAML and staging checks do not substitute for these service-side checks.
These are validation gates for [#15260](https://github.com/dotnet/msbuild/issues/15260),
not deferred implementation.

Servicing rollout is separate: additive release-artifact PRs
[#15261](https://github.com/dotnet/msbuild/pull/15261) (`vs18.11`) and
[#15302](https://github.com/dotnet/msbuild/pull/15302) (`vs18.12`) do not depend on
this cleanup. Servicing branch inventories and backport decisions are outside
this main-only layout change; use the release checklist's combined SDK/VS
lifecycle rules when evaluating them separately.

## Public API

MSBuild uses package API compatibility validation against `PackageValidationBaselineVersion` in [`eng/Versions.props`](../eng/Versions.props). Reviewed exceptions are recorded in the projects' `CompatibilitySuppressions.xml` files.

The repository no longer contains `PublicAPI.Unshipped.txt` / `PublicAPI.Shipped.txt` files or a promotion step for them. If the build reports API-compat failures, follow step 3.4 of the [release checklist](./release-checklist.md#phase-3-bump-main--update-pipelines) to regenerate and review suppressions. Do not suppress a new compatibility regression merely to make the build pass.

## Major version extra update steps

Update major version of VS in

- [BuildEnvironmentHelper.cs](../src/Shared/BuildEnvironmentHelper.cs)
- [Constants.cs](../src/Shared/Constants.cs)
- [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs)
