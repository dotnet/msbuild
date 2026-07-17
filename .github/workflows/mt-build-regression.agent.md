---
name: "PerfStar MT Build Regression Investigator"
description: "Scans production PerfStar Gold and Hosted data for possible MT build-time regressions, enriches candidates with exact run artifacts and source revisions, investigates all candidates as one batch, creates at most one aggregate tracking issue, and opens at most one draft PR containing all high-confidence fixes."
on:
  schedule:
    - cron: "17 19 * * *"
  workflow_dispatch:
  permissions: {}

if: needs.mt_regression_scan.outputs.has_regressions == 'true'

permissions:
  actions: read
  contents: read
  issues: read
  pull-requests: read

# The AI sandbox can reach GitHub and the approved .NET/NuGet ecosystem for source investigation,
# restore, and validation. Azure DevOps, Entra, and Kusto are intentionally absent: only the
# deterministic scan job receives those credentials and passes derived evidence through an artifact.
network:
  allowed:
    - defaults
    - dotnet
    - dnceng.pkgs.visualstudio.com

tools:
  edit:
  # The agent needs git/source inspection plus targeted tests and the repository build.
  bash: [":*"]
  github:
    mode: gh-proxy
    toolsets: [repos, issues, pull_requests, actions]

safe-outputs:
  create-issue:
    title-prefix: "[PerfStar MT Regression] "
    labels: ["Area: PerfStar", "Area: Performance", automation]
    max: 1
    expires: false
    # The prompt mandates a deterministic title containing candidateSetKey. gh-aw enforces exact
    # title deduplication again when applying the safe output, preventing repeated daily issues.
    deduplicate-by-title: true
  create-pull-request:
    title-prefix: "[PerfStar MT Regression] "
    labels: ["Area: PerfStar", "Area: Performance", automation]
    draft: true
    base-branch: main
    max: 1
    auto-close-issue: false
    # Performance fixes may touch C#, targets, tasks, or resources, but never files outside src/.
    # gh-aw v0.82.9 compiles this directory glob into the generated safe-output allowlist.
    allowed-files:
      - "src/**"
    # Redundant with the allowlist by design: keep workflow definitions explicitly forbidden.
    excluded-files:
      - ".github/**"
  noop:
    report-as-issue: false

# Allows source investigation, focused tests, and the 2-3 minute whole-repository build while
# keeping the scheduled automation bounded.
timeout-minutes: 75

jobs:
  mt_regression_scan:
    needs: [pre_activation]
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write
    outputs:
      has_regressions: ${{ steps.scan.outputs.has_regressions }}
      regression_count: ${{ steps.scan.outputs.regression_count }}
    steps:
      - name: Checkout repository
        uses: actions/checkout@v7
        with:
          persist-credentials: false

      # OIDC exchange adapted from dotnet/msbuild#13743 by Jan Krivanek.
      - name: Get GitHub OIDC token
        id: oidc
        shell: bash
        run: |
          set -euo pipefail
          oidc_token=$(
            curl --fail-with-body --silent --show-error \
              -H "Authorization: bearer ${ACTIONS_ID_TOKEN_REQUEST_TOKEN}" \
              "${ACTIONS_ID_TOKEN_REQUEST_URL}&audience=api://AzureADTokenExchange" |
              jq -r '.value'
          )
          if [[ -z "$oidc_token" || "$oidc_token" == "null" ]]; then
            echo "::error::GitHub did not issue an OIDC token."
            exit 1
          fi
          echo "::add-mask::$oidc_token"
          echo "token=$oidc_token" >> "$GITHUB_OUTPUT"

      - name: Exchange OIDC token for Kusto token
        shell: bash
        env:
          OIDC_TOKEN: ${{ steps.oidc.outputs.token }}
          CLIENT_ID: ${{ secrets.AZDO_READER_CLIENT_ID }}
          TENANT_ID: ${{ secrets.AZDO_READER_TENANT_ID }}
        run: |
          set -euo pipefail
          response=$(
            curl --fail-with-body --silent --show-error \
              -X POST "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
              --data-urlencode "grant_type=client_credentials" \
              --data-urlencode "client_id=${CLIENT_ID}" \
              --data-urlencode "client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer" \
              --data-urlencode "client_assertion=${OIDC_TOKEN}" \
              --data-urlencode "scope=https://kusto.kusto.windows.net/.default"
          )
          kusto_token=$(jq -r '.access_token' <<< "$response")
          if [[ -z "$kusto_token" || "$kusto_token" == "null" ]]; then
            echo "::error::Microsoft Entra ID did not issue a Kusto token."
            jq '{error, error_description, error_codes}' <<< "$response" || true
            exit 1
          fi
          echo "::add-mask::$kusto_token"
          echo "KUSTO_ACCESS_TOKEN=$kusto_token" >> "$GITHUB_ENV"

      - name: Exchange OIDC token for Azure DevOps token
        shell: bash
        env:
          OIDC_TOKEN: ${{ steps.oidc.outputs.token }}
          CLIENT_ID: ${{ secrets.AZDO_READER_CLIENT_ID }}
          TENANT_ID: ${{ secrets.AZDO_READER_TENANT_ID }}
        run: |
          set -euo pipefail
          response=$(
            curl --fail-with-body --silent --show-error \
              -X POST "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
              --data-urlencode "grant_type=client_credentials" \
              --data-urlencode "client_id=${CLIENT_ID}" \
              --data-urlencode "client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer" \
              --data-urlencode "client_assertion=${OIDC_TOKEN}" \
              --data-urlencode "scope=499b84ac-1321-427f-aa17-267ca6975798/.default"
          )
          azdo_token=$(jq -r '.access_token' <<< "$response")
          if [[ -z "$azdo_token" || "$azdo_token" == "null" ]]; then
            echo "::error::Microsoft Entra ID did not issue an Azure DevOps token."
            jq '{error, error_description, error_codes}' <<< "$response" || true
            exit 1
          fi
          echo "::add-mask::$azdo_token"
          echo "AZDO_ACCESS_TOKEN=$azdo_token" >> "$GITHUB_ENV"

      - name: Scan PerfStar MT build-time data
        id: scan
        shell: pwsh
        run: |
          ./.github/workflows/scripts/Invoke-MtBuildTimeRegressionScan.ps1 `
            -ClusterUri 'https://perfstar-experimental.swedencentral.kusto.windows.net' `
            -Database 'perfstar-dev' `
            -QueryPath './.github/workflows/scripts/Get-MtBuildTimeRegressions.kql' `
            -OutputDirectory "$env:RUNNER_TEMP/mt-regression-data"

      - name: Collect actual-run evidence
        if: steps.scan.outputs.has_regressions == 'true'
        shell: pwsh
        run: |
          ./.github/workflows/scripts/Add-MtBuildTimeRegressionEvidence.ps1 `
            -InputReport "$env:RUNNER_TEMP/mt-regression-data/mt-regressions.json" `
            -OutputDirectory "$env:RUNNER_TEMP/mt-regression-data"

      - name: Collect scheduled-binlog supporting evidence
        if: steps.scan.outputs.has_regressions == 'true'
        shell: pwsh
        run: |
          ./.github/workflows/scripts/Add-MtBuildTimeDiagnosticEvidence.ps1 `
            -InputEvidence "$env:RUNNER_TEMP/mt-regression-data/mt-regression-evidence.json" `
            -OutputDirectory "$env:RUNNER_TEMP/mt-regression-data"

      - name: Upload regression evidence
        uses: actions/upload-artifact@v7.0.1
        with:
          name: mt-regression-data
          path: ${{ runner.temp }}/mt-regression-data
          if-no-files-found: error
          retention-days: 14

steps:
  - name: Download regression evidence
    uses: actions/download-artifact@v8.0.1
    with:
      name: mt-regression-data
      path: /tmp/gh-aw/agent/mt-regression-data

# ###############################################################
# Select a PAT from the existing Copilot PAT pool and keep the
# agentic job inside the protected copilot-pat-pool environment.
# ###############################################################
imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool

environment: copilot-pat-pool

engine:
  id: copilot
  env:
    COPILOT_GITHUB_TOKEN: "${{ case( needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}"
---

# PerfStar MT Build Regression Investigator

You are an automated performance-regression investigator for **dotnet/msbuild**. A deterministic
Kusto scan has identified one or more **possible** multithreaded (`/mt`) build-time regressions in
production PerfStar data. Investigate every candidate together, distinguish plausible product
regressions from noise, and propose a single coherent draft fix only when the evidence supports it.

The scan runs outside your sandbox with a read-only managed identity. You receive only its
non-secret output:

- `/tmp/gh-aw/agent/mt-regression-data/mt-regressions.json`
- `/tmp/gh-aw/agent/mt-regression-data/mt-regression-context.md`
- `/tmp/gh-aw/agent/mt-regression-data/mt-regression-evidence.json`
- `/tmp/gh-aw/agent/mt-regression-data/mt-regression-evidence.md`
- `/tmp/gh-aw/agent/mt-regression-data/mt-regression-diagnostics.json`
- `/tmp/gh-aw/agent/mt-regression-data/mt-regression-diagnostics.md`

The evidence includes exact current and last-healthy PerfStar runs, their MSBuild component build
IDs and source revisions, candidate-specific current/healthy allowlisted metrics, safe Hosted timing
and completion lines, and Gold scenario result metrics. Raw artifacts are downloaded only in the
trusted scan job, reduced to an explicit allowlist, bounded, and deleted before the evidence is
uploaded.

When definition 28394 has a scheduled binlog run for the exact current or last-healthy MSBuild
source SHA, the diagnostic evidence also includes task, target, and evaluation-pass MT/non-MT
deltas from Kusto plus task migration markers. Overall diagnostic pipeline failure does not
invalidate scenario evidence when the affected scenario and OS have paired task data. For Gold
candidates, this Hosted diagnostic data is corroboration rather than direct backend evidence.

The workflow intentionally does **not** give you Azure, Kusto, or Azure DevOps credentials.

## Detector contract

The JSON report is the only source of truth for measured regressions. It covers both **Gold** and
**Hosted**, Windows and Linux, and contains one row per latest production MT/non-MT scenario pair
that passed all of these gates:

1. At least four paired historical production runs exist.
2. Current MT median build time is at least 5% and 250 ms above the historical MT median.
3. Current MT time is above the historical MT p90.
4. The current MT-minus-non-MT delta deteriorated by at least 250 ms and is above its historical p90.
5. The current paired run is no older than two days.

These gates reduce noise but do **not** prove causality. Shared infrastructure, asset changes,
measurement variance, SDK changes, or unrelated non-MT movement can still produce a candidate.

## Phase 1 — Read and validate all evidence

1. Read all six evidence files completely.
2. Confirm `candidateCount` is greater than zero and that every candidate has the expected fields.
   Read `candidateSetKey`; it is a deterministic SHA-256-derived key for the sorted unique
   `Backend/Os/ScenarioPair` set and is stable across runs while that set is unchanged.
3. Group candidates by likely shared root cause. A regression appearing across Gold and Hosted or
   across both operating systems is stronger evidence than a single-backend observation, but it can
   also indicate a shared SDK/asset change.
4. Explicitly inspect:
   - current MT and non-MT medians;
   - historical MT median and p90;
   - MT absolute and percentage regression;
   - non-MT movement;
   - MT-minus-non-MT differential movement;
   - baseline run count and time window.
5. Where scheduled-binlog evidence is available, inspect:
   - the largest task and target MT-minus-non-MT deltas;
   - how those deltas changed between the last-healthy and current source revisions;
   - evaluation-pass deltas;
   - `[MSBuildMultiThreadableTask]` migration state; and
   - migrated task controls as the machine-contention/noise floor.
6. Treat every row as **possible/flaky until investigated**. Never state that PerfStar proved a code
   regression merely because the detector emitted it.

## Phase 2 — Check for existing work and recent changes

1. Search open issues and pull requests for each scenario pair and for the exact visible marker
   `perfstar-mt-regression-key: <candidateSetKey>`.
2. If one existing open issue or PR already covers the complete candidate set, do not duplicate it.
   Emit `noop` with links and a concise explanation.
3. Use each candidate's last-healthy and current MSBuild source revisions to inspect the exact
   comparison range. Prioritize changes touching shared code paths used by all affected scenarios.
4. Compare evaluation subphase metrics and current Hosted log excerpts or Gold result metrics before
   attributing the regression to a source change.
5. Treat task/target wall-clock totals as supporting evidence, not additive attribution: nested and
   repeated work can be counted in multiple rows, and even migrated controls move under contention.
6. Use `git log`, `git diff`, `git blame`, GitHub issues/PRs, and source inspection to establish a
   concrete hypothesis. The source range narrows investigation but does not prove which commit caused
   the measurement change.

## Phase 3 — Investigate whether the signal is actionable

For every candidate, classify it as one of:

- **Actionable product regression** — a concrete recent code change explains the MT-specific signal.
- **Likely measurement/infrastructure noise** — evidence is inconsistent, isolated, or explained by
  non-product conditions.
- **Insufficient evidence** — the signal is real enough to track but no defensible cause is known.

Record the reasoning for every candidate. Look for one shared root cause before proposing multiple
unrelated edits.

## Phase 4 — Implement only a complete, high-confidence fix

Open a pull request only if you can explain and safely address **every actionable product regression
in this run**. The single PR may contain multiple related edits, but it must remain coherent.

Rules:

1. Modify only files under `src/**`. Never edit `.github/**`, pipeline configuration, root manifests,
   generated artifacts, or performance data.
2. Make the smallest behavior-preserving fix that addresses the identified MT-specific cause.
3. Preserve non-MT behavior and public compatibility.
4. Add or update focused tests for each code path changed.
5. Run the smallest relevant tests first, then run `./build.sh -v quiet`.
6. Inspect `git diff` and revert unrelated edits.
7. If any actionable candidate cannot be fixed confidently, create the aggregate issue but do not
   open a partial PR that claims to resolve the full set.
8. If all candidates are likely noise or already tracked, emit `noop`.

## Phase 5 — Create exactly one aggregate issue

When the candidates are not already fully tracked, create **one issue total** for this workflow run.
The issue must cover every candidate, including candidates classified as noise or insufficient
evidence.

Issue title: use this exact deterministic format:

```text
<candidateCount> possible MT build-time regressions [<candidateSetKey>]
```

Do not vary this title format. The safe-output layer uses exact-title deduplication as a second
cross-run guard.

Issue body:

1. State that this is an automated **possible-regression** investigation, not a confirmed regression.
2. Include a compact table for all candidates with backend, OS, scenario pair, current/baseline MT,
   regression percentage, differential regression, and build link.
3. Include the classification and reasoning for every candidate.
4. Describe any shared root-cause hypothesis and relevant commits/files.
5. State whether a draft fix PR was proposed.
6. Include the workflow run URL: `${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}`.
7. Include these visible markers:

   ```text
   perfstar-mt-regression-key: <candidateSetKey>
   perfstar-mt-regression-run: ${{ github.run_id }}
   ```

Do not assign or mention individual users.

## Phase 6 — Create at most one draft PR

If Phase 4 produced a complete high-confidence fix, create **one draft PR** containing all fixes.

PR body:

1. Clearly call it a candidate fix pending human review and PerfStar validation.
2. Summarize every candidate and explain how the changes address each actionable regression.
3. List candidates classified as noise/insufficient evidence and explain why no code change was made
   for them.
4. Describe the root cause, compatibility considerations, code changes, and tests run.
5. Use `Tracked by the automated PerfStar MT regression issue created in this workflow run`; do not
   use `Fixes`, `Closes`, or `Resolves`, because automated PerfStar verification is not implemented yet.
6. Include the workflow run URL.

The PR must remain draft. Do not merge, approve, or mark it ready for review.

## Exit rules

- Candidates found and not already tracked: create exactly one aggregate issue.
- Complete high-confidence code fix: create that issue plus exactly one draft PR.
- No complete high-confidence fix: create the issue only.
- Complete candidate set already tracked: emit `noop`.
- Never create more than one issue or more than one PR per run.
