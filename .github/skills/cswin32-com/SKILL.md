---
name: cswin32-com
description: "Add or migrate MSBuild struct-based COM activation, pointer ownership, manual interfaces, and vtable calls. Not general P/Invoke, automatic migration of unrelated COM code, or project-wide AOT analysis."
argument-hint: "Identify the COM interface, owner, activation path, and caller contract."
---

# Work with struct-based COM

**Use for:** COM ownership, activation, `ComScope<T>`/`AgileComPointer<T>`, manual `IComIID` implementations, or native vtable changes.

**Do not use for:** ordinary native buffers/platform guards alone; use [cswin32-interop](../cswin32-interop/SKILL.md) for that boundary. Struct-based production interop does not make built-in-marshaller test techniques Native-AOT-compatible.

## Source preflight

Read the caller and relevant [COM helpers](../../../src/Framework/Windows/Win32/System/Com), [IID.Get](../../../src/Framework/Windows/Win32/IID.cs), and the owning project's exclusions. Check [NativeMethods configuration](../../../src/Framework/NativeMethods.json) and the [generator pin](../../../eng/dependabot/Directory.Packages.props) before assuming generated shapes.

Follow the affected [Framework](../../instructions/framework.instructions.md) or [task instructions](../../instructions/tasks.instructions.md). Match current TFM/feature guards rather than copying a release number.

## Workflow

1. Establish the native interface/ABI and whether the current generator provides it. Use [manual interface guidance](references/manual-interfaces.md) only when hand-authored declarations are needed.
2. Classify each pointer as borrowed, newly owned, transferred, or retained. Apply [lifetime and activation](references/lifetime-and-activation.md); do not wrap a borrowed pointer in an owning scope accidentally.
3. Preserve activation, HRESULT, cleanup, and caller continuation behavior. Use the actual IID helper signature and dispose class factories as well as interface scopes.
4. Check compile-time exclusion and runtime platform/apartment boundaries. `SupportedOSPlatform` can annotate structs; foundational COM wrappers have different storage responsibilities from application owners.
5. Select existing ABI/lifetime/failure evidence. For managed mocks, read [COM testing](references/com-tests.md) and its runtime limitations.

## Evidence and stop condition

Stop when the requested ABI, ownership, activation, and error contract is accounted for on the affected hosts/TFMs. State unexercised native paths. Do not infer complete AOT support from struct declarations, add blanket suppressions, or mechanically rewrite unrelated raw-pointer fields.
