# MT migration plugin

Task-authoring guidance and an evidence-focused reviewer for MSBuild's multithreaded execution model. Use the plugin when changing task support for `MSBuildMultiThreadableTask`, `IMultiThreadableTask`, or `TaskEnvironment`, not for generic context audits, CI triage, or build-performance comparisons.

## Contents

| Component | Responsibility |
| --- | --- |
| [Migration skill](skills/multithreaded-task-migration/SKILL.md) | Scope, source preflight, migration workflow, and evidence/stop criteria |
| [MT reviewer](agents/mt-migration-reviewer.agent.md) | Read-only review of reachable MT-specific defects; reuse existing general review |
| [Source and migration](skills/multithreaded-task-migration/references/source-and-migration.md) | Attribute versus interface, construction/injection, environment lifetime, and migration decisions |
| [Path and diagnostic compatibility](skills/multithreaded-task-migration/references/path-and-diagnostic-compatibility.md) | Eight compatibility hazards, examples, exception timing, and input/output contracts |
| [Call chains and shared state](skills/multithreaded-task-migration/references/call-chains-and-shared-state.md) | Bases, abstractions, analyzer limits, caches, registered objects, and nested tasks |
| [ToolTask and processes](skills/multithreaded-task-migration/references/tooltask-and-processes.md) | Lifecycle overrides, executable lookup, working directories, arguments, and environment propagation |
| [Validation and review](skills/multithreaded-task-migration/references/validation-and-review.md) | Regression-sensitive examples, host evidence, test isolation, and finding format |

Read the entry point, then only the reference needed for the current boundary. The references preserve technical detail; they are not a checklist to preload or a reason to launch a reviewer per topic.

## Host adaptation

Determine the task-authoring repository, revision, supported hosts, framework references, TFMs, test runner, and available helpers before applying a recipe. Source landmarks in the references describe an MSBuild checkout; another task repository must resolve the corresponding implementation or package version.

Attribute eligibility, environment injection, process hosting, and test helpers are different contracts. A standalone task test does not prove engine routing, and a clean analyzer run does not establish complete runtime safety.

Reviews do not authorize code changes, GitHub publication, thread resolution, or installation. General review is requested only for an uncovered scope, not repeated as a fixed multi-agent pass. Leaving a task unannotated is a legitimate outcome when its intended concurrency contract cannot be supported.

## Packaging and refresh

`plugin.json` retains the `mt-migration` identity, version, skill directory, and agent path. This directory is repository-owned authoring source. An installed copy with the same name has a separate lifecycle; its name alone does not establish its source revision.

Installation or refresh is an explicit owner action, not part of a review or documentation change. Consult the current [CLI plugin documentation](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference) before using its local-directory install/update operations. Ordinary cached installs and local directory-marketplace sources have different refresh behavior.

Distinguish contributing-source inventory from effective skill resolution and from an already-running session. Updating these files does not prove an existing session has loaded them.

## Maintenance

Aim for small skill and interactive-agent entry points; 100 lines and 6 KiB are useful authoring targets, not mandatory contribution limits. Move corrected technical material into focused references rather than deleting it to reach a size target.

In the MSBuild repository, run the offline context validator from the repository root:

```powershell
pwsh -NoProfile -File scripts\Validate-AgentContext.ps1
```

Documentation changes need local-link, metadata, scope, and content checks, not a product build. Static validation does not prove every runtime scenario. Add source-backed examples for newly understood hazards; do not turn unverified anecdotes, analyzer-version assumptions, or task-specific exceptions into universal rules.
