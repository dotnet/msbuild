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
| `PREVIOUS_RELEASE_VERSION` | `18.9` | Previous release branch version: the highest `vs*` version lower than `THIS_RELEASE_VERSION` (list candidates with `git branch -r --list 'origin/vs*'`). |
| `PREVIOUS_RELEASE_EXACT_VERSION` | `18.9.6` | The version the previous release actually shipped as. From the previous release's tracking issue, or `git tag --list 'v18.9.*'`. Used by Phase 5.3a. |
| `THIS_RELEASE_VERSION` | `18.10` | Current `VersionPrefix` in `eng/Versions.props` (drop `.0`) |
| `NEXT_VERSION` | `18.11` | User-provided — not computable from current version |
| `BRANCH_SNAP_DATE` | `YYYY-MM-DD` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when MSBuild branches `vs*` from main, insertion targets VS `main` |
| `INSIDERS_SNAP_DATE` | `YYYY-MM-DD` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when VS snaps `main` → `rel/insiders`; final-branded bits must be in VS `main` before this |
| `STABLE_SNAP_DATE` | `YYYY-MM-DD` | From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) — when VS promotes `rel/insiders` → `rel/stable` |
| `VS_SHIP_DATE` | `YYYY-MM-DD` | When VS ships publicly (GA) — triggers post-release tasks |
| `PACKAGE_VALIDATION_BASELINE_VERSION` | `18.10.0-1.26378.2` | See [How to determine `PACKAGE_VALIDATION_BASELINE_VERSION`](#how-to-determine-package_validation_baseline_version) below — non-trivial: most "obvious" picks are wrong. |

> Version examples above track the current cycle (`eng/Versions.props` `VersionPrefix` is `18.10.0`). Dates are intentionally shown as a format only — always read the real ones from the VS-Dates wiki.

### How to determine `PACKAGE_VALIDATION_BASELINE_VERSION`

**The value is the latest `{{THIS_RELEASE_VERSION}}.0-<label>.<shortDate>.<rev>` MSBuild package that is both:**

_`<label>` is `PreReleaseVersionLabel` from `eng/Versions.props` (currently `1`), so the versions look like `18.11.0-1.26426.2`. Do **not** assume the legacy `-preview-NNNNN-NN` shape — MSBuild no longer produces it._

1. **Published on the public [dotnet-tools feed](https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet-tools)** — this is the feed the official build publishes to and that ApiCompat restores baselines from. If the version isn't here, ApiCompat fails with `NU1102`.
2. **Produced from a commit reachable from `vs{{THIS_RELEASE_VERSION}}`** — i.e. a commit on `vs{{THIS_RELEASE_VERSION}}`, or the `main` commit `vs{{THIS_RELEASE_VERSION}}` was branched from.

**Two tempting wrong answers — and why they're wrong:**

| Wrong pick | Why it fails |
|---|---|
| ❌ The release-versioned `{{THIS_RELEASE_VERSION}}.X` package that ships in VS / on nuget.org | Since [#14277](https://github.com/dotnet/msbuild/pull/14277) release branches build and insert **prerelease** versions, exactly like `main`; the release-versioned variants are produced by NuGetRepack at manual publish time. They never exist on the public CI feed, so ApiCompat cannot restore them. |
| ❌ Blindly the most recent `{{THIS_RELEASE_VERSION}}.0-*` on `dotnet-tools` | After `vs{{THIS_RELEASE_VERSION}}` branches, `main` keeps producing `{{THIS_RELEASE_VERSION}}.0-*` until **this** main-bump PR merges — so the most recent feed entries may be `{{NEXT_VERSION}}`-content builds wearing `{{THIS_RELEASE_VERSION}}` branding. Picking one drifts the API baseline forward and silently hides real compat breaks. |

**Timing caveat:** the branch-point build publishes to `dotnet-tools` asynchronously. If you branch and bump on the same day, the script may report the correct candidate as `[NOT on feed]` — wait for publication and re-run rather than substituting an older build.

**Procedure:** run the helper — it does the whole resolution mechanically (requires `az login` with devdiv access):

```
pwsh ./scripts/Get-PackageValidationBaseline.ps1 -ThisReleaseVersion {{THIS_RELEASE_VERSION}}
# -> prints e.g. 18.11.0-1.26426.2
```

It computes `git merge-base origin/main origin/vs{{THIS_RELEASE_VERSION}}`, finds the matching successful build in [pipeline 9434](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434), derives the package version from the OfficialBuildId, and verifies it on the dotnet-tools feed. If it fails, read the script's own `.DESCRIPTION` header for the manual equivalent rather than reproducing it here.

### Prerequisites
- gh cli
- az cli
- darc cli — Arcade enforces a minimum version; if `darc` refuses with a "below the minimum required version" error, run `.\eng\common\darc-init.ps1`

## Phase Summary

| Phase | Trigger | Key Actions |
|---|---|---|
| **0: Instantiate** | User-initiated | Validate inputs, create GitHub tracking issue |
| **1: Branch & Prepare** | `BRANCH_SNAP_DATE` | Create `vs*` branch, DARC channel setup (batched PR), **audit all `vs*` branches for retirement** |
| **2: DARC Subscription Updates** | Phase 1 branch exists (`vs*` created) | Retarget `main`-targeting subs + VMR backflow to next channel, retired-branch cleanup (batched PR), Arcade verify |
| **3: Bump Main** | Phase 2 ingested and verified | Branding PR in `main` (`VersionPrefix` → next, ApiCompat baseline, refresh OptProf baseline) |
| **4: Final Branding** | 7 days before `INSIDERS_SNAP_DATE` | Telemetry evidence checks, package API compatibility review, OptProf bootstrap, VS insertion before Insiders snap |
| **5: Post-GA** | VS shipped (`VS_SHIP_DATE`) | Resolve the exact shipped version (SDK-coupled? SDK wins over VS `rel/stable`), nuget.org publish, docs, GitHub release, Change Waves Learn sync, retro |

## DARC Batching

DARC write commands push to the [maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration) repo. Batch related changes into **one PR**:

1. Choose a branch name like `release/msbuild-{{THIS_RELEASE_VERSION}}`
2. Add `--configuration-branch <name> --no-pr` to every write command except the last
3. Last command: use `--configuration-branch <name>` without `--no-pr` to create the PR
4. Review the diff before merging; disable auto-complete if the PR must remain review-only.
5. After merging, complete [DARC configuration PR verification](../../../documentation/release-checklist.md#darc-configuration-pr-verification) before dependent release steps.

Check existing channels and mappings before writing: the previous release normally pre-created the current release's mapping. Skip satisfied writes rather than constructing a redundant batch.

Read-only commands (`get-default-channels`, `get-subscriptions`, `get-channel`) don't need these flags.

**Non-interactive (`-q`).** `darc add-default-channel` / `add-subscription` prompt `y/n` when the target branch does not exist yet (e.g. pre-creating the `vs{{NEXT_VERSION}}` mapping in Phase 1.2c, or adding the new `vs{{THIS_RELEASE_VERSION}}` backflow in Phase 2). `darc delete-subscriptions` always prompts for confirmation. Console input is redirected in an agent session, so the prompt **fails or hangs the command** — always pass `-q` for these writes. Note the verb is `delete-subscriptions` (plural); `delete-subscription` does not exist.

**Phase 2 — forward-flow rotation.** Move subscriptions targeting `main` (`dotnet/dotnet @ main`, `dotnet/fsharp @ main`) to the next channel so those consumers receive next-version builds. Keep VMR `release/*` targets, including `.NET-next` preview bands, on `VS {{THIS_RELEASE_VERSION}}` so the release branch continues to feed its SDK bands.

**Phase 2 — VMR backflow rotation.** Backflow (`dotnet/dotnet → msbuild`, source-enabled) keeps each MSBuild branch's dependencies aligned with its SDK band. When the new `vs{{THIS_RELEASE_VERSION}}` is paired with an SDK band, repoint the `→ main` backflow to the **next** SDK band channel and add backflow from the **outgoing** band channel into the release branch. Skip this for a VS-only release. See checklist steps 2.2b / 2.3f / 2.3g.

## Branch retirement (Phase 1.3)

Audit every release branch to keep required SDK/VS servicing flows active and remove unused dependency automation.

**Branch inventory:** [Maintained branches](../../../documentation/release.md#maintained-branches) is the static list. Update its branch rows, SDK/VS sources, and support deadlines when cutting or retiring branches; cross-check the live DARC mappings, subscriptions, and repository branches in checklist step 1.3a.

Check both lifecycles because either the SDK or Visual Studio can require a branch:

| Lifecycle | Source |
|---|---|
| **SDK band** | [supported .NET versions table](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs#supported-net-versions) — maps each feature band to its VS version and support end date |
| **Visual Studio** | [VS Product Lifecycle and Servicing](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs) — annual releases and the LTSC table |

**VS 2026 and later:** non-LTSC releases are serviced through Insiders and for one month after Stable release. A new Stable promotion ends VS support for the previous release. LTSC baselines receive one year of servicing after release; use the published LTSC table to identify them.

Use published SDK and VS deadlines for lifecycle decisions, and DARC channels and subscriptions for dependency routing.

Apply the **combined rule**: retire a branch paired with an SDK band and a VS version only when both lifecycles agree it is out of support. Check these candidates for automation cleanup:

| Signal | Meaning |
|---|---|
| Branch's `VS X.Y` channel has **no MSBuild outbound subscription** | No downstream consumes MSBuild builds from that branch. |
| Default channel exists for a **branch that isn't in the repo** | Pure orphan — delete the mapping. |

**Retirement cleanup (2.3e):** remove inbound subscriptions, unused MSBuild outbound subscriptions, and branch-to-channel associations, including disabled subscriptions. Preserve shared channels and flows still needed by other branches or producers; verify removals after merge and ingestion.

## Executing a Phase

When asked to execute a specific phase:

1. Read the full phase from `documentation/release-checklist.md`
2. Verify the trigger condition is met (previous phases completed, including ingestion and live verification of prerequisite DARC changes)
3. Execute steps in order — respect sequential/parallel annotations
4. For DARC commands: batch writes into one configuration PR per phase
5. Record all output URLs in the tracking issue's artifact table
6. Mark checkboxes as completed in the tracking issue
7. In **Phase 4**, complete telemetry checkpoints 4.0 and 4.4a using the shared evidence procedure in `documentation/release-checklist.md`.
8. In **Phase 4** (step 4.7): if `documentation/wiki/ChangeWaves.md` is changed for this release, update the public Learn page at https://learn.microsoft.com/visualstudio/msbuild/change-waves. Sync the Change Waves Learn page from `documentation/wiki/ChangeWaves.md` on the `vsXX.Y` branch that is live in VS Insiders / the latest preview SDK. PR goes to `MicrosoftDocs/visualstudio-docs-pr` (`docs/msbuild/change-waves.md`); example: https://github.com/MicrosoftDocs/visualstudio-docs-pr/pull/15662.

## Key Files

| File | Purpose |
|---|---|
| [`documentation/release-checklist.md`](../../../documentation/release-checklist.md) | **Operational checklist** — the source of truth |
| [`documentation/release.md`](../../../documentation/release.md) | Maintained-branch inventory and release process |
| [`documentation/wiki/ChangeWaves.md`](../../../documentation/wiki/ChangeWaves.md) | Source doc for the Learn page sync — always sync the `vsXX.Y` (Insiders/preview-SDK) copy, not `main` |
| [MSBuild Change Waves Learn page](https://learn.microsoft.com/visualstudio/msbuild/change-waves) | Public docs target to [`MicrosoftDocs/visualstudio-docs-pr`](https://github.com/MicrosoftDocs/visualstudio-docs-pr) (`docs/msbuild/change-waves.md`) |
| [`eng/Versions.props`](../../../eng/Versions.props) | `VersionPrefix`, `PackageValidationBaselineVersion`, `BootstrapSdkVersion` |
| [`azure-pipelines/vs-insertion.yml`](../../../azure-pipelines/vs-insertion.yml) | VS insertion pipeline — `TargetBranch` parameter and `InsertTargetBranch` routing |
| [`azure-pipelines/vs-insertion-experimental.yml`](../../../azure-pipelines/vs-insertion-experimental.yml) | Experimental insertion — `TargetBranch` parameter values |
| [`scripts/Get-PackageValidationBaseline.ps1`](../../../scripts/Get-PackageValidationBaseline.ps1) | Phase 3.2 — resolves `PackageValidationBaselineVersion` deterministically (merge-base → pipeline 9434 → dotnet-tools feed) |
| [`scripts/Get-LatestOptProfDrop.ps1`](../../../scripts/Get-LatestOptProfDrop.ps1) | Phase 3.3 — resolves the latest main OptProf drop (MSBuild-OptProf pipeline 17389) to refresh `OptProfBaselineDrop` in `.vsts-dotnet.yml` |
| [`.vsts-dotnet.yml`](../../../.vsts-dotnet.yml) | Build pipeline entry point — `OptProfBaselineDrop` (hardcoded OptProf seed for new `vs*` branches) |
| [`azure-pipelines/.vsts-dotnet-build-jobs.yml`](../../../azure-pipelines/.vsts-dotnet-build-jobs.yml) | Build jobs — `VisualStudio.ChannelName` is release-agnostic (`int.main`); no per-release edit |

## Validation

After completing all phases, verify:

1. Branch `vs{{THIS_RELEASE_VERSION}}` exists and has final branding
2. Main has `VersionPrefix` = `{{NEXT_VERSION}}.0`
3. DARC: main → `VS {{NEXT_VERSION}}` channel, release branch → `VS {{THIS_RELEASE_VERSION}}` channel
4. VS insertion PR merged
5. Packages published to nuget.org
6. GitHub release created with tag `v{{THIS_RELEASE_EXACT_VERSION}}`
7. The Learn page https://learn.microsoft.com/visualstudio/msbuild/change-waves lists exactly the waves present in `vsXX.Y` (the version in VS Insiders / the latest released preview SDK), or the sync is explicitly tracked

## Error Recovery

- **Branch already exists**: Release was partially started — check the tracking issue for progress
- **DARC channel already exists**: Safe to continue — `add-channel` is idempotent
- **OptProf fails on first build**: Expected — that's why we use main's OptProf data as fallback
- **DARC configuration PR conflicts**: Rebase the configuration branch on `production` and force-push

## Major Version Releases

If `NEXT_VERSION` is a new major version (e.g., 18.x → 19.0), additional steps are needed after Phase 5. See [release.md](../../../documentation/release.md) for:
- `src/Shared/BuildEnvironmentHelper.cs` — VS major version constants
- `src/Shared/Constants.cs` — version constants
- `src/Framework/Telemetry/TelemetryConstants.cs` — telemetry version
