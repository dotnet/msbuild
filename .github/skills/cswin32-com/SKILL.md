---
name: cswin32-com
description: 'Guides struct-based COM interop in MSBuild using CsWin32 patterns. Consult when working with ComScope<T>, ComClassFactory, IComIID, IID.Get<T>(), delegate* unmanaged vtables, CoCreateInstance, or manually defining COM interfaces not in Win32 metadata (e.g. WMI IWbemLocator, IWbemServices).'
argument-hint: 'Describe the COM interface or activation pattern you are working with.'
---

# CsWin32 COM Interop Guide

Struct-based COM interop using CsWin32 patterns — AOT-compatible, no `[ComImport]` or built-in marshalling.

**Paired skill:** [cswin32-interop](../cswin32-interop/SKILL.md) covers general P/Invoke (`[DllImport]` migration, `FEATURE_WINDOWSINTEROP` gating, blittable signature rules that apply to both `[DllImport]` and COM vtables, `BufferScope<T>`, source-build verification). This file covers only the COM-specific layer on top.

## Workflow

1. **Determine if the interface is in Win32 metadata.** If yes, add the name to `src/Framework/NativeMethods.txt` — CsWin32 generates it. If no (e.g. WMI), define a manual struct (see below).
2. **Create a `ComScope<T>`** for lifetime management: `using ComScope<T> scope = new();`
3. **Activate the COM object** via `ComClassFactory.TryCreate(CLSID, ...)` or `PInvoke.CoCreateInstance` with `IID.Get<T>()`.
4. **Call methods** via `scope.Pointer->Method(...)`. Pass `ComScope<T>` directly as `T**` output parameters.
5. **Guard with `#if FEATURE_WINDOWSINTEROP`** — add `&& NET` only when the struct uses
   the static-abstract `IComIID` form *exclusively*. The dual-target pattern below works
   on net472 without `&& NET`.

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
- `delegate* unmanaged[Stdcall]` for the function-pointer cast
- Static-abstract `IComIID` on .NET 7+ (gate manual structs with `#if NET`); the net472 polyfill is instance-based and is **not** attached to CsWin32-generated structs, so `ComScope<T>` over generated COM types is .NET-only
- Use the CsWin32-generated `PCWSTR` / `PWSTR` for wide string parameters (add the type to `NativeMethods.txt`); raw `char*` only when no typed equivalent exists
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

### Blittable vtable signatures

COM vtable methods must be blittable for the same reason `[DllImport]` signatures must be —
the runtime does no marshalling work. **Follow the general blittable rules in
[cswin32-interop](../cswin32-interop/SKILL.md#blittable-signatures)** (return `HRESULT`,
`.ThrowOnFailure()`, `T**` not `out T*`, `void*` for opaque, `PCWSTR` / `PWSTR` for wide
strings, typed flag enums, no managed reference types). COM-vtable-specific additions:

- **Function-pointer cast must match the native calling convention** — for COM vtables that
  is almost always `delegate* unmanaged[Stdcall]` (the IDL `STDMETHODCALLTYPE` macro expands to
  `__stdcall` on Win32). The general form is `delegate* unmanaged[Cc]` where `Cc` matches the
  native side: `Cdecl` for varargs / printf-style APIs, `Thiscall` for C++ instance method
  pointers, `Fastcall` for rare classic 32-bit APIs. Picking the wrong convention silently
  corrupts the stack on the call. The compiler emits an IL `calli` instruction either way and
  works on net472.
- **`[ComImport]` `PreserveSig` defaults the opposite way from `[DllImport]`** — `false` vs
  `true`. When migrating a `[ComImport]` interface, every method that lacked an explicit
  `[PreserveSig]` was previously throwing on failure HRESULTs; the new struct-based method
  must call `.ThrowOnFailure()` at every call site to preserve that contract. See
  "Error-Handling Parity When Migrating" below.
- **Vtable indices are exact** — unused slots may be omitted as long as the indices of the
  ones you expose are correct relative to the native interface layout (count from 3 after
  `QueryInterface` / `AddRef` / `Release`, and add the parent interface's method count when
  inheriting).

### Strongly-typed handle / token wrappers

When the native side `typedef`s a primitive into a family of "same shape, different meaning"
aliases — e.g. `corhdr.h`'s `typedef ULONG32 mdToken; typedef mdToken mdAssembly; typedef mdToken mdAssemblyRef;` —
mirror that hierarchy with distinct `readonly struct` wrappers (same pattern CsWin32 uses for
`HANDLE` / `HWND` / `HMODULE`). Each wrapper holds a single field of the underlying primitive
so it stays blittable and ABI-compatible with the native type; the `delegate*` cast and any
array (`MdAssemblyRef[]`) marshal at zero cost.

Conversion follows the typedef hierarchy:

- **Implicit** widening from a specific type to the generic base (`MdAssembly` → `MdToken`).
  Always safe — every `mdAssembly` is an `mdToken` at the C level — and lets specific tokens
  flow naturally into APIs that accept the generic base (e.g. `GetCustomAttributeByName`).
- **Explicit** narrowing from the base to a specific type (`(MdAssembly)token`). The C side
  can't enforce that the value really is the claimed kind — the runtime validates on use, not
  at the cast site — so the cast must be opt-in.

**Check the native header for the canonical validation primitives** before defining `IsNil` /
`IsValid` etc. on the wrapper. Encoding details often surface only in macros that don't show
up on a casual grep. For `mdToken` the encoding is `(TableType << 24) | Rid` with these
helpers in `corhdr.h`:

| C macro / constant | What it really means |
|---|---|
| `TypeFromToken(tk) = tk & 0xff000000` | Table-type tag (high byte) → `CorTokenType` enum |
| `RidFromToken(tk) = tk & 0x00ffffff`  | Row id (low 24 bits) |
| `IsNilToken(tk) = RidFromToken(tk) == 0` | Nil check — **row id half, not the whole value** |
| `mdAssemblyNil = mdtAssembly = 0x20000000` | Per-type nil is the table-type tag, **not 0** |

A naive `IsNil => Value == 0` would silently misclassify a valid "no assembly in this scope"
return from `GetAssemblyFromScope` (which writes `0x20000000`) as a non-nil token. Mirror the
macro: `IsNil => Rid == 0`.

Example: [`src/Tasks/AssemblyDependency/Metadata/Tokens.cs`](../../../src/Tasks/AssemblyDependency/Metadata/Tokens.cs)
defines `MdToken`, `MdAssembly`, `MdAssemblyRef`, `MdFile` with the implicit/explicit
conversions and exposes `Kind` (`CorTokenType`), `Rid`, `IsNil`, and `IsValid` on each.
The `CorTokenType` enum itself lives in
[`CorTokenType.cs`](../../../src/Tasks/AssemblyDependency/Metadata/CorTokenType.cs).

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

## Error-Handling Parity When Migrating

Struct-based COM returns raw `HRESULT`; `[ComImport]` and built-in activation threw automatically. Preserving the old throw-vs-return behavior is part of the migration.

| Old shape | Threw on failure via | Migrated shape |
|---|---|---|
| `new SomeCoClass()` | built-in interop activation | `PInvoke.CoCreateInstance(...).ThrowOnFailure()` |
| `(IFoo)rcw` cast | `InvalidCastException` on QI failure | `QueryInterface(&iid, scope).ThrowOnFailure()` |
| `[ComImport]` method (default `PreserveSig=false`) | marshaller throws on `FAILED(hr)` | `.ThrowOnFailure()` at the call site |
| `[ComImport]` method with `[PreserveSig]` | caller inspects return | mirror the existing `hr` branch — don't start throwing |

Factory-method exception: when the old code's contract was "return null for invalid input" (e.g. `Create(path)`), keep null-return only for the operation that legitimately rejects input; environment-level failures (`CoCreateInstance`, QI for guaranteed-implemented interfaces) still throw. Example: [`MetadataReader.cs`](../../../src/Tasks/ManifestUtil/MetadataReader.cs) throws on activation and QI, returns null on `OpenScope` failure.

## IComIID Polyfill for net472

CsWin32 emits `IComIID` (with static-abstract `Guid`) and attaches it to every generated COM struct **only on .NET 7+**. On older targets (net472, netstandard2.0):

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

**A raw `T*` (where `T` is a COM struct) must never appear as a field of a non-`ref` type.** Allowed locations for a raw `T*`:

- locals inside an `unsafe` method,
- parameters,
- fields of a `ref struct` (whose lifetime is statically bounded to a stack frame).

Anywhere else — instance fields of a `class` or non-`ref` `struct`, including `internal`/`private` ones — use `AgileComPointer<T>`. A raw pointer field in a managed object is an apartment-agility hazard (the field can be observed from any thread, but the underlying interface may be apartment-bound) and leaks the ref count whenever the owner is finalized without `Dispose`.

- `ComScope<T>` — `ref struct`, use with `using`. Releases on dispose. **The preferred way to scope any COM pointer that doesn't survive the current method**, including transient pointers received from `CoCreateInstance`, `QueryInterface`, `OpenScope`, factory methods, etc.
  - **Receive output parameters directly into the `ComScope`.** `ComScope<T>` implicitly converts to `T**` and `void**`, so pass the scope itself where the API expects a `T** ppvObject` / `void** ppv`. The call writes into the scope and the `using` Releases on scope exit. No `T* local; ...->Method(&local); try {...} finally { Release(local); }` patterns.

    ```csharp
    Guid clsid = SomeStruct.CLSID;
    Guid iid = IID.Get<ISomeInterface>();
    using ComScope<ISomeInterface> scope = new();
    PInvoke.CoCreateInstance(&clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, &iid, scope).ThrowOnFailure();
    scope.Pointer->DoThing(...);

    using ComScope<IOther> other = new();
    Guid otherIid = IOther.IID_IOther;
    scope.Pointer->QueryInterface(&otherIid, other).ThrowOnFailure();
    ```
  - Access methods via `scope.Pointer->Method(...)`. Check for null with `scope.IsNull`.

- `AgileComPointer<T>` — finalizable managed class. Use for **every COM pointer that outlives a single method call**, i.e. anything stored in a class field.
  - Registers in the Global Interface Table (thread-agile) and releases via the finalizer if `Dispose` is missed.
  - Access via `using ComScope<T> scope = agile.GetInterface();` then `scope.Pointer->Method(...)`. Each `GetInterface()` round-trips through the GIT, so hoist a single scope to the top of a method when several calls share it.
  - **Constructor `takeOwnership`**: pass `false` when the raw pointer was just received into a `ComScope<T>` that will Release on dispose — the GIT registration AddRefs independently, so two owners is correct. Pass `true` only when no other code path will Release the raw pointer (e.g. handing off a pointer that has no `ComScope` wrapper).
  - Dispose via the owner's `Dispose` / `DisposeManagedResources` (`AgileComPointer` is a managed object, not an unmanaged resource — it has its own finalizer).

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