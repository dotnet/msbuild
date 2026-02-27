# MSBuild Repository Task Enlightenment Guidelines

This document provides specific guidance for enlightening tasks within the MSBuild repository.

## Acceptable Changes in Task Behavior During Enlightenment

**The fundamental principle is to preserve task behavior and outputs for all input options.**

### Permissible Changes
Modifications to error messaging or failure location are acceptable when a task fails with a different but reasonable error at an alternative execution point, provided that **no significant side effects** (such as disk modifications or output changes) occur between the original and new failure points.

### Changes Requiring Change Waves
Modifications that alter the success or failure outcome of a task for given inputs require careful evaluation. Such changes are permissible only when implemented behind a **change wave** and when the affected scenarios represent **obscure or edge use cases**.

## Immutable Environment Variables in MSBuild

Certain MSBuild tasks (such as `GetFrameworkPath`) use internal infrastructure that depends on environment variables for resolution. These results are cached in-process for reuse by both tasks and internal MSBuild components. MSBuild assumes that these variables remain constant throughout the build process. 

While multiprocess mode cannot prevent tasks from modifying these variables, multithreaded mode enables MSBuild to enforce protection of environment variables that must remain immutable. Attempts to modify these protected variables will result in an `InvalidOperationException`.

With this protection in place, we will allow MSBuild tasks to continue using internal infrastructure directly without requiring the TaskEnvironment API.

MSBuild protects environment variables in these categories:

1. **Variables with MSBuild-specific prefixes** (e.g. ones used in Traits)
2. **Framework and SDK location variables** (e.g., `COMPLUS_INSTALL_ROOT`, `COMPLUS_VERSION`, `ReferenceAssemblyRoot`, `ProgramW6432`, etc)
