---
name: cswin32-interop
description: 'Guides CsWin32-based Windows interop migration in MSBuild. Consult when adding or modifying P/Invoke declarations, replacing [DllImport] or [ComImport] with CsWin32, working with the FEATURE_WINDOWSINTEROP define, conditioning Windows-only code for source builds, writing COM interop using struct-based patterns, or reviewing Windows interop changes.'
argument-hint: 'Describe the Windows API or interop code you are migrating or adding.'
---

# CsWin32 Interop Conversion Guide

Convert Win32 interop in this repository to [CsWin32](https://github.com/microsoft/CsWin32) with `FEATURE_WINDOWSINTEROP`. No fallback `[DllImport]` declarations — source builds exclude all Windows interop.

## Core Rules

1. **Replace `[DllImport]` with CsWin32 `PInvoke.*`**. Replace hand-written structs/enums with CsWin32-generated types. Delete old declarations entirely.
2. **Always preserve runtime checks** (`NativeMethodsShared.IsWindows` / `RuntimeInformation.IsOSPlatform`). `FEATURE_WINDOWSINTEROP` is the compile-time gate; runtime checks are the runtime gate. Both are required because .NET builds produce shared assemblies that run on all platforms.
3. **Gate all Windows code behind `#if FEATURE_WINDOWSINTEROP`**. Prefer this over `RUNTIME_TYPE_NETCORE`, `MONO`, or ad-hoc defines. Disabled in source builds = no Windows interop.
4. **Use CsWin32 types directly** (`HANDLE`, `HMODULE`, `MEMORYSTATUSEX`, etc.). Don't re-define types CsWin32 generates. Remove hand-written constants replaced by CsWin32 typed enums.
5. **Call `PInvoke.*` directly** from any project (Build, Tasks, Utilities, tests) via `InternalsVisibleTo`. Only wrap when the signature needs complex adaptation (multiple `fixed` blocks, struct translation). **Do not create or preserve abstraction layers** — callers should use CsWin32 types and `PInvoke.*` calls directly instead of going through `NativeMethods.*` wrappers.

### Dual Guard Pattern

Every Windows interop call in shared code needs **both** guards. The `#if FEATURE_WINDOWSINTEROP` should wrap the runtime `IsWindows` check — when the feature is disabled (source builds), the runtime check is a no-op with no Windows code to run, so wrapping it eliminates dead code:

```csharp
// CORRECT: #if wraps the runtime check; PInvoke.* called directly (no wrapper)
internal static bool TryGetLastWriteTimeUtc(string fullPath, out DateTime utc)
{
#if FEATURE_WINDOWSINTEROP
    if (IsWindows)
    {
        if (PInvoke.GetFileAttributesEx(fullPath, out WIN32_FILE_ATTRIBUTE_DATA data))
        {
            utc = DateTime.FromFileTimeUtc(data.ftLastWriteTime.ToLong());
            return true;
        }
        utc = DateTime.MinValue;
        return false;
    }
#endif
    utc = File.Exists(fullPath) ? File.GetLastWriteTimeUtc(fullPath) : DateTime.MinValue;
    return utc != DateTime.MinValue;
}

// WRONG: runtime check wrapping #if — leaves dead IsWindows check in source builds
if (IsWindows)       // ← pointless when FEATURE_WINDOWSINTEROP is disabled
{
#if FEATURE_WINDOWSINTEROP
    ...
#endif
}

// WRONG: #if/#else without runtime check — breaks on non-Windows
```

Purely Windows-only files (no cross-platform fallback, e.g. `WindowsFileSystem.cs`) don't need `#if FEATURE_WINDOWSINTEROP` inside — they're excluded at the project level via `<Compile Remove>` when the feature is off. Inside such a file, `PInvoke.*` is called directly; callers still guard with `if (IsWindows)` at the cross-platform boundary.

## Infrastructure

### Define and MSBuild Property

Defined in `src/Directory.BeforeCommon.targets`:

```xml
<PropertyGroup Condition="'$(DotNetBuildSourceOnly)' != 'true'">
  <DefineConstants>$(DefineConstants);FEATURE_WINDOWSINTEROP</DefineConstants>
  <FeatureWindowsInterop>true</FeatureWindowsInterop>
</PropertyGroup>
```

- **Currently active on all platforms** in non-source builds (shared assemblies). Only disabled in source-only builds.
- **`$(FeatureWindowsInterop)`**: Use in `.csproj` for `<Compile Remove>`/`<Compile Include>` conditions.

### Compile-Time Guard Selection

| Guard | When to use | Runtime `IsWindows` check? |
|-------|-------------|----------------------------|
| `#if FEATURE_WINDOWSINTEROP` | Windows calls in code shared across TFMs (net472 + netstandard + net10.0) | Yes — shared assemblies run cross-platform |
| `#if FEATURE_WINDOWSINTEROP && NET` | `delegate* unmanaged` vtables, `ComScope<T>`, `static abstract IComIID` — .NET 7+ only | Yes |
| `#if FEATURE_WINDOWSINTEROP && !NETSTANDARD` | Helpers using CsWin32 types but not `static abstract` (net472 + net10.0) | Yes |
| `#if !NET` / `#if NETFRAMEWORK` / `#if FEATURE_MSCOREE` / `#if FEATURE_WIN32_REGISTRY` / `#if !FEATURE_ASSEMBLYLOADCONTEXT` | net472-only = inherently Windows | No |

**Key insight:** The only non-.NET-Core TFM is net472, which only runs on Windows. Any block gated on a net472-only condition (`#if !NET`, `#if NETFRAMEWORK`, or any `FEATURE_*` defined only for net472) is inherently Windows-only — use CsWin32 types directly with no `FEATURE_WINDOWSINTEROP` guard and no runtime `IsWindows` check.

### CsWin32 Configuration

CsWin32 is configured in `Microsoft.Build.Framework` — other projects consume types via `InternalsVisibleTo`.

**`src/Framework/NativeMethods.txt`** — APIs to generate. Add entries when migrating new P/Invoke calls.

**`src/Framework/NativeMethods.json`**:
```json
{
  "$schema": "https://aka.ms/CsWin32.schema.json",
  "allowMarshaling": false,
  "useSafeHandles": false,
  "className": "PInvoke",
  "comInterop": { "preserveSigMethods": ["*"] }
}
```

- `allowMarshaling: false` → raw pointer signatures (AOT-safe)
- `useSafeHandles: false` → `HANDLE`/`HMODULE` structs
- `preserveSigMethods: ["*"]` → COM methods return `HRESULT` directly

### Namespace Imports

Import inside `#if FEATURE_WINDOWSINTEROP`. During incremental migration where old hand-written types coexist with CsWin32 types, use individual type aliases for `FileSystem` to avoid shadowing. Once migration is complete and old types are deleted, full namespace imports (`using Windows.Win32.Storage.FileSystem;`) are preferred:

```csharp
#if FEATURE_WINDOWSINTEROP
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;
using Wdk = Windows.Wdk;
#endif
```

WDK (Nt*) APIs use `Windows.Wdk` namespace — add the API name to `NativeMethods.txt` and CsWin32 resolves it automatically.

### File Organization

- **`src/Framework/Windows/`** — CsWin32 partial/extension types (e.g., `HRESULT` cast, `BSTR` disposable, `FILETIME.ToLong()`, `VARIANT.Dispose()`, `PInvoke.*` safe overloads). Mirrors CsWin32 namespace hierarchy.
- **`src/Shared/Win32/`** — Shared Win32 helpers (`IID.cs`, `ComScope.cs`, `ComClassFactory.cs`), linked into Framework via `<Compile Include>`.
- **`src/Framework/Utilities/Wmi/`** — Manually defined COM structs for interfaces not in Win32 metadata (e.g., WMI). `.NET`-only, excluded on net472/netstandard.
- **`src/Framework/Framework/`** — net472-only polyfills (e.g., instance-based `IComIID`), excluded from other TFMs via `DefaultItemExcludes`.

### Constant Replacements

Replace hand-written constants with CsWin32 typed enums. The pattern is `CsWin32EnumType.ORIGINAL_CONSTANT_NAME`:

| Old | CsWin32 |
|-----|---------|
| `NativeMethodsShared.S_OK` | `HRESULT.S_OK` |
| `NativeMethodsShared.S_FALSE` | `HRESULT.S_FALSE` |
| `InvalidHandle` / `NullIntPtr` | `HANDLE.INVALID_HANDLE_VALUE` / `HANDLE.Null` |
| `ERROR_INSUFFICIENT_BUFFER` | `WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER` |
| `FILE_ATTRIBUTE_DIRECTORY` | `FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY` |
| `STD_OUTPUT_HANDLE` | `STD_HANDLE.STD_OUTPUT_HANDLE` |
| `GENERIC_READ` | `FILE_ACCESS_RIGHTS.FILE_GENERIC_READ` |
| `NORMAL_PRIORITY_CLASS` | `PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS` |
| `RPC_C_AUTHN_LEVEL_*` / `RPC_C_IMP_LEVEL_*` | `RPC_C_AUTHN_LEVEL.*` / `RPC_C_IMP_LEVEL.*` |

Other constants follow the same pattern — check the CsWin32-generated enum type in `obj/` for the exact name.

## Conversion Patterns

### Replace `[DllImport]` with `PInvoke.*`

```csharp
// BEFORE: hand-written DllImport
[DllImport("kernel32.dll", SetLastError = true)]
internal static extern bool CloseHandle(IntPtr hObject);

// AFTER: DllImport deleted. Callers use PInvoke directly:
#if FEATURE_WINDOWSINTEROP
if (NativeMethodsShared.IsWindows)
{
    PInvoke.CloseHandle((HANDLE)handle);
}
#endif
```

If a `PInvoke.*` call is 1-2 lines, don't wrap it. Remove redundant convenience methods that just forwarded to a DllImport.

### Eliminate Wrapper Abstractions

During migration, **actively remove wrapper methods and intermediate abstractions**. Callers should use `PInvoke.*` and CsWin32 types directly:

```csharp
// WRONG: preserving a wrapper that just delegates
internal static bool CloseHandle(IntPtr h) => PInvoke.CloseHandle((HANDLE)h);
// Caller: NativeMethods.CloseHandle(handle);

// CORRECT: caller uses PInvoke directly
PInvoke.CloseHandle((HANDLE)handle);
```

Delete any `NativeMethods.*` wrapper that simply forwards to a single `PInvoke.*` call. Replace hand-written constants with CsWin32 typed enums at the call site.

For code inside `#if !NET` / `#if FEATURE_MSCOREE` or other net472-only blocks, use CsWin32 types directly without any additional compile-time guard:

```csharp
#if FEATURE_MSCOREE
    // net472-only = inherently Windows. Use CsWin32 directly, no FEATURE_WINDOWSINTEROP needed.
    HMODULE lib = PInvoke.LoadLibrary(path);
    if (!lib.IsNull)
    {
        try { /* use lib */ }
        finally { PInvoke.FreeLibrary(lib); }
    }
#endif
```

### Use CsWin32 Convenience Overloads

Check CsWin32-generated code (in `obj/`) before writing manual `fixed` blocks:

| API | Convenience overload |
|-----|---------------------|
| `GetFileAttributesEx` | `(string, out WIN32_FILE_ATTRIBUTE_DATA)` |
| `GetShortPathName` | `(string, Span<char>)` |
| `GetModuleFileName` | `(HMODULE, Span<char>)` |
| `GetConsoleMode` | `(HANDLE, out CONSOLE_MODE)` |
| `GetSystemInfo` | `(out SYSTEM_INFO)` |

### Buffer Allocation with `BufferScope<T>`

Use `BufferScope<T>` (defined in `src/Framework/Utilities/BufferScope.cs`, namespace `Microsoft.Build.Utilities`) for renting buffers for interop. It optionally takes a stack-allocated initial buffer with automatic `ArrayPool<T>` fallback for larger results, avoiding heap allocations in the common case. Despite the namespace, it lives in the **Framework** project and is available to all projects via `InternalsVisibleTo`.

```csharp
// Start with a MAX_PATH stack buffer; rent from ArrayPool only if the API needs more.
using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);
int length = (int)PInvoke.GetShortPathName(path, buffer.AsSpan());

if (length > buffer.Length)
{
    buffer.EnsureCapacity(length);
    length = (int)PInvoke.GetShortPathName(path, buffer.AsSpan());
}

if (length > 0)
{
    path = buffer.Slice(0, length).ToString();
}
```

**Key points:**
- Never stack-allocate more than 1024 bytes.
- `BufferScope<T>` is a `ref struct` — always use with `using`.
- Pass just `stackalloc` for a fixed-size initial buffer, or just an `int` minimum length when the required size is known.
- Call `EnsureCapacity()` when the API indicates a larger buffer is needed, then retry.
- Prefer `BufferScope<T>` over `new char[length]` or manual `ArrayPool<T>.Shared.Rent()` calls.

### Exclude Windows-Only Files at Project Level

Use `<Compile Remove>` instead of wrapping entire files in `#if`/`#endif` (avoids IDE0005, CS1587):

```xml
<ItemGroup Condition="'$(FeatureWindowsInterop)' != 'true'">
  <Compile Remove="Windows\**\*.cs" />
  <Compile Remove="FileSystem\WindowsFileSystem.cs" />
</ItemGroup>

<!-- Files requiring .NETCoreApp -->
<ItemGroup Condition="'$(FeatureWindowsInterop)' != 'true' OR '$(TargetFrameworkIdentifier)' != '.NETCoreApp'">
  <Compile Remove="Utilities\Wmi\*.cs" />
</ItemGroup>

<!-- Shared files via conditional Include -->
<ItemGroup Condition="'$(FeatureWindowsInterop)' == 'true' AND '$(TargetFrameworkIdentifier)' == '.NETCoreApp'">
  <Compile Include="..\Shared\Win32\ComScope.cs" Link="Shared\Win32\ComScope.cs" />
</ItemGroup>
```

## COM Interop

### COM Interfaces in Win32 Metadata

Add the interface name to `NativeMethods.txt` → CsWin32 generates it → use `ComScope<T>`:

```csharp
#if FEATURE_WINDOWSINTEROP
using ComScope<IRunningObjectTable> rot = new();
HRESULT hr = PInvoke.GetRunningObjectTable(0, rot);
if (hr.Failed) return;
// rot.Pointer->...
#endif
```

### Manual COM Structs (Not in Metadata)

For COM interfaces not in Win32 metadata (e.g., WMI), define struct-based implementations. Place each interface in its own file, excluded via `<Compile Remove>` on non-.NET / source-only / non-Windows. Annotate with versioned `[SupportedOSPlatform("windows6.1")]`.

```csharp
[SupportedOSPlatform("windows6.1")]
internal unsafe struct IWbemLocator : IComIID
{
    public static Guid Guid { get; } = new(0xDC12A687, ...);

    // .NET 7+ static abstract IComIID implementation
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data = [ /* GUID bytes */ ];
            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }

    private readonly void** _lpVtbl;

    // IUnknown (vtable 0-2) + interface methods at correct indices
    public HRESULT QueryInterface(Guid* riid, void** ppvObject) { ... }
    public uint AddRef() { ... }
    public uint Release() { ... }
    public HRESULT ConnectServer(char* strNetworkResource, ...) { ... }

    public static Guid CLSID { get; } = new(0x4590F811, ...);
}
```

Requirements: `delegate* unmanaged[Stdcall]` (needs .NET 5+), exact vtable indices, dual `IComIID` implementation, `char*` with `fixed` for BSTR parameters. Only methods actually called need full typed signatures — unused vtable slots can be omitted as long as the indices of defined methods are correct.

### COM Activation

```csharp
// CsWin32 COM types → ComClassFactory (AOT-compatible)
if (ComClassFactory.TryCreate(MyInterface.CLSID, out var factory, out HRESULT hr))
{
    using ComScope<MyInterface> instance = factory.TryCreateInstance<MyInterface>(out hr);
}

// Direct CoCreateInstance — use IID.Get<T>() for the interface GUID
Guid clsid = IWbemLocator.CLSID;
using ComScope<IWbemLocator> locator = new();
hr = PInvoke.CoCreateInstance(&clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.Get<IWbemLocator>(), locator);
```

**Key points:**
- Use `IID.Get<T>()` to obtain the IID — do not create a local `Guid` variable and take its address with `&iid`.
- Initialize `ComScope<T>` with `new()`. Pass it directly to COM creation methods — it implicitly converts to the required output pointer (`T**` / `void**`).

### Accessing COM Object Members

Use `ComScope<T>.Pointer` to invoke interface methods. Pass a `ComScope<T>` directly where a `T**` output parameter is expected:

```csharp
// Call methods via Pointer
hr = locator.Pointer->ConnectServer(networkResource, ...args..., services);

// Pass ComScope as output parameter (implicit T** conversion)
using ComScope<IEnumWbemClassObject> enumerator = new();
hr = services.Pointer->ExecQuery(queryLanguage, queryStr, flags, pCtx: null, enumerator);
```

## Gotchas

### CS3016 CLS Compliance

CsWin32-generated COM structs trigger CS3016 under `[assembly: CLSCompliant(true)]`. **CS3016 is handled semantically** via `[CLSCompliant(false)]` partial declarations in `src/Framework/Windows/Win32/GeneratedInteropClsCompliance.cs`. The resulting CS3019 warnings ("CLSCompliant attribute doesn't make sense on internal types") are suppressed in `.editorconfig` for `{**/Windows/**/*.cs}` — do not add per-file suppressions. See https://github.com/dotnet/roslyn/issues/68526 for background.

### CA1416 Platform Compatibility

CsWin32-generated APIs carry versioned `[SupportedOSPlatform]` attributes (e.g., `windows5.1.2600`). **No blanket `NoWarn` for CA1416** — handle it semantically:

1. **`NativeMethods.IsWindows`** has `[SupportedOSPlatformGuard("windows6.1")]` — code inside `if (IsWindows)` blocks needs no pragma.
2. **`NativeMethods.IsUnixLike`** has `[UnsupportedOSPlatformGuard("windows")]` — code inside `if (IsUnixLike)` blocks can call `[UnsupportedOSPlatform("windows")]` APIs without pragma.
3. **Use versioned `[SupportedOSPlatform("windows6.1")]`** on methods/classes that call CsWin32 APIs. Match the API's documented Minimum Supported Client; fall back to `windows6.1` when unknown.
4. **`#pragma warning disable CA1416`** is only needed for **static local functions** (they don't inherit the enclosing method's platform attribute — analyzer limitation).
5. **Never use `!IsWindows`** — it means "not-windows6.1+" which doesn't satisfy `[UnsupportedOSPlatform("windows")]`. Use `else if (IsUnixLike)` instead. See `documentation/specs/CA1416-analyzer-analysis.md`.
6. **`[SupportedOSPlatform]` on structs** — CS0592 prevents placing it on `partial struct` declarations. Put it on the struct's individual methods/properties instead.

```csharp
// CORRECT: versioned guard satisfies CsWin32 APIs — no pragma needed
#if FEATURE_WINDOWSINTEROP
if (NativeMethodsShared.IsWindows)
{
    PInvoke.GetConsoleMode(PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE), out CONSOLE_MODE mode);
}
#endif

// CORRECT: positive checks on both sides
return NativeMethodsShared.IsWindows
    ? StartProcessWindows(...)
    : NativeMethodsShared.IsUnixLike
        ? StartProcessUnix(...)
        : throw new PlatformNotSupportedException();

// WRONG: !IsWindows negation breaks versioned guard analysis
return NativeMethodsShared.IsWindows
    ? StartProcessWindows(...)
    : StartProcessUnix(...);  // ← CA1416: "not-windows6.1+" ≠ "not-windows"
```

### FILETIME

CsWin32 uses `System.Runtime.InteropServices.ComTypes.FILETIME` (int fields), not `Windows.Win32.Foundation.FILETIME`. Convert to `long` via the `ToLong()` extension in `src/Framework/Windows/Win32/Foundation/FileTimeExtensions.cs`:

```csharp
DateTime.FromFileTimeUtc(data.ftLastWriteTime.ToLong());
```

### Type Conversions

- **Nullable structs**: `PInvoke.CreateFile(..., (SECURITY_ATTRIBUTES?)null, ...)`
- **`BOOL` vs `bool`**: CsWin32 returns `BOOL` struct; implicit conversion usually works
- **`HANDLE` ↔ `IntPtr`**: `(HANDLE)intPtr` / `(IntPtr)handle.Value`. Sentinels: `HANDLE.Null`, `HANDLE.INVALID_HANDLE_VALUE`
- **Enum casting**: `(STD_HANDLE)nStdHandle`. Prefer `.HasFlag()` over bitwise-AND
- **Anonymous unions**: Access via `systemInfo.Anonymous.Anonymous.wProcessorArchitecture` — check generated source in `obj/`
- **`SafeFileHandle`**: Create with `new SafeFileHandle((IntPtr)h.Value, true)`. Pass with `(HANDLE)handle.DangerousGetHandle()`

### Build Warnings

- **IDE0005**: Guard `using` directives with `#if FEATURE_WINDOWSINTEROP` (or matching compound guard)
- **CS1587**: Move XML doc comments **inside** `#if` blocks, not before them
- **DefaultItemExcludes**: For net472-only polyfill folders, exclude from other TFMs:
  ```xml
  <PropertyGroup Condition="'$(TargetFramework)' != '$(FullFrameworkTFM)'">
    <DefaultItemExcludes>$(DefaultItemExcludes);**/Framework/**/*</DefaultItemExcludes>
  </PropertyGroup>
  ```

## `LibraryImport`

**Prefer CsWin32 for Windows APIs** — works on all TFMs. Use `[LibraryImport]` only for non-Windows native calls (e.g., `libc`), guarded with `#if NET` (not available on net472/netstandard2.0):

```csharp
#if NET
[LibraryImport("libc", SetLastError = true)]
private static partial int sysctl(ReadOnlySpan<int> name, uint namelen, ...);
#endif
```

Key differences from `[DllImport]`: `static partial` (not `extern`), `StringMarshalling` (not `CharSet`), requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.

## Project Configuration

### `Microsoft.Build.Framework.csproj`

```xml
<PackageReference Include="Microsoft.Windows.CsWin32"
                  Condition="'$(FeatureWindowsInterop)' == 'true'">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

No `<NoWarn>` for CS3016 or CA1416 — both are handled semantically (see **Gotchas** section).

Other projects: **do not** add their own CsWin32 reference — types flow via `InternalsVisibleTo`. PolySharp is used for net472 polyfills — see `Microsoft.Build.Framework.csproj` for current configuration.

Package versions are managed in `Directory.Packages.props`.

## Decision Flowchart

```
Is it a Windows API?
├── YES → In Win32/WDK metadata?
│   ├── YES → Add to NativeMethods.txt, use PInvoke.* directly at call site
│   │   └── What compile-time context?
│   │       ├── #if !NET / FEATURE_MSCOREE / net472-only → Use directly (inherently Windows)
│   │       ├── #if FEATURE_WINDOWSINTEROP (multi-TFM) → Add runtime IsWindows check inside
│   │       └── Unconditional code → Add #if FEATURE_WINDOWSINTEROP wrapping IsWindows check
│   └── NO → COM interface?
│       ├── YES → Struct with delegate* unmanaged vtable
│       │   └── Guard with #if FEATURE_WINDOWSINTEROP && NET
│       └── NO → [LibraryImport] inside #if NET
└── NO (cross-platform) → [LibraryImport] inside #if NET
```
