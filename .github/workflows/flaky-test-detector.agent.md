---
name: "Flaky Test Triage and Fix"
description: "Scheduled daily workflow that scans recent msbuild CI builds (approved PRs + rolling main builds) for tests that fail across multiple independent sources, files/updates flaky-test tracking issues, then reproduces/fixes-or-quarantines the new candidates. It also scans the quarantine pipeline (definition 344) to un-quarantine tests that have gone consistently green and re-fix tests still flaking there, opening ONE combined draft PR per run."
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

1. Scan CI for **new** flaky candidates (Step 1) and scan the **quarantine pipeline** for backlog
   signal (Step 1b).
2. Classify flake vs. regression; never quarantine a regression (Step 3).
3. File/update one tracking issue per likely-flake (Step 4).
4. Select the candidates to act on today: new flakes to fix/quarantine (Step 5) **and** backlog
   actions — un-quarantine consistently-green tests + re-fix still-flaky quarantined tests (Step 5b).
5. Build the repo **once** (Step 6), and for each selected test apply a determinism fix, a quarantine,
   or an **un-quarantine**, accumulating **all** edits in the working tree (Step 7).
6. Validate the accumulated edits (Step 8) and open **exactly one** combined draft PR (Step 9).

## Step 1 — Run the detector

Run the detector script via the `bash` tool:

```bash
pwsh -File .github/skills/flaky-test-detector/scripts/Get-FlakyTests.ps1 -TargetBranch main -DaysBack 14 -MinSources 3 -MaxBuilds 60 -JsonOut flaky-report.json
```

The script writes a human-readable progress report to the log/host stream and the structured JSON
report to stdout (also written to `flaky-report.json`). Parse the JSON.

## Step 1b — Scan the quarantine pipeline (backlog signal)

Also scan the **quarantine pipeline** (AzDO definition **344**, `azure-pipelines/quarantine.yml`),
which re-runs **only** the already-quarantined (`[ActiveIssue]` / `Category=failing`) tests on
Windows/Linux/macOS twice daily. This is the signal for **clearing the backlog**: un-quarantining
tests that have gone consistently green, and re-attempting fixes on tests still flaking there. It uses
the **same detector** with `-IncludePassed`, which also records passing observations:

```bash
pwsh -File .github/skills/flaky-test-detector/scripts/Get-FlakyTests.ps1 -DefinitionId 344 -TargetBranch main -DaysBack 21 -MinSources 2 -MaxBuilds 60 -IncludePassed -JsonOut quarantine-health.json
```

This emits the usual JSON plus a `passedTests` array (per normalized test: `distinctBuilds`,
`distinctDays`, `buildIds`, `legs`, `tfms`, `assemblies`, `firstSeen`/`lastSeen`). Interpret it as:

- `flakyTests` = quarantined tests **still flaking** in 344 (failed across ≥ `MinSources` distinct
  quarantine builds) → **backlog-fix** candidates.
- `passedTests` = quarantined tests **observed passing** (actually ran and passed) in 344 → potential
  **un-quarantine** candidates.
- A test appearing in **both** is still flaky — never un-quarantine it.

Then enumerate what is **currently quarantined on `main`** so each quarantined test can be mapped to its
tracking issue and source location:

```bash
grep -rn "ActiveIssue(" src --include=*.cs
```

Each `[ActiveIssue("https://github.com/dotnet/msbuild/issues/<NNNN>"[, TestPlatforms.X])]` embeds the
tracking-issue URL (and any platform scope) directly above the test method, so the URL gives the issue
number and the file:line gives the source to edit. Derive each quarantined test's fully-qualified name
(`Namespace.Class.Method`) from its source file to match it against `flakyTests`/`passedTests` by
`testName`.

## Step 2 — Guard rails (stop conditions)

- If the **new-flake** scan's `scanComplete` (Step 1) is `false`, that scan was truncated and is biased.
  **Do not file issues, do not quarantine, and do not open a PR from it.** Emit a single `noop`
  explaining the scan was incomplete, and stop the new-flake flow.
- If `flakyTests` from Step 1 is empty (and no backlog actions qualify below), emit a `noop` ("no flaky
  tests detected") and stop.
- The **backlog** scan (Step 1b) is independent. If **its** `scanComplete` is `false`, do **not** act on
  the backlog this run (no un-quarantines, no backlog fixes) — biased green/flaky data could wrongly
  un-quarantine a still-flaky test — but the new-flake flow (Steps 3–9) may still proceed. If def 344 has
  no builds yet (`buildsScanned == 0`), there is simply no backlog signal — skip the backlog work. Note:
  if **both** scans yield nothing actionable, emit a `noop` and open no PR.

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
failures spanning multiple distinct days). If none qualify, there is no new-flake work today — continue
to Step 5b (backlog actions may still apply).

## Step 5b — Select backlog actions from quarantine health (independent caps)

Using the Step 1b data (only if its `scanComplete` is `true`), pick **backlog** actions on the
**already-quarantined** tests. These have **separate caps** from the new-flake work so the combined PR
stays reviewable; they still fold into the same single PR.

**Un-quarantine candidates** — a currently-quarantined test `T` (from the `grep`) qualifies only if:

- `T` is in `passedTests` with `distinctBuilds >= 4` **and** `distinctDays >= 3` (genuinely green across
  many real CI runs spanning multiple days — not a one-off), **and**
- `T` is **not** in the Step 1b `flakyTests` at all (zero failures in 344 over the window), **and**
- the green evidence covers the platform scope the `[ActiveIssue]` actually applies to:
  - **Unconditional** `[ActiveIssue(url)]`: require passing observations on **Windows, Linux, and
    macOS** legs (`passedTests.legs` spans all three OS families). If green on only some platforms, do
    **not** fully un-quarantine — at most **narrow** the attribute to the still-untrusted platform(s)
    (e.g. add `TestPlatforms.Windows`) **only** when that edit drops no existing argument and compiles;
    otherwise leave it quarantined.
  - **Platform-scoped** `[ActiveIssue(url, TestPlatforms.X)]`: require green on the leg(s) for `X`.

  Take **at most 5** un-quarantines per run, strongest first (most `distinctBuilds`, then `distinctDays`).

**Backlog-fix candidates** — a currently-quarantined test `T` that is in the Step 1b `flakyTests` (still
flaking in 344). These are eligible for a **determinism fix attempt** (Step 7), but only succeed if they
reproduce on this Linux/.NET runner; a Windows-, macOS-, or `net472`-only flake cannot be reproduced or
verified here, so it stays quarantined. Take **at most 3** per run, strongest first.

If neither new-flake (Step 5) nor backlog (Step 5b) candidates qualify, open no PR and emit a `noop`.

## Step 6 — Build the repo once

Build the whole repo a single time up front so every subsequent reproduction/validation reuses it:

```bash
./build.sh
```

The whole-repo build takes ~2-3 minutes — **never cancel it**. `./build.sh` also builds every test
project, so each test's runnable assembly already exists under
`artifacts/bin/<TestProject>/Debug/<tfm>/<TestProject>.dll` for the reproduction loops below. Network
egress is restricted by the Agentic Workflow firewall; if NuGet restore or the build fails for
environmental/network reasons (not a test failure), do **not** attempt any determinism code fix —
quarantine every selected test (Step 7d) and note in the PR that local reproduction was blocked by the
environment.

## Step 7 — Reproduce, then fix, quarantine, or un-quarantine

Accumulate **all** edits in one working tree — do **not** open a PR per test. The plan: build candidate
projects once (7a), screen **all** selected reproduction candidates for reproducibility **in parallel**
(7b), then author determinism fixes **sequentially** for the strongest reproducers (7c) and quarantine
the rest (7d), and finally **un-quarantine** the consistently-green backlog tests (7e). Reserve
wall-clock time for Step 8 validation and Step 9.

Treat the **backlog-fix** candidates from Step 5b (still flaking in 344) exactly like new flakes in
7a–7c: locate, build, screen, and attempt a determinism fix. The only difference is that a backlog test
is **already quarantined**, so the "quarantine" fallback (7d) is a no-op for it — if you cannot
reproduce-and-fix it on this runner, **leave its existing `[ActiveIssue]` in place** (no edit). If you
**do** confirm a fix, additionally **remove** its existing `[ActiveIssue]` attribute (un-quarantine it)
as part of the fix.

### 7a — Locate tests and build their projects (sequential)

For each selected test, map `assemblies[0]` to its test project using the skill's convention (e.g.
`Microsoft.Build.Engine.UnitTests` → `src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj`) and
find the test method source. For each **distinct** candidate project, run one fast incremental build so
the test assembly is current (no-op if `./build.sh` already built it):

```bash
dotnet build src/<TestProject>/<TestProject>.csproj --no-restore
```

### 7b — Screen all candidates for reproducibility (parallel)

Reproduce by running each test's **prebuilt assembly directly** — *not* `dotnet test`. The direct
assembly skips MSBuild and (on Linux) the auto-injected `--coverage`, so many loops can run at once
without coverage-file contention. Run the loops under **bounded concurrency (~4)**, one pass/fail tally
per candidate. A single reproduction is:

```bash
# Resolve the test assembly once per project (TFM-agnostic):
#   dll=$(ls -1 artifacts/bin/<TestProject>/Debug/net*/<TestProject>.dll | head -n1)
# Use the FULLY-QUALIFIED test name from the TRX (Namespace.Class.Method), NOT a bare "*Name*",
# so a same-named method in a different class can't be screened by mistake:
dotnet "$dll" --filter-method "MyNamespace.MyClass.MyMethod"
```

Run this **once without redirecting output** first and eyeball it: confirm exactly the intended test
ran (sane match count, the expected method name) before committing to the bulk loop below.

Screen all candidates concurrently with `xargs -P`. Build a tab-separated manifest (one line per
candidate: `<id>` TAB `<absolute dll path>` TAB `<fully-qualified method filter>`), then:

```bash
rm -rf /tmp/repro && mkdir -p /tmp/repro && : > /tmp/repro/candidates.tsv
# ...append one TAB-separated line per candidate to /tmp/repro/candidates.tsv...
# <id> must match [A-Za-z0-9_] (e.g. the tracking-issue number) — it names the result file.

xargs -a /tmp/repro/candidates.tsv -P 4 -d '\n' -n 1 -r bash -c '
  IFS=$'"'"'\t'"'"' read -r id dll method <<< "$0"
  if [ ! -f "$dll" ]; then
    printf "%s\tpass=0\tfail=0\tstatus=setup-error\n" "$id" > "/tmp/repro/$id.result"; exit 0
  fi
  pass=0; fail=0; status=ok
  for i in $(seq 1 25); do
    timeout --kill-after=10s 120s dotnet "$dll" --filter-method "$method" >/dev/null 2>&1
    rc=$?
    if   [ "$rc" -eq 0   ]; then pass=$((pass+1))
    elif [ "$rc" -eq 8   ]; then status=nomatch; break   # MTP exit 8 = filter matched no test
    elif [ "$rc" -eq 124 ]; then status=timeout; break   # test hung (>120s) — likely a real deadlock
    else fail=$((fail+1)); fi
  done
  printf "%s\tpass=%d\tfail=%d\tstatus=%s\n" "$id" "$pass" "$fail" "$status" > "/tmp/repro/$id.result"
'
cat /tmp/repro/*.result
```

Do **not** use `set -e` around this — a test failure is expected and must not abort the loop. Classify
each candidate from its result file:

- **`status=setup-error`** (assembly not found): the manifest path is wrong — re-resolve the dll (Step
  7a) and re-run; do **not** treat a missing assembly as a failing test.
- **`status=nomatch`** (exit 8 on the first run): the filter located no test — re-check the
  fully-qualified name from the TRX; if still no match, quarantine it (7d) and note it could not be run.
- **`status=timeout`** (a run exceeded 120s): the test **hung**, which is a real problem (likely a
  deadlock), not a flake the quarantine pipeline should keep re-running. Do **not** fix or quarantine —
  comment on the tracking issue and exclude it from the PR.
- **`pass>0` and `fail>0`** (intermittent): **reproduced** — a fix candidate for 7c.
- **`fail=0`** (passed every iteration): could **not** reproduce — quarantine it (7d).
- **`fail=25`** (failed every iteration): possibly a real regression, **but** parallel CPU pressure can
  make a load-sensitive test fail deterministically. Re-run it **alone, sequentially** (no other loops
  running) for 25 iterations before deciding. If it still fails N/N, it is **not** flaky — treat it as a
  regression: do **not** quarantine or "fix" it, comment on the tracking issue that it reproduces
  deterministically (likely a real bug), and exclude it from the PR. If it now shows intermittency,
  treat it as a reproduced flake (7c).

### 7c — Author determinism fixes (sequential, strongest reproducers, time-boxed)

For the reproduced (intermittent) candidates only, author minimal fixes **one at a time** — never with
screening loops still running, so timing is representative. Attempt at most the **5 strongest**
reproducers, and **stop early** when less than ~30 minutes of budget remains, or after ~10–15 minutes on
a single test without a concrete root cause (quarantine that test instead and move on).

**First, re-confirm the candidate alone, sequentially** (no other loops running) with the same 25×
`timeout`-wrapped loop on just that test. Parallel screening can induce failures via CPU starvation or
child-process/temp-dir interference between MSBuild tests, so only proceed to a fix if the test **still**
fails intermittently on its own. If it now passes 25/25 in isolation, you cannot reproduce the cause to
verify a fix — **quarantine it (7d)** instead of fixing.

A fix is valid only if you identified a concrete, minimal source of nondeterminism (test ordering,
timing/sleep race, unawaited task, culture/timezone assumption, reliance on dictionary/enumeration order,
shared static state) and made the **smallest** change that removes it. **Prefer test-only fixes.** Never
weaken assertions just to make a test pass. If the fix would require a non-trivial **product-code**
change, do **not** attempt it here — **quarantine** instead (7d) and leave the real fix to a human.

After each fix, **rebuild that project and re-run the loop sequentially** to confirm it now passes
consistently (a `--no-build` re-run would validate stale binaries):

```bash
dotnet build src/<TestProject>/<TestProject>.csproj --no-restore
dll=$(ls -1 artifacts/bin/<TestProject>/Debug/net*/<TestProject>.dll | head -n1)
for i in $(seq 1 25); do timeout --kill-after=10s 120s dotnet "$dll" --filter-method "MyNamespace.MyClass.MyMethod" >/dev/null 2>&1 || { echo "still failing on iteration $i"; break; }; done
```

If it still fails intermittently after the fix, revert the edit and quarantine the test instead.

### 7d — Quarantine (safe default for everything not fixed)

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

### 7e — Un-quarantine consistently-green backlog tests

For each **un-quarantine candidate** selected in Step 5b, locate the test (the Step 1b `grep` already
gave `file:line`; `assemblies[0]` → project confirms it) and **delete** the `[ActiveIssue(...)]`
attribute for the scope now proven green — the **whole attribute line** for a full un-quarantine, or the
single platform-scoped attribute for a narrowing — leaving the `[Fact]`/`[Theory]` and the method body
intact. For a **narrowing** (green on some platforms only), rewrite the attribute to cover only the
still-untrusted platform(s) without dropping any existing argument.

**Do not run a local reproduction to "confirm green."** Definition 344 runs the real test in real CI
across many builds and multiple days and is the authority here; a single local pass (only on this
Linux/.NET runner) is **weaker** evidence and cannot speak for Windows/macOS/`net472`. Rely on the 344
`passedTests` evidence (and the absence from 344 `flakyTests`) that Step 5b already gated on.

After editing, the project is rebuilt and the diff re-checked in Step 8.

## Step 8 — Validate the accumulated edits

Before opening the PR, confirm the working tree is clean and correct:

1. `git diff --name-only` — every changed path **must** be a `.cs` file under `src/`. If **any** path is
   under `.github/`, or outside the selected tests / their projects / the proven nondeterministic product
   file, **revert that change** (the PR must contain only intended edits). Expected edits are: an added
   `[ActiveIssue]` (new quarantine, 7d), a determinism fix (7c), or a **removed/narrowed** `[ActiveIssue]`
   (un-quarantine, 7e) — all in the selected tests' `.cs` files.
2. Compile the affected test projects (e.g. `dotnet build src/<TestProject>/<TestProject>.csproj
   --no-restore`) so a malformed `[ActiveIssue]` attribute or determinism edit can't ship a broken PR. If
   a project no longer compiles, revert the offending edit (or drop that test from the PR).
3. Re-fetch the open `flaky-test` PR bodies (re-run the Step 5 `gh pr list ... --label flaky-test`
   command) and exact-string-match each remaining test's marker against them again — a concurrent run
   may have opened a combined PR since Step 5. Revert and drop any test now covered by an open PR.

## Step 9 — Open ONE combined draft PR

If, after Step 8, no edits remain, open **no** PR and emit a `noop`. Otherwise open **exactly one** draft
PR (`create_pull_request`, base `main`, label `flaky-test`) containing all accumulated edits:

- Title: e.g. `Quarantine/fix/un-quarantine <N> flaky tests` (the `[Flaky Test] ` prefix is added
  automatically); summarize the mix of actions.
- Body **must**, for **every** included test, contain its marker on its own line so future runs detect the
  in-flight PR:
  ```
  <!-- flaky-test-id: <testName> -->
  ```
- Then, per test, a short section stating whether it was a **determinism fix (7c)**, a **quarantine
  (7d)**, or an **un-quarantine (7e)**, the affected sources/evidence, and the issue reference:
  - **Determinism fix:** `Fixes #<issue>` (closes the tracking issue on merge). Also include the exact
    local repro command + failure output, the root-cause, why a code change (not a quarantine) is
    warranted, and the post-fix repeated-run pass count.
  - **Quarantine:** `Tracked by #<issue>` (must **not** auto-close — the issue stays open until a real
    fix). Be careful never to write a closing keyword (`Fixes`/`Closes`/`Resolves`) before a quarantine
    issue reference.
  - **Un-quarantine (7e):** state the def-344 evidence (distinct green builds, distinct days, legs, the
    window) that justifies removing the `[ActiveIssue]`. Use `Fixes #<issue>` **only if** no other
    `[ActiveIssue(".../issues/<issue>")]` reference remains anywhere in the tree after your edits (i.e.
    the issue is fully resolved — including any platform-scoped sibling you only **narrowed**). If other
    tests still reference the same issue, or you only narrowed the scope, use `Tracked by #<issue>`
    instead and do **not** write a closing keyword. For a **narrowing**, always `Tracked by #<issue>`.
- Post one `add_comment` on each included test's tracking issue summarizing the action and linking the PR.

## Important

- Respect all caps (`create-issue` max 5, `add-comment` max 12, `create-pull-request` max 1). If more
  tests qualify than the caps allow, prioritize the highest `distinctSources` first. New tracking issues
  are capped at 5/run; tests with a pre-existing open issue do not consume that budget and may still be
  included in the PR (up to the Step 5 cap of 8).
- **Backlog actions have their own caps** (Step 5b: **at most 5** un-quarantines and **at most 3**
  backlog fixes per run) and still fold into the **same single** combined PR. If the `add-comment` budget
  (12) is tight, prioritize comments for new quarantines/fixes over un-quarantine confirmations.
- **Open at most ONE pull request**, and it must be a **draft** based on `main`.
- **Never modify anything under `.github/**`** (no workflow, skill, or action edits) and never touch root
  manifests (`NuGet.config`, `global.json`, `Directory.Packages.props`, etc.). Only test sources, their
  projects, or a product file a reproduction proves nondeterministic — all `.cs` under `src/`.
- Never invent data. Only act on what the detector JSON reports.
- Do not ping, cc, or @-mention any user.
