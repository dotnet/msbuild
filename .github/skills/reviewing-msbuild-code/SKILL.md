---
name: reviewing-msbuild-code
description: "Review an MSBuild code, PR, or design change for actionable defects and completeness. Not CI triage, a request to edit or post, or a generic audit of agent configuration."
---

# Review MSBuild changes

Use the [expert-reviewer](../../agents/expert-reviewer.agent.md) when available. If the host cannot invoke custom agents, follow that role directly; do not invent an agent tool.

1. Establish the exact change, base/head, requested outcome, and read/write authority.
2. Select the relevant [review lenses](references/review-lenses.md) and follow affected source boundaries.
3. Require a concrete trigger and consequence for a finding. Separate source reasoning, executed evidence, and coverage gaps.
4. Return only actionable findings and requested completeness evidence. Do not publish or repair the change unless asked.

A small diff is not a reason for a multi-agent workflow. Use independent investigators only when their scopes require substantial separate context; never one agent per review dimension.

Stop after the scoped review, or continue for a specific new finding, changed diff, or failed proof. A fixed number of passes and a majority vote among models are not completeness criteria.
