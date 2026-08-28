---
applyTo: "src/Utilities/TaskLoggingHelper.cs,src/Utilities/AssemblyFolders/**/*.cs,src/Tasks/TaskLoggingHelperExtension.cs,src/Tasks/PropertyParser.cs,src/Tasks/PlatformNegotiation.cs"
---

# Multi-Assembly Source Instructions

These files are owned by their public API assembly but are also compiled into another MSBuild assembly with different conditional symbols.

* Preserve the public type's assembly identity and API surface.
* Validate every consuming project and target framework after changes.
* Treat `BUILD_ENGINE` branches as separate implementations with distinct CLR type identities.
* Do not add assembly-specific resource dependencies without verifying both compiled copies.
