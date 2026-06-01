---
name: "Flaky Test Detector"
description: "Scheduled triage that scans recent msbuild CI builds (approved PRs + rolling main builds) for tests that fail across multiple independent sources, files/updates flaky-test tracking issues, and optionally dispatches the fixer."
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
  bash: ["pwsh", "pwsh:*", "gh", "gh:*", "cat", "ls", "echo"]
  github:
    toolsets: [issues]

safe-outputs:
  create-issue:
    title-prefix: "[Flaky Test] "
    labels: [flaky-test]
    max: 5
  add-comment:
    target: "*"
    max: 10
  dispatch-workflow:
    workflows: [flaky-test-fix.agent]
    max: 2

timeout-minutes: 30
---

# Flaky Test Detector (scheduled triage)

You are an automated maintenance agent for the **dotnet/msbuild** repository. Your job is to find
**flaky tests** — tests that fail intermittently rather than because of a real product regression —
and to track them as GitHub issues so they can be fixed.

Read the skill at `.github/skills/flaky-test-detector/SKILL.md` for full background on the data path,
the "evidence source" model (approved PRs + rolling `main` builds), the JSON schema, thresholds, and
conventions. Follow it.

## Step 1 — Run the detector

Run the detector script via the `bash` tool:

```bash
pwsh -File .github/skills/flaky-test-detector/scripts/Get-FlakyTests.ps1 -TargetBranch main -DaysBack 14 -MinSources 3 -MaxBuilds 60 -JsonOut flaky-report.json
```

The script prints a human report to stderr and the structured JSON report to stdout (also written to
`flaky-report.json`). Parse the JSON.

## Step 2 — Guard rails (stop conditions)

- If `scanComplete` is `false`, the scan was truncated and is biased. **Do not file issues or dispatch
  the fixer.** Emit a single `noop` explaining the scan was incomplete, and stop.
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
`<!-- flaky-test-id: -->` marker, and do **not** dispatch the fixer (quarantining would mask the bug).
If an open related issue already exists (`relatedIssues`), post one `add_comment` noting the test now
looks like a **possible regression needing human investigation**. Otherwise emit a `noop` line in your
report flagging it for human triage. Then move on.

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
    links to `sampleBuildUrl`. Provide the fully-qualified test name and the assembly so a fixer can
    locate it.
  - Add brief guidance: the fix is either a minimal determinism fix (preferred, with proven local
    reproduction) or quarantine via `[ActiveIssue("<issue url>")]` (from `Microsoft.DotNet.XUnitV3Extensions`,
    namespace `Xunit`) — not `[Fact(Skip=...)]`.

## Step 5 — Optionally dispatch the fixer (at most a couple)

Only for **strong, likely-flake** candidates — `distinctSources >= 5` **or** failures spanning
multiple distinct days (`firstSeen` != `lastSeen`) — dispatch the fixer with `dispatch_workflow`
(workflow `flaky-test-fix.agent`). The `max: 2` cap is enforced; pick the strongest candidates.

Before dispatching, skip the candidate if a fixer is already in flight. Check for an existing open
fixer PR carrying the same marker, e.g.:

```bash
gh pr list --repo dotnet/msbuild --state open --search '"<!-- flaky-test-id: <testName> -->" in:body' --json number
```

Skip dispatch for likely-regression entries (Step 3), for tests with an open marker-carrying fixer PR,
and for tests dispatched very recently (look for a recent bot dispatch comment on the tracking issue).

Provide these inputs to each dispatch:

- `failing_test`: the fully-qualified (normalized) test name.
- `test_assembly`: the assembly name (from `assemblies[0]`).
- `affected_sources`: a short comma-separated list of PR numbers and rolling build ids.
- `tracking_issue`: the issue number you filed or updated in Step 4.

## Important

- Respect all caps (`create-issue` max 5, `add-comment` max 10, `dispatch-workflow` max 2). If more
  tests qualify than the caps allow, prioritize the highest `distinctSources` first.
- Never invent data. Only act on what the detector JSON reports.
- Do not ping, cc, or @-mention any user.
