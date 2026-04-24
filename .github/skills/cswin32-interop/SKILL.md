---
name: cswin32-interop
description: 'Guides CsWin32 P/Invoke interop in MSBuild. Consult when working with the PInvoke class, Windows.Win32 namespaces, FEATURE_WINDOWSINTEROP, HANDLE/HMODULE/HRESULT types, BufferScope<T>, replacing [DllImport] with CsWin32, or conditioning Windows-only code for source builds.'
argument-hint: 'Describe the Windows API or interop code you are migrating or adding.'
---

# CsWin32 Interop Guide

[CsWin32](https://github.com/microsoft/CsWin32) replaces `[DllImport]` with source-generated `PInvoke.*` calls. `FEATURE_WINDOWSINTEROP` is the compile-time gate; source builds disable it.

## Rules

1. **Replace `[DllImport]` with `PInvoke.*`**. Delete old declarations and hand-written structs/enums/constants.
2. **Gate with `#if FEATURE_WINDOWSINTEROP`**, add runtime `IsWindows` check inside. Both required.
3. **Use CsWin32 types directly** (`HANDLE`, `HMODULE`, `HRESULT.S_OK`, `FILE_FLAGS_AND_ATTRIBUTES`, etc.).
4. **Call `PInvoke.*` directly** — no wrappers. Types flow via `InternalsVisibleTo`.
5. **Prefer CsWin32 for Windows APIs**. Use `[LibraryImport]` only for non-Windows native calls (e.g. `libc`), guarded with `#if NET`.

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
| `#if FEATURE_WINDOWSINTEROP && NET` | `delegate* unmanaged`, `ComScope<T>` | Yes |
| `#if FEATURE_WINDOWSINTEROP && !NETSTANDARD` | CsWin32 types without `static abstract` (net472 + net10) | Yes |
| `#if !NET` / `#if FEATURE_MSCOREE` | net472-only = inherently Windows | No |

**Namespace imports** must be inside `#if FEATURE_WINDOWSINTEROP`. WDK APIs use `Windows.Wdk` namespace.

**Files**: `src/Framework/Windows/` (CsWin32 partials), `src/Shared/Win32/` (COM helpers), `src/Framework/Utilities/Wmi/` (.NET-only COM structs), `src/Framework/Framework/` (net472 polyfills).

### Constant Replacements

`NativeMethodsShared.S_OK` → `HRESULT.S_OK`, `InvalidHandle` → `HANDLE.INVALID_HANDLE_VALUE`, `FILE_ATTRIBUTE_DIRECTORY` → `FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY`, `STD_OUTPUT_HANDLE` → `STD_HANDLE.STD_OUTPUT_HANDLE`, `GENERIC_READ` → `FILE_ACCESS_RIGHTS.FILE_GENERIC_READ`. Pattern: `CsWin32EnumType.ORIGINAL_NAME` — check generated types in `obj/`.

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
- `FILETIME → long`: `data.ftLastWriteTime.ToLong()` — CsWin32 uses `ComTypes.FILETIME` (int fields), not `Win32.Foundation.FILETIME`
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
- **IDE0051/IDE0052**: Private members (methods, fields)
- **CS1587**: XML doc comments (move inside `#if`, not before)