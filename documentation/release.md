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
| `vs18.11` | [LTSC 18.11](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs#long-term-servicing-channel-ltsc-support) | November 2028 |
| `vs18.12` | [SDK 11.0.1xx](https://github.com/dotnet/dotnet/blob/release/11.0.1xx/src/msbuild/eng/Versions.props) | [November 9, 2028 (planned GA lifecycle)](https://github.com/dotnet/core/blob/main/release-notes/11.0/README.md) |
| `vs18.13` | [Visual Studio 18.13](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs#stable-channel-support) | Next Stable promotion after 18.13, unless selected as an LTSC baseline |
| `vs18.14` | [Visual Studio 18.14](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs#stable-channel-support) | Next Stable promotion after 18.14, unless selected as an LTSC baseline |
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
  - Verify the frozen OptProf pin and retention
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

[`.vsts-dotnet.yml`](../.vsts-dotnet.yml) and [`.vsts-dotnet-exp-perf.yml`](../azure-pipelines/.vsts-dotnet-exp-perf.yml) share [`azure-pipelines/.vsts-dotnet-build-jobs.yml`](../azure-pipelines/.vsts-dotnet-build-jobs.yml) and use Arcade Publishing V4 (`PublishingVersion=4` in [`eng/Publishing.props`](../eng/Publishing.props)).

| Artifact | Producer | Contents / consumer |
|---|---|---|
| `Windows_NT_Artifacts` | `Windows_NT` (pipeline artifact) | Arcade V4 staged tree: `packages/shipping/*.nupkg`, `packages/nonshipping/*.nupkg`, `assets/assets/symbols/.../*.symbols.nupkg`, `pdbs/Windows_NT/**/*.pdb`, `manifests/Windows_NT.xml`. Consumed by `Asset_Registry_Publish`, post-build `Signing Validation`, Darc/Maestro promotion, and VS insertion (`vs-insertion.yml`, `vs-insertion-experimental.yml`). |
| `AssetManifests` | `Asset_Registry_Publish` (pipeline artifact) | `MergedManifest.xml` (PublishingVersion 4), consumed by Maestro promotion. |
| `ReleaseConfigs` | `Asset_Registry_Publish` (pipeline artifact) | `ReleaseConfigs.txt` (BAR build ID, default channels, stability) and symbol exclusions. |
| `ReleasePackages` | `Windows_NT` (pipeline artifact; nonexperimental, real-signed builds on `main` and branches cut after this change) | Repacked signed final-version shipping `.nupkg` files for nuget.org release handoff. Not listed in the V4 manifest; validated in-job with Arcade `SigningValidation` and hashed to `logs/ReleasePackages.sha256` before staging. Requires `DotNetFinalVersionKind` to be empty (not supported on older final-branded branches). |
| `logs` | `Windows_NT` (build artifact, `always()`) | Build logs and binlogs; on nonexperimental real-signed builds also includes `signcheck*.log` and `ReleasePackages.sha256`. Kept as a build artifact so job retries can append without name collisions. |
| `xsd` | `Windows_NT` (pipeline artifact) | MSBuild schema files + `Update-MSBuildXsds.ps1` for VS insertion. |
| `VSSetup` | `Windows_NT` (build artifact; OptProf or `publishVSSetup`) | VSIXes and VS insertion manifests (also uploaded to VSTS Drop). |
| `MicroBuildOutputs` | `Windows_NT` (build artifact; OptProf only) | `BootstrapperInfo.json`. |

Legacy `PackageArtifacts` (including nested `Shipping`, `NonShipping`, `Release`, and `PreRelease` copies) is removed on `main`; PR CI (`FullReleaseOnWindows`) still runs `-publish` to validate V4 local staging without uploading V3 package/manifest artifacts. Until the first V4 official build on `main` succeeds, `vs-insertion.yml` on `main` cannot insert older pre-V4 builds (which lack `Windows_NT_Artifacts`); for manual insertion of a pre-V4 build, run the insertion pipeline from a pre-V4 commit or release branch. Reverting the V4 migration commit restores V3 for subsequent builds, though builds already produced under V4 cannot be inserted by reverted V3 insertion YAML.

## OptProf inputs

Official `main` builds use centrally collected VS `main` profiles. Before cutting a release branch, select a compatible `OptProfBaselineDrop` and ensure it remains retained for the release's support and rebuild lifetime. Release branches keep that fixed baseline during servicing. `OptProfDropName` can override either default.

## Public API

MSBuild uses package API compatibility validation against `PackageValidationBaselineVersion` in [`eng/Versions.props`](../eng/Versions.props). Reviewed exceptions are recorded in the projects' `CompatibilitySuppressions.xml` files.

The repository no longer contains `PublicAPI.Unshipped.txt` / `PublicAPI.Shipped.txt` files or a promotion step for them. If the build reports API-compat failures, follow step 3.5 of the [release checklist](./release-checklist.md#phase-3-bump-main--update-pipelines) to regenerate and review suppressions. Do not suppress a new compatibility regression merely to make the build pass.

## Major version extra update steps

Update major version of VS in

- [BuildEnvironmentHelper.cs](../src/Shared/BuildEnvironmentHelper.cs)
- [Constants.cs](../src/Shared/Constants.cs)
- [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs)
