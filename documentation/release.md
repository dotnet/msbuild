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

Inspect [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml) on the relevant MSBuild branch for the actual routing. Current main resolves the `TargetBranch` parameter into `InsertTargetBranch`, defaulting to VS `main`; older release branches may differ.

## Public API

Current library projects use `GenerateReferenceAssemblySource` and
`EnablePackageValidation`, for example
[Microsoft.Build.Framework.csproj](../src/Framework/Microsoft.Build.Framework.csproj).
The shared reference-generation configuration is in
[src/Directory.Build.targets](../src/Directory.Build.targets), and
`PackageValidationBaselineVersion` is defined in
[eng/Versions.props](../eng/Versions.props). Inspect the requested release ref
because older branches may use different machinery.

Review the generated reference/API surface and package-compatibility results for
the release. There are no `PublicAPI.Unshipped.txt` or `PublicAPI.Shipped.txt`
files in current main to promote. Do not create them from a historical checklist.
For the next-version main bump, choose a published, release-reachable package
baseline using [baseline provenance](../.github/skills/release/references/package-validation-baseline.md).
Investigate compatibility failures before accepting any suppression; do not
silence an unintended API break by moving the baseline forward.

## Major version extra update steps

Update major version of VS in

- [BuildEnvironmentHelper.cs](../src/Shared/BuildEnvironmentHelper.cs)
- [Constants.cs](../src/Shared/Constants.cs)
- [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs)
