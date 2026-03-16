---
applyTo: "src/Shared/**"
---

# Shared Code Instructions

Code in `src/Shared/` is linked (compiled) into multiple MSBuild assemblies. Changes here have cross-assembly impact and require extra care.

## Cross-Assembly Impact

* Shared files are compiled into `Microsoft.Build`, `Microsoft.Build.Tasks`, `Microsoft.Build.Utilities`, and the CLI. A bug here breaks multiple assemblies simultaneously.
* Test changes against all consuming assemblies, not just one.
* `#if` conditional compilation is used extensively — verify behavior for all target configurations (`FEATURE_*`, `RUNTIME_TYPE_NETCORE`, etc.).

## IPC Packet Stability

* IPC packet types in Shared (e.g., `NodePacketTranslator`, `ITranslatable` implementations) define the wire format between MSBuild nodes.
* Never change packet layout without versioning. Old out-of-proc nodes must communicate with new in-proc nodes.
* Serialization must handle missing fields gracefully (forward compatibility).
* Test IPC round-trip for all modified packet types.

## FileUtilities Safety

* `FileUtilities.cs` is the primary path manipulation utility. Use it instead of raw `System.IO.Path` calls.
* Path normalization must handle: UNC paths, long paths (> MAX_PATH), trailing separators, relative paths, and embedded `.`/`..` segments.
* File existence checks and path comparisons must be OS-appropriate (case-sensitive on Linux, insensitive on Windows/macOS).
* Avoid unnecessary file system calls — they are slow and can fail on network paths.

## String Comparison Correctness

* Use `MSBuildNameIgnoreCaseComparer` for MSBuild identifiers (properties, items, targets).
* Use `StringComparison.OrdinalIgnoreCase` for general case-insensitive comparisons — never `CurrentCulture`.
* Never use `ToLower()`/`ToUpper()` for comparison purposes — use `StringComparer` or `String.Equals` with the right `StringComparison`.

## Performance Considerations

* Shared utilities run on every hot path. Allocation discipline is critical.
* Prefer `Span<char>` and `stackalloc` over string allocations for temporary parsing.
* Cache results from `FileUtilities` and `EnvironmentUtilities` when called repeatedly with the same arguments.

## Cross-Platform Correctness

* Never hardcode path separators. Use `Path.DirectorySeparatorChar` or `FileUtilities` helpers.
* Handle .NET Framework vs .NET Core behavioral differences with appropriate `#if` guards.
* Environment variable access patterns differ across platforms — use the shared helpers.

## Related Documentation

* [Contributing Code](../../documentation/wiki/Contributing-Code.md)
