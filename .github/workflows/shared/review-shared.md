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
  # Read PR data through the GitHub MCP, not shell commands.
  bash: false
  cli-proxy: false
  github:
    toolsets: [pull_requests, repos]
    # Required so the MCP gateway allows pull_request_read (get_diff / get_files)
    # and PR-head get_file_contents on a public repo: gh-aw-mcpg unconditionally
    # labels pull-request-content as `none` integrity (github/gh-aw-mcpg#15160,
    # closed as not planned). Exposure is bounded by the `roles: [admin, maintainer,
    # write]` pre-activation gate, base-only checkout on `review-on-open` (the
    # manual `/review` command checks out the PR branch while restoring agent
    # config from base), bash/cli-proxy disabled, read-only GitHub MCP scoped to
    # public repos, threat detection, and comment/review-only safe outputs (no
    # APPROVE or push).
    min-integrity: none
    allowed-repos: public

safe-outputs:
  create-pull-request-review-comment:
    max: 30
  submit-pull-request-review:
    max: 1
    allowed-events: [COMMENT, REQUEST_CHANGES]
  add-comment:
    max: 5
---

# Expert Code Review

Review pull request #${{ github.event.pull_request.number || github.event.issue.number }} using the `expert-reviewer` agent defined at `.github/agents/expert-reviewer.agent.md`.

## Instructions

1. Fetch the full diff for the pull request.
2. Call the `expert-reviewer` agent. Make sure to call it as subagent (`task` tool, `agent_type: "general-purpose"`, `model: "gpt-5.6-sol"`). And make sure to follow the guidance on subagent calls from within the `expert-reviewer` agent. We expect 2+ levels of agents to be called.
3. Do **not** post comments or reviews yourself, except for the fallback in step 4 if the subagent posts nothing. The subagent will post its own comments using the available safe-output tools:
   - **Inline review comments** on specific diff lines via `create_pull_request_review_comment`
   - **Design-level concerns** (not tied to a line) via `add_comment`
   - **Final review verdict** (COMMENT or REQUEST_CHANGES) via `submit_pull_request_review`
   - **Never use APPROVE** — the agent must not count as a PR approval. Use COMMENT for clean reviews.
4. If the subagent does not post anything (e.g. no issues found), this is the only exception to step 3: post a brief fallback review using `submit_pull_request_review` with event `COMMENT` (not `APPROVE`). Do not use `add_comment` for this fallback.
