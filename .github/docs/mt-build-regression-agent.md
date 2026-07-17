# PerfStar MT build regression agent

The `PerfStar MT Build Regression Investigator` GitHub Agentic Workflow scans production PerfStar
data for possible multithreaded (`/mt`) build-time regressions across both Gold and Hosted machines.
It investigates all candidates as one batch, creates at most one aggregate tracking issue per run,
and can open one draft pull request containing all high-confidence fixes.

## Attribution

The OIDC authentication pattern is based on [dotnet/msbuild#13743](https://github.com/dotnet/msbuild/pull/13743),
authored by Jan Krivanek. That pull request proved that a GitHub Actions workflow in this repository
can exchange a GitHub OIDC token for a Microsoft Entra token without storing an Azure DevOps PAT.

This workflow reuses the existing `msbuild-azdo-reader` identity and the repository secrets created
for that proof of concept:

- `AZDO_READER_CLIENT_ID`
- `AZDO_READER_TENANT_ID`

The workflow exchanges the OIDC token for a Kusto-scoped token. It does not expose that token or any
Azure credential to the AI agent.

## Execution flow

1. A deterministic custom job runs daily or through `workflow_dispatch`.
2. The job uses GitHub OIDC to authenticate as `msbuild-azdo-reader`.
3. `Get-MtBuildTimeRegressions.kql` queries `perfstar-dev/PerfStarDataRaw`.
4. `Invoke-MtBuildTimeRegressionScan.ps1` writes bounded JSON and Markdown evidence.
5. The evidence is uploaded as a workflow artifact.
6. The Agentic Workflow downloads only that evidence into its secret-free sandbox.
7. The agent investigates every candidate and:
   - creates one aggregate issue when new candidates need tracking;
   - opens one draft PR only when it can safely address every actionable regression; or
   - emits a no-op when the complete candidate set is already tracked.

The workflow does not queue PerfStar validation runs. Automated experimental-build and targeted
PerfStar verification are intentionally deferred.

## Detector scope

The detector uses production `build-time` rows from:

- `Backend == "Gold"` with `RunKind == "Gold"`
- `Backend == "Hosted"` with `RunKind == "Hosted"`
- Windows and Linux
- `SourceBranch == "refs/heads/main"`

MT and non-MT scenarios are paired by removing the `-mt-` infix or trailing `-mt`. Per-build medians
are used so runs with more iterations do not receive extra weight.

A pair is emitted only when:

- the current paired run is no more than two days old;
- at least four paired baseline runs exist in the 21-day window;
- MT regressed by at least 5% and 250 ms versus its historical median;
- current MT exceeds its historical p90; and
- the MT-minus-non-MT differential deteriorated by at least 250 ms and exceeds its historical p90.

The output remains a possible-regression signal. The agent must still evaluate measurement noise,
shared infrastructure, SDK or asset changes, and recent source changes.

## Required setup

The existing OIDC identity needs Kusto read access:

1. Open the `perfstar-dev` database permissions.
2. Add the `msbuild-azdo-reader` managed identity.
3. Grant `Database Viewer`.

No Azure DevOps queue permission is required for the initial workflow.

The workflow reuses the repository's existing `copilot-pat-pool` environment and PAT rotation
mechanism used by the other Agentic Workflows.

## Current limitations

- The detector uses robust thresholds but cannot prove causality.
- The initial evidence identifies PerfStar builds but not the exact MSBuild commit consumed by each
  build, so commit attribution remains an investigation hypothesis.
- The agent receives summarized Kusto evidence, not raw binlogs.
- The workflow creates candidate fixes but does not run PerfStar against them.
- A draft PR is opened only when the agent can address every actionable candidate without claiming
  that noisy or insufficient-evidence candidates were fixed.
