---
name: cswin32-interop
description: 'Guides CsWin32 P/Invoke interop in MSBuild. Consult when working with the PInvoke class, Windows.Win32 namespaces, FEATURE_WINDOWSINTEROP, HANDLE/HMODULE/HRESULT types, BufferScope<T>, replacing [DllImport] with CsWin32, or conditioning Windows-only code for source builds.'
argument-hint: 'Describe the Windows API or interop code you are migrating or adding.'
---

# CsWin32 Interop Guide

[CsWin32](https://github.com/microsoft/CsWin32) replaces `[DllImport]` with source-generated `PInvoke.*` calls. `FEATURE_WINDOWSINTEROP` is the compile-time gate; source builds disable it.

**Paired skill:** [cswin32-com](../cswin32-com/SKILL.md) covers struct-based COM interop on top of CsWin32 (`ComScope<T>`, `AgileComPointer<T>`, `delegate* unmanaged` vtables, `IComIID`, `CoCreateInstance`, manual COM structs not in Win32 metadata). This file covers only the general P/Invoke layer; the COM skill builds on its blittable-signature rules.

## Rules

1. **Replace `[DllImport]` with `PInvoke.*`**. Delete old declarations and hand-written structs/enums/constants.
2. **Gate with `#if FEATURE_WINDOWSINTEROP`**, add runtime `IsWindows` check inside. Both required.
3. **Use CsWin32 types directly** (`HANDLE`, `HMODULE`, `HRESULT.S_OK`, `FILE_FLAGS_AND_ATTRIBUTES`, etc.).
4. **Call `PInvoke.*` directly** — no wrappers. Types flow via `InternalsVisibleTo`.
5. **Prefer CsWin32 for Windows APIs**. Use `[LibraryImport]` only for non-Windows native calls (e.g. `libc`), guarded with `#if NET`.
6. **Preserve the old error-handling contract.** Check the original `[DllImport]` for `PreserveSig` / `SetLastError` / `BOOL` / `HRESULT` semantics and reproduce them: `PreserveSig=false` → `.ThrowOnFailure()`; `SetLastError=true` + failed `BOOL` → `throw new Win32Exception()`. Silently returning where the old code threw is a behavior change. See [cswin32-com's parity table](../cswin32-com/SKILL.md#error-handling-parity-when-migrating) for the COM-side equivalent.

## Blittable signatures

CsWin32 is configured with `allowMarshaling: false`, so every `[DllImport]` and every manual
COM vtable method must be blittable — no marshalling at the boundary. These rules apply
to both. For COM-vtable-only additions, see
[cswin32-com](../cswin32-com/SKILL.md#blittable-vtable-signatures).

- **Return `HRESULT`** from HRESULT-returning APIs (not `int`). Blittable (single `int` field),
  exposes `.Succeeded` / `.Failed` / `.ThrowOnFailure()`. Use `HRESULT.S_OK` over `0`; cast
  `e.HResult` to `(HRESULT)` when wrapping. `AddRef` / `Release` return `uint`.
- **Call `.ThrowOnFailure()`** instead of `if (hr.Failed) Marshal.ThrowExceptionForHR(hr)` —
  same exception, IErrorInfo-enriched, one-line call site:
  `iface->Method(...).ThrowOnFailure();`. Branch on `hr` only when handling a specific HRESULT
  (e.g. `ERROR_INSUFFICIENT_BUFFER`) before throwing. See
  [cswin32-com's parity table](../cswin32-com/SKILL.md#error-handling-parity-when-migrating)
  for the migration contract.
- **Use `PCWSTR` / `PWSTR`** for wide strings, never managed `string`. Implicit conversion from
  `fixed (char* p = managedString)`. Add to `NativeMethods.txt` if not yet generated.
- **`T**` not `out T*`** for pointer outputs. `out` triggers marshaling + a `fixed` round-trip
  at every call site.
- **`void*` for opaque / reserved params** — never `IntPtr.Zero`; pass `null` literally.
  `IntPtr` is fine at boundaries with the wider .NET surface (`Marshal.*`,
  `SafeHandle.DangerousGetHandle`, public API).
- **Prefer `nint` / `nuint`** over `IntPtr` / `UIntPtr` for native-sized integers — no boxing,
  better cast semantics, no `IntPtr.Zero` ceremony.
- **No managed reference types** (`string`, `StringBuilder`, arrays) in blittable signatures.
- **Don't specify `PreserveSig = true` on `[DllImport]`** — it's the default. Use
  `PreserveSig = false` only to force marshaller throw-on-failure (rare; prefer returning
  `HRESULT` and `.ThrowOnFailure()`). `[ComImport]` defaults the opposite way, but struct-based
  COM uses raw `delegate*` and isn't affected — see
  [cswin32-com](../cswin32-com/SKILL.md#blittable-vtable-signatures).
- **Constrain flag / option parameters to a typed `enum`.** When a native `DWORD` / `ULONG` /
  `int` is documented as a `typedef enum` or `#define` set, declare a C# `[Flags] enum Foo : uint`
  (matching the underlying type) and use it in the signature (and `delegate*` cast for COM).
  Mirror the constraint even when the native side has no named enum. Self-documenting at the
  call site: `OpenScope(path, CorOpenFlags.ofRead, ...)` vs `OpenScope(path, 0, ...)`. Co-locate
  the enum next to its consumer. See
  [`CorOpenFlags.cs`](../../../src/Tasks/AssemblyDependency/Metadata/CorOpenFlags.cs) and
  [`CorAssemblyFlags.cs`](../../../src/Tasks/AssemblyDependency/Metadata/CorAssemblyFlags.cs).

### Dual Guard Pattern

```csharp
#if FEATURE_WINDOWSINTEROP
    if (IsWindows)
    {
        PInvoke.GetFileAttributesEx(fullPath, out WIN32_FILE_ATTRIBUTE_DATA data);
    }
#endif
    // Cross-platform fallback
```

**WRONG**: `if (IsWindows) { #if FEATURE_WINDOWSINTEROP ... #endif }` — dead code in source builds.

Windows-only files are excluded via `<Compile Remove>` instead — no `#if` inside needed.

## Infrastructure

**Define**: `src/Directory.BeforeCommon.targets` sets `FEATURE_WINDOWSINTEROP` + `$(FeatureWindowsInterop)` when `DotNetBuildSourceOnly != true`. Use `$(FeatureWindowsInterop)` in `.csproj` for `<Compile Remove>`/`<Compile Include>`.

**CsWin32 config**: `src/Framework/NativeMethods.txt` (API list) + `NativeMethods.json` (`allowMarshaling: false`, `useSafeHandles: false`). Lives in Framework; other projects consume via `InternalsVisibleTo`. Do not add CsWin32 to other projects.

**Guard selection**:

| Guard | When | Runtime check? |
|-------|------|----------------|
| `#if FEATURE_WINDOWSINTEROP` | Multi-TFM Windows calls | Yes |
| `#if FEATURE_WINDOWSINTEROP && NET` | Manual COM structs that use static-abstract `IComIID` (e.g. WMI). CsWin32-generated COM types via `ComScope<T>` work on net472 via the `IComIID` polyfill — see [cswin32-com](../cswin32-com/SKILL.md#icomiid-polyfill-for-net472) | Yes |
| `#if FEATURE_WINDOWSINTEROP && !NETSTANDARD` | CsWin32 types without `static abstract` (net472 + net10) | Yes |
| `#if !NET` / `#if FEATURE_MSCOREE` | net472-only = inherently Windows | No |

**Namespace imports** must be inside `#if FEATURE_WINDOWSINTEROP`. WDK APIs use `Windows.Wdk` namespace.

**Files**: `src/Framework/Windows/` (CsWin32 partials), `src/Shared/Win32/` (COM helpers), `src/Framework/Utilities/Wmi/` (.NET-only COM structs), `src/Framework/Polyfills/IComIID*.cs` (net472/netstandard2.0 polyfills, gated `#if !NET`).

### Constant Replacements

`NativeMethodsShared.S_OK` → `HRESULT.S_OK`, `InvalidHandle` → `HANDLE.INVALID_HANDLE_VALUE`, `FILE_ATTRIBUTE_DIRECTORY` → `FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY`, `STD_OUTPUT_HANDLE` → `STD_HANDLE.STD_OUTPUT_HANDLE`, `GENERIC_READ` → `FILE_ACCESS_RIGHTS.FILE_GENERIC_READ`. Pattern: `CsWin32EnumType.ORIGINAL_NAME` — check generated types in `obj/`.

**Always prefer the generated enum over a local copy.** Before defining `private const int ERROR_*` / `private enum FooFlags`, grep the CsWin32 metadata:

- `ERROR_*` (Win32 error codes) → `WIN32_ERROR.ERROR_*` (uint enum)
- `HRESULT` codes → `HRESULT.S_OK`, etc.
- Restart Manager status/type → `RM_APP_STATUS` / `RM_APP_TYPE`
- File flags → `FILE_FLAGS_AND_ATTRIBUTES`, `FILE_ACCESS_RIGHTS`, `FILE_SHARE_MODE`, `FILE_CREATION_DISPOSITION`
- Process flags → `PROCESS_CREATION_FLAGS`, `STARTUPINFOW_FLAGS`, `PROCESS_ACCESS_RIGHTS`
- Memory mapping → `PAGE_PROTECTION_FLAGS`, `FILE_MAP`
- Shell folder → `KNOWN_FOLDER_FLAG`

Cast `int`/`uint` return codes via `(WIN32_ERROR)res` for `switch` and equality. Add the enum to `NativeMethods.txt` if not yet generated, then check `obj/.../generated/Microsoft.Windows.CsWin32/.../Windows.Win32.<EnumName>.g.cs`.

**Match local types to the CsWin32 type.** Instead of `int res = (int)PInvoke.RmStartSession(...)` and casting at every comparison, declare `WIN32_ERROR res = PInvoke.RmStartSession(...)` and let helpers like `GetException(WIN32_ERROR res, ...)` take the typed value. Cast to `int`/`uint` only at the boundary where a non-CsWin32 API needs it (e.g. `new Win32Exception((int)res, ...)`). The same applies to `HRESULT`, `BOOL`, `HANDLE`, `PROCESS_CREATION_FLAGS`, etc.

**Delete local mirror enums** that exist solely to mirror the Win32 one. The generated CsWin32 type is the source of truth.

### FILETIME Conversions

Use the helpers in [`src/Framework/Windows/Win32/Foundation/FileTimeExtensions.cs`](../../../src/Framework/Windows/Win32/Foundation/FileTimeExtensions.cs):

- `fileTime.ToLong()` → 64-bit ticks
- `fileTime.ToDateTime()` → local `DateTime` (FILETIME values returned as local time, e.g. `RM_PROCESS_INFO.ProcessStartTime`)
- `fileTime.ToDateTimeUtc()` → UTC `DateTime` (FILETIME values returned as UTC, e.g. `WIN32_FILE_ATTRIBUTE_DATA.ftLastWriteTime`)

Do **not** hand-roll `DateTime.FromFileTime((long)hi << 32 | lo)` — use the helpers for consistency. Note CsWin32-generated structs use `ComTypes.FILETIME` (int fields) for COM members and `Windows.Win32.Foundation.FILETIME` (uint fields) for kernel ones; the extension covers `ComTypes.FILETIME`.

## BufferScope<T>

`BufferScope<T>` (`src/Framework/Utilities/BufferScope.cs`) — stackalloc initial buffer with `ArrayPool<T>` fallback. Lives in Framework, available to all projects via `InternalsVisibleTo`.

```csharp
using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);
int length = (int)PInvoke.GetShortPathName(path, buffer.AsSpan());
if (length > buffer.Length)
{
    buffer.EnsureCapacity(length);
    length = (int)PInvoke.GetShortPathName(path, buffer.AsSpan());
}
if (length > 0) path = buffer.Slice(0, length).ToString();
```

- `ref struct` — always use with `using`. Never stack-allocate more than 1024 bytes.
- Check CsWin32 convenience overloads (e.g. `GetShortPathName(string, Span<char>)`) before writing `fixed` blocks.

## Gotchas

### CA1416 Platform Compatibility

No blanket `NoWarn` — handle semantically:
- `if (IsWindows)` satisfies `[SupportedOSPlatform]` — no pragma needed
- `if (IsUnixLike)` satisfies `[UnsupportedOSPlatform("windows")]`
- **Never use `!IsWindows`** — use `else if (IsUnixLike)`. See `documentation/specs/CA1416-analyzer-analysis.md`
- Use versioned `[SupportedOSPlatform("windows6.1")]` on methods calling CsWin32 APIs
- `#pragma warning disable CA1416` only for **static local functions** (analyzer limitation)
- CS0592 prevents `[SupportedOSPlatform]` on `partial struct` — put on individual members instead

### Type Conversions

- `HANDLE ↔ IntPtr`: `(HANDLE)intPtr` / `(IntPtr)h.Value`. Sentinels: `HANDLE.Null`, `HANDLE.INVALID_HANDLE_VALUE`
- `FILETIME` conversion: `data.ftLastWriteTime.ToLong()`, `.ToDateTime()` (local), `.ToDateTimeUtc()` — see "FILETIME Conversions" above. CsWin32 uses `ComTypes.FILETIME` (int fields), not `Win32.Foundation.FILETIME`
- `SafeFileHandle`: `new SafeFileHandle((IntPtr)h.Value, true)`, pass with `(HANDLE)handle.DangerousGetHandle()`
- Nullable structs: `(SECURITY_ATTRIBUTES?)null`
- Enum flags: use bitwise `&` — `HasFlag()` boxes on .NET Framework
- Anonymous unions: `systemInfo.Anonymous.Anonymous.wProcessorArchitecture` — check generated source in `obj/`

### Source-Build Verification (REQUIRED before pushing)

Source builds (`DotNetBuildSourceOnly=true`) disable `FEATURE_WINDOWSINTEROP`. CI treats **all warnings as errors**. Run both builds before every push:

```shell
# Normal build
dotnet msbuild MSBuild.Dev.slnf -v:q

# Source-build — catches unused usings/members/docs from #if guards
dotnet msbuild MSBuild.SourceBuild.slnf /p:DotNetBuildSourceOnly=true -v:q
```

**Everything** only referenced inside `#if FEATURE_WINDOWSINTEROP` must also be guarded:
- **IDE0005**: `using` directives — most common failure
- **IDE0051/IDE0052**: Private members (methods, fields, including helpers like `StringToByteArray`, constants like `ERROR_SHARING_VIOLATION`)
- **CA1823**: Unused private fields (e.g. constants only consumed inside the guard)
- **CS1587**: XML doc comments (move inside `#if`, not before)

The same applies when adding **polyfills** in `src/Framework/Polyfills/` (e.g. `IComIID`, `SpanExtensions`, `IndexOfAnyExcept`): polyfills usually live behind `#if !NET` (or similar TFM guards) but are still consumed from `#if FEATURE_WINDOWSINTEROP` code paths. Always run the source-build to confirm the polyfill, its callers, and any helper members compile cleanly when interop is disabled — a polyfill referenced only by Windows-only code will trip IDE0051/CA1823 in source-only builds.