---
name: release
description: 'Orchestrate an MSBuild release: create the tracking issue, branch, configure DARC channels and subscriptions, bump version in main, final-brand the release branch, insert into VS, and publish post-GA. Covers the full monthly release lifecycle aligned with VS shipping cadence.'
argument-hint: 'Specify which phase to execute (0-5) and provide required inputs (THIS_RELEASE_VERSION, NEXT_VERSION, etc.)'
---

# MSBuild Release Orchestration

This skill guides an agent through the MSBuild release process defined in [`documentation/release-checklist.md`](../../../documentation/release-checklist.md). The checklist is the single source of truth — this skill provides context on how to execute it.

## Overview

MSBuild is a **component** that gets inserted into Visual Studio. VS ships monthly; MSBuild must branch and prepare its bits **before** VS is ready to take them. See the [release process doc](../../../documentation/release.md#how-msbuild-releases-flow-into-vs) for the full timeline diagram.

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

> **Execution model**: This skill is designed for an **interactive Copilot session**. The agent walks through each phase step-by-step, but every command that modifies state (git push, DARC writes, pipeline changes, PR creation) requires **user approval** before execution. Read-only queries (DARC get-*, git log, etc.) can run without approval.

## Required Inputs

Before starting any phase, ensure you have these values (the user must provide them — version increments are irregular and cannot be computed):

| Input | Example | How to determine |
|---|---|---|
| `PREVIOUS_RELEASE_VERSION` | `18.5` | Previous entry in the merge-flow chain |
| `THIS_RELEASE_VERSION` | `18.6` | Current `VersionPrefix` in `eng/Versions.props` (drop `.0`) |
| `NEXT_VERSION` | `18.7` | User-provided — not computable from current version |
| `BRANCH_SNAP_DATE` | `2026-04-08` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when MSBuild branches `vs*` from main, insertion targets VS `main` |
| `INSIDERS_SNAP_DATE` | `2026-04-22` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when VS snaps `main` → `rel/insiders`; final-branded bits must be in VS `main` before this |
| `STABLE_SNAP_DATE` | `2026-05-06` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when VS promotes `rel/insiders` → `rel/stable` |
| `VS_SHIP_DATE` | `2026-05-12` | When VS ships publicly (GA) — triggers post-release tasks |
| `PACKAGE_VALIDATION_BASELINE_VERSION` | `18.7.0-preview-26230-02` | See [How to determine `PACKAGE_VALIDATION_BASELINE_VERSION`](#how-to-determine-package_validation_baseline_version) below — non-trivial: most "obvious" picks are wrong. |

### How to determine `PACKAGE_VALIDATION_BASELINE_VERSION`

**The value is the latest `{{THIS_RELEASE_VERSION}}.0-preview-NNNNN-NN` MSBuild package that is both:**

1. **Published on the public [dotnet-tools feed](https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet-tools)** — this is the feed `darc publish` pushes to and that ApiCompat restores baselines from. If the version isn't here, ApiCompat fails with `NU1102`.
2. **Produced from a commit reachable from `vs{{THIS_RELEASE_VERSION}}`** — i.e. a `vs{{THIS_RELEASE_VERSION}}` commit prior to [stabilization (Phase 4.2)](../../../documentation/release-checklist.md#phase-4-final-branding--vs-insertion), or the `main` commit `vs{{THIS_RELEASE_VERSION}}` was branched from.

**Two tempting wrong answers — and why they're wrong:**

| Wrong pick | Why it fails |
|---|---|
| ❌ The `{{THIS_RELEASE_VERSION}}.X` package that actually ships in VS (e.g. `18.7.1`) | After Phase 4.2 `Stabilize-Release.ps1` runs, builds become **final-versioned**. So such package is not resolvable from public CI. |
| ❌ Blindly the most recent `{{THIS_RELEASE_VERSION}}.0-preview-*` on `dotnet-tools` | After `vs{{THIS_RELEASE_VERSION}}` branches, `main` keeps producing `{{THIS_RELEASE_VERSION}}.0-preview-*` until **this** main-bump PR merges — so the most recent feed entries may be `{{NEXT_VERSION}}`-content builds wearing `{{THIS_RELEASE_VERSION}}` branding. Picking one drifts the API baseline forward and silently hides real compat breaks. |

**Procedure:**

**Determinized (preferred):** run the helper, which does all of the below mechanically (requires `az login` with devdiv access):

```
pwsh ./scripts/Get-PackageValidationBaseline.ps1 -ThisReleaseVersion {{THIS_RELEASE_VERSION}}
# -> prints e.g. 18.9.0-preview-26330-01
```

It computes `git merge-base origin/main origin/vs{{THIS_RELEASE_VERSION}}`, finds the matching successful build in pipeline 9434, derives the package version from the OfficialBuildId, and verifies it on the dotnet-tools feed. The manual procedure below is the fallback / explanation of what the script does:

1. Open [MSBuild official build pipeline 9434](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434).
2. Filter runs to branch `vs{{THIS_RELEASE_VERSION}}`. Find the run that final-branded the release (it produces `{{THIS_RELEASE_VERSION}}.X` — no `-preview-` — corresponding to the commit that ran `Stabilize-Release.ps1`; see [Phase 4.2 + 4.3](../../../documentation/release-checklist.md#phase-4-final-branding--vs-insertion)). Anything successful **before** that on `vs{{THIS_RELEASE_VERSION}}` is a candidate.
3. If `vs{{THIS_RELEASE_VERSION}}` has no successful pre-stabilization preview runs (common — the branch sees little churn before stabilization), fall back to the most recent successful `main` run whose commit is the branch-point ancestor: \
`git merge-base origin/main origin/vs{{THIS_RELEASE_VERSION}}` gives the SHA — find a `main` run at or before that SHA in pipeline 9434.
4. Read the package version from that run's `Pack` step output: `{{THIS_RELEASE_VERSION}}.0-preview-NNNNN-NN` (example: `18.7.0-preview-26230-02`).
5. Verify the exact version is on the [dotnet-tools feed](https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet-tools) (search `Microsoft.Build`). If not, fall back to the next-older eligible run.
6. Use that version. Do **not** include any `+sha` suffix.

### Prerequisites
- gh cli
- az cli
- darc cli

## Phase Summary

| Phase | Trigger | Key Actions |
|---|---|---|
| **0: Instantiate** | User-initiated | Validate inputs, create GitHub tracking issue |
| **1: Branch & Prepare** | `BRANCH_SNAP_DATE` | Create `vs*` branch, DARC channel setup (batched PR), merge-flow config, `VisualStudio.ChannelName` |
| **2: DARC Subscription Updates** | Phase 1 branch exists (`vs*` created) | Retarget `main`-targeting subs + VMR backflow to next channel, retired-branch cleanup (batched PR), Arcade verify |
| **3: Bump Main** | Phase 2 merged | Branding PR in `main` (`VersionPrefix` → next, ApiCompat baseline, refresh OptProf baseline) |
| **4: Final Branding** | 7 days before `INSIDERS_SNAP_DATE` | Public API promotion, `Stabilize-Release.ps1`, OptProf bootstrap, get final-branded bits into VS `main` before insiders snap |
| **5: Post-GA** | VS shipped (`VS_SHIP_DATE`) | nuget.org publish, docs, GitHub release, cleanup |

## DARC Batching

DARC write commands push to the [maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration) repo. Batch related changes into **one PR**:

1. Choose a branch name like `release/msbuild-{{THIS_RELEASE_VERSION}}`
2. Add `--configuration-branch <name> --no-pr` to every write command except the last
3. Last command: use `--configuration-branch <name>` without `--no-pr` to create the PR
4. Get the PR reviewed and merged

Read-only commands (`get-default-channels`, `get-subscriptions`, `get-channel`) don't need these flags.

**Non-interactive (`-q`).** `darc add-default-channel` / `add-subscription` prompt `y/n` when the target branch does not exist yet (e.g. pre-creating the `vs{{NEXT_VERSION}}` mapping in Phase 1.2c, or adding the new `vs{{THIS_RELEASE_VERSION}}` backflow in Phase 2). Console input is redirected in an agent session, so the prompt **fails the command** — always pass `-q` for these "branch doesn't exist yet" writes.

**Phase 2 — what moves vs. what stays.** When rotating `main` to the next channel, retarget **only** the subscriptions whose **target branch is `main`** (`dotnet/dotnet @ main`, `dotnet/fsharp @ main`). **Never** retarget a subscription that targets a VMR servicing/release branch (`dotnet/dotnet @ release/*`) — that includes the SDK band paired with the new `vs{{THIS_RELEASE_VERSION}}` branch and any `.NET-next` preview band (`release/*-preview*`). Those stay on `VS {{THIS_RELEASE_VERSION}}` so the new release branch owns their downstream flow; moving them steals it. (This bit the 18.9 release: the band and preview subs were moved and had to be reverted.)

**Phase 2 — VMR backflow rotation (easy to miss).** Backflow (`dotnet/dotnet → msbuild`, source-enabled) must rotate too **when the new `vs{{THIS_RELEASE_VERSION}}` is paired with an SDK band** (skip for a VS-only release): repoint the `→ main` backflow to the **next** SDK band channel (`.NET <NEXT_BAND> SDK`, the channel `dotnet/dotnet @ main` publishes to), and **add** a backflow from the **outgoing** band channel into the new `vs{{THIS_RELEASE_VERSION}}` branch (mirror the prior release branch's backflow, e.g. `vs18.0 ← .NET 10.0.1xx SDK`). See checklist steps 2.2b / 2.3f / 2.3g.

## Executing a Phase

When asked to execute a specific phase:

1. Read the full phase from `documentation/release-checklist.md`
2. Verify the trigger condition is met (previous phases completed)
3. Execute steps in order — respect sequential/parallel annotations
4. For DARC commands: batch writes into one configuration PR per phase
5. Record all output URLs in the tracking issue's artifact table
6. Mark checkboxes as completed in the tracking issue
7. In **Phase 5**: if `documentation/wiki/ChangeWaves.md` is changed for this release, update the public Learn page at `https://learn.microsoft.com/en-us/visualstudio/msbuild/change-waves?view=visualstudio` (or track the required update with an explicit issue/link in the release artifacts).

## Key Files

| File | Purpose |
|---|---|
| [`documentation/release-checklist.md`](../../../documentation/release-checklist.md) | **Operational checklist** — the source of truth |
| [`documentation/release.md`](../../../documentation/release.md) | Process description: final branding, public API, major version steps |
| [`documentation/wiki/ChangeWaves.md`](../../../documentation/wiki/ChangeWaves.md) | Source doc whose release-cycle updates may require a Learn page sync |
| [MSBuild Change Waves Learn page](https://learn.microsoft.com/en-us/visualstudio/msbuild/change-waves?view=visualstudio) | Public docs target to update/track during Phase 5 docs work |
| [`eng/Versions.props`](../../../eng/Versions.props) | `VersionPrefix`, `PackageValidationBaselineVersion`, `BootstrapSdkVersion` |
| [`.config/git-merge-flow-config.jsonc`](../../../.config/git-merge-flow-config.jsonc) | Branch merge chain — update each release |
| [`azure-pipelines/vs-insertion.yml`](../../../azure-pipelines/vs-insertion.yml) | VS insertion pipeline — `AutoInsertTargetBranch` mappings |
| [`azure-pipelines/vs-insertion-experimental.yml`](../../../azure-pipelines/vs-insertion-experimental.yml) | Experimental insertion — `TargetBranch` parameter values |
| [`scripts/Stabilize-Release.ps1`](../../../scripts/Stabilize-Release.ps1) | Final branding automation (`-DryRun` to preview) |
| [`scripts/Get-PackageValidationBaseline.ps1`](../../../scripts/Get-PackageValidationBaseline.ps1) | Phase 3.2 — resolves `PackageValidationBaselineVersion` deterministically (merge-base → pipeline 9434 → dotnet-tools feed) |
| [`scripts/Get-LatestOptProfDrop.ps1`](../../../scripts/Get-LatestOptProfDrop.ps1) | Phase 3.3 — resolves the latest main OptProf drop (MSBuild-OptProf pipeline 17389) to refresh `OptProfBaselineDrop` in `.vsts-dotnet.yml` |
| [`.vsts-dotnet.yml`](../../../.vsts-dotnet.yml) | Build pipeline — `VisualStudio.ChannelName`, and `OptProfBaselineDrop` (hardcoded OptProf seed for new `vs*` branches) |

## Validation

After completing all phases, verify:

1. Branch `vs{{THIS_RELEASE_VERSION}}` exists and has final branding
2. Main has `VersionPrefix` = `{{NEXT_VERSION}}.0`
3. DARC: main → `VS {{NEXT_VERSION}}` channel, release branch → `VS {{THIS_RELEASE_VERSION}}` channel
4. VS insertion PR merged
5. Packages published to nuget.org
6. GitHub release created with tag `v{{THIS_RELEASE_EXACT_VERSION}}`
7. If `documentation/wiki/ChangeWaves.md` is changed, the corresponding Learn page update at `https://learn.microsoft.com/en-us/visualstudio/msbuild/change-waves?view=visualstudio` is completed or explicitly tracked

## Error Recovery

- **Branch already exists**: Release was partially started — check the tracking issue for progress
- **DARC channel already exists**: Safe to continue — `add-channel` is idempotent
- **`Stabilize-Release.ps1` says "already stabilized"**: Skip — idempotent
- **OptProf fails on first build**: Expected — that's why we use main's OptProf data as fallback
- **DARC configuration PR conflicts**: Rebase the configuration branch on `production` and force-push

## Major Version Releases

If `NEXT_VERSION` is a new major version (e.g., 18.x → 19.0), additional steps are needed after Phase 5. See [release.md](../../../documentation/release.md) for:
- `src/Shared/BuildEnvironmentHelper.cs` — VS major version constants
- `src/Shared/Constants.cs` — version constants
- `src/Framework/Telemetry/TelemetryConstants.cs` — telemetry version
