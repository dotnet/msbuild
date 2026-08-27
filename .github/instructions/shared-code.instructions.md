---
applyTo: "src/Shared/**"
---

# Shared Code Instructions

Code in `src/Shared/` is linked (compiled) into multiple MSBuild assemblies. A bug here breaks multiple assemblies simultaneously.

## Cross-Assembly Impact

* Compiled into `Microsoft.Build`, `Microsoft.Build.Tasks`, `Microsoft.Build.Utilities`, and the CLI.
* Test changes against all consuming assemblies, not just one.
* `#if` conditional compilation is used extensively — verify behavior for all target configurations (`FEATURE_*`, `RUNTIME_TYPE_NETCORE`, etc.).

## IPC Packet Stability

* `NodePacketTranslator` and `ITranslatable` implementations define the wire format between MSBuild nodes.
* Never change packet layout without versioning — old out-of-proc nodes must communicate with new in-proc nodes.
* Serialization must handle missing fields gracefully (forward compatibility).
* Test IPC round-trip for all modified packet types.

## FileUtilities Safety

* Use `FileUtilities.cs` instead of raw `System.IO.Path` calls.
* Must handle: UNC paths, long paths (> MAX_PATH), trailing separators, relative paths, embedded `.`/`..` segments.
* Avoid unnecessary file system calls — slow and can fail on network paths.

## Cross-Platform Correctness

* Handle .NET Framework vs .NET Core differences with appropriate `#if` guards.
* Environment variable access patterns differ across platforms — use the shared helpers.
