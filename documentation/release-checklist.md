# MSBuild Release Checklist {{THIS_RELEASE_VERSION}}

<!-- TEMPLATE: Copy this entire file into a new GitHub issue to track a release.
     Replace ALL {{PLACEHOLDERS}} before starting work.
     See release.md for background on how MSBuild releases flow into VS. -->

## Release Output

Artifacts produced over the course of the release. Record each URL here as the corresponding phase completes so this issue serves as the single index back into every PR / build / tag that defines `{{THIS_RELEASE_EXACT_VERSION}}`.

| Artifact | URL |
|---|---|
| Phase 1.2d — maestro-configuration PR (channels for `{{THIS_RELEASE_VERSION}}` / `{{NEXT_VERSION}}`) | {{URL_OF_PHASE1_DARC_PR}} |
| Phase 2.3j — maestro-configuration PR (`main`-targeting subs + VMR backflow retargeted, retired-branch cleanup) | {{URL_OF_PHASE2_DARC_PR}} |
| Phase 3.5 — `main` next-version main-bump PR | {{URL_OF_NEXT_VERSION_MAIN_BUMP_PR}} |
| Phase 4.4 — VS insertion PR | {{URL_OF_VS_INSERTION}} |
| Phase 5.3 — GitHub release tag | https://github.com/dotnet/msbuild/releases/tag/v{{THIS_RELEASE_EXACT_VERSION}} |

---

## Inputs

Fill in these values before starting. Version increments are irregular — they must be specified explicitly.

| Placeholder | Description | Value |
|---|---|---|
| `{{PREVIOUS_RELEASE_VERSION}}` | Version being replaced as latest | |
| `{{THIS_RELEASE_VERSION}}` | Version being released now | |
| `{{THIS_RELEASE_EXACT_VERSION}}` | The `VersionPrefix` that **actually shipped** to customers — read it from VS `rel/stable` (see Phase 5.1a), **not** assumed. It is usually `{{THIS_RELEASE_VERSION}}.0`, but an OptProf-driven insertion bump can make the shipped version `{{THIS_RELEASE_VERSION}}.1` or higher (e.g. 18.7 shipped as `18.7.1`). **Not known when first instantiating this checklist — leave blank until Phase 5.1a confirms it.** | |
| `{{NEXT_VERSION}}` | Version that main will be bumped to | |
| `{{BRANCH_SNAP_DATE}}` | Date we create `vs{{THIS_RELEASE_VERSION}}` from `main`. | |
| `{{INSIDERS_SNAP_DATE}}` | Date VS snaps `main` → `rel/insiders`. Final-branded MSBuild must be in VS `main` **before** this date. From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) | |
| `{{STABLE_SNAP_DATE}}` | Date VS snaps `rel/insiders` → `rel/stable`. From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) | |
| `{{VS_SHIP_DATE}}` | Date VS ships publicly (GA). Post-GA tasks (nuget.org, docs) happen after this. | |
| `{{PACKAGE_VALIDATION_BASELINE_VERSION}}` | Latest `{{THIS_RELEASE_VERSION}}.0-preview-NNNNN-NN` MSBuild build reachable from `vs{{THIS_RELEASE_VERSION}}`. Used as the ApiCompat baseline for the bumped `main`. **How to determine it:** see the [release skill](https://github.com/dotnet/msbuild/blob/main/.github/skills/release/SKILL.md#how-to-determine-package_validation_baseline_version). | |

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
  `darc get-channels`\
  If missing, it should have been created during the previous release (Phase 1.2b "create next channel" step). Create it now: `darc add-channel --name "VS {{THIS_RELEASE_VERSION}}"`
- [ ] Create this tracking issue in dotnet/msbuild with all `{{PLACEHOLDERS}}` replaced
- [ ] As phases complete, record artifact URLs in the **Release Output** table at the top of this checklist.

---

## Phase 1: Branch & Prepare

> **Trigger**: `{{BRANCH_SNAP_DATE}}` reached.

Steps are **sequential** — complete in order.

- [ ] **1.0** **Pre-snap team check.** Before snapping the branch, ping the MSBuild team to confirm there is nothing they still need to merge into `main` that should ship in `{{THIS_RELEASE_VERSION}}`. Anything that lands in `main` after Phase 1.1 will go into `{{NEXT_VERSION}}` instead.
- [ ] **1.1** Create branch `vs{{THIS_RELEASE_VERSION}}` from HEAD of `main` (**requires repo admin rights** — `git push` to `refs/heads/vs*` is restricted; if you don't have permission, ask a repo admin with `vs*` push rights to do it): \
`git push upstream HEAD:refs/heads/vs{{THIS_RELEASE_VERSION}}`
  - _If branched too early_ (main has commits that shouldn't be in the release): fast-forward the branch to the correct commit (the one currently inserted into VS main): \
  `git push upstream <correct_sha>:refs/heads/vs{{THIS_RELEASE_VERSION}}`
- [ ] **1.2** DARC configuration — batch all channel/mapping changes into **one PR** on the [maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration) repo. \
Use `--configuration-branch msbuild-{{THIS_RELEASE_VERSION}}` on every command and `--no-pr` on all but the last:
  - [ ] **1.2a** Ensure branch-to-channel association exists: \
  First check: `darc get-default-channels --channel "VS {{THIS_RELEASE_VERSION}}" --branch vs{{THIS_RELEASE_VERSION}} --source-repo https://github.com/dotnet/msbuild` \
  If `No matching channels were found.`: \
  `darc add-default-channel --channel "VS {{THIS_RELEASE_VERSION}}" --branch vs{{THIS_RELEASE_VERSION}} --repo https://github.com/dotnet/msbuild --configuration-branch msbuild-{{THIS_RELEASE_VERSION}} --no-pr`
  - [ ] **1.2b** Create DARC channel for **next** release: \
  `darc add-channel --name "VS {{NEXT_VERSION}}" --configuration-branch msbuild-{{THIS_RELEASE_VERSION}} --no-pr` \
  _(If channel already exists, this is a no-op.)_
  - [ ] **1.2c** Pre-create default channel mapping for the **next** release branch (**last command — omit `--no-pr` to create the PR**). The `vs{{NEXT_VERSION}}` branch does not exist yet, so pass `-q` (non-interactive) to skip the "branch doesn't exist" prompt — otherwise the command blocks/aborts: \
  `darc add-default-channel --channel "VS {{NEXT_VERSION}}" --branch vs{{NEXT_VERSION}} --repo https://github.com/dotnet/msbuild --configuration-branch msbuild-{{THIS_RELEASE_VERSION}} -q`
  - [ ] **1.2d** Get the maestro-configuration PR reviewed and merged: {{URL_OF_PHASE1_DARC_PR}}
- [ ] **1.3** Update `.config/git-merge-flow-config.jsonc`:
  - [ ] **1.3a** Insert `vs{{THIS_RELEASE_VERSION}}` as the last entry before `main` in the merge chain. Add a comment noting the VS/SDK version context.
  - [ ] **1.3b** **Retire predecessor branches that will no longer be supported.** Remove their `MergeToBranch` entries and rewire the chain to skip them so automation does not open stale forward-merge PRs. \
  How to identify a retired branch:
    - **The combined rule:** a branch paired with both an SDK band and a VS version is retired **only when both lifecycles agree it is out of support**. If only one side says retired but the other is still supported, **keep the branch** — automation must still flow forward-merges so the still-supported lifecycle keeps receiving fixes.
    - **SDK lifecycle** — for branches paired with an SDK band, check https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle. If the paired SDK band is past its support end date, the branch is SDK-retired.
    - **VS lifecycle** — check the [VS Servicing Information wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/27212/Visual-Studio-Servicing-Information). Rule of thumb: the VS support window covers the current release plus two preceding versions, so the first candidate for VS-retirement is `vs{{THIS_RELEASE_VERSION}} - 3` — **always confirm on the wiki**, since servicing exceptions can extend specific versions beyond the rule of thumb.
    - **VS-only branches** (not paired with any active SDK band) are retired purely on the VS lifecycle.

---

## Phase 2: DARC Subscription Updates

> **Trigger**: `vs{{THIS_RELEASE_VERSION}}` branch exists (Phase 1 complete). \
> **Why this runs before bumping `main`:** consumers of MSBuild via `main` (notably the VMR) should start receiving next-version bits from the `VS {{NEXT_VERSION}}` channel **the moment `main` is bumped**. Reassigning `main`'s default channel **before** the Phase 3 branding bump means the first `main` build at the new version is already published to the correct channel; otherwise it lands on the now-stale `VS {{THIS_RELEASE_VERSION}}` channel.

First, **gather information** (read-only queries — no PR needed):

- [ ] **2.1** Identify the **forward-flow** subscriptions to retarget: \
`darc get-subscriptions --exact --source-repo https://github.com/dotnet/msbuild --channel "VS {{THIS_RELEASE_VERSION}}"` \
This lists every `msbuild → downstream` subscription currently on the outgoing channel. **Retarget ONLY the subscriptions whose _target branch_ is `main`** — normally `dotnet/dotnet @ main` (the VMR/SDK main) and `dotnet/fsharp @ main` (fsharp tracks the channel msbuild `main` publishes to). Record their IDs. \
🛑 **Do NOT touch subscriptions that target a VMR servicing/release branch** (`dotnet/dotnet @ release/*`). That includes the SDK band now paired with `vs{{THIS_RELEASE_VERSION}}` (it is now fed by `vs{{THIS_RELEASE_VERSION}}` via the `VS {{THIS_RELEASE_VERSION}}` channel) **and** any `.NET-next` preview band (`release/*-preview*`). Leaving them on `VS {{THIS_RELEASE_VERSION}}` is what lets the new release branch own its downstream flow; moving them would steal it. The single rule: **retarget a forward sub only if its target branch is `main`.**
- [ ] **2.2** Verify release branch channel association: \
`darc get-default-channels --source-repo https://github.com/dotnet/msbuild --branch vs{{THIS_RELEASE_VERSION}}` \
Note whether the association exists (needed for step 2.3d).
- [ ] **2.2b** **(VMR backflow — do this only if `vs{{THIS_RELEASE_VERSION}}` is paired with an SDK band that `main` was feeding; skip entirely for a VS-only release with no SDK band.)** Identify the backflow subscriptions (VMR → msbuild, source-enabled) and the band channels: \
`darc get-subscriptions --target-repo https://github.com/dotnet/msbuild --target-branch main --source-repo https://github.com/dotnet/dotnet` → record the source-enabled `→ main` backflow **ID** (for 2.3f). \
`darc get-default-channels --source-repo https://github.com/dotnet/dotnet --branch main` → the **next** SDK band channel `main` now publishes to, e.g. `.NET <NEXT_BAND> SDK`. Compare to the current `→ main` backflow channel — if unchanged, 2.3f is a no-op (for 2.3f). \
`darc get-default-channels --source-repo https://github.com/dotnet/dotnet --branch release/<outgoing-band>` → the **outgoing** band channel that `vs{{THIS_RELEASE_VERSION}}` now owns, e.g. `.NET <OUTGOING_BAND> SDK` (for 2.3g).

Then, **batch all write operations into one PR** on the [maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration) repo. \
Use `--configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump` (distinct from the Phase 1 channel branch) and `--no-pr` on all but the last command. \
_Tip: `darc add-default-channel` / `add-subscription` prompt interactively when the target branch does not exist yet; pass `-q` (non-interactive) to skip that prompt._

- [ ] **2.3** DARC channel/subscription updates:
  - [ ] **2.3a** Remove main → old channel mapping: \
  `darc delete-default-channel --repo https://github.com/dotnet/msbuild --branch main --channel "VS {{THIS_RELEASE_VERSION}}" --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **2.3b** Associate main with next channel: \
  `darc add-default-channel --channel "VS {{NEXT_VERSION}}" --branch main --repo https://github.com/dotnet/msbuild --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **2.3c** Retarget **each** `main`-targeting forward subscription from 2.1 to the next channel — run once per ID (typically `dotnet/dotnet @ main` and `dotnet/fsharp @ main`): \
  `darc update-subscription --id <main_targeting_sub_id> --channel "VS {{NEXT_VERSION}}" --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **2.3d** If release branch association was missing in 2.2, add it: \
  `darc add-default-channel --channel "VS {{THIS_RELEASE_VERSION}}" --branch vs{{THIS_RELEASE_VERSION}} --repo https://github.com/dotnet/msbuild --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **2.3e** **Delete subscriptions for retired branches.** For each branch identified as retired in step 1.3b (apply the same combined SDK+VS rule — do **not** delete subscriptions for a branch that's retired on only one side, since fixes must keep flowing into the still-supported lifecycle), remove its inbound subscriptions and any default channel associations.
  List them: `darc get-subscriptions --target-repo https://github.com/dotnet/msbuild --target-branch <retired_branch>` \
  Delete each: `darc delete-subscription --id <subscription_id> --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **2.3f** **(VMR backflow — skip for a VS-only release, or if 2.2b found the channel unchanged.)** Repoint the `→ main` backflow (ID from 2.2b) to the **next** SDK band channel so the bumped `main` pulls next-version VMR dependencies: \
  `darc update-subscription --id <main_backflow_id> --channel ".NET <NEXT_BAND> SDK" --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **2.3g** **(VMR backflow — skip for a VS-only release.)** Add a backflow from the **outgoing** SDK band into the new release branch so that band keeps flowing into `vs{{THIS_RELEASE_VERSION}}` (mirrors the previous release branch's backflow: source-enabled, source dir `msbuild`, everyDay, Standard merge, excluded assets `*`; the branch is brand-new so pass `-q`): \
  `darc add-subscription --channel ".NET <OUTGOING_BAND> SDK" --source-repo https://github.com/dotnet/dotnet --target-repo https://github.com/dotnet/msbuild --target-branch vs{{THIS_RELEASE_VERSION}} --update-frequency everyDay --source-enabled --source-directory msbuild --excluded-assets '*' --standard-automerge --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr -q`
  - [ ] **2.3h** **Arcade fix-up (run 2.4 first if you haven't).** _If the Arcade subscription from 2.4 below is missing or pointed at the wrong channel, include the fix-up here with `--no-pr` before creating the PR._
  - [ ] **2.3i** **Create the PR** — re-run the final write command without `--no-pr` to open the PR on the configuration branch.
  - [ ] **2.3j** Get the maestro-configuration PR reviewed and merged: {{URL_OF_PHASE2_DARC_PR}}

Verifications (**parallel** — read-only, no ordering dependency):

- [ ] **2.4** Verify the Arcade subscription for `vs{{THIS_RELEASE_VERSION}}`: \
`darc get-subscriptions --exact --target-repo https://github.com/dotnet/msbuild --source-repo https://github.com/dotnet/arcade`
  - **Every supported branch must have an Arcade subscription** from the matching `.NET <X> Eng` channel (the channel is determined by the .NET band the branch is paired with — e.g. a branch paired with .NET 10 subscribes to `.NET 10 Eng`).
> _Roslyn subscription verification intentionally omitted from the per-release checklist: there is always exactly one Roslyn subscription, targeting `main` only and its channel does not rotate with SDK bands._
>
> **NuGet subscription:** when the next-to-ship SDK band rotates (e.g. `4xx` → `5xx`), the NuGet → `msbuild/main` subscription must be re-pointed to the new band's channel.

---

## Phase 3: Bump Main & Update Pipelines

> **Trigger**: Phase 2 DARC updates merged (`main`'s default channel is now `VS {{NEXT_VERSION}}`).

Create **one PR in `main`** containing all of the following changes:

- [ ] **3.1** `eng/Versions.props`: Update `VersionPrefix` to `{{NEXT_VERSION}}.0`
- [ ] **3.2** `eng/Versions.props`: Update `PackageValidationBaselineVersion` to `{{PACKAGE_VALIDATION_BASELINE_VERSION}}`. \
Resolve it deterministically with `pwsh ./scripts/Get-PackageValidationBaseline.ps1 -ThisReleaseVersion {{THIS_RELEASE_VERSION}}` (requires `az login` with devdiv access). See [How to determine `PACKAGE_VALIDATION_BASELINE_VERSION`](https://github.com/dotnet/msbuild/blob/main/.github/skills/release/SKILL.md#how-to-determine-package_validation_baseline_version) in the release skill for the manual fallback.
- [ ] **3.3** `.vsts-dotnet.yml`: Refresh the hardcoded OptProf baseline so the **next** `vs*` branch cut from `main` inherits valid OptProf data (this is what lets that branch's first official build succeed without the manual Phase 4.4 rerun). \
Resolve the current value with `pwsh ./scripts/Get-LatestOptProfDrop.ps1` (requires `az login` with devdiv access), then set it as `OptProfBaselineDrop`: \
`<name: OptProfBaselineDrop` → `value: 'OptimizationData/DotNet-msbuild-Trusted/main/<NNNNNNNN.N>/<buildId>/1'`.
- [ ] **3.4** If the build pipeline fails on API-compat (only then — this step is a fix-up, not a routine action), update `CompatibilitySuppressions.xml` files. Run: \
`dotnet pack MSBuild.Dev.slnf /p:ApiCompatGenerateSuppressionFile=true` \
See [API compat documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/overview) for details.
- [ ] **3.5** Merge main-bump PR: {{URL_OF_NEXT_VERSION_MAIN_BUMP_PR}}

---

## Phase 4: Final Branding & VS Insertion

> **Trigger**: 7 calendar days before `{{INSIDERS_SNAP_DATE}}`. \
> **Precondition**: Phases 1–3 complete. Preview builds from `vs{{THIS_RELEASE_VERSION}}` have been inserting into VS `main` since Phase 2. \
> **Goal**: Final-brand the release branch and get the final-branded bits inserted into VS `main` before VS snaps to `rel/insiders`.

Steps are **sequential**.

- [ ] **4.1** Promote public API on `vs{{THIS_RELEASE_VERSION}}` branch: \
Move contents of `PublicAPI.Unshipped.txt` → `PublicAPI.Shipped.txt` for all projects with API changes. See [release.md](./release.md) for details.
- [ ] **4.2** Bootstrap OptProf for `vs{{THIS_RELEASE_VERSION}}`. **If the Phase 3.3 hardcoded `OptProfBaselineDrop` was kept current, the auto-triggered build should already pick it up (`.vsts-dotnet.yml` seeds `OptProfDrop` from it on `vs*` branches) and this step is a no-op.** Only if the official build still fails for lack of OptProf data (e.g. the baseline was stale/empty at branch-cut):
  - [ ] **4.2a** **Cancel** the auto-triggered official build for `vs{{THIS_RELEASE_VERSION}}`.
  - [ ] **4.2b** **Re-run the official build manually** for `vs{{THIS_RELEASE_VERSION}}` with the OptProf override from `main` — set `Optional OptProfDrop Override` to `main`'s latest OptProf drop path (`pwsh ./scripts/Get-LatestOptProfDrop.ps1`).
- [ ] **4.3** Get M2 or QB approval as necessary per the VS schedule. \
_**Only required if we are behind the VS schedule** — i.e. the insertion didn't land in VS `main` before `{{INSIDERS_SNAP_DATE}}` (4.4 was missed) and a milestone-gate approval is now needed. If the insertion made the schedule, **skip this step**._
- [ ] **4.4** Babysit the VS insertion PR from `vs{{THIS_RELEASE_VERSION}}` into VS `main` (auto-generated at https://devdiv.visualstudio.com/DevDiv/_git/VS/pullrequests). The inserted bits must be in VS `main` **before** `{{INSIDERS_SNAP_DATE}}` so they are included when VS snaps to `rel/insiders`: {{URL_OF_VS_INSERTION}} \
The insertion PR contains the inserted package versions — useful for the nuget.org publishing step.

**After insiders snap** (only if a backport to insiders is needed):

> 🛑 **4.5 and 4.6 are NOT part of the regular release flow — skip them entirely on a normal release.** \
> They only apply when **servicing** a previously-shipped release (i.e. you actually have a hotfix commit on `vs{{THIS_RELEASE_VERSION}}` that needs to be inserted into VS's already-snapped `rel/insiders` or `rel/stable` branch). If you have no such commit to service, leave `AutoInsertTargetBranch` untouched and move on to Phase 5.
>
> ⚠️ When you *do* need to service: re-confirm which VS branch you actually want to insert into before flipping `AutoInsertTargetBranch`. The default is `main`, so forgetting to retarget after the snap silently lands your fix in the next VS instead of the one you're servicing.

- [ ] **4.5** Update [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml): retarget `AutoInsertTargetBranch` for `vs{{THIS_RELEASE_VERSION}}` from VS `main` → `rel/insiders`. This enables direct insertion of hotfix commits into the insiders branch.

**After stable snap** (only if a backport to stable is needed):

- [ ] **4.6** Update [`azure-pipelines/vs-insertion.yml`](../azure-pipelines/vs-insertion.yml): retarget `AutoInsertTargetBranch` for `vs{{THIS_RELEASE_VERSION}}` → `rel/stable`. This enables direct insertion of hotfix commits into the stable branch.

---

## Phase 5: Post-GA

> **Trigger**: `{{VS_SHIP_DATE}}` has passed and VS release has shipped.

Steps are **mostly parallel** unless noted.

- [ ] **5.1** Push packages to nuget.org.

  > **How publishing works:** We don't push packages ourselves. We hand a link to the **Release** artifacts of the official build to the _.NET Release Team_, and they push to nuget.org. Searching past mail for the subject _"Publish MSBuild {{THIS_RELEASE_VERSION}} to NuGet.org" for the template.

  - [ ] **5.1a** Determine the exact MSBuild version that actually shipped to customers.
    - **If this release is coupled with an SDK release: use the SDK as the source of truth**. Look up the MSBuild version baked into the shipped SDK build.
    - Otherwise, read the **authoritative GA'd value from VS `rel/stable`**: the `Microsoft.Build` component version in [`.corext/Configs/msbuild-components.json`](https://devdiv.visualstudio.com/DevDiv/_git/VS?path=/.corext/Configs/msbuild-components.json&version=GBrel/stable) (e.g. `18.7.1-servicing-NNNNN-NN+<sha>`). Extract just the **numeric `VersionPrefix`** from that string — drop the `-servicing-NNNNN-NN+<sha>` suffix — and use it as `{{THIS_RELEASE_EXACT_VERSION}}` (e.g. `18.7.1`). **Do not** rely solely on the VS insertion PR — that PR targets VS `main` and can be superseded by a later servicing insertion before GA, whereas `rel/stable` reflects what actually shipped.
  - [ ] **5.1b** In the [MSBuild official build pipeline](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434), filter to the `vs{{THIS_RELEASE_VERSION}}` branch and locate the build whose output version matches the one identified in 5.1a (e.g. `{{THIS_RELEASE_EXACT_VERSION}}`, such as `18.6.3`).
  - [ ] **5.1c** From that build, open the **Publish Artifacts** step and grab the link to the **`PackageArtifacts/Release`** drop. Verify the **Release** folder contains all of:
    - `Microsoft.Build.Utilities.Core.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Framework.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Runtime.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Tasks.Core.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.NET.StringTools.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
    - `Microsoft.Build.Templates.{{THIS_RELEASE_EXACT_VERSION}}.nupkg`
  - [ ] **5.1d** Email the _.NET Release Team_ with the `Release` link from 5.1c and ask them to publish to nuget.org.

- [ ] **5.2** Publish docs

  > **How publishing works:** The reference-publishing vendor team generates Microsoft Learn reference pages from the shipped MSBuild assemblies/xmldoc and then sends us a docs-repo PR with the regenerated content.

  - [ ] **5.2a** Create a reference-publishing ticket for the new release based on [this existing ticket](https://dev.azure.com/msft-skilling/Content/_workitems/edit/565854) as a template. Then wait for the vendor team to ping you with a link to the generated PR.
  - [ ] **5.2b** Review and approve the docs-repo PR the vendor team opens (example: [msbuild-api-docs#61](https://github.com/dotnet/msbuild-api-docs/pull/61)).
- [ ] **5.3** Create GitHub release:
  - [ ] **5.3a** **Precondition — confirm the previous release tag exists on `upstream`.** \
  `git fetch upstream --tags && git tag --list 'v{{PREVIOUS_RELEASE_EXACT_VERSION}}'` \
  _(Assumes `upstream` is configured as the `dotnet/msbuild` remote. If not: `git remote add upstream https://github.com/dotnet/msbuild.git`.)_ \
  If the tag is missing (e.g. the previous release was never tagged), create and push it **first**.
  - [ ] **5.3b** **Identify the commit to tag.** It is the source commit of the build identified in **5.1b** (the build that produced `{{THIS_RELEASE_EXACT_VERSION}}`). Find the SHA in that build run's "Source version" field on the pipeline page.
  - [ ] **5.3c** Tag this release and push:
    ```
    git checkout <commit identified in 5.3b>
    git tag v{{THIS_RELEASE_EXACT_VERSION}}
    git push upstream v{{THIS_RELEASE_EXACT_VERSION}}
    ```
  - [ ] **5.3d** Create release at https://github.com/dotnet/msbuild/releases/new — use `Generate Release Notes` to prepopulate.
- [ ] **5.4** Update `BootstrapSdkVersion` in [`eng/Versions.props`](https://github.com/dotnet/msbuild/blob/main/eng/Versions.props) if a fresh SDK was released. Check https://dotnet.microsoft.com/download/visual-studio-sdks — always verify the details for the targeted .NET version.
- [ ] **5.4b** Update `tools.dotnet` in [`global.json`](https://github.com/dotnet/msbuild/blob/main/global.json) to the latest released SDK in the targeted band.
- [ ] **5.5** Verify the overall subscription map across **every still-supported branch** — each `vsXX.Y` branch has an Arcade subscription matching its targeted .NET band, and each supported branch's outbound subscriptions land in the right downstream (e.g. SDK band, VMR). \
  You can find more info [here](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/52573/MSBuild-Maestro-Flow).
- [ ] **5.6** Review this tracking issue for any process deviations. If the process changed, create a PR to update `documentation/release-checklist.md` with the improvements.

---

## If {{NEXT_VERSION}} is a new major version

- [ ] Update VS major version references per [release.md](./release.md):
  - [`src/Shared/BuildEnvironmentHelper.cs`](https://github.com/dotnet/msbuild/blob/main/src/Shared/BuildEnvironmentHelper.cs)
  - [`src/Shared/Constants.cs`](https://github.com/dotnet/msbuild/blob/main/src/Shared/Constants.cs)
  - [`src/Framework/Telemetry/TelemetryConstants.cs`](https://github.com/dotnet/msbuild/blob/main/src/Framework/Telemetry/TelemetryConstants.cs)
