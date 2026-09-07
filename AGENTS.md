# Working in MSBuild

Preserve compatibility across MSBuild's SDK, Visual Studio, and third-party consumers. Measure performance-sensitive changes.

## Establish the task

1. Identify the requested outcome and authority: explanation/audit/review, local edits, or an external action. A review is read-only unless changes are requested; it does not authorize posting, merging, resolving threads, or changing CI.
2. For a PR, establish repository, base, head, and actual diff. For repros/benchmarks, identify binaries, SDK, host, configuration, and mode. Recheck after a branch/base change.
3. Inspect the worktree before editing. Preserve unrelated changes. Do not commit, push, or create a PR unless requested.
4. Check claims against source/configuration at the relevant revision. Old specs, session summaries, and memory can be stale. Resolve contradictions against implementation or report them.

## Load context progressively

- Start with this file and the instructions matching the affected paths in [.github/instructions](.github/instructions). Read nested `AGENTS.md` files before changing their subtree.
- Select the skill whose **description matches the task**, not every skill mentioning the same technology. Read its entry point, then only the reference needed for the current question. Do not recursively preload links.
- Use the routing and authoring guide in [Agent context](documentation/agent-context.md) when the right workflow is unclear or when changing instructions, skills, agents, or prompts.
- Start with a focused source lookup; expand to callers, consumers, history, or external docs when evidence requires it.
- Discover available tools before using them. Plugin availability is session-specific; do not assume a named plugin, agent, or MCP operation is installed. Report missing capabilities and use a supported read-only alternative where possible.

## Repository invariants

- Follow [.editorconfig](.editorconfig) and surrounding style. Use supported modern C# features, including collection expressions, without unrelated modernization.
- New files use nullable reference types. In existing `#nullable disable` files, preserve that convention unless nullable migration is part of the task. Use `is null` / `is not null`.
- Use `MSBuildNameIgnoreCaseComparer` for MSBuild names. Choose comparisons for other strings and paths from their actual contract, not a blanket culture/OS rule.
- Avoid unnecessary allocations and repeated I/O on hot paths. Choose collections and APIs for the measured access pattern and supported target frameworks.
- Preserve existing build semantics. New warnings can break `WarnAsError` builds. Use the compatibility skill to assess opt-in behavior and whether a ChangeWave is warranted; neither every bug fix nor every new diagnostic has the same policy.
- Reuse existing helpers and resource-based diagnostics. Do not swallow failures, add broad catches, or replace a failure with a success-shaped result.
- Tests use xUnit and Shouldly. Match the affected behavior with meaningful assertions, including the regression scenario; do not weaken isolation to make tests faster.

## Build and test deliberately

- Read [global.json](global.json), [Directory.Build.props](Directory.Build.props), and [src/Directory.Build.props](src/Directory.Build.props) for the SDK, runner, TFMs, and language configuration. Do not copy version numbers from these instructions or use an arbitrary installed SDK.
- For a scoped build, use `dotnet msbuild <project> -v:q` with the correct SDK and restored prerequisites. For repository setup or a justified full build, use `.\build.cmd -v quiet` on Windows or `./build.sh -v quiet` on Unix.
- Use [running-unit-tests](.github/skills/running-unit-tests/SKILL.md) before choosing test commands, and [use-bootstrap-msbuild](.github/skills/use-bootstrap-msbuild/SKILL.md) when proving behavior in locally built MSBuild.
- Start with the smallest existing validation covering the change. Escalate for uncovered consumers, TFMs, or integration boundaries, not because every task must run a full build. Documentation-only changes need link/content checks, not a product rebuild.
- Do not redirect MSBuild output to a file. Use a binary logger or `-flp:"v=q;LogFile=ErrorsAndWarnings.log"` when a log is needed. Diagnose the **first** relevant errors.
- Do not manually edit generated `artifacts` or `.dotnet` contents. Do not change `global.json` or `NuGet.config` without an explicit request.
- Run independent builds in distinct output/worktree locations. Terminate only processes positively identified as belonging to the task, by PID; never kill all processes with a shared name.

## Finish with evidence, not repetition

- A test passing is not proof that it exercises the changed code. For a regression claim, establish the failing baseline and successful fixed behavior under the same conditions when feasible.
- Distinguish measured results, source-derived conclusions, and unverified hypotheses. A clean review or green retry is not proof that no edge cases remain.
- Iterate for a finding, failed check, or uncovered requirement. Do not default to fixed review loops, per-dimension agent swarms, or model votes instead of evidence.
- Keep the final handoff concise and matched to its audience. Preserve useful implementation guidance when shortening public-facing documentation.
- Update related documentation at its authoring source, not an automation-generated mirror. Keep durable contributor-wide guidance in its owning context layer; do not append one-off session fixes or machine state to this file.

## Ownership boundaries

- [eng/common](eng/common/AGENTS.md) is owned by Arcade; propose changes upstream rather than editing its generated copy.
- [src/MSBuildTaskHost](src/MSBuildTaskHost/AGENTS.md) is a legacy compatibility component with stricter change rules.
- Installed plugin caches and personal configuration are not repository policy. Audit them separately; fixes belong in the owning plugin source or an explicitly requested personal setting change.
