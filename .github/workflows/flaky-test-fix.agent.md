---
name: "Flaky Test Fix"
description: "Dispatched fixer that re-verifies a flagged flaky test, attempts a local reproduction, and opens a single draft PR with either a minimal determinism fix or a quarantine ([ActiveIssue]) — never both, never touching .github/**."
on:
  workflow_dispatch:
    inputs:
      failing_test:
        description: "Fully-qualified (normalized) name of the flaky test."
        required: true
        type: string
      test_assembly:
        description: "Test assembly name reported by the detector (e.g. Microsoft.Build.Engine.UnitTests)."
        required: true
        type: string
      affected_sources:
        description: "Comma-separated PR numbers and/or rolling build ids that observed the failure."
        required: false
        type: string
      tracking_issue:
        description: "Issue number of the flaky-test tracking issue to reference and comment on. Required: the quarantine [ActiveIssue] URL points at this issue."
        required: true
        type: string

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
    # gh CLI used by the detector re-verification step (read-only, public repo).
    GH_TOKEN: ${{ github.token }}

permissions:
  contents: read
  issues: read
  pull-requests: read

# Building/restoring msbuild needs the dotnet (NuGet + SDK) ecosystem; the detector re-run needs
# Azure DevOps (dev.azure.com). Test-log artifact `$format=zip` downloads 302-redirect to
# *.vsblob.vsassets.io, which is already covered by the `dotnet` ecosystem identifier.
network:
  allowed:
    - defaults
    - dotnet
    - dev.azure.com

tools:
  edit:
  bash: [":*"]
  github:
    toolsets: [repos, issues, pull_requests]

safe-outputs:
  create-pull-request:
    title-prefix: "[Flaky Test] "
    labels: [flaky-test]
    draft: true
    base-branch: main
    max: 1
    # Belt-and-braces enforcement of "never touch .github/**" (also enforced in the prompt + a git diff check).
    excluded-files:
      - ".github/**"
  add-comment:
    target: "*"
    max: 2

timeout-minutes: 60
---

# Flaky Test Fix (dispatched fixer)

You are an automated maintenance agent for **dotnet/msbuild**. You were dispatched to fix a single
test that the detector flagged as flaky. Read `.github/skills/flaky-test-detector/SKILL.md` for the
data model, conventions, and the assembly → test-project mapping before you start.

Inputs:
- `failing_test`: `${{ github.event.inputs.failing_test }}`
- `test_assembly`: `${{ github.event.inputs.test_assembly }}`
- `affected_sources`: `${{ github.event.inputs.affected_sources }}`
- `tracking_issue`: `${{ github.event.inputs.tracking_issue }}`

## Hard rules (do not violate)

1. **Open at most ONE pull request**, and it must be a **draft**, based on `main`.
2. **Never modify anything under `.github/**`** (no workflow, skill, or action edits). Before opening
   the PR, run `git diff --name-only` and abort the PR (open none) if any changed path is under
   `.github/` or outside the single failing test, its test project, or the one product file proven to
   cause nondeterminism.
3. Only touch the single failing test, its test project, or — for a determinism fix — the specific
   product code that the reproduction proves is the source of nondeterminism.
4. If you cannot do better, **quarantine** the test with `[ActiveIssue]` (see Step 4b). Quarantine is
   the safe default whenever a
   local reproduction is not achievable or the root cause is unclear.
5. Reference the `tracking_issue` in the PR and post one comment on it summarizing the outcome.

## Step 0 — Validate the tracking issue

`tracking_issue` is required. Confirm it is a numeric issue that exists in `dotnet/msbuild` and is
labeled `flaky-test` (e.g. `gh issue view <tracking_issue> --repo dotnet/msbuild --json number,state,labels`).
If it is missing, non-numeric, does not exist, or is not a flaky-test tracking issue, **stop and open
no PR** (a quarantine [ActiveIssue] needs a valid issue URL).

## Step 1 — Re-verify the test is still flaky

Re-run the detector to confirm the signal is current and not a one-off:

```bash
pwsh -File .github/skills/flaky-test-detector/scripts/Get-FlakyTests.ps1 -TargetBranch main -DaysBack 14 -MinSources 3 -JsonOut reverify.json
```

- If `scanComplete` is `false`, **stop**: comment on the tracking issue that re-verification was
  inconclusive (scan incomplete) and open no PR.
- If `failing_test` no longer appears in `flakyTests`, or its `distinctSources` has dropped below 3,
  the flake may have resolved or weakened. Comment on the tracking issue and open no PR.
- If **every** observed failure is on rolling `main` builds with the **same** error signature
  (`distinctPRs == 0` and a single `errorHashes` value), or the only PR failures began after the first
  failing rolling-`main` build, treat this as a **likely real regression, not a flake**: do **not**
  quarantine and do **not** "fix" it. Comment on the tracking issue that this looks like a regression
  needing human investigation, and open no PR.

## Step 2 — Locate the test

Map `test_assembly` to its test project using the convention in the skill (e.g.
`Microsoft.Build.Engine.UnitTests` → `src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj`).
Find the test method source for `failing_test`.

## Step 3 — Attempt a local reproduction

Build the repo once, then run the single test repeatedly to try to reproduce the intermittent failure:

```bash
./build.sh
# repeat the single test many times to surface the flake
for i in $(seq 1 25); do \
  dotnet test src/<TestProject>/<TestProject>.csproj -- --filter-method "*<ShortMethodName>*" || break; \
done
```

The whole-repo build takes ~2-3 minutes — **never cancel it**. Network egress is restricted by the
Agentic Workflow firewall; if NuGet restore or the build fails for environmental/network reasons
(not a test failure), do **not** attempt a code fix — go straight to Step 4b (quarantine) and clearly
note in the PR that local reproduction was blocked by the environment.

**Interpreting the loop:** a flake fails only *some* iterations. If the test instead fails on **every**
iteration (deterministic N/N failure), it is **not** flaky — it is a genuine/regressed failure on
`main`. Do **not** quarantine or "fix" it: comment on the tracking issue that the failure reproduces
deterministically (likely a real bug/regression) and open no PR.

## Step 4 — Produce the fix (exactly one of 4a or 4b)

### 4a — Minimal determinism fix (preferred, ONLY if reproduced)

If — and only if — you reproduced the failure locally **and** identified a concrete, minimal source
of nondeterminism (e.g. test ordering, timing/sleep race, unawaited task, culture/timezone
assumption, reliance on dictionary/enumeration order, shared static state), make the **smallest**
change that removes the nondeterminism. Re-run the test loop to confirm it now passes consistently.
Do not refactor unrelated code. Do not weaken assertions just to make it pass.

### 4b — Quarantine (safe default)

If you could not reproduce, or the root cause is unclear, or the environment blocked the build,
quarantine the test with `[ActiveIssue]` from `Microsoft.DotNet.XUnitV3Extensions` (namespace
`Xunit`, already referenced by every test project and already imported via `using Xunit;`). **Do not
use `[Fact(Skip=...)]`** — `[ActiveIssue]` is the msbuild/dotnet convention: it stamps the
`Category=failing` trait, which normal CI excludes (`--filter-not-trait Category=failing`), and which
the scheduled quarantine pipeline (`azure-pipelines/quarantine.yml`) runs on its own to keep
collecting signal.

Add the attribute **above** the existing `[Fact]`/`[Theory]` (keep the test method and its `[Fact]`/
`[Theory]` intact — do not delete or rename it):

```csharp
// Unconditional quarantine (default):
[ActiveIssue("https://github.com/dotnet/msbuild/issues/<NNNN>")]
[Fact]
public void TheTest() { ... }

// Only when the evidence is clearly platform-specific, scope the quarantine so the test keeps
// running everywhere else (TestPlatforms is in the Xunit namespace):
[ActiveIssue("https://github.com/dotnet/msbuild/issues/<NNNN>", TestPlatforms.Linux)]
```

Use the `tracking_issue` number for `<NNNN>`. **Prefer the unconditional form** unless the evidence
clearly confines the flake to one platform. The scheduled re-validation pipeline
(`azure-pipelines/quarantine.yml`) that keeps collecting data on quarantined tests runs on **Windows,
Linux and macOS**, so a platform-scoped quarantine (`TestPlatforms.Windows`, `Linux`, `OSX`,
`AnyUnix`) is still re-validated on the matching leg. Only scope the quarantine when `affected_sources`
plus the detector's per-source data show the flake is confined to that platform; otherwise use the
unconditional form so the test is fully quarantined. Change only the attributes on the
single failing test.

## Step 5 — Open the PR and comment

- Open one **draft** PR (base `main`, label `flaky-test`) with a clear title and a body that:
  - **starts** with the hidden marker `<!-- flaky-test-id: <failing_test> -->` on its own line, so the
    detector can recognize an in-flight fixer PR and avoid re-dispatching;
  - states whether this is a determinism fix (4a) or a quarantine (4b);
  - links the `tracking_issue` (e.g. `Fixes #<NNNN>` for a real fix, or `Tracked by #<NNNN>` for a
    quarantine — quarantine should NOT auto-close the issue);
  - summarizes the evidence (`affected_sources`);
  - **for a determinism fix (4a)** additionally includes, so reviewers can verify the product-code
    change is justified: the exact local command and failure output that reproduced the flake; the
    root-cause explanation; why a product-code change (not a test-only quarantine) is warranted; the
    post-fix repeated-run command and its pass count; and the list of changed files.
  - for 4b, explicitly notes the test is quarantined pending a real fix.
- Post one `add_comment` on the `tracking_issue` summarizing the action taken and linking the PR.

## Important

- Never ping, cc, or @-mention any user.
- If you take no action (Step 1 stop conditions), open no PR and only comment on the tracking issue.
