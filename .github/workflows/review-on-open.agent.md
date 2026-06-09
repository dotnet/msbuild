---
name: "Expert Code Review (on open)"
description: "Automatically runs the expert-reviewer agent when a non-draft PR is opened."

# Non-draft PRs still trigger this workflow, including fork PRs.
# The `roles` setting below does not block triggering; it restricts
# execution past pre-activation to users with admin, maintainer, or
# write permissions (see the compiled lock file for the gating logic).
#
# Uses pull_request_target (not pull_request) so that fork PRs have
# access to the repo secret needed for the Copilot token. This is safe
# because the agent reads the diff via GitHub MCP tools — it does not
# check out or execute code from the PR branch.
#
# NOTE: The gh-aw compiler does not support `ready_for_review` as a
# type for pull_request_target. Only `opened` is used here; for PRs
# transitioned from draft to ready, use the `/review` slash command.
# Add `ready_for_review` after https://github.com/github/gh-aw/issues/25436
# is fixed and deployed.
on:
  pull_request_target:
    types: [opened] # TODO: add ready_for_review (gh-aw#25436)
    forks: ["*"]
  roles: [admin, maintainer, write]

# Skip draft PRs — only run for PRs opened as ready or converted from draft
if: github.event.pull_request.draft == false

engine:
  id: copilot

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/review-shared.md

# The agent reads the PR diff via GitHub MCP tools, so the auto-injected checkout is not needed (and can be a security concern)
checkout: false

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
