---
name: merge-dependency-updates
description: Triage MSBuild bot dependency, source-backflow, and OneLoc PRs with a read-only dashboard. Prepare branches or merge only when explicitly requested; use CI or flow specialists for individual failures and propagation questions.
---

# Bot PR triage and authorized preparation

## Before acting

- Establish repository, selected PRs/categories, and whether the request is read-only triage or an authorized mutation.
- Prefer `gh` for GitHub. Inspect installed command help and discover current-session tools before using optional capabilities.
- Read current PR heads, target branches, reviews, checks, and complete decision-critical file/thread data. A bot identity is not evidence that changes are safe.
- Resolve version policy from the target branch's [Versions.props](../../../eng/Versions.props) and [release process](../../../documentation/release.md), not a memorized release number.

## Workflow

1. Follow [read-only triage](references/triage.md) for discovery, evidence collection, and a compact dashboard.
2. Keep incomplete, unknown, failing, and successful evidence distinct. Do not claim a sampled set of files or reviews is complete.
3. Route codeflow health to the available flow-analysis workflow, individual CI failures to CI analysis, and code correctness to [review](../reviewing-msbuild-code/SKILL.md).
4. Stop after triage unless the requested authority includes the particular branch update, commit/push, or merge.
5. For an authorized action, use [branch preparation](references/branch-preparation.md), then refresh evidence at the new head.

## Evidence and stop condition

Report repository-qualified links, relevant head SHAs, blockers, and coverage
limitations. Stop when the requested inventory or authorized action is accounted
for. Do not post the dashboard, resolve reviews, enable auto-merge, or retry CI as
a side effect of gathering status.
