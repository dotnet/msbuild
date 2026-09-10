# P/Invoke ABI and platform boundaries

## Generator ownership and discovery

[NativeMethods.json](../../../../src/Framework/NativeMethods.json) configures the Framework-owned CsWin32 generator, including `allowMarshaling: false`, `useSafeHandles: false`, the `PInvoke` class, and COM preserve-signature settings. [NativeMethods.txt](../../../../src/Framework/NativeMethods.txt) selects APIs and supporting types.

Check the [package version](../../../../eng/dependabot/Directory.Packages.props) and current generated API when resolving a signature question. Generation behavior is version-dependent. Output may be available in the IDE or a configured compiler-generated-files directory; do not assume it is always emitted under a literal `obj\generated` path or that cached output matches this checkout.

Shared types reach the assemblies granted access in [Framework AssemblyInfo](../../../../src/Framework/Properties/AssemblyInfo.cs). This is not access for arbitrary task/plugin projects.

Prefer generated Windows declarations and existing convenience partials. For example, [PInvoke.GetFileAttributesEx](../../../../src/Framework/Windows/Win32/PInvoke.GetFileAttributesEx.cs) deliberately supplies a typed overload. Wrappers that own pinning, lifetime, platform support, or a compatibility contract are legitimate; avoid redundant layers, not all wrappers.

For a missing API, determine whether the pinned Win32 metadata can generate it before defining it manually. Prefer source-generated non-Windows imports where supported, preserving the project's TFM guards and existing ABI. Do not replace declarations outside the requested migration.

## Raw signatures

`allowMarshaling: false` controls generated signatures; it does not rewrite every hand-authored import in the repository. For a new raw/AOT-compatible boundary:

- Match native return/parameter widths and calling conventions. HRESULT-returning APIs should use `HRESULT`; Win32 error-code APIs may instead return `WIN32_ERROR`, BOOL, or another contract.
- Keep managed strings, arrays, and `StringBuilder` out of raw blittable signatures. A managed convenience overload can pin or prepare storage around the raw call.
- Use `PCWSTR` for const wide-string input and `PWSTR` for writable buffers where the generated API supplies them. A pinned managed string is not a buffer to modify.
- Prefer `T**` / `void**` for COM outputs used with `ComScope<T>`'s operators. `out T*` is not universally non-blittable; the pointer form fits this repository's scope pattern without an extra adapter.
- Use `void*` with `null` for raw reserved/opaque pointer arguments where that is the declared contract. Keep `IntPtr` at APIs that require it; do not mechanically rewrite established signatures.
- `nint`/`nuint` can express native-sized integers clearly. They do not uniquely avoid boxing compared with `IntPtr`/`UIntPtr`.
- Use typed enums for real option domains. Match signedness and mark only bitmask enums with `[Flags]`.

`DllImport` preserves signatures by default. An old `PreserveSig=false` declaration may have hidden an HRESULT and automatically thrown; raw function pointers do not supply that behavior. Match the native ABI before replacing that declaration.

For manual COM details, use the [manual interface reference](../../cswin32-com/references/manual-interfaces.md), not a guessed managed projection.

## Preserve error behavior

| Native/old contract | Migration consideration |
|---|---|
| Failing HRESULT previously propagated as an exception | Preserve the throw boundary, typically with `ThrowOnFailure` |
| Preserve-signature HRESULT inspected by the caller | Keep success/failure and special-code branches; do not introduce unconditional throws |
| `SetLastError=true` with a BOOL result | The runtime caches the last error; inspect the old caller to decide return, retry, fallback, or throw |
| Win32 error code as the return value | Compare the typed returned code, not an unrelated last-error value |
| Optional lookup returning absence | Preserve legitimate absence without swallowing unrelated failures |

Retrieve a last-error value at the appropriate failing call before unrelated native calls can change it. Runtime details differ between .NET and .NET Framework; see the [SetLastError API contract](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportattribute.setlasterror).

Removing a caught exception can change which later operations execute. Trace callers and cleanup, not just the final return value. [COM lifetime/error guidance](../../cswin32-com/references/lifetime-and-activation.md) includes the different setup-discovery and metadata-reader contracts.

## Compilation and runtime support

[Directory.BeforeCommon.targets](../../../../src/Directory.BeforeCommon.targets) defines `FEATURE_WINDOWSINTEROP` / `FeatureWindowsInterop` for non-source-only builds. This is not a runtime Windows test: a non-source build can execute on another OS.

The [Framework project](../../../../src/Framework/Microsoft.Build.Framework.csproj) excludes Windows files when interop is disabled and restricts its WMI implementation to the appropriate runtime target. Check the equivalent exclusions in another owning project.

Choose the boundary:

| Situation | Pattern |
|---|---|
| Mixed-platform implementation | Guard generated-type references for compilation and establish Windows support before native calls |
| Whole Windows-only file | Use the existing project exclusion and platform annotation/guard strategy |
| Shared raw helper called only through a supported boundary | Propagate the platform contract; do not require a redundant runtime test in every helper |
| Source-only or unsupported runtime path | Preserve the intended fallback or explicit unsupported-operation behavior |

`SupportedOSPlatformAttribute` is valid on structs as well as methods. Both the [repository polyfill](../../../../src/Framework/Polyfills/SupportedOSPlatformAttribute.cs) and [IWbemLocator](../../../../src/Framework/Utilities/Wmi/IWbemLocator.cs) demonstrate this.

Use the minimum OS version actually required by the API, not a copied Windows-version annotation. [NativeMethods](../../../../src/Framework/NativeMethods.cs) annotates `IsWindows` with a versioned Windows guard and `IsUnixLike` as excluding Windows. Consequently, the false branch of that versioned `IsWindows` guard does not prove to the analyzer that *every* Windows version is excluded. Use `IsUnixLike` when that is the needed proof; this is not a ban on `!IsWindows` in unrelated logic.

Avoid blanket analyzer suppression. Check the semantic guard, annotation, and generated signature first; a narrowly justified analyzer limitation is different from ignoring an unsupported call.

## Source-only fallout and evidence

Removing interop references can expose unused imports, helpers, fields, or XML documentation. Keep code and its `using`/documentation boundaries coherent; inspect the actual diagnostics rather than guarding every nearby member automatically.

Select the affected normal/source-only and runtime targets from current project configuration. If compilation is warranted, use the [repository build guidance](../../../../AGENTS.md) and established setup; use [running-unit-tests](../../running-unit-tests/SKILL.md) for selected tests. Do not impose an unconditional full-solution checklist. Source compilation does not prove native ABI, apartment, or runtime error behavior.
