# ToolTask and child-process boundaries

Use this reference for ToolTask-derived migrations or tasks that launch programs. Read the actual Utilities implementation first: overrides can inherit substantial environment handling already.

## Current base behavior to account for

In the inspected `src\Utilities\ToolTask.cs`:

- `ToolTask` implements `IMultiThreadableTask` and has a virtual `TaskEnvironment` property initialized to fallback.
- `GetProcessStartInfo` starts with `TaskEnvironment.GetProcessStartInfo`, sets executable/arguments and redirection, and uses non-shell execution for its normal tool path.
- A nonempty `GetWorkingDirectory()` override is resolved through the task environment. When the override is null/empty, the start information's existing working directory is retained.
- Task-level environment overrides are applied after the environment-based start information is created.
- `ComputePathToTool` distinguishes executable paths from bare names and has PATH/command-processor handling.

These facts are not a guarantee for an older Utilities package, a custom override that bypasses the base, or an unrelated process launcher.

## Audit the actual lifecycle

| Boundary | Questions |
| --- | --- |
| Constructors and initializers | Are defaults computed before engine injection? Does a base already own the environment property? |
| `Execute` and called helpers | Is the base path used, or is process setup duplicated/bypassed? |
| `ValidateParameters` | Do probes resolve against the intended base while preserving optional-input and failure behavior? |
| `SkipTaskExecution` | Are both input and output timestamps/probes based on the correct paths? Does a default/failure incorrectly make the task appear up to date? |
| `GenerateFullPathToTool` / tool lookup | Is this a project-relative filesystem executable, an SDK/tool installation path, or an intentional bare command name? |
| `GetWorkingDirectory` | Is the override intentional, and does the base already resolve it? What directory is retained when no override is supplied? |
| Command/response-file generation | Does the child interpret relative arguments in its working directory, relative to a response file, or under another explicit base? |
| Host-object execution | Does work go through an IDE/compiler host rather than the child-process path being reviewed? |
| Cancellation and cleanup | Which process/files are owned, which context resolves cleanup paths, and can callbacks outlive the task? |

Do not describe every override as running "before or after Execute"; inspect the real invocation chain, including overrides called from the base `Execute`.

## Executable lookup is distinct from the child's directory

For a filesystem executable intended to be project-relative, resolve it against the appropriate task environment before launch. Do not assume setting a child's working directory causes executable lookup to use that directory on every OS or shell mode.

Conversely, do not require every `FileName` to be absolute without checking the contract. ToolTask supports bare command names/PATH lookup and command-processor routes. Blindly prefixing a project directory can break those routes.

Inspect `UseShellExecute`, PATH provenance, command-processor behavior, `ToolPath`/`ToolExe`, runtime/platform, and any tool-discovery helpers. A check that a file exists at an absolutized path is not proof that the same path is ultimately launched.

## Working directory and arguments

The MT environment's start information supplies a project directory; fallback returns ordinary process start information. A null `GetWorkingDirectory()` override does not always mean "inherit the process CWD": the base may already have an environment-derived directory.

Keep relative command arguments when that is the existing child-tool contract. Absolutizing them can alter logged command lines, diagnostics, response-file contents, generated files, or reproducibility.

Do not assume all relative arguments use the process working directory. Some tools resolve entries relative to the response file, a configuration file, or another command option. Establish that base and preserve quoting/escaping.

Avoid building or rewriting command strings outside existing ToolTask helpers merely to change path resolution. Distinguish I/O paths used by the task from the serialized values handed to the tool.

## Environment propagation

Start from the supplied task environment when appropriate, then retain existing tool-specific overrides and precedence.

Check the actual `ProcessStartInfo.Environment`/`EnvironmentVariables` behavior for added, replaced, and removed variables. In the inspected MT driver, virtual values are copied onto the created start information; do not infer complete replacement of inherited state without examining the implementation and the resulting child behavior.

The MT driver's variable setter does not impose a universal ban on engine-prefixed names. Instead, identify whether a value is intended for an already-initialized engine subsystem, subsequent project work, or just this child. Engine caches may have read a value earlier; a successful setter call is not proof that such a cache changes.

Do not set persistent machine/user environment variables as part of migrating a task. Do not convert a task-local override into shared process mutation or assume that virtualizing an intentional global side effect preserves its meaning.

## Response files, logs, and failures

Trace the file used to write/read a response file, the switch passed to the child, and what the child considers its base. Preserve encoding, escaping, deletion, and existing error handling.

Compare logged tool commands and path-bearing exceptions with the baseline. Use original-form values where the existing diagnostic contract requires them; do not broadly sanitize arbitrary exception text.

For a failed launch or cancellation, retain the established return/error behavior and clean up only owned resources. Replacing a library operation with a generic catch-and-success path is not an MT fix.

## Evidence to request

Select evidence for the changed boundary: correct executable chosen, expected child working directory, relevant environment additions/removals, command/response-file content, output location/content, or cancellation cleanup.

A captured start-info object can prove configuration construction but not necessarily OS launch behavior. A unit test of a derived override does not prove that the engine selected that override or host. State the level demonstrated and the unresolved boundary.

## Source landmarks

In the reviewed checkout, inspect `src\Utilities\ToolTask.cs`: `TaskEnvironment`, `ComputePathToTool`, `GetProcessStartInfo`, `GetWorkingDirectory`, `ExecuteTool`, and the relevant overrides/callers. Compare `src\Framework\MultiProcessTaskEnvironmentDriver.cs` with `MultiThreadedTaskEnvironmentDriver.cs`. For constructor/property timing, follow `TaskExecutionHost.InitializeForBatch` and the selected factory.
