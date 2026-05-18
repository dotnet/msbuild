---
name: "Close Stale Pull Requests"
description: "Automatically warn about and close pull requests that have been open for more than 180 days with no recent activity."
on:
  schedule: weekly on monday
  workflow_dispatch: # Allow manual triggering
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
