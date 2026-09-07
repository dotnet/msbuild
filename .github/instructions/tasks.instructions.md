---
applyTo: "src/Tasks/**/*.cs"
---

# Built-in tasks

- Preserve task names, parameter types, required/optional status, output semantics, and diagnostic behavior. New optional parameters should preserve existing behavior by default.
- Follow the existing task base and logging/resource helpers. A generic "validate early" rule does not authorize new errors for previously accepted inputs.
- Match each output's contract; an optional output need not become non-null merely because `Execute` succeeds.
- Reuse appropriate path helpers and APIs supported on the affected TFMs. Verify overload availability and semantics before replacing a fallback or TOCTOU-sensitive sequence.
- For ResolveAssemblyReference, use the [RAR documentation](../../documentation/wiki/ResolveAssemblyReference.md) and [core scenarios](../../documentation/specs/rar-core-scenarios.md) relevant to the changed path, not an unbounded audit.

## Multithreaded execution

- Inspect the specific task's base classes, `IMultiThreadableTask`/`TaskEnvironment` use, and `[MSBuildMultiThreadableTask]` attestation. Not every task or inherited API establishes MT safety.
- Follow transitive calls for process current directory, environment variables, static caches, and registered task objects. State shared by threads within one process is different from state in separate workers/TaskHosts.
- Preserve virtual task-environment semantics and fallback behavior. Confirm the actual execution mode/host in tests; passing a flag is not proof of in-process execution.
- Use the [host map](../skills/use-bootstrap-msbuild/references/host-map.md) to distinguish modern MSBuild TaskHosts from the legacy `MSBuildTaskHost.exe` project.
- Registered-object registry operations being thread-safe does not make the registered object safe for concurrent use. Review the object's contract and lifetime; do not automatically label all sharing a warning or demand migration.
- Use an available MT-migration specialist for a substantial migration review, or perform the focused call-chain analysis directly. Do not require an installed plugin or infer safety from the attribute alone.

Use [compatibility assessment](../skills/assessing-breaking-changes/SKILL.md) for behavior changes and [diagnostic authoring](../skills/authoring-errors-and-warnings/SKILL.md) when changing user-facing messages.
