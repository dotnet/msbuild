---
name: "Expert Code Review (on open)"
description: "Automatically runs the expert-reviewer agent when a non-draft PR is opened."

# Non-draft PRs still trigger this workflow, including fork PRs.
# The `roles` setting below does not block triggering; it restricts
# execution past pre-activation to users with admin, maintainer, or
# write permissions (see the compiled lock file for the gating logic).
#
# Uses pull_request_target (not pull_request) so that fork PRs have
# access to the repo secret needed for the Copilot token. To keep that
# safe, the workflow checks out only the trusted base repo (see `checkout:`
# below) and the agent reviews the diff via GitHub MCP tools; it never
# executes PR code.
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
if: ${{ github.event.pull_request.draft == false && !github.event.repository.fork }}

permissions:
  contents: read
  pull-requests: read

# The agent reads the PR diff via GitHub MCP tools, so it never needs the
# untrusted PR head checked out. Because pull_request_target runs with secret
# access, check out only the trusted base repository (never the PR head) to
# satisfy the framework's git operations without the "pwn request" risk.
checkout:
  repository: ${{ github.repository }}
  ref: ${{ github.event.pull_request.base.sha }}

timeout-minutes: 60

# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# Run agentic jobs in an isolated `copilot-pat-pool` environment.
#
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool
  - shared/review-shared.md

environment: copilot-pat-pool

engine:
  id: copilot
  env:
     COPILOT_GITHUB_TOKEN: |
      ${{ case(
        needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0,
        needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1,
        needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2,
        needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3,
        needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4,
        needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5,
        needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6,
        needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7,
        needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8,
        needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9,
        'NO COPILOT PAT AVAILABLE')
      }}
---

<!-- Body provided by shared/review-shared.md -->
