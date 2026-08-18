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
  - Final branding via `Stabilize-Release.ps1`
  - OptProf bootstrap
  - Final-branded bits land in VS main
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

The `AutoInsertTargetBranch` mapping in [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml) encodes which MSBuild branch maps to which VS branch.

## Final branding/versioning

To produce packages without a `-prerelease` suffix, we need to specify `<DotNetFinalVersionKind>release</DotNetFinalVersionKind>` (see the [Arcade versioning docs]). This is ideally done on the same line as the version specification so that it causes a Git merge conflict when merging to the next release's branch. See [#6902](https://github.com/dotnet/msbuild/pull/6902) for an example.

Run `scripts/Stabilize-Release.ps1` to automate this process. The script modifies `eng/Versions.props` to add `DotNetFinalVersionKind` and change `PreReleaseVersionLabel` from `preview` to `servicing`. Use `-DryRun` to preview changes before applying them.

[Arcade versioning docs]: https://github.com/dotnet/arcade/blob/31cecde14e1512ecf60d2d8afb71fd240919f4a8/Documentation/CorePackages/Versioning.md

## Public API

As of [#7018](https://github.com/dotnet/msbuild/pull/7018), MSBuild uses a Roslyn analyzer to ensure compatibility with assemblies compiled against older versions of MSBuild. The workflow of the analyzer is:

1. The analyzer keeps the `PublicAPI.Unshipped.txt` files updated.
2. New API surface goes into `PublicAPI.Unshipped.txt`.
3. At release time, we must manually promote the `Unshipped` public API to `Shipped`.

That is a new step in our release process for each formal release (including patch releases if they change API surface).

## Major version extra update steps

Update major version of VS in

- [BuildEnvironmentHelper.cs](../src/Shared/BuildEnvironmentHelper.cs)
- [Constants.cs](../src/Shared/Constants.cs)
- [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs)
