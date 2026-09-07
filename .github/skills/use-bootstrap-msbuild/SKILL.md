---
name: use-bootstrap-msbuild
description: "Prove a reproduction or fix using locally built MSBuild binaries. Use when source identity and the actual product host matter, not for ordinary source inspection or every unit-test run."
---

# Reproduce with locally built MSBuild

## Establish identity first

Identify the requested source revision, broken baseline, host/runtime, architecture, SDK, build mode, and assertion. An installed `dotnet` or a previous bootstrap is not automatically the code being reviewed.

If "TaskHost" or "server" is ambiguous, use the [host map](references/host-map.md) before choosing a project or executable.

Bootstrap layout is defined by [BootStrapMsBuild.props](../../../eng/BootStrapMsBuild.props) and [BootStrapMsBuild.targets](../../../eng/BootStrapMsBuild.targets); the SDK payload version comes from [Versions.props](../../../eng/Versions.props). Architecture can add another path component.

## Workflow

1. Inspect existing outputs and their provenance. Build the relevant bootstrap when missing or stale, using the repo build entry point and its pinned prerequisites; do not rewrite SDK/feed configuration to force success.
2. Run the reproduction through the actual bootstrap host, not a `dotnet` resolved from an unrelated `PATH`.
3. Capture the command, working directory, product/SDK identity, exit status, assertion, and a binlog when it establishes the execution path.
4. For a fix, execute the same scenario against baseline and candidate outputs with equivalent configuration. Keep their worktrees and output paths separate.
5. Stop when the requested observable difference is established, or report the exact setup/runtime boundary preventing proof.

Typical **Windows, default-architecture** entry points:

```powershell
& .\artifacts\bin\bootstrap\core\dotnet.exe build <repro-project> -bl:<binlog>
& .\artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\MSBuild.exe <repro-project> -bl:<binlog>
```

Select the appropriate one and discover the actual layout before running it. On Unix, use the produced `dotnet` host and shell-appropriate paths. Do not assume `MSBuild.dll` sits at the bootstrap root; the Core bootstrap is an SDK layout.

## Avoid false proof

- Use an explicit working directory and executable in each shell invocation. Changing directory in one tool call may not affect the next.
- After a source/base change, rebuild the affected output. File existence or a successful test from another TFM is insufficient evidence.
- If stale servers/nodes or locked outputs interfere, identify the processes belonging to this exact task/bootstrap before stopping them by PID. Do not kill all dotnet/MSBuild processes or reset shared machine state.
- Do not infer MT, worker, TaskHost, or server usage merely from requested flags; inspect relevant process/log evidence.
- For a performance comparison, use the [benchmark protocol](../benchmarking-msbuild/SKILL.md). A functional repro is not a controlled timing experiment.

For deeper build setup, read the relevant section of [Bootstrap documentation](../../../documentation/wiki/Bootstrap.md) or [Framework build/debug guidance](../../../documentation/wiki/Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md), checking it against current configuration.
