---
applyTo: "src/Framework/**"
---

# Framework contracts and shared implementation

This assembly contains both public task/logger contracts and internal utilities, interop, and transport code. Do not assume everything in this folder is public API.

## Preserve the boundary

- Default new implementation to `internal`; public additions need compatibility rationale and XML documentation.
- Preserve existing public signatures and third-party implementors. Do not add interface members assuming default implementations work on every supported runtime; follow the existing numbered `IBuildEngine` pattern where appropriate.
- Inspect actual implementors, including [TaskHost](../../src/Build/BackEnd/Components/RequestBuilder/TaskHost.cs) and out-of-process hosts. `TaskExecutionHost` is not the `IBuildEngine` implementation.
- The [project](../../src/Framework/Microsoft.Build.Framework.csproj) uses reference-assembly generation and package validation. Follow that machinery and the current baseline configuration; do not create nonexistent `PublicAPI.Unshipped.txt` files because a generic guide mentions them.
- Check the library TFMs in [src/Directory.Build.props](../../src/Directory.Build.props). Availability on modern .NET does not establish availability on .NET Framework or the reference-only target.

## Implementation changes

- Preserve type/assembly identity and resource ownership when moving formerly linked code into Framework.
- Trace actual callers before changing shared utilities; use the performance skill only for a relevant hot path.
- For IPC, inspect the supported peer/handshake and actual translator. Missing data does not automatically become a default value in an unframed stream.
- For event changes, use [binary-log compatibility](../skills/maintaining-binary-log-compatibility/SKILL.md) and trace forwarding separately.
- For Windows interop or COM, load only the matching interop skill and retain platform/feature guards.

BuildCheck has public APIs in [Build/BuildCheck/API](../../src/Build/BuildCheck/API) and internal transport contracts here; use the [BuildCheck instructions](buildcheck.instructions.md) for that boundary.
