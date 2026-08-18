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
### Prerequisites
- gh cli
- az cli
- darc cli

## Phase Summary

| Phase | Trigger | Key Actions |
|---|---|---|
| **0: Instantiate** | User-initiated | Validate inputs, create GitHub tracking issue |
| **1: Branch & Prepare** | `BRANCH_SNAP_DATE` | Create `vs*` branch, DARC channel setup (batched PR), merge-flow config, `VisualStudio.ChannelName` |
| **2: Bump Main** | Phase 1 branch exists | Branding PR in main (version bump, baseline, pipeline YAML) |
| **3: DARC Updates** | Phase 2 merged | Channel reassignment, subscription updates (batched PR), verification |
| **4: Final Branding** | 7 days before `INSIDERS_SNAP_DATE` | Public API promotion, `Stabilize-Release.ps1`, OptProf bootstrap, get final-branded bits into VS `main` before insiders snap |
| **5: Post-GA** | VS shipped (`VS_SHIP_DATE`) | nuget.org publish, docs, GitHub release, cleanup |

## DARC Batching

DARC write commands push to the [maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration) repo. Batch related changes into **one PR**:

1. Choose a branch name like `release/msbuild-{{THIS_RELEASE_VERSION}}`
2. Add `--configuration-branch <name> --no-pr` to every write command except the last
3. Last command: use `--configuration-branch <name>` without `--no-pr` to create the PR
4. Get the PR reviewed and merged

Read-only commands (`get-default-channels`, `get-subscriptions`, `get-channel`) don't need these flags.

## Executing a Phase

When asked to execute a specific phase:

1. Read the full phase from `documentation/release-checklist.md`
2. Verify the trigger condition is met (previous phases completed)
3. Execute steps in order — respect sequential/parallel annotations
4. For DARC commands: batch writes into one configuration PR per phase
5. Record all output URLs in the tracking issue's artifact table
6. Mark checkboxes as completed in the tracking issue

## Key Files

| File | Purpose |
|---|---|
| [`documentation/release-checklist.md`](../../../documentation/release-checklist.md) | **Operational checklist** — the source of truth |
| [`documentation/release.md`](../../../documentation/release.md) | Process description: final branding, public API, major version steps |
| [`eng/Versions.props`](../../../eng/Versions.props) | `VersionPrefix`, `PackageValidationBaselineVersion`, `BootstrapSdkVersion` |
| [`.config/git-merge-flow-config.jsonc`](../../../.config/git-merge-flow-config.jsonc) | Branch merge chain — update each release |
| [`azure-pipelines/vs-insertion.yml`](../../../azure-pipelines/vs-insertion.yml) | VS insertion pipeline — `AutoInsertTargetBranch` mappings |
| [`azure-pipelines/vs-insertion-experimental.yml`](../../../azure-pipelines/vs-insertion-experimental.yml) | Experimental insertion — `TargetBranch` parameter values |
| [`scripts/Stabilize-Release.ps1`](../../../scripts/Stabilize-Release.ps1) | Final branding automation (`-DryRun` to preview) |
| [`.vsts-dotnet.yml`](../../../.vsts-dotnet.yml) | Build pipeline — `VisualStudio.ChannelName` setting |

## Validation

After completing all phases, verify:

1. Branch `vs{{THIS_RELEASE_VERSION}}` exists and has final branding
2. Main has `VersionPrefix` = `{{NEXT_VERSION}}.0`
3. DARC: main → `VS {{NEXT_VERSION}}` channel, release branch → `VS {{THIS_RELEASE_VERSION}}` channel
4. VS insertion PR merged
5. Packages published to nuget.org
6. GitHub release created with tag `v{{THIS_RELEASE_EXACT_VERSION}}`

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
