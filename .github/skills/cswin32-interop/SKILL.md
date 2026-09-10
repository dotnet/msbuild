---
name: cswin32-interop
description: "Add or migrate MSBuild Windows P/Invoke calls, native types, buffers, and platform/source-build guards. Not general .NET work, project-wide AOT migration, or COM ownership/vtable design."
argument-hint: "Identify the native API, caller, and affected compilation/runtime paths."
---

# Work with CsWin32 P/Invoke

**Use for:** an actual Windows interop boundary, its declaration or typed result, buffer handling, or platform/source-build behavior.

**Do not use for:** unrelated `Span<T>` usage, general build troubleshooting, or automatically migrating every native declaration. Use [cswin32-com](../cswin32-com/SKILL.md) when COM lifetime or vtables are the question.

## Source preflight

Inspect the caller and its old native/error contract. Read [NativeMethods.txt](../../../src/Framework/NativeMethods.txt), [NativeMethods.json](../../../src/Framework/NativeMethods.json), the [package pin](../../../eng/dependabot/Directory.Packages.props), and relevant existing [PInvoke partials](../../../src/Framework/Windows/Win32).

Check [feature definitions](../../../src/Directory.BeforeCommon.targets), the owning project exclusions, and current TFMs in [src/Directory.Build.props](../../../src/Directory.Build.props). Follow [Framework](../../instructions/framework.instructions.md) or [task instructions](../../instructions/tasks.instructions.md) for the affected source.

## Workflow

1. Find an existing generated API or helper before adding a declaration. Framework owns the shared generator; do not add another generator dependency merely to reach its types.
2. Verify signature, native widths, string/buffer ownership, and error reporting using [ABI and platform guidance](references/abi-and-platforms.md). Inspect the selected version's API rather than guessing an overload or enum.
3. Preserve the caller's failure, cleanup, and fallback behavior. `SetLastError=true` is not an instruction to start throwing.
4. Establish the compile-time exclusion and runtime support boundary. An existing annotated Windows-only boundary may already supply the runtime contract.
5. Use [buffer and native-type patterns](references/buffers-and-types.md) as needed. Select evidence for the changed TFM/platform/source-build surface rather than running two full solutions before every push.

## Evidence and stop condition

Stop when the exact interop contract and affected boundaries are accounted for. A read-only lookup needs source/API evidence, not a build. For implementation, distinguish compiled coverage from native runtime coverage and disclose any unexercised platform or unavailable generated output.
