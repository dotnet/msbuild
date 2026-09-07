# Agent context: routing, evidence, and ownership

This is the maintenance guide for the repository's agent context, not a second always-loaded policy file. Start with [AGENTS.md](../AGENTS.md); use this page to choose a workflow or change the context framework.

## Layers and responsibility

| Layer | Owns | Does not own |
|---|---|---|
| `AGENTS.md` | Small, durable repository invariants; authority and evidence boundaries | Tutorial content, machine state, copied SDK versions |
| `.github/copilot-instructions.md` | Pointer to the canonical root guidance | A competing or duplicated policy |
| `.github/instructions/*.instructions.md` | Local invariants selected by `applyTo`, links to implementation and relevant skills | General C# tutorials or full build requirements for every edit |
| `.github/skills/<name>/SKILL.md` and plugin-owned skills | Task trigger, exclusions, preflight, short workflow, stopping criteria | Every example and every possible subtask |
| Skill `references/` and scripts | Detailed, source-backed procedures needed for a particular step | Automatic instructions to run every linked procedure |
| `.github/agents` | A specialist's role, boundaries, and result contract | Mandatory fan-out, fixed models, implied GitHub write authority |
| Workflow prompts | The authorized automation task and its supported tools/outputs | An interactive workflow copied into a different execution environment |
| Personal/plugin context | Portable personal preferences and provider capabilities | Repository-specific policy copied into an installed cache |

Checked-in plugin sources under `plugins` are maintained here, but their installed copies have a separate lifecycle. Audit their manifests, skills, agents, and references as well as `.github`; directory location alone does not prove they are active.

Context is additive only when relevant. A more verbose document is not more authoritative, and a newer session summary is not automatically more correct than current source. Higher-priority platform and user instructions still apply; these layers do not redefine the host's instruction precedence.

## Retrieval loop

1. **Frame:** outcome, read/write authority, repository/ref, environment, and required artifact.
2. **Route:** choose one primary skill from its description; add another only for a distinct boundary encountered during the task.
3. **Retrieve:** read the entry point and the single relevant reference or implementation. A reference link is a navigation option, not an instruction to load an entire directory.
4. **Resolve:** check claims against source/configuration at the requested revision. Distinguish observed behavior from a proposal or an old design document.
5. **Act:** do the scoped work. Delegate only independent, substantial scopes with explicit ownership, relevant evidence, and a result contract.
6. **Evaluate:** inspect the requested observable outcome. Continue for a concrete gap; otherwise stop and state any limitation.

For an ambiguous workflow, pick a reasonable read-only starting point. Ask for clarification when the wrong choice would cause material rework or an unauthorized external action, not for routine formatting choices.

## Route by outcome, not keywords

| Requested outcome | Primary context | Load later only if needed |
|---|---|---|
| Review an MSBuild diff | [reviewing-msbuild-code](../.github/skills/reviewing-msbuild-code/SKILL.md) | A domain skill for the changed boundary; CI investigation for a failing check |
| Compare build modes or SDKs | [benchmarking-msbuild](../.github/skills/benchmarking-msbuild/SKILL.md) | Performance diagnosis after the experiment identifies a bottleneck |
| Optimize engine implementation | [optimizing-msbuild-performance](../.github/skills/optimizing-msbuild-performance/SKILL.md) | Benchmark protocol, evaluation, allocation, or interop details for the hypothesis |
| Run local tests | [running-unit-tests](../.github/skills/running-unit-tests/SKILL.md) | Bootstrap reproduction when the assertion must exercise the product executable |
| Prove an installed/local behavior change | [use-bootstrap-msbuild](../.github/skills/use-bootstrap-msbuild/SKILL.md) | The domain implementation and the exact regression scenario |
| Assess compatibility or diagnostics | [assessing-breaking-changes](../.github/skills/assessing-breaking-changes/SKILL.md) | ChangeWave mechanics or diagnostic authoring after the policy decision |
| Check broad pipeline/insertion health | [pipelines-health-check](../.github/skills/pipelines-health-check/SKILL.md) | Installed CI/Helix/codeflow specialist for the identified failing leg or flow |
| Triage bot updates | [merge-dependency-updates](../.github/skills/merge-dependency-updates/SKILL.md) | Codeflow tracing or merge actions only for the selected PRs |
| Manage issues or release state | [project-management](../.github/skills/project-management/SKILL.md) or [release](../.github/skills/release/SKILL.md) | Only the requested phase and its current service data |
| Change docs or prepare a handoff | [Task brief examples](agent-context/task-briefs.md) | Authoring-source discovery and the relevant API/implementation |
| Audit or improve agent context | [maintaining-agent-context](../.github/skills/maintaining-agent-context/SKILL.md) | Current provider inventory, source-backed corrections, and relevant session evidence |

Optional plugins are accelerators, not prerequisites for understanding the repository. In Copilot CLI, `/env` describes the running session, while `copilot plugins list` inventories discovered configuration for a new invocation. Recheck capability availability after configuration changes; do not assume a long-lived session has reloaded.

## Provider boundaries and overrides

Do not assume a conflicting repository instruction automatically overrides a provider's workflow. Repair the owning rule, narrow its routing, or use a deliberate supported adapter.

Copilot CLI's [plugin loading rules](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference#loading-order-and-precedence) resolve same-name skills before plugin contributions, with personal/project sources preceding plugins. Avoid accidental shadowing. A workaround for one user's installed provider is not automatically appropriate for shared repository guidance.

Use `copilot plugins list` to inventory contributing sources and `copilot skill list --json` to inspect resolved skill paths. The inventory can show both a plugin and repository copy; that alone does not identify which one is selected. Verify the resolved path after a change, especially if a personal skill has the same name.

If a deliberate adapter is needed, document its scope and removal condition. Do not let a permanent local copy hide future provider improvements.

Instruction edits need a new/resumed session; `/skills reload` refreshes skill discovery. Custom-agent profile changes require restarting the CLI according to its [agent documentation](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-custom-agents-for-cli). Ordinary installed plugin copies require a supported update/reinstall; a local path-sourced plugin has different refresh behavior. Consult the current CLI help instead of assuming one reload mechanism refreshes every layer.

## Writing or repairing context

Use this structure for skill entry points:

```markdown
---
name: directory-name
description: Specific task and boundary; exclude the most likely false trigger.
---
# Task
## Use when / Do not use when
## Before acting
## Workflow
## Evidence and stop condition
## References
```

The headings can vary when a shorter structure communicates the same contract.

- Descriptions are routing metadata, not a list of every adjacent technology. Avoid triggers such as "any .NET work" for a specialized procedure.
- Give each substantive rule one owner. Link to that owner instead of copying its checklist into the root, agent, skill, and workflow.
- Keep useful detail. Move a long procedure into focused references, correct its claims, and repair its links; shortening an entry point is not permission to discard the procedure.
- Put repository links in Markdown so they can be checked. Prefer links to an implementation/configuration plus the symbol to inspect over a prose copy of a volatile value.
- Label commands by execution environment and side effects. Shell syntax, available tools, runner modes, generated paths, and resource IDs must be discovered or verified before use.
- Include a read-only path and a meaningful stopping condition. "Everything is complete," a fixed number of rounds, or agreement among models is not an evidence standard.
- Do not turn a task-specific correction into a universal rule. Feedback about concise public docs does not imply deleting detailed migration guidance.

## Maintenance and regression checks

Run `pwsh -NoProfile -File scripts\Validate-AgentContext.ps1` from the repo root after context edits. It covers `.github`, checked-in `plugins`, root guidance, and these framework docs: required metadata, local Markdown file targets outside code fences, and the tracked pointer file type. It does not build MSBuild, contact services, or validate remote URLs, heading anchors, or the full YAML grammar.

Suggested entry-point targets are 100 lines / 6 KiB for root guidance and skill/interactive-agent entries, and 80 lines / 4 KiB for path instructions. References carry the detail. These are authoring aids, not mandatory contribution limits, a claim about token usage, or an excuse to remove necessary knowledge. Add `-CheckBudgets` to the validator to assess these targets explicitly; the default CI invocation does not enforce them.

The optional size assessment uses a separate 20-line / 1 KiB target for the Copilot pointer, to discourage a second copy of the root policy.

Keep that pointer a regular tracked file. With `core.symlinks=false`, Windows can show a tracked symlink as a tiny ordinary file; replacing its target text with prose without changing the Git file type creates a broken symlink on other systems.

Then walk the [task briefs](agent-context/task-briefs.md) against the changed routing. Check that a small task stays small, a proof task reaches executable evidence, and an external mutation requires authority. Static checks cannot prove semantic correctness; implementation-backed review and real workflow feedback remain necessary.

When a failure recurs, record the cause, owning layer, correcting evidence, and a regression scenario. Change the owner, not every consumer. Keep personal history, credentials, machine-specific observations, and temporary operational state out of committed context.
