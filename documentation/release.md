# MSBuild release process

This is a description of the steps required to release MSBuild. The complete operational checklist is in [release-checklist.md](./release-checklist.md).

MSBuild ships in both Visual Studio (monthly) and the .NET SDK (quarterly). Each monthly VS release gets its own `vs*` branch, final branding, and VS insertion. The checklist is organized into 6 timeline-gated phases (0–5) with explicit triggers and ordering.

## Maintained branches

Update this list when cutting or retiring a branch.

| Branch | Maintained for (source) | Support end / retirement trigger |
|---|---|---|
| `vs15.9` | [Visual Studio 2017](https://learn.microsoft.com/lifecycle/products/visual-studio-2017) | April 2027 |
| `vs16.11` | [Visual Studio 2019](https://learn.microsoft.com/visualstudio/releases/2019/servicing-vs2019) | April 2029 |
| `vs17.14` | [Visual Studio 2022](https://learn.microsoft.com/visualstudio/releases/2022/servicing-vs2022) | January 2032 |
| `vs18.0` | [SDK 10.0.1xx](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs#supported-net-versions) | [November 14, 2028](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) |
| `vs18.9` | [SDK 10.0.4xx](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs#supported-net-versions) | [November 14, 2028](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) |
| `vs18.11` | [VS Stable](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs#stable-channel-support) | Next Stable promotion, unless selected as an LTSC baseline |
| `vs18.12` | [SDK 11.0.1xx](https://github.com/dotnet/dotnet/blob/release/11.0.1xx/src/msbuild/eng/Versions.props) | [November 9, 2028 (planned GA lifecycle)](https://github.com/dotnet/core/blob/main/release-notes/11.0/README.md) |
| `vs18.13` | [Visual Studio 18.13](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs#stable-channel-support) | Next Stable promotion after 18.13, unless selected as an LTSC baseline |
| `main` | [Development](../eng/Versions.props) | No fixed end date |

Check Stable promotions and the published [LTSC baseline](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs) at each audit; confirm planned SDK deadlines at GA.

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

V3 container download filters match artifact-prefixed paths, so insertion uses
`PackageArtifacts/Microsoft.Build.*.nupkg` (and the corresponding StringTools and
ExternalAPIs patterns), not bare filename patterns.

### Validation and compatibility

The Windows job checks that the final package folder is nonempty and invokes
Arcade's `SigningValidation` task against that folder **before** staging and
publishing `ReleasePackages`. A failure fails the build job and prevents release
publication, BAR registration, and downstream publishing. Official builds cannot
disable final-package or post-build signing validation. Validation logs remain
in `logs`.
The payload check also records each final package's relative path and SHA-256 in
`logs/ReleasePackages.sha256` before staging; release retrieval can compare the
downloaded bytes with this build-time inventory.

`ReleasePackages` is a retrieval contract, not approval to release: downstream
validation and publishing must also succeed. Require the intended shipped source
commit, successful **non-skipped** final-package validation, and release-team
approval before publishing to nuget.org. The release checklist records
build/artifact URLs, source SHA, BAR build ID, and package hashes rather than
relying on payload copies for traceability.

`PackageArtifacts` contains per-build packages at its root, without nested
`Shipping`, `NonShipping`, `Release`, or `PreRelease` copies. Final packages are
in `ReleasePackages`; generated `PreRelease` repacks are not published. Consumers
of older nested layouts must use the flat per-build packages or `ReleasePackages`.

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

## Public API

MSBuild uses package API compatibility validation against `PackageValidationBaselineVersion` in [`eng/Versions.props`](../eng/Versions.props). Reviewed exceptions are recorded in the projects' `CompatibilitySuppressions.xml` files.

The repository no longer contains `PublicAPI.Unshipped.txt` / `PublicAPI.Shipped.txt` files or a promotion step for them. If the build reports API-compat failures, follow step 3.4 of the [release checklist](./release-checklist.md#phase-3-bump-main--update-pipelines) to regenerate and review suppressions. Do not suppress a new compatibility regression merely to make the build pass.

## Major version extra update steps

Update major version of VS in

- [BuildEnvironmentHelper.cs](../src/Shared/BuildEnvironmentHelper.cs)
- [Constants.cs](../src/Shared/Constants.cs)
- [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs)
