---
# Shared configuration for expert-review workflows.
#
# Imported by review.agent.md (slash command) and review-on-open.agent.md
# (pull request opened). Keeps PAT rotation, permissions, tools, and
# safe-outputs in one place.

description: "Shared configuration for expert-review workflows"

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

jobs:
  pre-activation:
    outputs:
      copilot_pat_number: ${{ steps.select-copilot-pat.outputs.copilot_pat_number }}

engine:
  id: copilot
  env:
    # We cannot use line breaks in this expression as it leads to a syntax error in the compiled workflow
    # If none of the `COPILOT_GITHUB_TOKEN_#` secrets were selected, then the default COPILOT_GITHUB_TOKEN is used
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pre_activation.outputs.copilot_pat_number == '0', secrets.COPILOT_GITHUB_TOKEN, needs.pre_activation.outputs.copilot_pat_number == '1', secrets.COPILOT_GITHUB_TOKEN_1, needs.pre_activation.outputs.copilot_pat_number == '2', secrets.COPILOT_GITHUB_TOKEN_2, needs.pre_activation.outputs.copilot_pat_number == '3', secrets.COPILOT_GITHUB_TOKEN_3, needs.pre_activation.outputs.copilot_pat_number == '4', secrets.COPILOT_GITHUB_TOKEN_4, needs.pre_activation.outputs.copilot_pat_number == '5', secrets.COPILOT_GITHUB_TOKEN_5, needs.pre_activation.outputs.copilot_pat_number == '6', secrets.COPILOT_GITHUB_TOKEN_6, needs.pre_activation.outputs.copilot_pat_number == '7', secrets.COPILOT_GITHUB_TOKEN_7, needs.pre_activation.outputs.copilot_pat_number == '8', secrets.COPILOT_GITHUB_TOKEN_8, needs.pre_activation.outputs.copilot_pat_number == '9', secrets.COPILOT_GITHUB_TOKEN_9, secrets.COPILOT_GITHUB_TOKEN) }}

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
