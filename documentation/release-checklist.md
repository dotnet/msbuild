# MSBuild Release Checklist {{THIS_RELEASE_VERSION}}

<!-- TEMPLATE: Copy this entire file into a new GitHub issue to track a release.
     Replace ALL {{PLACEHOLDERS}} before starting work.
     See release.md for background on how MSBuild releases flow into VS. -->

## Inputs

Fill in these values before starting. Version increments are irregular — they must be specified explicitly.

| Placeholder | Description | Value |
|---|---|---|
| `{{PREVIOUS_RELEASE_VERSION}}` | Version being replaced as latest (e.g. `18.5`) | |
| `{{THIS_RELEASE_VERSION}}` | Version being released now (e.g. `18.6`) | |
| `{{THIS_RELEASE_EXACT_VERSION}}` | Full `VersionPrefix` from `eng/Versions.props` on the release branch after final branding (e.g. `18.6.0`). For non-patch releases this is always `{{THIS_RELEASE_VERSION}}.0`. | |
| `{{NEXT_VERSION}}` | Version that main will be bumped to (e.g. `18.7`) | |
| `{{BRANCH_SNAP_DATE}}` | Date MSBuild branches `vs*` from `main`. Insertion targets VS `main`. From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) | |
| `{{INSIDERS_SNAP_DATE}}` | Date VS snaps `main` → `rel/insiders`. Final-branded MSBuild must be in VS `main` **before** this date. From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) | |
| `{{STABLE_SNAP_DATE}}` | Date VS snaps `rel/insiders` → `rel/stable`. From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) | |
| `{{VS_SHIP_DATE}}` | Date VS ships publicly (GA). Post-GA tasks (nuget.org, docs) happen after this. | |

**Derived values** (do not edit — computed from inputs):
- Release branch: `vs{{THIS_RELEASE_VERSION}}`
- DARC channel: `VS {{THIS_RELEASE_VERSION}}`
- Next DARC channel: `VS {{NEXT_VERSION}}`
- VS insertion target: VS `main` (VS snaps main → insiders → stable on its own schedule)

## Phase 0: Instantiate Release

> **Trigger**: User decides to start a release. Do this first.

- [ ] Validate inputs:
  - [ ] Confirm `eng/Versions.props` on `main` has `VersionPrefix` = `{{THIS_RELEASE_VERSION}}.0` — if not, the inputs are wrong
  - [ ] Confirm branch `vs{{THIS_RELEASE_VERSION}}` does **not** already exist — if it does, this release was already started
  - [ ] Confirm DARC channel `VS {{THIS_RELEASE_VERSION}}` exists: \
  `darc get-channel --name "VS {{THIS_RELEASE_VERSION}}"` \
  If missing, it should have been created during the previous release (Phase 1.2b "create next channel" step). Create it now: `darc add-channel --name "VS {{THIS_RELEASE_VERSION}}"`
- [ ] Create this tracking issue in dotnet/msbuild with all `{{PLACEHOLDERS}}` replaced
- [ ] Record all tracking URLs in the table below as phases are completed:

| Artifact | URL |
|---|---|
| Next-version branding PR | {{URL_OF_NEXT_VERSION_BRANDING_PR}} |
| VisualStudio.ChannelName PR | {{URL_OF_CHANNEL_NAME_PR}} |
| Phase 1 DARC config PR | {{URL_OF_PHASE1_DARC_PR}} |
| Phase 3 DARC config PR | {{URL_OF_PHASE3_DARC_PR}} |
| Final branding PR | {{URL_OF_FINAL_BRANDING_PR}} |
| VS insertion PR | {{URL_OF_VS_INSERTION}} |
| Channel promotion PR | {{URL_OF_CHANNEL_PROMOTION_PR}} |

---

## Phase 1: Branch & Prepare

> **Trigger**: `{{BRANCH_SNAP_DATE}}` reached.

Steps are **sequential** — complete in order.

- [ ] **1.1** Create branch `vs{{THIS_RELEASE_VERSION}}` from HEAD of `main`: \
`git push upstream HEAD:refs/heads/vs{{THIS_RELEASE_VERSION}}`
  - _If branched too early_ (main has commits that shouldn't be in the release): fast-forward the branch to the correct commit (the one currently inserted into VS main): \
  `git push upstream <correct_sha>:refs/heads/vs{{THIS_RELEASE_VERSION}}`
- [ ] **1.2** DARC configuration — batch all channel/mapping changes into **one PR** on the [maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration) repo. \
Use `--configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}}` on every command and `--no-pr` on all but the last:
  - [ ] **1.2a** Ensure branch-to-channel association exists: \
  First check: `darc get-default-channels --channel "VS {{THIS_RELEASE_VERSION}}" --branch vs{{THIS_RELEASE_VERSION}} --source-repo https://github.com/dotnet/msbuild` \
  If `No matching channels were found.`: \
  `darc add-default-channel --channel "VS {{THIS_RELEASE_VERSION}}" --branch vs{{THIS_RELEASE_VERSION}} --repo https://github.com/dotnet/msbuild --configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}} --no-pr`
  - [ ] **1.2b** Create DARC channel for **next** release: \
  `darc add-channel --name "VS {{NEXT_VERSION}}" --configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}} --no-pr` \
  _(If channel already exists, this is a no-op.)_
  - [ ] **1.2c** Pre-create default channel mapping for the **next** release branch (**last command — omit `--no-pr` to create the PR**): \
  `darc add-default-channel --channel "VS {{NEXT_VERSION}}" --branch vs{{NEXT_VERSION}} --repo https://github.com/dotnet/msbuild --configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}}`
  - [ ] **1.2d** Get the maestro-configuration PR reviewed and merged: {{URL_OF_PHASE1_DARC_PR}}
  - [ ] **1.2e** Ping internal "First Responders" Teams channel to get the new `VS {{NEXT_VERSION}}` channel available as a promotion target: {{URL_OF_CHANNEL_PROMOTION_PR}}
- [ ] **1.3** Update `.config/git-merge-flow-config.jsonc`:
  - [ ] **1.3a** Insert `vs{{THIS_RELEASE_VERSION}}` as the last entry before `main` in the merge chain. Add a comment noting the VS/SDK version context.
  - [ ] **1.3b** **Retire predecessor branches that will no longer be supported.** Remove their `MergeToBranch` entries and rewire the chain to skip them so automation does not open stale forward-merge PRs. \
  How to identify a retired branch:
    - **SDK releases** — check the lifecycle table at https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle; any branch tied to an SDK band that is past its support end date is retired.
    - **VS releases** — the retired branch is always the version directly preceding the one being released (i.e. `vs{{THIS_RELEASE_VERSION}} - 1`).

---

## Phase 2: Bump Main & Update Pipelines

> **Trigger**: `vs{{THIS_RELEASE_VERSION}}` branch exists (Phase 1.1 done). Previous release is in insiders stage.

Create **one PR in `main`** containing all of the following changes:

- [ ] **2.1** `eng/Versions.props`: Update `VersionPrefix` to `{{NEXT_VERSION}}.0`
- [ ] **2.2** `eng/Versions.props`: Update `PackageValidationBaselineVersion` to the last released version (the `{{PREVIOUS_RELEASE_VERSION}}` GA version published to nuget.org).
- [ ] **2.3** If needed, update `CompatibilitySuppressions.xml` files. Run: \
`dotnet pack MSBuild.Dev.slnf /p:ApiCompatGenerateSuppressionFile=true` \
See [API compat documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/overview) for details.
- [ ] **2.4** Update [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml): set `AutoInsertTargetBranch` for `vs{{THIS_RELEASE_VERSION}}` → VS `main`.
- [ ] **2.5** Update [`azure-pipelines/vs-insertion-experimental.yml`](../azure-pipelines/vs-insertion-experimental.yml): \
Add `rel/insiders` and/or `rel/stable` to `TargetBranch` parameter values if not already present.
- [ ] **2.6** Merge branding PR: {{URL_OF_NEXT_VERSION_BRANDING_PR}}

---

## Phase 3: DARC Subscription Updates

> **Trigger**: Phase 2 branding PR merged (main now has `{{NEXT_VERSION}}` version).

First, **gather information** (read-only queries — no PR needed):

- [ ] **3.1** Find the SDK main subscription ID to update: \
`darc get-subscriptions --exact --source-repo https://github.com/dotnet/msbuild --channel "VS {{THIS_RELEASE_VERSION}}"` \
Note the subscription ID for the SDK `main` branch entry.
- [ ] **3.2** Verify release branch channel association: \
`darc get-default-channels --source-repo https://github.com/dotnet/msbuild --branch vs{{THIS_RELEASE_VERSION}}` \
Note whether the association exists (needed for step 3.3d).

Then, **batch all write operations into one PR** on the [maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration) repo. \
Use `--configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}}-main-bump` and `--no-pr` on all but the last command:

- [ ] **3.3** DARC channel/subscription updates:
  - [ ] **3.3a** Remove main → old channel mapping: \
  `darc delete-default-channel --repo https://github.com/dotnet/msbuild --branch main --channel "VS {{THIS_RELEASE_VERSION}}" --configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **3.3b** Associate main with next channel: \
  `darc add-default-channel --channel "VS {{NEXT_VERSION}}" --branch main --repo https://github.com/dotnet/msbuild --configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **3.3c** Update SDK main subscription to new channel: \
  `darc update-subscription --id <subscription_id_from_3.1> --channel "VS {{NEXT_VERSION}}" --configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **3.3d** If release branch association was missing in 3.2, add it: \
  `darc add-default-channel --channel "VS {{THIS_RELEASE_VERSION}}" --branch vs{{THIS_RELEASE_VERSION}} --repo https://github.com/dotnet/msbuild --configuration-branch release/msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **3.3e** _If the Arcade subscription from 3.4 below is missing or pointed at the wrong channel, include the fix here with `--no-pr`._
  - [ ] **3.3f** **Create the PR** — re-run step 3.3b (or whichever was the last write command executed) without `--no-pr` to open the PR on the configuration branch.
  - [ ] **3.3g** Get the maestro-configuration PR reviewed and merged: {{URL_OF_PHASE3_DARC_PR}}

Verifications (**parallel** — read-only, no ordering dependency):

- [ ] **3.4** Verify Arcade subscription — **and add it if missing**. \
`darc get-subscriptions --exact --target-repo https://github.com/dotnet/msbuild --source-repo https://github.com/dotnet/arcade`
  - **Every supported branch must have an Arcade subscription** from the matching `.NET <X> Eng` channel.
  - The **channel** is determined by the .NET band the branch is paired with (e.g. a branch paired with .NET 10 subscribes to `.NET 10 Eng`).

> _Roslyn and NuGet subscription verification intentionally omitted: there is always exactly one Roslyn and NuGet subscription, it targets `main` only, is permanently disabled, and never changes per release — re-verifying it every release adds no signal._

---

## Phase 4: Final Branding & VS Insertion

> **Trigger**: 7 calendar days before `{{INSIDERS_SNAP_DATE}}`. \
> **Precondition**: Phases 1–3 complete. Preview builds from `vs{{THIS_RELEASE_VERSION}}` have been inserting into VS `main` since Phase 2. \
> **Goal**: Final-brand the release branch and get the final-branded bits inserted into VS `main` before VS snaps to `rel/insiders`.

Steps are **sequential**.

- [ ] **4.1** Promote public API on `vs{{THIS_RELEASE_VERSION}}` branch: \
Move contents of `PublicAPI.Unshipped.txt` → `PublicAPI.Shipped.txt` for all projects with API changes. See [release.md](./release.md) for details.
- [ ] **4.2** Run `scripts/Stabilize-Release.ps1` on `vs{{THIS_RELEASE_VERSION}}` branch: \
Use `-DryRun` first to preview. The script adds `<DotNetFinalVersionKind>release</DotNetFinalVersionKind>` on the same line as `VersionPrefix` (creates merge conflict for forward-flow) and changes `PreReleaseVersionLabel` from `preview` to `servicing`. \
_If the script says "already stabilized" — skip (idempotent)._
- [ ] **4.3** Create and merge final branding PR to `vs{{THIS_RELEASE_VERSION}}`: {{URL_OF_FINAL_BRANDING_PR}}
- [ ] **4.4** Bootstrap OptProf for `vs{{THIS_RELEASE_VERSION}}`:
  - [ ] Run the [official build](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434) for `vs{{THIS_RELEASE_VERSION}}` with `Optional OptProfDrop Override` set to main's latest OptProf drop path. _(Find the path in main CI logs: Windows_NT → Build → search for `OptimizationData`.)_ Alternatively, set `SkipApplyOptimizationData` to `true` in Advanced options.
  - [ ] Verify that the [OptProf data collection](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=17389) pipeline triggers for `vs{{THIS_RELEASE_VERSION}}`. If not triggered, run manually ('Run pipeline' in upper right).
  - [ ] Run the [official build](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434) for `vs{{THIS_RELEASE_VERSION}}` with no overrides — OptProf should succeed now.
- [ ] **4.5** Get M2 or QB approval as necessary per the VS schedule
- [ ] **4.6** Babysit the VS insertion PR from `vs{{THIS_RELEASE_VERSION}}` into VS `main` (auto-generated at https://devdiv.visualstudio.com/DevDiv/_git/VS/pullrequests). The final-branded bits must be in VS `main` **before** `{{INSIDERS_SNAP_DATE}}` so they are included when VS snaps to `rel/insiders`: {{URL_OF_VS_INSERTION}} \
The insertion PR contains the inserted package versions — useful for the nuget.org publishing step.

**After insiders snap** (only if a backport to insiders is needed):

> 🛑 **4.7 and 4.8 are NOT part of the regular release flow — skip them entirely on a normal release.** \
> They only apply when **servicing** a previously-shipped release (i.e. you actually have a hotfix commit on `vs{{THIS_RELEASE_VERSION}}` that needs to be inserted into VS's already-snapped `rel/insiders` or `rel/stable` branch). If you have no such commit to service, leave `AutoInsertTargetBranch` untouched and move on to Phase 5.
>
> ⚠️ When you *do* need to service: re-confirm which VS branch you actually want to insert into before flipping `AutoInsertTargetBranch`. The default is `main`, so forgetting to retarget after the snap silently lands your fix in the next VS instead of the one you're servicing.

- [ ] **4.7** Update [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml): retarget `AutoInsertTargetBranch` for `vs{{THIS_RELEASE_VERSION}}` from VS `main` → `rel/insiders`. This enables direct insertion of hotfix commits into the insiders branch.

**After stable snap** (only if a backport to stable is needed):

- [ ] **4.8** Update [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml): retarget `AutoInsertTargetBranch` for `vs{{THIS_RELEASE_VERSION}}` → `rel/stable`. This enables direct insertion of hotfix commits into the stable branch.

---

## Phase 5: Post-GA

> **Trigger**: `{{VS_SHIP_DATE}}` has passed and VS release has shipped.

Steps are **mostly parallel** unless noted.

- [ ] **5.1** Push packages to nuget.org.

  > **How publishing works:** We don't push packages ourselves. We hand a link to the **Shipping** artifacts of the official build to the dnceng release team, and they push to nuget.org. Searching past mail for the subject _"Publish MSBuild {{THIS_RELEASE_VERSION}} to NuGet.org"_ will surface the contact and template; do **not** hard-code the contact's name or email in this checklist — internal ownership rotates and we don't want individual addresses exposed in a public GitHub doc.

  - [ ] **5.1a** Determine the exact MSBuild version that actually shipped to customers — **do not trust `VersionPrefix` in the release branch by itself**, it has been misleading in past releases.
    - **If this release is coupled with an SDK release: use the SDK as the source of truth** — it has the highest fidelity, particularly for security-only patches where only some intervals get bumped. Look up the MSBuild version baked into the shipped SDK build.
    - Otherwise: look at the MSBuild version inserted into the corresponding VS build. Note this version differs in non-obvious ways between VS major/minor versions, so confirm against the actual VS insertion PR.
  - [ ] **5.1b** In the [MSBuild official build pipeline](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434), filter to the `vs{{THIS_RELEASE_VERSION}}` branch and locate the build whose output version matches the one identified in 5.1a (e.g. `{{THIS_RELEASE_EXACT_VERSION}}`, such as `18.6.3`).
  - [ ] **5.1c** From that build, open the **Publish Artifacts** step and grab the link to the **`artifacts-shipping`** drop. Verify the Shipping folder contains all of:
    - `Microsoft.Build.Utilities.Core.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Framework.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Runtime.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Tasks.Core.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.NET.StringTools.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Templates.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
  - [ ] **5.1d** Email the dnceng release team (use the saved _"Publish MSBuild ... to NuGet.org"_ template from your sent mail) with the `artifacts-shipping` link from 5.1c and ask them to publish to nuget.org. Track the reply until publication is confirmed.

- [ ] **5.2** Publish docs

  > **How publishing works:** The reference-publishing vendor team generates Microsoft Learn reference pages from the shipped MSBuild assemblies/xmldoc and then sends us a docs-repo PR with the regenerated content.

  - [ ] **5.2a** Create a reference-publishing ticket for the new release based on [this existing ticket](https://dev.azure.com/msft-skilling/Content/_workitems/edit/565854) as a template. Then wait for the vendor team to ping you with a link to the generated PR.
  - [ ] **5.2b** Review and approve the docs-repo PR the vendor team opens (example: [msbuild-api-docs#61](https://github.com/dotnet/msbuild-api-docs/pull/61)).
- [ ] **5.3** Create GitHub release:
  - [ ] **5.3a** **Precondition — confirm the previous release tag exists on `upstream`.** \
  `git fetch upstream --tags && git tag --list 'v{{PREVIOUS_RELEASE_EXACT_VERSION}}'` \
  If the tag is missing (e.g. the previous release was never tagged), create and push it **first**.
  - [ ] **5.3b** **Identify the commit to tag.** It is **not always** the final-branding commit from 4.3 — it must be the commit whose bits **actually shipped** in `{{THIS_RELEASE_EXACT_VERSION}}`. \
  If a hotfix was applied to `vs{{THIS_RELEASE_VERSION}}` after final branding and that fix was inserted into the VS build that shipped, tag **that** commit instead. \
  To find it: cross-reference the SHA from the **VS insertion PR that actually shipped** (the package version listed there must equal `{{THIS_RELEASE_EXACT_VERSION}}` from 5.1a) with the commit history on `vs{{THIS_RELEASE_VERSION}}`.
  - [ ] **5.3c** Tag this release and push:
    ```
    git checkout <commit identified in 5.3b>
    git tag v{{THIS_RELEASE_EXACT_VERSION}}
    git push upstream v{{THIS_RELEASE_EXACT_VERSION}}
    ```
  - [ ] **5.3d** Create release at https://github.com/dotnet/msbuild/releases/new — use `Generate Release Notes` to prepopulate.
- [ ] **5.4** Update `BootstrapSdkVersion` in [`eng/Versions.props`](https://github.com/dotnet/msbuild/blob/main/eng/Versions.props) if a fresh SDK was released. Check https://dotnet.microsoft.com/download/visual-studio-sdks — always verify the details for the targeted .NET version.
- [ ] **5.4b** Update `tools.dotnet` in [`global.json`](https://github.com/dotnet/msbuild/blob/main/global.json) to the latest released SDK in the targeted band. `DotNetCliVersion` in `eng/Versions.props` is derived from this and **must** match (see the comment on `DotNetCliVersion`). This bump should happen monthly as new SDKs release.
- [ ] **5.5** Verify the overall subscription map is correct — not just for `{{THIS_RELEASE_VERSION}}` but for every still-supported branch:
  - [ ] **5.5a** `main` subscriptions point to `VS {{NEXT_VERSION}}` channel (should have been done in Phase 3; confirm): \
  `darc get-subscriptions --exact --target-repo https://github.com/dotnet/msbuild --target-branch main`
  - [ ] **5.5b** Every supported `vsXX.Y` branch has an Arcade subscription matching its targeted .NET band, and each supported branch's outbound subscriptions land in the right downstream (e.g. SDK band, VMR). \
  Open question (track with Rainer): MSBuild `main` currently flows to *two* downstream targets (VMR and the legacy SDK path) but only one subscription is configured — confirm whether this needs a dual subscription before signing off the release.
- [ ] **5.6** Review this tracking issue for any process deviations. If the process changed, create a PR to update `documentation/release-checklist.md` with the improvements.

---

## If {{NEXT_VERSION}} is a new major version

- [ ] Update VS major version references per [release.md](./release.md):
  - [`src/Shared/BuildEnvironmentHelper.cs`](https://github.com/dotnet/msbuild/blob/main/src/Shared/BuildEnvironmentHelper.cs)
  - [`src/Shared/Constants.cs`](https://github.com/dotnet/msbuild/blob/main/src/Shared/Constants.cs)
  - [`src/Framework/Telemetry/TelemetryConstants.cs`](https://github.com/dotnet/msbuild/blob/main/src/Framework/Telemetry/TelemetryConstants.cs)
