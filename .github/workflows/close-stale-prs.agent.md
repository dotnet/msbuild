---
name: "Close Stale Pull Requests"
description: "Automatically warn about and close pull requests that have been open for more than 180 days with no recent activity."
on:
  schedule: weekly on monday
  workflow_dispatch: # Allow manual triggering

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

# Override the COPILOT_GITHUB_TOKEN expression used in the activation job
# Consume the PAT number from the pre-activation step and select the corresponding secret
engine:
  id: copilot
  env:
    # We cannot use line breaks in this expression as it leads to a syntax error in the compiled workflow
    # If none of the `COPILOT_GITHUB_TOKEN_#` secrets were selected, then the default COPILOT_GITHUB_TOKEN is used
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pre_activation.outputs.copilot_pat_number == '0', secrets.COPILOT_GITHUB_TOKEN, needs.pre_activation.outputs.copilot_pat_number == '1', secrets.COPILOT_GITHUB_TOKEN_1, needs.pre_activation.outputs.copilot_pat_number == '2', secrets.COPILOT_GITHUB_TOKEN_2, needs.pre_activation.outputs.copilot_pat_number == '3', secrets.COPILOT_GITHUB_TOKEN_3, needs.pre_activation.outputs.copilot_pat_number == '4', secrets.COPILOT_GITHUB_TOKEN_4, needs.pre_activation.outputs.copilot_pat_number == '5', secrets.COPILOT_GITHUB_TOKEN_5, needs.pre_activation.outputs.copilot_pat_number == '6', secrets.COPILOT_GITHUB_TOKEN_6, needs.pre_activation.outputs.copilot_pat_number == '7', secrets.COPILOT_GITHUB_TOKEN_7, needs.pre_activation.outputs.copilot_pat_number == '8', secrets.COPILOT_GITHUB_TOKEN_8, needs.pre_activation.outputs.copilot_pat_number == '9', secrets.COPILOT_GITHUB_TOKEN_9, secrets.COPILOT_GITHUB_TOKEN) }}

safe-outputs:
  close-pull-request:
    max: 25
  add-comment:
    max: 30
---

# Close Stale Pull Requests

You are an automated repository maintenance agent for the MSBuild repository.

## Task

Find pull requests that have been open for more than **180 days** and have had no recent activity. Depending on how long they have been inactive, either **warn** the author or **close** the pull request.

## Definitions

- **Created date**: The date the pull request was originally opened.
- **Last activity date**: The date of the most recent **non-bot** activity on the pull request. To determine this, list the PR's comments and reviews and find the most recent one **not** authored by a bot (i.e., ignore comments from users whose login ends with `[bot]`). If there are no non-bot comments or reviews, fall back to the PR's `created_at` date. Do **not** rely on `updated_at` alone, because the bot's own stale-warning comment updates `updated_at` and would reset the inactivity timer.
- **Stale (warning)**: A PR is eligible for a stale warning if it was created more than 180 days ago **and** its last non-bot activity was more than 30 days ago but no more than 37 days ago (i.e., 30 < days_since_last_non_bot_activity ≤ 37), **and** the PR does not already have a stale warning comment from this bot.
- **Stale (close)**: A PR is eligible for closure if it was created more than 180 days ago **and** its last non-bot activity was more than 37 days ago.

## Instructions

1. List all open pull requests in this repository.
2. For each open pull request:
   a. Skip it if it has the label `no-stale` — these are exempt from this policy.
   b. Skip it if it was authored by `dotnet-maestro[bot]` or `dotnet-maestro` — dependency update PRs are managed separately.
   c. Skip it if it was created **fewer than 180 days ago**.
   d. Determine the **last non-bot activity date**: fetch the PR's comments and reviews, find the most recent entry not authored by a bot (login ending in `[bot]`), and use its date. If none exist, use the PR's `created_at` date.
3. For each eligible pull request (created more than 180 days ago):
   - If last non-bot activity was **more than 37 days ago**: **Close** the pull request using the `close_pull_request` tool (with `pull_request_number` set to the PR number) and the closing comment below.
   - If last non-bot activity was **more than 30 days ago but 37 or fewer days ago** and the PR does not already have a stale warning comment from this bot: **Post a stale warning comment** using the `add_comment` tool (with `item_number` set to the PR number) and the warning comment below.
   - Otherwise (non-bot activity within the last 30 days): Skip it — it is not stale.

## Important

- You **must** use the `close_pull_request` tool to close pull requests. Always provide the `pull_request_number` parameter with the PR number — this workflow runs on a schedule, not on a PR event, so the tool cannot auto-detect the target PR.
- You **must** use the `add_comment` tool to post stale warning comments. Always provide the `item_number` parameter with the PR number.
- When determining staleness, ignore all bot activity (comments/reviews from users with logins ending in `[bot]`). Only human activity resets the inactivity timer.
- Process all eligible pull requests, up to the tool limits.

## Stale Warning Comment Template

Use the following comment when warning about a stale pull request (using `add_comment`):

> This PR has been automatically marked as stale because it has no activity for 30 days. It will be closed if no further activity occurs within another 7 days of this comment. If it is closed, you may reopen it anytime when you're ready again.

## Closing Comment Template

Use the following comment when closing a stale pull request (as the `body` of `close_pull_request`):

> This pull request has been automatically closed because it has been open for more than 180 days with no recent activity.
>
> If you believe this work is still relevant, please feel free to reopen or create a new pull request. Thank you for your contribution!
