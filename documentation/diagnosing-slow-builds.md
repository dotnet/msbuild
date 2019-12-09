# Diagnosing slow builds

No build is ever fast enough. It's often worth investing some effort on understanding where MSBuild spends its time when building your code, and thinking about whether it can be improved.

[Obligatory XKCD](https://www.xkcd.com/303/)

Logs at MSBuild’s “normal” level don't capture most of the really interesting information, though.

Try [capturing a binary log](https://aka.ms/msbuild/binlog) in your build; that preserves detailed information and allows for offline analysis. Changing log verbosity levels can affect build performance, but doesn't usually cause dramatic slowdown for full builds. Cross-check the results of with-logging and without-logging builds to make sure they're not completely out of whack before delving deeply into analyzing the binary log.

## Setting expectations

Do not expect to see 100% processor utilization during the entire build. Builds are often constrained by disk I/O, and the build process is also constrained by the dependencies between projects that are being built. If 100 projects depend on a common library, that library must be built before they can start building.

For this reason, avoid unnecessary dependencies. Dependencies are usually `ProjectReference` items, but can also be expressed in a solution file as "solution build dependencies".

Dependencies between projects also usually keep `-maxcpucount` values from scaling linearly.

## Building in parallel

The biggest performance win is going from single-threaded build to multi-process build by adding `-m` to your command-line build invocation (or equivalent in build automation).

Standard project types should support multiprocessor build without any work on your part, but customizations or very old projects may have race conditions that need to be fixed. It's worth the effort!

## `DetailedSummary` and `PerformanceSummary`

At minimum, [enable DetailedSummary and PerformanceSummary logging](https://docs.microsoft.com/visualstudio/msbuild/msbuild-command-line-reference). That captures information about how MSBuild schedules work to its available nodes and what parts of the build process take up the most time.

TODO: sample outputs and analysis

📝 The performance summary can be misleading with complicated builds, because it counts time spent waiting on another project to build redundantly ([microsoft/msbuild#4189](https://github.com/microsoft/msbuild/issues/4189)).

## If a task or target dominates build time

If most of the build time is being spent in a specific task:

* Can the thing be avoided? Often it cannot (sadly, we can't skip running the compiler or linker), but if it can, that's a huge win.
* Does the target declare correct inputs and outputs for [incremental build](https://docs.microsoft.com/visualstudio/msbuild/incremental-builds)?
* Can the inputs be simplified? For instance, can generated code that causes compiler delays be restructured?
* Should the tool be improved? Is there a bug that can be filed against the task or tool owner?

## If MSBuild project evaluation dominates build time

See [evaluation profiling](evaluation-profiling.md).
