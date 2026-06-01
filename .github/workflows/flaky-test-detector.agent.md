---
name: "Flaky Test Triage and Fix"
description: "Scheduled daily workflow that scans recent msbuild CI builds (approved PRs + rolling main builds) for tests that fail across multiple independent sources, files/updates flaky-test tracking issues, then reproduces/fixes-or-quarantines the new candidates and opens ONE combined draft PR per run."
on:
  schedule: daily
  workflow_dispatch: # Allow manual triggering

  # ###############################################################
  # Override the COPILOT_GITHUB_TOKEN secret usage for the workflow
  # with a randomly-selected token from a pool of secrets.
  #
  # As soon as organization-level billing is offered for Agentic
  # Workflows, this stop-gap approach will be removed.
  #
  # See: /.github/actions/select-copilot-pat/README.md
  # ###############################################################
  steps:
    - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
      name: Checkout the select-copilot-pat action folder
      with:
        persist-credentials: false
        sparse-checkout: .github/actions/select-copilot-pat
        sparse-checkout-cone-mode: true
        fetch-depth: 1

    - id: select-copilot-pat
      name: Select Copilot token from pool
      uses: ./.github/actions/select-copilot-pat
      env:
        # If the secret names are changed here, they must also be changed
        # in the `engine: env` case expression below
        SECRET_0: ${{ secrets.COPILOT_GITHUB_TOKEN }}
        SECRET_1: ${{ secrets.COPILOT_GITHUB_TOKEN_1 }}
        SECRET_2: ${{ secrets.COPILOT_GITHUB_TOKEN_2 }}
        SECRET_3: ${{ secrets.COPILOT_GITHUB_TOKEN_3 }}
        SECRET_4: ${{ secrets.COPILOT_GITHUB_TOKEN_4 }}
        SECRET_5: ${{ secrets.COPILOT_GITHUB_TOKEN_5 }}
        SECRET_6: ${{ secrets.COPILOT_GITHUB_TOKEN_6 }}
        SECRET_7: ${{ secrets.COPILOT_GITHUB_TOKEN_7 }}
        SECRET_8: ${{ secrets.COPILOT_GITHUB_TOKEN_8 }}
        SECRET_9: ${{ secrets.COPILOT_GITHUB_TOKEN_9 }}

# Add the pre-activation output of the randomly selected PAT
jobs:
  pre-activation:
    outputs:
      copilot_pat_number: ${{ steps.select-copilot-pat.outputs.copilot_pat_number }}

# Override the COPILOT_GITHUB_TOKEN expression used in the activation job
# Consume the PAT number from the pre-activation step and select the corresponding secret
engine:
  id: copilot
  env:
    # We cannot use line breaks in this expression as it leads to a syntax error in the compiled workflow
    # If none of the `COPILOT_GITHUB_TOKEN_#` secrets were selected, then the default COPILOT_GITHUB_TOKEN is used
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pre_activation.outputs.copilot_pat_number == '0', secrets.COPILOT_GITHUB_TOKEN, needs.pre_activation.outputs.copilot_pat_number == '1', secrets.COPILOT_GITHUB_TOKEN_1, needs.pre_activation.outputs.copilot_pat_number == '2', secrets.COPILOT_GITHUB_TOKEN_2, needs.pre_activation.outputs.copilot_pat_number == '3', secrets.COPILOT_GITHUB_TOKEN_3, needs.pre_activation.outputs.copilot_pat_number == '4', secrets.COPILOT_GITHUB_TOKEN_4, needs.pre_activation.outputs.copilot_pat_number == '5', secrets.COPILOT_GITHUB_TOKEN_5, needs.pre_activation.outputs.copilot_pat_number == '6', secrets.COPILOT_GITHUB_TOKEN_6, needs.pre_activation.outputs.copilot_pat_number == '7', secrets.COPILOT_GITHUB_TOKEN_7, needs.pre_activation.outputs.copilot_pat_number == '8', secrets.COPILOT_GITHUB_TOKEN_8, needs.pre_activation.outputs.copilot_pat_number == '9', secrets.COPILOT_GITHUB_TOKEN_9, secrets.COPILOT_GITHUB_TOKEN) }}
    # gh CLI used by the detector script for PR metadata + existing-issue lookups (read-only, public repo).
    GH_TOKEN: ${{ github.token }}

permissions:
  contents: read
  issues: read
  pull-requests: read

# The detector reaches the public Azure DevOps build APIs (dev.azure.com) and downloads test-log
# artifacts whose `$format=zip` URLs 302-redirect to AzDO's pipeline-artifact blob CDN
# (*.vsblob.vsassets.io), which is covered by the `dotnet` ecosystem identifier.
network:
  allowed:
    - defaults
    - dotnet
    - dev.azure.com

tools:
  edit:
  # The fix phase builds the repo and runs tests, so the agent needs the full bash toolset.
  bash: [":*"]
  github:
    toolsets: [repos, issues, pull_requests]

safe-outputs:
  create-issue:
    title-prefix: "[Flaky Test] "
    labels: [flaky-test]
    max: 5
  add-comment:
    target: "*"
    max: 12
  # When there is nothing to do (incomplete scan, no flakes, or every candidate already tracked/in an
  # open PR) the agent emits a `noop`. Keep it logged in the run summary for debuggability, but do NOT
  # file a GitHub issue for it (the gh-aw default is report-as-issue: true) — a daily no-op run should
  # be silent, not create issue noise.
  noop:
    report-as-issue: false
  create-pull-request:
    title-prefix: "[Flaky Test] "
    labels: [flaky-test]
    draft: true
    base-branch: main
    max: 1
    # Exclusive allowlist: this workflow only ever edits test sources (adding an [ActiveIssue]
    # attribute) or the product code a reproduction proves nondeterministic — all under src/ as .cs.
    # Any change to a root manifest (e.g. NuGet.config, global.json, Directory.Packages.props) or
    # other file is then refused outright. This is a stronger, casing-independent guard than the
    # gh-aw default protected-files manifest list (whose hard-coded "NuGet.Config" entry would miss
    # this repo's lower-cased "NuGet.config" on a case-sensitive filesystem).
    allowed-files:
      - "src/**/*.cs"
    # Belt-and-braces enforcement of "never touch .github/**" (also enforced in the prompt + a git diff check).
    excluded-files:
      - ".github/**"

# Builds the repo once and runs reproduction loops for a few candidates, so allow a generous budget.
timeout-minutes: 120
---

# Flaky Test Triage and Fix (scheduled daily)

You are an automated maintenance agent for the **dotnet/msbuild** repository. Your job is to find
**flaky tests** — tests that fail intermittently rather than because of a real product regression —
track them as GitHub issues, and, in a **single combined draft pull request per run**, either apply a
minimal determinism fix or quarantine them so CI stops being disrupted.

Read the skill at `.github/skills/flaky-test-detector/SKILL.md` for full background on the data path,
the "evidence source" model (approved PRs + rolling `main` builds), the JSON schema, thresholds, the
assembly → test-project mapping, quarantine conventions, and the determinism-fix vs. quarantine
decision. Follow it.

## Overall shape

1. Scan CI for flaky candidates (Step 1–2).
2. Classify flake vs. regression; never quarantine a regression (Step 3).
3. File/update one tracking issue per likely-flake (Step 4).
4. Select the candidates to act on today (Step 5), build the repo **once** (Step 6), and for each
   selected test apply a determinism fix or a quarantine, accumulating **all** edits in the working
   tree (Step 7).
5. Validate the accumulated edits (Step 8) and open **exactly one** combined draft PR (Step 9).

## Step 1 — Run the detector

Run the detector script via the `bash` tool:

```bash
pwsh -File .github/skills/flaky-test-detector/scripts/Get-FlakyTests.ps1 -TargetBranch main -DaysBack 14 -MinSources 3 -MaxBuilds 60 -JsonOut flaky-report.json
```

The script writes a human-readable progress report to the log/host stream and the structured JSON
report to stdout (also written to `flaky-report.json`). Parse the JSON.

## Step 2 — Guard rails (stop conditions)

- If `scanComplete` is `false`, the scan was truncated and is biased. **Do not file issues, do not edit
  any source, and do not open a PR.** Emit a single `noop` explaining the scan was incomplete, and stop.
- If `flakyTests` is empty, emit a `noop` ("no flaky tests detected") and stop.

## Step 3 — Classify each flagged test (flake vs. regression)

For every entry in `flakyTests` (these already meet the `MinSources >= 3` issue threshold), decide
whether it looks like a genuine **flake** (sporadic, non-deterministic) or a **real regression**
(a code change made the test start failing). Quarantining a regression is harmful — it hides a real
bug — so be conservative and treat the following as **likely regression, NOT flake**:

- **all** failures share a single stable `errorHashes` value (identical failure signature), **and**
- the evidence is dominated by rolling `main` builds (`distinctPRs == 0`, **or** the only PR failures
  started *after* the first failing rolling-`main` build — PR validations run against a red `main`
  baseline, so those failures are not independent flake evidence), **and**
- failures are clustered in time (recent `firstSeen`, not spread sporadically across the window).

For a **likely regression**: do **not** create a `flaky-test` issue, do **not** add the
`<!-- flaky-test-id: -->` marker, and do **not** edit/quarantine the test (quarantining would mask the
bug). If an open related issue already exists (`relatedIssues`), post one `add_comment` noting the test
now looks like a **possible regression needing human investigation**. Otherwise emit a `noop` line in
your report flagging it for human triage. Then move on.

Otherwise treat it as a **likely flake** — failures spread across multiple distinct PRs and/or
multiple rolling builds and/or multiple `errorHashes` values and/or multiple distinct days — and
proceed to track (and possibly fix).

## Step 4 — File or update the tracking issue

For each likely-flake test to track, first establish whether an issue already exists. Use
`relatedIssues` from the JSON **and** explicitly search issues for the stable marker (open and
recently-closed), e.g. via the `github` tools or:

```bash
gh issue list --repo dotnet/msbuild --state all --search '"<!-- flaky-test-id: <testName> -->" in:body' --json number,state,title
```

- **If a related issue is OPEN:** post an `add_comment` to that issue number with the **new** evidence
  (latest sources, build URLs, dates, legs/TFMs). Do not open a duplicate.
- **If a related issue exists but is CLOSED recently** (e.g. within ~30 days): do **not** open a
  duplicate. Post an `add_comment` on the closed issue noting the flake has recurred so a human can
  decide whether to reopen, and do not create a new issue.
- **Otherwise (no related issue):** create exactly one issue via `create_issue`:
  - Title: the test's short name (the `[Flaky Test] ` prefix is added automatically), e.g.
    `Microsoft.Build.Engine.UnitTests.SomeClass.SomeMethod`.
  - Body **must** start with the hidden stable marker on its own line so future runs can find it:
    ```
    <!-- flaky-test-id: <testName> -->
    ```
    using the normalized `testName` exactly.
  - Then include an evidence summary: distinct sources (PRs + rolling builds), PR numbers, rolling
    build ids, affected legs/TFMs, assemblies, first/last seen, a representative error message, and
    links to `sampleBuildUrl`. Provide the fully-qualified test name and the assembly so the fix phase
    can locate it.
  - Add brief guidance: the fix is either a minimal determinism fix (preferred, with proven local
    reproduction) or quarantine via `[ActiveIssue("<issue url>")]` (from `Microsoft.DotNet.XUnitV3Extensions`,
    namespace `Xunit`) — not `[Fact(Skip=...)]`.

## Step 5 — Select the candidates to act on in today's PR

Build the set of tests to fix or quarantine in today's combined PR. A candidate qualifies only if **all**
of these hold:

- It was classified as a **likely flake** in Step 3 (never act on a regression).
- It has an **OPEN** `flaky-test` tracking issue (the one created or updated in Step 4). Do **not** use a
  recently-*closed* issue as the quarantine target — an `[ActiveIssue]` URL must point at an open issue.
  If only a closed issue exists, skip the test from today's PR (Step 4 already commented on it).
- It is **not already covered by an open `flaky-test` PR** (this is the primary cross-run dedup — a
  previous run's combined PR may still be open and unmerged, and its quarantines live on **that PR's
  branch, not `main`**, so they will not show up in your fresh `main` checkout). Fetch the bodies of all
  open `flaky-test` PRs **once** up front and **exact-string-match** each candidate's marker locally —
  do **not** rely on GitHub's fuzzy `--search ... in:body`, which strips the HTML-comment punctuation and
  can miss/over-match the marker:
  ```bash
  # One call; there are only ever a handful of open flaky-test PRs. Keep the JSON for Step 8.
  gh pr list --repo dotnet/msbuild --state open --label flaky-test --json number,body --limit 100 > open-flaky-prs.json
  ```
  Treat a candidate as covered if the literal string `<!-- flaky-test-id: <testName> -->` (normalized
  `testName`, exact) appears in any open PR body. Skip every covered candidate.
- It is **not already quarantined** in the working tree: after locating the test (Step 7), if the method
  already carries an `[ActiveIssue]` attribute on `main`, skip it (nothing to do). (Note this only
  catches tests quarantined by an **already-merged** PR; in-flight unmerged PRs are caught by the
  open-PR body match above.)

From the qualifying set, take **at most 8** tests, strongest first (highest `distinctSources`, then
failures spanning multiple distinct days). If none qualify, skip to Step 9 (open no PR; emit a `noop`).

## Step 6 — Build the repo once

Build the whole repo a single time up front so every subsequent reproduction/validation reuses it:

```bash
./build.sh
```

The whole-repo build takes ~2-3 minutes — **never cancel it**. Network egress is restricted by the
Agentic Workflow firewall; if NuGet restore or the build fails for environmental/network reasons (not a
test failure), do **not** attempt any determinism code fix — quarantine every selected test (Step 7b)
and note in the PR that local reproduction was blocked by the environment.

## Step 7 — For each selected test: determinism fix or quarantine

Budget awareness: a reproduction loop is expensive. Attempt a determinism fix only for the **top 3**
strongest candidates and only while wall-clock budget remains; **quarantine** the rest directly. Reserve
time for Step 8 validation and Step 9. Accumulate **all** edits in the working tree — do **not** open a
PR per test.

Locate the test first: map `assemblies[0]` to its test project using the skill's convention (e.g.
`Microsoft.Build.Engine.UnitTests` → `src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj`) and
find the test method source.

### 7a — Minimal determinism fix (preferred, ONLY if reproduced; top candidates only)

Reproduce the intermittent failure by running the single test repeatedly:

```bash
for i in $(seq 1 25); do \
  dotnet test src/<TestProject>/<TestProject>.csproj --no-restore -- --filter-method "*<ShortMethodName>*" || break; \
done
```

**Interpreting the loop:** a flake fails only *some* iterations. If the test fails on **every** iteration
(deterministic N/N failure), it is **not** flaky — treat it as a regression: do **not** quarantine or
"fix" it, comment on the tracking issue that it reproduces deterministically (likely a real bug), and
exclude it from the PR.

If — and only if — you reproduced an intermittent failure **and** identified a concrete, minimal source
of nondeterminism (test ordering, timing/sleep race, unawaited task, culture/timezone assumption,
reliance on dictionary/enumeration order, shared static state), make the **smallest** change that removes
it, then re-run the loop to confirm it now passes consistently. **Prefer test-only fixes.** If a fix would
require a non-trivial **product-code** change, do **not** attempt it in this combined PR — **quarantine**
instead (7b) and leave the real fix to a human follow-up. Never weaken assertions just to make it pass.

### 7b — Quarantine (safe default)

If you could not reproduce, the root cause is unclear, the environment blocked the build, or the fix
would be a non-trivial product-code change, quarantine the test with `[ActiveIssue]` from
`Microsoft.DotNet.XUnitV3Extensions` (namespace `Xunit`, already imported via `using Xunit;`). **Do not
use `[Fact(Skip=...)]`** — `[ActiveIssue]` stamps the `Category=failing` trait, which normal CI excludes
(`--filter-not-trait Category=failing`) and which the scheduled quarantine pipeline
(`azure-pipelines/quarantine.yml`, Windows/Linux/macOS) runs on its own to keep collecting signal.

Add the attribute **above** the existing `[Fact]`/`[Theory]` (keep the method intact):

```csharp
// Unconditional quarantine (default):
[ActiveIssue("https://github.com/dotnet/msbuild/issues/<NNNN>")]
[Fact]
public void TheTest() { ... }

// Only when the evidence clearly confines the flake to one platform (TestPlatforms is in Xunit):
[ActiveIssue("https://github.com/dotnet/msbuild/issues/<NNNN>", TestPlatforms.Linux)]
```

Use the test's **open** tracking-issue number for `<NNNN>`. **Prefer the unconditional form** unless the
per-source data clearly confines the flake to one platform (the quarantine pipeline re-validates on all
three OS legs, so a platform-scoped quarantine is still re-checked on the matching leg).

## Step 8 — Validate the accumulated edits

Before opening the PR, confirm the working tree is clean and correct:

1. `git diff --name-only` — every changed path **must** be a `.cs` file under `src/`. If **any** path is
   under `.github/`, or outside the selected tests / their projects / the proven nondeterministic product
   file, **revert that change** (the PR must contain only intended edits).
2. Compile the affected test projects (e.g. `dotnet build src/<TestProject>/<TestProject>.csproj
   --no-restore`) so a malformed `[ActiveIssue]` attribute or determinism edit can't ship a broken PR. If
   a project no longer compiles, revert the offending edit (or drop that test from the PR).
3. Re-fetch the open `flaky-test` PR bodies (re-run the Step 5 `gh pr list ... --label flaky-test`
   command) and exact-string-match each remaining test's marker against them again — a concurrent run
   may have opened a combined PR since Step 5. Revert and drop any test now covered by an open PR.

## Step 9 — Open ONE combined draft PR

If, after Step 8, no edits remain, open **no** PR and emit a `noop`. Otherwise open **exactly one** draft
PR (`create_pull_request`, base `main`, label `flaky-test`) containing all accumulated edits:

- Title: e.g. `Quarantine/fix <N> flaky tests` (the `[Flaky Test] ` prefix is added automatically).
- Body **must**, for **every** included test, contain its marker on its own line so future runs detect the
  in-flight PR:
  ```
  <!-- flaky-test-id: <testName> -->
  ```
- Then, per test, a short section stating whether it was a **determinism fix (7a)** or a **quarantine
  (7b)**, the affected sources/evidence, and the issue reference:
  - **Determinism fix:** `Fixes #<issue>` (closes the tracking issue on merge). Also include the exact
    local repro command + failure output, the root-cause, why a code change (not a quarantine) is
    warranted, and the post-fix repeated-run pass count.
  - **Quarantine:** `Tracked by #<issue>` (must **not** auto-close — the issue stays open until a real
    fix). Be careful never to write a closing keyword (`Fixes`/`Closes`/`Resolves`) before a quarantine
    issue reference.
- Post one `add_comment` on each included test's tracking issue summarizing the action and linking the PR.

## Important

- Respect all caps (`create-issue` max 5, `add-comment` max 12, `create-pull-request` max 1). If more
  tests qualify than the caps allow, prioritize the highest `distinctSources` first. New tracking issues
  are capped at 5/run; tests with a pre-existing open issue do not consume that budget and may still be
  included in the PR (up to the Step 5 cap of 8).
- **Open at most ONE pull request**, and it must be a **draft** based on `main`.
- **Never modify anything under `.github/**`** (no workflow, skill, or action edits) and never touch root
  manifests (`NuGet.config`, `global.json`, `Directory.Packages.props`, etc.). Only test sources, their
  projects, or a product file a reproduction proves nondeterministic — all `.cs` under `src/`.
- Never invent data. Only act on what the detector JSON reports.
- Do not ping, cc, or @-mention any user.
