---
name: release
description: Explain, inspect, plan, or execute an explicitly selected MSBuild release phase. Release status is read-only; branching, DARC changes, posting, insertion, tagging, and publication each require appropriate authorization.
---

# MSBuild release phases

## Before acting

- Establish the release, tracking issue, requested phase (0-5), and mode: explain/status/plan or authorized execution.
- Read the relevant phase in the [release checklist](../../../documentation/release-checklist.md) and its current source configuration. Do not start the whole lifecycle for a phase question.
- Resolve phase-specific inputs from the tracking issue, applicable branch, current service data, and release schedule. Main may already contain the next version.
- Discover current-session tools and inspect version-specific CLI help. Missing tools, permissions, or essential release decisions are blockers; do not install or authenticate automatically.

## Workflow

1. Use [phase guidance](references/phase-guide.md) to identify required inputs, source files, and completion evidence.
2. For channel rotation or retirement, use [configuration and lifecycle](references/configuration-and-lifecycle.md). Verify current IDs, topology, and both SDK and VS support evidence.
3. For a package baseline, use [baseline provenance](references/package-validation-baseline.md); a helper's output is not proof of complete candidate coverage.
4. Execute only authorized steps whose trigger and prerequisites are met. Updating the tracking issue, retrying a build, or opening a PR is itself a write.
5. Reconcile actual results before continuing or retrying. Stop at the requested phase boundary.

## Evidence and stop condition

Report relevant repository/ref, current state, artifact URLs, completed authorized
actions, and unresolved blockers. Do not claim a whole release is complete from
one successful phase, or infer published versions from branch HEAD.

## How to determine `PACKAGE_VALIDATION_BASELINE_VERSION`

The detailed procedure is now in [baseline provenance](references/package-validation-baseline.md).
This heading preserves existing links to the release skill.
