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

For interfaces not in Win32 metadata (e.g. WMI), define struct-based implementations in their own files, excluded via `<Compile Remove>` in source builds. Guard with `#if FEATURE_WINDOWSINTEROP && NET` because manual structs typically use the static-abstract `IComIID` member (only emitted on .NET 7+) — unlike CsWin32-generated COM types, which can run on net472 via the `IComIID` polyfill (see below).

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
- `char*` with `fixed` for BSTR string parameters
- CS0592 prevents `[SupportedOSPlatform]` on structs — put on individual methods instead

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

- `ComScope<T>` is a `ref struct` — use with `using`. Calls `Release()` on dispose.
- Access methods via `scope.Pointer->Method(...)`.
- Pass `ComScope<T>` directly as `T**` or `void**` output parameter (implicit conversion).

## File Organization

| Location | Contents |
|----------|----------|
| `src/Framework/Windows/Win32/System/Com/` | `ComScope.cs`, `ComClassFactory.cs` |
| `src/Framework/Windows/Win32/IID.cs` | Generic IID lookup |
| `src/Framework/Utilities/Wmi/` | Manual WMI structs (.NET-only — use static-abstract `IComIID`) |
| `src/Framework/Polyfills/IComIID.cs` | net472/netstandard2.0 instance-based `IComIID` polyfill (`#if !NET`) |
| `src/Framework/Polyfills/IComIIDPolyfills.cs` | Per-struct partials attaching `IComIID` to CsWin32-generated COM types on net472/netstandard2.0 |

## CS3016 CLS Compliance

CsWin32 COM structs trigger CS3016 under `[assembly: CLSCompliant(true)]`. Handled via `[CLSCompliant(false)]` partial declarations in `GeneratedInteropClsCompliance.cs`. CS3019 warnings suppressed in `.editorconfig` for `{**/Windows/**/*.cs}` — do not add per-file suppressions. See https://github.com/dotnet/roslyn/issues/68526.