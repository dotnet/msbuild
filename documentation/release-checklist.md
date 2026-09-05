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
| Phase 5.1b — official build that produced `{{THIS_RELEASE_EXACT_VERSION}}` | {{URL_OF_SHIPPED_OFFICIAL_BUILD}} |
| Phase 4.7 — Change Waves Learn page sync PR (visualstudio-docs-pr) | {{URL_OF_CHANGE_WAVES_DOCS_PR}} |
| Phase 5.3 — GitHub release tag | https://github.com/dotnet/msbuild/releases/tag/v{{THIS_RELEASE_EXACT_VERSION}} |

---

## Inputs

Fill in these values before starting. Version increments are irregular — they must be specified explicitly.

| Placeholder | Description | Value |
|---|---|---|
| `{{PREVIOUS_RELEASE_VERSION}}` | Version being replaced as latest | |
| `{{PREVIOUS_RELEASE_EXACT_VERSION}}` | The `VersionPrefix` the **previous** release actually shipped as — used by Phase 5.3a to look up its tag. Read it from the previous release's tracking issue, or from `git tag --list 'v{{PREVIOUS_RELEASE_VERSION}}.*'`. | |
| `{{THIS_RELEASE_VERSION}}` | Version being released now | |
| `{{THIS_RELEASE_EXACT_VERSION}}` | The `VersionPrefix` that **actually shipped** to customers — determined in Phase 5.1a, **not** assumed. It is usually `{{THIS_RELEASE_VERSION}}.0`, but servicing insertions routinely ship a higher patch. **Not known when first instantiating this checklist — leave blank until Phase 5.1a confirms it.** | |
| `{{NEXT_VERSION}}` | Version that main will be bumped to | |
| `{{BRANCH_SNAP_DATE}}` | Date we create `vs{{THIS_RELEASE_VERSION}}` from `main`. | |
| `{{INSIDERS_SNAP_DATE}}` | Date VS snaps `main` → `rel/insiders`. Final-branded MSBuild must be in VS `main` **before** this date. From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) | |
| `{{STABLE_SNAP_DATE}}` | Date VS snaps `rel/insiders` → `rel/stable`. From [VS-Dates wiki](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/49807/VS-Dates) | |
| `{{VS_SHIP_DATE}}` | Date VS ships publicly (GA). Post-GA tasks (nuget.org, docs) happen after this. | |
| `{{PACKAGE_VALIDATION_BASELINE_VERSION}}` | Latest `{{THIS_RELEASE_VERSION}}.0-<label>.<shortDate>.<rev>` MSBuild build reachable from `vs{{THIS_RELEASE_VERSION}}` (`<label>` is `PreReleaseVersionLabel` from `eng/Versions.props`, currently `1` — e.g. `18.11.0-1.26426.2`). Used as the ApiCompat baseline for the bumped `main`. **How to determine it:** see the [release skill](https://github.com/dotnet/msbuild/blob/main/.github/skills/release/SKILL.md#how-to-determine-package_validation_baseline_version). | |

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
- [ ] **1.3** **Audit _every_ live `vs*` branch for retirement.** Record the list here — Phase 2.3e uses it to clean up their DARC subscriptions. \
  ⚠️ **This is a full audit, not a single-candidate check.** Do **not** just evaluate `vs{{THIS_RELEASE_VERSION}} - 3`. Retirement is missed far more often than it is done wrongly, and every miss is permanent: nothing else in this process ever revisits an older branch, so stale branches accumulate indefinitely and keep consuming Arcade/OptProf maintenance. Enumerate the full list and judge each one. \
  - [ ] **1.3a** Enumerate current state (both commands, plus the repo's real branches): \
  `darc get-default-channels --source-repo https://github.com/dotnet/msbuild` \
  `darc get-subscriptions --source-repo https://github.com/dotnet/msbuild` \
  `git fetch upstream && git branch -r --list 'upstream/vs*'`

  How to identify a retired branch:
    - **The combined rule:** a branch paired with both an SDK band and a VS version is retired **only when both lifecycles agree it is out of support**. If only one side says retired but the other is still supported, **keep the branch** — the still-supported lifecycle must keep receiving fixes.
    - **SDK lifecycle.** The band ↔ VS version ↔ EOL mapping is the [supported .NET versions table](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs#supported-net-versions). It states which VS version each SDK band pairs with and when that band's support ends. \
    🛑 **Never infer lifecycle from Maestro.** The existence of a `.NET X.Y.Zxx SDK` channel — including `... SDK Release` channels — says nothing about support status; channels for long-dead bands persist indefinitely. Using channel existence as a proxy is how `vs18.6` (band 10.0.3xx, EOL Aug 2026) was missed during the 18.11 release.
    - **VS lifecycle — a separate source; the SDK table does not answer it.** Use [VS Product Lifecycle and Servicing](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs). \
    **VS 2026 and later: 2 years per annual release** — one year of monthly feature updates, then one security-only year on the LTSC. Only the **LTSC baseline version** gets the second year; an ordinary monthly release falls out of support as soon as the next monthly ships. Each year exactly one `vs18.x` becomes the LTSC and must be kept ~2 years while its neighbours retire quickly — read the LTSC table on that page (`2026-LTSC` ends **November 9, 2027**). Rule of thumb for the non-LTSC ones: the window is the current release plus two preceding, so `vs{{THIS_RELEASE_VERSION}} - 3` is the **newest** candidate — never the only one. \
    **Long-lived VS versions — hardcoded, do not re-derive each release:**

      | Visual Studio | MSBuild branch | Supported until |
      |---|---|---|
      | Visual Studio 2022 | `vs17.14` | January 2032 |
      | Visual Studio 2019 | `vs16.11` | April 2029 |
      | Visual Studio 2017 | `vs15.9` | April 2027 |

    - ⚠️ **A VS LTSC can expire *before* the SDK band that shipped with it** — `2026-LTSC` ends Nov 2027 while .NET 10 (LTS) runs to Nov 2028. The SDK side is therefore often what keeps a branch alive; that is why `vs18.0` stays despite being the oldest branch. Always check both directions.
    - **VS-only branches** (not paired with any active SDK band) are retired purely on the VS lifecycle.
    - **Worked example (18.11).** `vs18.0` pairs with 10.0.1xx (EOL Nov 2028) → **keep**, despite being the oldest branch. `vs18.6` pairs with 10.0.3xx (EOL Aug 2026) and VS 18.6 is outside the window → **retire**. `vs18.4` (10.0.2xx, EOL May 2026) and `vs18.5` (VS-only) had default channels for branches already deleted from the repo → **retire the mappings**.

  - [ ] **1.3b** For **each** `vs*` default channel, apply the rules above and classify it as **keep** or **retire**. Two red flags that almost always mean "retire", and are worth checking first because they are mechanical:
    - **No outbound subscription** — nothing consumes that branch's `VS X.Y` channel, so the branch feeds nothing. (A live branch looks like `vs18.0 → dotnet/dotnet release/10.0.1xx`.)
    - **Default channel for a branch that does not exist** in `dotnet/msbuild` — a pure orphan; delete the mapping.
  - [ ] **1.3c** Record the verdict for every branch in the table below, including the ones you keep and why. This is what makes the next release's audit cheap.

  | Branch | Paired SDK band | Band EOL | VS supported? | Verdict |
  |---|---|---|---|---|
  | | | | | |

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
  - [ ] **2.3e** **Delete subscriptions for retired branches.** For each branch identified as retired in step 1.3 (apply the same combined SDK+VS rule — do **not** delete subscriptions for a branch that's retired on only one side, since fixes must keep flowing into the still-supported lifecycle), remove its inbound subscriptions and any default channel associations.
  List them: `darc get-subscriptions --target-repo https://github.com/dotnet/msbuild --target-branch <retired_branch>` \
  Delete each: `darc delete-subscriptions --id <subscription_id> -q --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr` \
  _(Note the plural verb: `delete-subscription` does not exist. `-q` skips the confirmation prompt, which would otherwise hang in a non-interactive session.)_
  - [ ] **2.3f** **(VMR backflow — skip for a VS-only release, or if 2.2b found the channel unchanged.)** Repoint the `→ main` backflow (ID from 2.2b) to the **next** SDK band channel so the bumped `main` pulls next-version VMR dependencies: \
  `darc update-subscription --id <main_backflow_id> --channel ".NET <NEXT_BAND> SDK" --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr`
  - [ ] **2.3g** **(VMR backflow — skip for a VS-only release.)** Add a backflow from the **outgoing** SDK band into the new release branch so that band keeps flowing into `vs{{THIS_RELEASE_VERSION}}` (mirrors the previous release branch's backflow: source-enabled, source dir `msbuild`, everyDay, Standard merge, excluded assets `*`; the branch is brand-new so pass `-q`): \
  `darc add-subscription --channel ".NET <OUTGOING_BAND> SDK" --source-repo https://github.com/dotnet/dotnet --target-repo https://github.com/dotnet/msbuild --target-branch vs{{THIS_RELEASE_VERSION}} --update-frequency everyDay --source-enabled --source-directory msbuild --excluded-assets '*' --standard-automerge --configuration-branch msbuild-{{THIS_RELEASE_VERSION}}-main-bump --no-pr -q`
  - [ ] **2.3h** **Arcade fix-up (run 2.4 first if you haven't).** _If the Arcade subscription from 2.4 below is missing or pointed at the wrong channel, include the fix-up here with `--no-pr` before creating the PR._
  - [ ] **2.3i** **Create the PR** — omit `--no-pr` on the *last* write command of the batch so it both applies its change and opens the PR. (Do **not** re-run an already-applied command without `--no-pr`; that would duplicate the change.)
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

**Change waves documentation sync**:

- [ ] **4.7** Sync the public [Change Waves Learn page](https://learn.microsoft.com/visualstudio/msbuild/change-waves) with `documentation/wiki/ChangeWaves.md`. \
The page must list the waves live in **VS Insiders** — the same set that ships in the **latest released preview SDK**.
  - [ ] **4.7a** Confirm the version in Insiders — normally `{{THIS_RELEASE_VERSION}}` right after the snap: the `Microsoft.Build` version in [`msbuild-components.json` on VS `rel/insiders`](https://devdiv.visualstudio.com/DevDiv/_git/VS?path=/.corext/Configs/msbuild-components.json&version=GBrel/insiders), cross-checked with `dotnet msbuild -version` from the latest preview SDK (https://dotnet.microsoft.com/download/dotnet). If they disagree, pick latest.
  - [ ] **4.7b** Port the wave lists and their section grouping from [`documentation/wiki/ChangeWaves.md` on `vs{{THIS_RELEASE_VERSION}}`](https://github.com/dotnet/msbuild/blob/vs{{THIS_RELEASE_VERSION}}/documentation/wiki/ChangeWaves.md). Bump `ms.date`.
  - [ ] **4.7c** Open the PR against [MicrosoftDocs/visualstudio-docs-pr](https://github.com/MicrosoftDocs/visualstudio-docs-pr) (**not** the public `visualstudio-docs` mirror), file `docs/msbuild/change-waves.md`. Example: [visualstudio-docs-pr#15662](https://github.com/MicrosoftDocs/visualstudio-docs-pr/pull/15662).

---

## Phase 5: Post-GA

> **Trigger**: `{{VS_SHIP_DATE}}` has passed and VS release has shipped.

Steps are **mostly parallel** unless noted.

- [ ] **5.1** Push packages to nuget.org.

  > **How publishing works:** We don't push packages ourselves. We hand a link to the **Release** artifacts of the official build to the _.NET Release Team_, and they push to nuget.org. Search past mail for the subject _"Publish MSBuild {{THIS_RELEASE_VERSION}} to NuGet.org"_ for the template.

  - [ ] **5.1a** Determine the exact MSBuild version that actually shipped to customers. \
  ⚠️ **VS and the SDK might ship _different_ patch versions off the same `vs{{THIS_RELEASE_VERSION}}` branch during servicing — so this must be looked up, never inferred from `eng/Versions.props` at branch HEAD or from the Phase 4.4 insertion PR.**
    - **First, decide whether this release is coupled with an SDK release:** \
    `darc get-subscriptions --target-repo https://github.com/dotnet/msbuild --target-branch vs{{THIS_RELEASE_VERSION}} --source-repo https://github.com/dotnet/dotnet` \
    If that returns a source-enabled subscription from a `.NET <X.Y.Zxx> SDK` channel, the release **is** SDK-coupled.
    - **If SDK-coupled: the SDK is the source of truth** — look up the MSBuild version baked into the shipped SDK build of that band. It wins over VS `rel/stable`.
    - **Otherwise**, read the **authoritative GA'd value from VS `rel/stable`**: the `Microsoft.Build` component version in [`.corext/Configs/msbuild-components.json`](https://devdiv.visualstudio.com/DevDiv/_git/VS?path=/.corext/Configs/msbuild-components.json&version=GBrel/stable) (e.g. `18.7.1-servicing-NNNNN-NN+<sha>`). Extract just the **numeric `VersionPrefix`** from that string — drop the `-servicing-NNNNN-NN+<sha>` suffix — and use it as `{{THIS_RELEASE_EXACT_VERSION}}` (e.g. `18.7.1`). **Do not** rely solely on the VS insertion PR — that PR targets VS `main` and can be superseded by a later servicing insertion before GA, whereas `rel/stable` reflects what actually shipped.
    - _Worked example (18.9): the Phase 4.4 insertion PR said `18.9.0`, VS `rel/stable` said `18.9.1`, branch HEAD was already `18.9.8` — and the correct answer was `18.9.6`, from the coupled .NET 10.0.4xx SDK._
  - [ ] **5.1b** In the [MSBuild official build pipeline](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434), filter to the `vs{{THIS_RELEASE_VERSION}}` branch and locate the build whose output version matches the one identified in 5.1a (e.g. `{{THIS_RELEASE_EXACT_VERSION}}`, such as `18.6.3`). Take the latest build that produced the matching versioned artifacts.
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
  - [ ] **5.3d** Create release at https://github.com/dotnet/msbuild/releases/new — use `Generate Release Notes` to prepopulate, with `v{{PREVIOUS_RELEASE_EXACT_VERSION}}` as the previous tag.
- [ ] **5.4** Update `BootstrapSdkVersion` in [`eng/Versions.props`](https://github.com/dotnet/msbuild/blob/main/eng/Versions.props) if a fresh SDK was released. Check https://dotnet.microsoft.com/download/visual-studio-sdks — always verify the details for the targeted .NET version.
- [ ] **5.4b** Update `tools.dotnet` in [`global.json`](https://github.com/dotnet/msbuild/blob/main/global.json) to the latest released SDK in the targeted band.
- [ ] **5.5** Verify the overall subscription map across **every still-supported branch** — each `vsXX.Y` branch has an Arcade subscription matching its targeted .NET band, and each supported branch's outbound subscriptions land in the right downstream (e.g. SDK band, VMR). \
  You can find more info [here](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/52573/MSBuild-Maestro-Flow).
- [ ] **5.6** Review this tracking issue for any process deviations. If the process changed, create a PR to update `documentation/release-checklist.md` with the improvements.

---

## If {{NEXT_VERSION}} is a new major version

- [ ] Update VS major version references per [release.md](./release.md):
  - [`src/Framework/BuildEnvironmentHelper.cs`](https://github.com/dotnet/msbuild/blob/main/src/Framework/BuildEnvironmentHelper.cs)
  - [`src/Framework/Constants.cs`](https://github.com/dotnet/msbuild/blob/main/src/Framework/Constants.cs)
  - [`src/Framework/Telemetry/TelemetryConstants.cs`](https://github.com/dotnet/msbuild/blob/main/src/Framework/Telemetry/TelemetryConstants.cs)
