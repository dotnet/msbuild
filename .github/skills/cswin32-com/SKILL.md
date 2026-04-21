---
name: cswin32-com
description: 'Guides struct-based COM interop in MSBuild using CsWin32 patterns. Consult when working with ComScope<T>, ComClassFactory, IComIID, IID.Get<T>(), delegate* unmanaged vtables, CoCreateInstance, or manually defining COM interfaces not in Win32 metadata (e.g. WMI IWbemLocator, IWbemServices).'
argument-hint: 'Describe the COM interface or activation pattern you are working with.'
---

# CsWin32 COM Interop Guide

Struct-based COM interop using CsWin32 patterns — AOT-compatible, no `[ComImport]` or built-in marshalling.

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

For interfaces not in Win32 metadata (e.g. WMI), define struct-based implementations in their own files, excluded via `<Compile Remove>` in source builds. Guard with `#if FEATURE_WINDOWSINTEROP && NET` (needs `delegate* unmanaged`).

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
- `delegate* unmanaged[Stdcall]` — needs .NET 5+
- Exact vtable indices — unused slots can be omitted as long as used method indices are correct
- Dual `IComIID` — static abstract on .NET 7+, instance-based on net472 (polyfill in `src/Framework/Framework/`)
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

## Lifetime & Access

- `ComScope<T>` is a `ref struct` — use with `using`. Calls `Release()` on dispose.
- Access methods via `scope.Pointer->Method(...)`.
- Pass `ComScope<T>` directly as `T**` or `void**` output parameter (implicit conversion).

## File Organization

| Location | Contents |
|----------|----------|
| `src/Framework/Windows/Win32/System/Com/` | `ComScope.cs`, `ComClassFactory.cs` |
| `src/Framework/Windows/Win32/IID.cs` | Generic IID lookup |
| `src/Framework/Utilities/Wmi/` | Manual WMI structs (.NET-only) |
| `src/Framework/Framework/` | net472 `IComIID` polyfill |

## CS3016 CLS Compliance

CsWin32 COM structs trigger CS3016 under `[assembly: CLSCompliant(true)]`. Handled via `[CLSCompliant(false)]` partial declarations in `GeneratedInteropClsCompliance.cs`. CS3019 warnings suppressed in `.editorconfig` for `{**/Windows/**/*.cs}` — do not add per-file suppressions. See https://github.com/dotnet/roslyn/issues/68526.