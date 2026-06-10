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

  # Run the imported pat_pool job before the activation gate so its pat_number
  # output is available to the activation and agent jobs (which consume it in
  # engine.env). See: shared/pat_pool.README.md.
  needs: [pat_pool]

# Skip draft PRs — only run for PRs opened as ready or converted from draft
if: github.event.pull_request.draft == false

engine:
  id: copilot
  env:
    # If none of the COPILOT_GITHUB_TOKEN[_#] pool secrets were selected, the default COPILOT_GITHUB_TOKEN is used.
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_GITHUB_TOKEN, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_GITHUB_TOKEN_2, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_GITHUB_TOKEN_3, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_GITHUB_TOKEN_4, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_GITHUB_TOKEN_5, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_GITHUB_TOKEN_6, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_GITHUB_TOKEN_7, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_GITHUB_TOKEN_8, secrets.COPILOT_GITHUB_TOKEN) }}

permissions:
  contents: read
  pull-requests: read

# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
imports:
  - shared/pat_pool.md
  - shared/review-shared.md

# The agent reads the PR diff via GitHub MCP tools, so the auto-injected checkout is not needed (and can be a security concern)
checkout: false

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
