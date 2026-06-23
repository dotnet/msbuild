# MSBuildTaskHost — Agent Instructions

This folder builds `MSBuildTaskHost.exe`, which targets .NET Framework 3.5 and
exists to maintain Visual Studio asset compatibility (especially around
targeting .NET Framework 3.5). It is **not under active development**.

**Do not edit any files in this folder unless the user explicitly tells you to
modify this folder or a specific file within it.**

When working on a task that would otherwise touch this folder:

* Skip it. Do not change code here for style, modernization, cleanup, or other
  minor reasons.
* Make changes here only when a compatibility or servicing need requires it, and
  only with explicit user confirmation.
* Remember this code targets .NET Framework 3.5 — newer language/runtime
  features and APIs are not available, and it must keep its named-pipe
  communication compatible with the rest of the repository.

See `README.md` in this folder for more context.
