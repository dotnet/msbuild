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
| Nested package tree in `PackageArtifacts` | Windows job's legacy 1ES build-artifact output | Still required for Arcade V3 signing validation of final packages and compatibility with existing release retrieval. Includes `Shipping`, `NonShipping`, `Release`, and `PreRelease`; external consumers of these paths are not fully inventoried. This temporarily duplicates final packages in `ReleasePackages`. |
| `AssetManifests`, `BlobArtifacts`, `PdbArtifacts` | Arcade V3 `Publish` | Asset registry registration, Darc asset/symbol publishing, and provenance. Arcade uploads each manifest directly to `AssetManifests`; there is no second Windows-job manifest upload. |
| `ReleaseConfigs` and `BlobArtifacts/MergedManifest.xml` | Arcade asset-registry job | BAR build ID, default channels, stability, and merged asset metadata linking source/build to published assets. Keep these for audit and downstream publishing. |
| VS payload drop (`Upload VSTS Drop`) and `xsd` | MicroBuild drop task and Windows job's 1ES output | VS insertion payload and schema-update script. The drop task's display name is a consumer contract. |
| OptProf profiling-input drop, `MicroBuildOutputs`, `VSSetup` | Windows job's 1ES outputs | External DartLab/OptProf templates consume bootstrapper information, runsettings, metadata, and profiling inputs. Outputs already conditional on OptProf remain conditional. |
| `logs`, post-build logs, and source-build outputs | Windows job and Arcade source-build/post-build jobs | Build/signing/publishing diagnostics and source-build dependency assets; retained. |

`ReleasePackages` is a retrieval contract, not approval to release. The build job
publishes it before downstream validation finishes. Require successful signing
validation and publishing, the intended shipped source commit, and release-team
approval before publishing to nuget.org. The unchanged V3 signing-validation job
downloads `PackageArtifacts` and recursively validates its directory, including
the nested final packages. The release checklist records build/artifact URLs,
source SHA, BAR build ID, and downloaded hashes rather than relying on payload
copies for traceability.

### Remaining simplification and verification

[Issue #15260](https://github.com/dotnet/msbuild/issues/15260) tracks the remaining
work. Do not remove the nested archive until final-package signing validation has
a verified replacement and release-team/external `PreRelease` consumers have
been checked. The pinned Arcade post-build template accepts extra validation
arguments but has no hook to download `ReleasePackages` into its validation job;
overriding the package path alone would lose coverage. Do not hand-edit mirrored
`eng/common` templates to work around this.

The broad `ComponentBuildUnderTest` download in [`.opt-prof.yml`](../.opt-prof.yml)
also remains: the external OptProf entry template forwards this hook to test
execution, but the transitive DartLab deployment requirements have not been
fully verified. Explicit bootstrapper/runsettings/metadata downloads elsewhere
in that pipeline do not establish that those are its only dependencies.

The publication overlap was observed in successful real-signed official
[build 15712807](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=15712807)
(source `b879db1322ae7b3553e96d1490a19eee16a81e8a`, BAR build `336915`):
build log 129 uploads flat packages and `Windows_NT.xml` through Arcade; logs
176 and 178 upload the full package tree and the same manifest through YAML.
That run predates this change. Live downstream subscription inventory and
acceptance of the new artifact are not verified by repository inspection.

Before accepting the new contract, run a real-signed official build and record
package identities, relative paths, and SHA-256 equality between `ReleasePackages`
and `PackageArtifacts/Release`; successful final-package signing validation;
VS insertion using the flat packages; BAR registration, Darc publishing and
downstream flow; and release-team retrieval. Check experimental/test-signing
absence of `ReleasePackages` and OptProf enabled/disabled behavior as applicable.
Local YAML checks do not substitute for these service-side checks.

Servicing rollout is separate: additive release-artifact PRs
[#15261](https://github.com/dotnet/msbuild/pull/15261) (`vs18.11`) and
[#15302](https://github.com/dotnet/msbuild/pull/15302) (`vs18.12`) do not depend on
this cleanup. Evaluate other supported branches using the combined SDK/VS
lifecycle rules in the release checklist; do not backport broader cleanup merely
to provide release-artifact availability.

## Public API

MSBuild uses package API compatibility validation against `PackageValidationBaselineVersion` in [`eng/Versions.props`](../eng/Versions.props). Reviewed exceptions are recorded in the projects' `CompatibilitySuppressions.xml` files.

The repository no longer contains `PublicAPI.Unshipped.txt` / `PublicAPI.Shipped.txt` files or a promotion step for them. If the build reports API-compat failures, follow step 3.4 of the [release checklist](./release-checklist.md#phase-3-bump-main--update-pipelines) to regenerate and review suppressions. Do not suppress a new compatibility regression merely to make the build pass.

## Major version extra update steps

Update major version of VS in

- [BuildEnvironmentHelper.cs](../src/Shared/BuildEnvironmentHelper.cs)
- [Constants.cs](../src/Shared/Constants.cs)
- [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs)
