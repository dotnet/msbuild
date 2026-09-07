---
name: maintaining-agent-context
description: "Audit or repair repository skills, instructions, agents, workflow prompts, and context routing using source and session evidence. Not product code review, cloud-runner setup, or automatic changes to installed plugins."
---

# Maintain agent context

## Scope and ownership

Inventory the repository's root/nested instructions, scoped instructions, skills and references, agents, and automation prompts. Distinguish checked-in source from generated workflow output, vendored files, personal settings, and installed plugin caches.

Audit installed providers read-only unless the user explicitly requests changing their source or personal configuration. Use current CLI documentation and capability discovery, not remembered tool names or inferred activation.

## Workflow

1. Inspect the worktree and context inventory. Record current entry-point size and the files actually in scope.
2. Sample relevant session history when available: distinguish user corrections from injected skills, notifications, and delegated prompts. Keep private history out of committed reports.
3. Check substantive claims against current source/configuration or authoritative documentation. Identify incorrect rules, contradictory routing, unclear authority, duplication, missing links, and compulsory work unrelated to the task.
4. Repair the owning layer. Keep invariant policy small and move corrected domain procedures into focused references; do not discard useful knowledge just to meet a length target.
5. Use the [framework guide](../../../documentation/agent-context.md) and [task briefs](../../../documentation/agent-context/task-briefs.md) to exercise progressive retrieval and realistic failure cases.
6. Run the offline [context validator](../../../scripts/Validate-AgentContext.ps1). Report semantic limitations and provider/upstream changes that remain outside scope.

For a large audit, delegate disjoint file/provider groups, not multiple labeled passes over the same files. Agree on the common architecture before parallel edits.

## Stop condition

Every in-scope context surface has a disposition, confirmed defects have an owning repair or explicit blocker, useful references remain reachable, and representative tasks route without unnecessary work or authority escalation. Static checks cannot guarantee an agent will never misunderstand.
