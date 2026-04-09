---
name: "Expert Code Review (on open)"
description: "Automatically runs the expert-reviewer agent when a non-draft PR is opened."

# Non-draft PRs still trigger this workflow, including fork PRs.
# The `roles` setting below does not block triggering; it restricts
# execution past pre-activation to users with admin, maintainer, or
# write permissions (see the compiled lock file for the gating logic).
#
# Uses pull_request_target (not pull_request) so that fork PRs have
# access to repo secrets needed for the Copilot PAT pool. This is safe
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

  # ###############################################################
  # Override the COPILOT_GITHUB_TOKEN secret usage for the workflow
  # with a randomly-selected token from a pool of secrets.
  #
  # As soon as organization-level billing is offered for Agentic
  # Workflows, this stop-gap approach will be removed.
  #
  # See: /.github/actions/select-copilot-pat/README.md
  # ###############################################################
  steps:
    - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
      name: Checkout the select-copilot-pat action folder
      with:
        persist-credentials: false
        sparse-checkout: .github/actions/select-copilot-pat
        sparse-checkout-cone-mode: true
        fetch-depth: 1

    - id: select-copilot-pat
      name: Select Copilot token from pool
      uses: ./.github/actions/select-copilot-pat
      env:
        # If the secret names are changed here, they must also be changed
        # in the `engine: env` case expression below
        SECRET_0: ${{ secrets.COPILOT_GITHUB_TOKEN }}
        SECRET_1: ${{ secrets.COPILOT_GITHUB_TOKEN_1 }}
        SECRET_2: ${{ secrets.COPILOT_GITHUB_TOKEN_2 }}
        SECRET_3: ${{ secrets.COPILOT_GITHUB_TOKEN_3 }}
        SECRET_4: ${{ secrets.COPILOT_GITHUB_TOKEN_4 }}
        SECRET_5: ${{ secrets.COPILOT_GITHUB_TOKEN_5 }}
        SECRET_6: ${{ secrets.COPILOT_GITHUB_TOKEN_6 }}
        SECRET_7: ${{ secrets.COPILOT_GITHUB_TOKEN_7 }}
        SECRET_8: ${{ secrets.COPILOT_GITHUB_TOKEN_8 }}
        SECRET_9: ${{ secrets.COPILOT_GITHUB_TOKEN_9 }}

# Add the pre-activation output of the randomly selected PAT
jobs:
  pre-activation:
    outputs:
      copilot_pat_number: ${{ steps.select-copilot-pat.outputs.copilot_pat_number }}

# Skip draft PRs — only run for PRs opened as ready or converted from draft
if: github.event.pull_request.draft == false

# Override the COPILOT_GITHUB_TOKEN expression used in the activation job
# Consume the PAT number from the pre-activation step and select the corresponding secret
engine:
  id: copilot
  env:
    # We cannot use line breaks in this expression as it leads to a syntax error in the compiled workflow
    # If none of the `COPILOT_GITHUB_TOKEN_#` secrets were selected, then the default COPILOT_GITHUB_TOKEN is used
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pre_activation.outputs.copilot_pat_number == '0', secrets.COPILOT_GITHUB_TOKEN, needs.pre_activation.outputs.copilot_pat_number == '1', secrets.COPILOT_GITHUB_TOKEN_1, needs.pre_activation.outputs.copilot_pat_number == '2', secrets.COPILOT_GITHUB_TOKEN_2, needs.pre_activation.outputs.copilot_pat_number == '3', secrets.COPILOT_GITHUB_TOKEN_3, needs.pre_activation.outputs.copilot_pat_number == '4', secrets.COPILOT_GITHUB_TOKEN_4, needs.pre_activation.outputs.copilot_pat_number == '5', secrets.COPILOT_GITHUB_TOKEN_5, needs.pre_activation.outputs.copilot_pat_number == '6', secrets.COPILOT_GITHUB_TOKEN_6, needs.pre_activation.outputs.copilot_pat_number == '7', secrets.COPILOT_GITHUB_TOKEN_7, needs.pre_activation.outputs.copilot_pat_number == '8', secrets.COPILOT_GITHUB_TOKEN_8, needs.pre_activation.outputs.copilot_pat_number == '9', secrets.COPILOT_GITHUB_TOKEN_9, secrets.COPILOT_GITHUB_TOKEN) }}

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/review-shared.md

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
