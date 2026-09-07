# Release phases and evidence

The [release checklist](../../../../documentation/release-checklist.md) owns the
operational steps. Read only the requested phase and its prerequisites. This
reference explains inputs and evidence; it is not authorization to execute every
phase, open tracking issues, or change service state.

## Overview

MSBuild is a component inserted into Visual Studio. Confirm the current release
schedule before deriving phase dates. See the [release process](../../../../documentation/release.md#how-msbuild-releases-flow-into-vs)
for the relationship between MSBuild preparation and VS branch snaps.

The insertion pipeline routes MSBuild branches to VS branches:
- `main` → VS `main` (daily canary)
- `vs*` release branch → VS `main` (replaces `main` → `main` after branch snap)

VS handles the progression from `main` → `rel/insiders` → `rel/stable` on its own schedule. MSBuild's responsibility is to have final-branded bits in VS `main` before the insiders snap date.

Each monthly VS release produces:
- A new `vs*` branch from `main`
- Final branding on that branch
- A version bump in `main`
- DARC channel/subscription updates
- A VS insertion
- Post-GA publishing to nuget.org and docs

The process is organized into **6 timeline-gated phases** (0–5), each with an explicit trigger.

Explanation, status, and planning are read-only. For execution, establish the
specific phase and authorized writes first. If an essential decision or
authorization is unavailable in a noninteractive session, report that phase as
blocked rather than guessing, prompting indefinitely, or starting another phase.

## Required Inputs

Resolve only inputs required by the selected phase. Read existing values from
the tracking issue and relevant branch; version increments are policy decisions,
not arithmetic defaults.

| Input | Example | How to determine |
|---|---|---|
| `PREVIOUS_RELEASE_VERSION` | Major.minor | Previous release identified by the tracking issue and release policy, not simply the largest branch name |
| `PREVIOUS_RELEASE_EXACT_VERSION` | Major.minor.patch | Actual previously shipped version and tag; needed for Phase 5, not every earlier phase |
| `THIS_RELEASE_VERSION` | Major.minor | Requested release/tracking issue; current main is a cross-check only before its version bump |
| `NEXT_VERSION` | Major.minor | Explicitly selected next version; needed for channel rotation and main branding |
| `BRANCH_SNAP_DATE` | `YYYY-MM-DD` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when MSBuild branches `vs*` from main, insertion targets VS `main` |
| `INSIDERS_SNAP_DATE` | `YYYY-MM-DD` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when VS snaps `main` → `rel/insiders`; final-branded bits must be in VS `main` before this |
| `STABLE_SNAP_DATE` | `YYYY-MM-DD` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when VS promotes `rel/insiders` → `rel/stable` |
| `VS_SHIP_DATE` | `YYYY-MM-DD` | When VS ships publicly (GA) — triggers post-release tasks |
| `PACKAGE_VALIDATION_BASELINE_VERSION` | Published prerelease package version | Resolve in Phase 3 using [baseline provenance](package-validation-baseline.md) |

Dates come from the applicable VS schedule, not a copied release example. Resolve
`THIS_RELEASE_EXACT_VERSION` from shipped artifacts in Phase 5; it is intentionally
unknown during initial planning.

### Prerequisites
- Use only tools needed for the selected phase: `gh`, authenticated Azure DevOps
  reads through an available tool, and a supported DARC CLI for DARC actions.
- Discover current-session capabilities and inspect installed command help.
  Missing tools, access, or an unsupported DARC version are blockers to report;
  do not initialize tools or refresh credentials automatically.

## Phase Summary

| Phase | Trigger | Key Actions |
|---|---|---|
| **0: Instantiate** | User-initiated | Validate inputs, create GitHub tracking issue |
| **1: Branch & Prepare** | `BRANCH_SNAP_DATE` | Create `vs*` branch, DARC channel setup (batched PR), **audit all `vs*` branches for retirement**, `VisualStudio.ChannelName` |
| **2: DARC Subscription Updates** | Phase 1 branch exists (`vs*` created) | Retarget `main`-targeting subs + VMR backflow to next channel, retired-branch cleanup (batched PR), Arcade verify |
| **3: Bump Main** | Phase 2 merged | Branding PR in `main` (`VersionPrefix` → next, ApiCompat baseline, refresh OptProf baseline) |
| **4: Final Branding** | 7 days before `INSIDERS_SNAP_DATE`, subject to current schedule | API/reference and package-compatibility review, conditional OptProf bootstrap, schedule-dependent approval, observe insertion into VS `main` |
| **5: Post-GA** | VS shipped (`VS_SHIP_DATE`) | Resolve the exact shipped version (SDK-coupled? SDK wins over VS `rel/stable`), nuget.org publish, docs, GitHub release, Change Waves Learn sync, retro |

## Configuration and branch retirement

For Phases 1 and 2, use [configuration and lifecycle](configuration-and-lifecycle.md).
It preserves the batching protocol, forward/backflow distinction, and combined
SDK/VS support rule. Retirement requires a current inventory and dated lifecycle
evidence; a historical channel or missing subscription alone is not proof of EOL.

## Executing a Phase

When asked to execute a specific phase:

1. Read the requested phase from the [checklist](../../../../documentation/release-checklist.md).
2. Verify the trigger condition is met (previous phases completed)
3. Execute only the authorized steps in order; independent reads may run together.
4. For DARC commands: batch writes into one configuration PR per phase
5. Record resulting URLs and states locally; update the tracking issue only if that write is authorized.
6. Mark completed steps from observed results, not from commands merely having been attempted.
7. For Phase 4.7, establish the version actually in VS Insiders and the relevant preview SDK before selecting the Change Waves source branch. If they differ, resolve the intended audience rather than blindly choosing the numerically larger version. The docs target is `MicrosoftDocs/visualstudio-docs-pr`, `docs/msbuild/change-waves.md`; a historical example is MicrosoftDocs/visualstudio-docs-pr#15662. Publishing that PR is a separate authorized write.

## Key Files

| File | Purpose |
|---|---|
| [Release checklist](../../../../documentation/release-checklist.md) | Operational phase steps |
| [Release process](../../../../documentation/release.md) | Branding, current API validation, major-version considerations |
| [Change Waves](../../../../documentation/wiki/ChangeWaves.md) | Read the version corresponding to the actual intended shipped/preview audience |
| [MSBuild Change Waves Learn page](https://learn.microsoft.com/visualstudio/msbuild/change-waves) | Public docs target to [`MicrosoftDocs/visualstudio-docs-pr`](https://github.com/MicrosoftDocs/visualstudio-docs-pr) (`docs/msbuild/change-waves.md`) |
| [Versions.props](../../../../eng/Versions.props) | Version, prerelease label, package baseline, and bootstrap SDK at the relevant ref |
| [VS insertion](../../../../azure-pipelines/vs-insertion.yml) | Inspect `TargetBranch` and `InsertTargetBranch` in current main; older branches may differ |
| [Experimental insertion](../../../../azure-pipelines/vs-insertion-experimental.yml) | Separate insertion path; inspect its current parameters |
| [Baseline helper](../../../../scripts/Get-PackageValidationBaseline.ps1) | Candidate discovery with the limitations in the baseline reference |
| [OptProf helper](../../../../scripts/Get-LatestOptProfDrop.ps1) | Inspect current pipeline identity, successful drop provenance, and query coverage |
| [Build entry](../../../../.vsts-dotnet.yml) | `OptProfBaselineDrop` seed for new branches |
| [Build jobs](../../../../azure-pipelines/.vsts-dotnet-build-jobs.yml) | `VisualStudio.ChannelName`; check whether the selected phase requires a change |

## Validation

Use only evidence relevant to the completed phase:

1. Phase 0: intended tracking issue and agreed inputs, if creation was authorized.
2. Phase 1: release branch points to the intended commit; channel setup and retirement decisions are recorded.
3. Phase 2: approved configuration has the intended main/release mappings without stealing servicing flow.
4. Phase 3: main has the selected next version and a published, release-reachable package baseline.
5. Phase 4: API/package compatibility evidence and the intended VS insertion; track any required docs sync.
6. Phase 5: shipped-version/build provenance, published packages/docs, and the tag/release actually created by the authorized owners.

## Error Recovery

- **Branch already exists**: Release was partially started — check the tracking issue for progress
- **DARC channel already exists**: Query and compare configuration; `add-channel` rejects duplicates. Skip an already satisfied change, not an unexplained failure.
- **OptProf build fails**: Inspect the failure. Only a missing/stale profiling seed justifies the corresponding recovery step; do not cancel or rerun unrelated failures.
- **DARC configuration PR conflicts**: Inspect both sides and branch ownership. Do not force-push shared work or treat an update conflict as permission to overwrite.

## Major Version Releases

If `NEXT_VERSION` changes major version, plan the necessary changes before the
affected branding/build phase, not after publishing. See [release.md](../../../../documentation/release.md) for:
- `src/Shared/BuildEnvironmentHelper.cs` — VS major version constants
- `src/Shared/Constants.cs` — version constants
- `src/Framework/Telemetry/TelemetryConstants.cs` — telemetry version
