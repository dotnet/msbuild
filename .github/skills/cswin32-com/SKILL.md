---
name: cswin32-com
description: 'Guides struct-based COM interop in MSBuild using CsWin32 patterns. Consult when working with ComScope<T>, ComClassFactory, IComIID, IID.Get<T>(), delegate* unmanaged vtables, CoCreateInstance, or manually defining COM interfaces not in Win32 metadata (e.g. WMI IWbemLocator, IWbemServices).'
argument-hint: 'Describe the COM interface or activation pattern you are working with.'
---

# CsWin32 COM Interop Guide

Struct-based COM interop using CsWin32 patterns — AOT-compatible, no `[ComImport]` or built-in marshalling.

## Workflow

1. **Determine if the interface is in Win32 metadata.** If yes, add the name to `src/Framework/NativeMethods.txt` — CsWin32 generates it. If no (e.g. WMI), define a manual struct (see below).
2. **Create a `ComScope<T>`** for lifetime management: `using ComScope<T> scope = new();`
3. **Activate the COM object** via `ComClassFactory.TryCreate(CLSID, ...)` or `PInvoke.CoCreateInstance` with `IID.Get<T>()`.
4. **Call methods** via `scope.Pointer->Method(...)`. Pass `ComScope<T>` directly as `T**` output parameters.
5. **Guard with `#if FEATURE_WINDOWSINTEROP`** (or `&& NET` for manual structs needing `delegate* unmanaged`).

## COM Interfaces in Win32 Metadata

Add the interface name to `src/Framework/NativeMethods.txt` → CsWin32 generates it → use `ComScope<T>`:

```csharp
#if FEATURE_WINDOWSINTEROP
using ComScope<IRunningObjectTable> rot = new();
HRESULT hr = PInvoke.GetRunningObjectTable(0, rot);
if (hr.Failed) return;
rot.Pointer->SomeMethod(...);
#endif
```

## Manual COM Structs (Not in Metadata)

For interfaces not in Win32 metadata (e.g. WMI, Fusion), define struct-based implementations in their own files, excluded via `<Compile Remove>` in source builds. Guard with `#if FEATURE_WINDOWSINTEROP && NET` when the struct uses **only** the static-abstract `IComIID` member (only emitted on .NET 7+). If the struct needs to compile on net472, provide **both** the static-abstract member (gated `#if NET`) and an instance member (gated `#else`) so the right one binds against the per-target `IComIID` shape — see Fusion structs in `src/Tasks/AssemblyDependency/Fusion/` for this pattern.

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
            ReadOnlySpan<byte> data = [ /* 16 GUID bytes */ ];
            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }

    private readonly void** _lpVtbl;

    // IUnknown (vtable 0-2) + interface methods at correct indices
    public HRESULT ConnectServer(char* strNetworkResource, ...) {
        fixed (IWbemLocator* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IWbemLocator*, char*, ..., HRESULT>)_lpVtbl[3])(pThis, ...);
    }

    public static Guid CLSID { get; } = new(0x4590F811, ...);
}
```

**Requirements:**
- `delegate* unmanaged[Stdcall]` — C# 9 / IL `calli`, works on net472 too
- Static-abstract `IComIID` on .NET 7+ (gate manual structs with `#if NET`); the net472 polyfill is instance-based and is **not** attached to CsWin32-generated structs, so `ComScope<T>` over generated COM types is .NET-only
- Exact vtable indices — unused slots can be omitted as long as used method indices are correct
- Use the CsWin32-generated `PCWSTR` / `PWSTR` for wide string parameters (add the type to `NativeMethods.txt`). Use raw `char*` only when no typed equivalent exists.
- CS0592 prevents `[SupportedOSPlatform]` on structs — put on individual methods instead

### Dual-target manual structs (net472 + .NET)

When a manual COM struct must compile on both net472 and .NET, declare both forms of the `IComIID` member in the same struct so the right one binds against the per-target `IComIID` shape (CsWin32-generated static-abstract on .NET, polyfill instance member on net472):

```csharp
internal unsafe struct IAssemblyCache : IComIID
{
    public static readonly Guid IID_IAssemblyCache = new(0xE707DCDE, ...);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IAssemblyCache);
    }
#else
    readonly Guid IComIID.Guid => IID_IAssemblyCache;
#endif

    private readonly void** _lpVtbl;
    // ... IUnknown + interface methods
}
```

See `src/Tasks/AssemblyDependency/Fusion/` for the full pattern.

### Blittable signatures — no marshalling

Manual COM structs and their `[DllImport]`s must be effectively blittable so the runtime does no marshalling work:

- **Return `HRESULT`, not `int`**, from COM vtable methods and `[DllImport]`s that return an HRESULT. `HRESULT` is blittable (single `int` field) and exposes `.Succeeded` / `.Failed` / `.Value` / `.ThrowOnFailure()`. Use `HRESULT.S_OK` instead of `0`; cast `e.HResult` to `(HRESULT)` when wrapping. `AddRef` / `Release` return `uint` (IUnknown contract).
- **Throwing on failure: use `hr.ThrowOnFailure()`** instead of `if (hr.Failed) Marshal.ThrowExceptionForHR(hr)`. It is the idiomatic CsWin32 helper, produces the same exception (with proper IErrorInfo enrichment), and reads cleanly at call sites: `someInterface->SomeMethod(...).ThrowOnFailure();`. Reserve manual `.Failed` checks for cases where you need to handle specific HRESULTs (e.g. `ERROR_INSUFFICIENT_BUFFER`) before throwing.
- **No `out T*` on signatures** — use `T**`. `out` triggers marshaling + a `fixed` round-trip at every call site.
- **No `IntPtr` for opaque/reserved params** — use `void*` and pass `null`. Inside `unsafe` there is no reason to round-trip through `IntPtr.Zero`.
- **Prefer `nint` / `nuint` over `IntPtr` / `UIntPtr`** for native-sized integers — better cast semantics, no boxing surprises.
- **Use `PCWSTR` / `PWSTR` (CsWin32) for wide strings**, never managed `string`. The caller side pins with `fixed (char* p = managedString) ... new PCWSTR(p)` (implicit on most overloads).
- **No managed reference types** (`string`, `StringBuilder`, arrays) in COM vtable signatures.
- **Do not specify `PreserveSig = true` on `[DllImport]`** — it is the default. Only specify `PreserveSig = false` if you want the marshaller to throw on failure HRESULTs (rare; prefer returning `HRESULT` and calling `.ThrowOnFailure()` at the call site). Note `PreserveSig` defaults the opposite way for `[ComImport]` interfaces (where it defaults to `false`), but struct-based COM here uses raw `delegate*` invocations and isn't affected.

## Activation

```csharp
// Via ComClassFactory (AOT-compatible)
if (ComClassFactory.TryCreate(IWbemLocator.CLSID, out var factory, out HRESULT hr))
    using ComScope<IWbemLocator> instance = factory.TryCreateInstance<IWbemLocator>(out hr);

// Via CoCreateInstance — use IID.Get<T>() for the IID
Guid clsid = IWbemLocator.CLSID;
using ComScope<IWbemLocator> locator = new();
hr = PInvoke.CoCreateInstance(&clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.Get<IWbemLocator>(), locator);
```

**Key points:**
- Use `IID.Get<T>()` — do not take `&localGuid`
- Initialize `ComScope<T>` with `new()`. It implicitly converts to `T**` / `void**` output parameters

## IComIID Polyfill (.NET Framework / netstandard2.0)

CsWin32 emits `IComIID` (with static-abstract `Guid`) and attaches it to every generated COM struct **only on .NET 7+**. On older targets:

- The `IComIID` interface itself is missing — provide an instance-based version at `src/Framework/Polyfills/IComIID.cs`.
- Generated COM structs do not have `IComIID` in their base list — provide a partial struct that adds it.

This is a **known case that always requires polyfilling** for .NET Framework support. Pattern in [`src/Framework/Polyfills/IComIIDPolyfills.cs`](../../../src/Framework/Polyfills/IComIIDPolyfills.cs):

```csharp
namespace Windows.Win32.System.Com;

internal partial struct IRunningObjectTable : IComIID
{
    readonly Guid IComIID.Guid => IID_Guid; // CsWin32-emitted field, always present
}
```

When a new CsWin32-generated COM type is used through `ComScope<T>`, add a partial entry to that file. Modeled after [winforms `IDataObject.cs`](https://github.com/dotnet/winforms/blob/main/src/System.Private.Windows.Core/src/Framework/Windows/Win32/System/Com/IDataObject.cs).

Both polyfill files are gated with `#if !NET` so they compile on net472/netstandard2.0 and become empty on .NET (where CsWin32 emits its own static-abstract `IComIID` and attaches it to generated structs).

For manual COM structs (WMI, Setup Configuration, etc.) that already use the static-abstract `IComIID` form, the polyfill approach won't compile on net472 — those structs stay .NET-only via `<Compile Remove>`.

## Lifetime & Access

- `ComScope<T>` — `ref struct`, use with `using`. Releases on dispose. Use for **all local-scope COM pointers**, including transient locals during initialization (don't declare a raw `T* p = null;` to receive an `out` — use `ComScope<T>` and pass it as `T**`).
- `AgileComPointer<T>` — finalizable managed class. Use for COM pointers stored in **managed class fields**.
  - **Never store a raw `T*` in a managed class field.** Raw fields leak ref counts if the owner is GC'd undisposed and are an apartment-agility hazard.
  - Access via `using ComScope<T> scope = agile.GetInterface();`
  - **Pairing with `ComScope`**: when the source is a `ComScope<T>` that already owns the reference, construct with `takeOwnership: false` — the GIT AddRefs on registration and the `ComScope` Releases deterministically on scope exit. Use `takeOwnership: true` only when handing off a raw pointer that has no other owner (rare).
- Access methods via `scope.Pointer->Method(...)`.
- Pass `ComScope<T>` directly as `T**` or `void**` output parameter (implicit conversion).

## File Organization

| Location | Contents |
|----------|----------|
| `src/Framework/Windows/Win32/System/Com/` | `ComScope.cs`, `ComClassFactory.cs`, `AgileComPointer.cs`, `GlobalInterfaceTable.cs` |
| `src/Framework/Windows/Win32/IID.cs` | Generic IID lookup |
| `src/Framework/Utilities/Wmi/` | Manual WMI structs (.NET-only — use static-abstract `IComIID`) |
| `src/Framework/Polyfills/IComIID.cs` | net472/netstandard2.0 instance-based `IComIID` polyfill (`#if !NET`) |
| `src/Framework/Polyfills/IComIIDPolyfills.cs` | Per-struct partials attaching `IComIID` to CsWin32-generated COM types on net472/netstandard2.0 |

## CS3016 CLS Compliance

CsWin32 COM structs trigger CS3016 under `[assembly: CLSCompliant(true)]`. Handled via `[CLSCompliant(false)]` partial declarations in `GeneratedInteropClsCompliance.cs`. CS3019 warnings suppressed in `.editorconfig` for `{**/Windows/**/*.cs}` — do not add per-file suppressions. See https://github.com/dotnet/roslyn/issues/68526.