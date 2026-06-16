---
name: "Expert Code Review (command)"
description: "Runs the expert-reviewer agent on a pull request when a contributor comments /review."

on:
  slash_command:
    name: review
    events: [pull_request_comment]
  roles: [admin, maintainer, write]

  # Run the imported pat_pool job before the activation gate so its pat_number
  # output is available to the activation and agent jobs (which consume it in
  # engine.env). See: shared/pat_pool.README.md.
  needs: [pat_pool]

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

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
