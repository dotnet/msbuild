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
    allowed-events: [COMMENT, REQUEST_CHANGES]
  add-comment:
    max: 5
---

# Expert Code Review

Review pull request #${{ github.event.pull_request.number || github.event.issue.number }}.

The expert MSBuild reviewer instructions — the 24 review dimensions, the 13
overarching principles, and the folder hotspot mapping — are imported into this
prompt from `.github/agents/expert-reviewer.agent.md`. Apply them directly.

## Instructions

1. Fetch the full diff for the pull request.
2. Review the diff yourself, applying every dimension from the reviewer
   instructions above. Do not delegate the review to a sub-agent.
3. Post your findings using the safe-output tools:
   - **Inline review comments** on specific diff lines via `create_pull_request_review_comment`
   - **Design-level concerns** (not tied to a line) via `add_comment`
   - **Final review verdict** (COMMENT or REQUEST_CHANGES) via `submit_pull_request_review`
   - **Never use APPROVE** — this review must not count as a PR approval. Use COMMENT for clean reviews.
4. Always finish with `submit_pull_request_review`, including when the diff is
   clean. A clean verdict must name the dimensions you actually checked — never
   report a pass you did not verify.
