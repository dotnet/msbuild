---
# Shared configuration for expert-review workflows.
#
# Imported by review.agent.md (slash command) and review-on-open.agent.md
# (pull request opened). Keeps permissions, tools, and safe-outputs
# in one place.
#
# NOTE: PAT rotation (steps, jobs, engine) must be in each workflow
# file directly — it cannot be shared via imports.

description: "Shared configuration for expert-review workflows"

permissions:
  contents: read
  pull-requests: read

tools:
  github:
    toolsets: [pull_requests, repos]

safe-outputs:
  add-comment:
    max: 3
---

# Expert Code Review

Review pull request #${{ github.event.pull_request.number || github.event.issue.number }} using the expert-reviewer agent defined at `.github/agents/expert-reviewer.md`.

## Instructions

1. Fetch the full diff for the pull request.
2. Apply the expert-reviewer methodology from `.github/agents/expert-reviewer.md` — all 24 review dimensions, prioritized by severity and weighted by file location.
3. Post a single review comment summarizing findings, organized by severity (BLOCKING > MAJOR > MODERATE > MINOR).
4. If no issues are found, post a brief approval comment.
