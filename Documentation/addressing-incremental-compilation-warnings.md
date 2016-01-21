Addressing Incremental Compilation Warnings
===========================================

Incremental compilation is unsafe when compilation relies on tools with potential side effects (tools that can cause data races or modify the state of other projects that have been deemed safe to skip compilation. Or tools that integrate timestamps or guids into the build output).

The presence of such cases will turn off incremental compilation.

The following represent warning codes printed by CLI when the project structure is unsafe for incremental build and advice on how to address them:

- __[Pre / Post scripts]__: Scripts that run before and after each compiler invocation can introduce side effects that could cause incremental compilation to output corrupt builds (not building when it should have built) or to over-build. Consider modifying the project structure to run these scripts before / after the entire compile process, not between compiler invocations.

- __[PATH probing]__: Resolving tool names from PATH is problematic. First, we cannot detect when PATH tools change (which would to trigger re-compilation of sources). Second, it adds machine specific dependencies which would cause the build to succeed on some machines and fail on others. Consider using Nuget packages instead of PATH resolved tools. Thus there would be no machine specific dependencies and we would be able track when Nuget packages change and therefore trigger re-compilation. 

- __[Unknown Compiler]__: csc, vbc, and fsc have known side effects (which files and directories they read, write, and what they are not reading/writing).
We don’t know this for other compilers. So we choose to be safe and disable incremental compilation for now. We are planning to enable specification of tool side effects in a future version, so that they can participate in incremental compilation as well.

- __[Forced Unsafe]__: The build was not incremental because the `--no-incremental` flag was used. Remove this flag to enable incremental compilation.
