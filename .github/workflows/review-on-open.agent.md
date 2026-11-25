---
name: "Expert Code Review (on open)"
description: "Automatically runs the expert-reviewer agent when a non-draft PR is opened or a draft PR becomes ready for review."

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
on:
  pull_request_target:
    types: [opened, ready_for_review]
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

# Reviewing fork PRs is the point of this workflow, so explicitly keep
# `forks: ["*"]` under gh-aw strict mode. The mitigations above (roles gate,
# base-only checkout, diff read via MCP, no PR code execution) still apply.
features:
  dangerously-allow-pull-request-target-forks-all: true

# The review reads PR data through the guarded GitHub MCP, not shell commands.
# With the current public-repository integrity default, PR diffs are filtered,
# so reviews report incomplete until the gateway supports trusted PR content:
# https://github.com/github/gh-aw-mcpg/issues/15160
tools:
  bash: false
  cli-proxy: false

timeout-minutes: 60
max-ai-credits: 2000

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
  # Copilot can auto-approve read-only tools even without explicit grants.
  args: ["--deny-tool", "shell", "--deny-tool", "github(get_commit)"]
  env:
     COPILOT_GITHUB_TOKEN: "${{ case( needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}"
---

## Automatic review safeguards

These safeguards take precedence over the shared review instructions:

- Fetch the pull request metadata and complete diff through the GitHub MCP. If required content is denied or filtered, including from a partial response, do not retrieve it through another tool, shell command, or direct network request. If the complete diff cannot be verified, call `report_incomplete` with the reason and stop without submitting a review.
- Include the complete diff verbatim in the `expert-reviewer` subagent's prompt and require it to include the same complete diff, not a summary, in every dimension subagent's prompt. Require the expert reviewer to defer all review outputs until every dimension and required validation is complete, and to return an explicit completion status.
- If the complete diff cannot be passed to every reviewer, any dimension cannot be evaluated, or completion cannot be confirmed, call `report_incomplete` rather than posting a review or inline comments. Use the shared fallback review only when the subagent explicitly confirms that all dimensions are complete.
