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
  github:
    toolsets: [pull_requests, repos]

safe-outputs:
  create-pull-request-review-comment:
    max: 30
  submit-pull-request-review:
    max: 1
  add-comment:
    max: 5
---

# Expert Code Review

Review pull request #${{ github.event.pull_request.number || github.event.issue.number }} using the `expert-reviewer` agent defined at `.github/agents/expert-reviewer.md`.

## Instructions

1. Fetch the full diff for the pull request.
2. Call the `expert-reviewer` agent. Make sure to call it as subagent (`task` tool, `agent_type: "general-purpose"`, `model: "claude-opus-4.6"`). And make sure to follow the guidance on subagent calls from within the `expert-reviewer` agent. We expect 2+ levels of agents to be called.
3. Do **not** post comments yourself. The subagent will post its own comments using the available safe-output tools:
   - **Inline review comments** on specific diff lines via `create_pull_request_review_comment`
   - **Design-level concerns** (not tied to a line) via `add_comment`
   - **Final review verdict** (APPROVE / COMMENT / REQUEST_CHANGES) via `submit_pull_request_review`
4. If the subagent does not post anything (e.g. no issues found), post a brief approval comment.
