---
name: pipelines-health-check
description: Read-only overview of MSBuild pipelines, VS insertion PRs, or MSBuild-to-VMR codeflow checks. For an individual CI failure use CI analysis; for flow freshness or commit propagation use the corresponding flow workflow.
---

# Pipeline and insertion overview

## Before acting

- Select the requested surface, branch, and time window; do not run every collector for a single-pipeline question.
- Discover current-session tools and inspect version-specific help. Installed plugins may not be loaded. Do not install tools, accept licenses, refresh credentials, or retry builds during discovery.
- Verify pipeline, repository, and reviewer identities against current service metadata. Defaults are discovery hints, not permanent authority.
- Use [collector procedures and contracts](references/collectors.md) for the applicable read-only script or supported service tool.

## Workflow

1. Collect the requested overview only. The scripts fail on unavailable required data rather than returning an empty healthy result.
2. Distinguish successful collection from successful builds. Report unknown, pending, canceled, partially successful, stale, and failing evidence explicitly.
3. For PR checks, tie evidence to the current revision/iteration and actual blocking policies; do not infer merge readiness from sampled or historical green runs.
4. Present a compact status summary with source links, observation time, sample/window limits, and distinct blockers.
5. If deeper investigation was requested, follow [investigation](references/investigation.md) and route the identified failure or flow question to the available specialist. Group shared causes before considering delegation.

## Evidence and stop condition

Stop when the requested status is accounted for, or identify the unavailable
surface precisely. Never report "all clear" if a requested collection failed, a
current revision is unmatched, or required evidence is unknown. A status check
does not authorize posting, merging, rerunning, canceling, or changing subscriptions.
