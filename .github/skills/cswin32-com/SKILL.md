---
name: cswin32-com
description: 'Guides struct-based COM interop in MSBuild using CsWin32 patterns. Consult when working with ComScope<T>, ComClassFactory, IComIID, IID.Get<T>(), delegate* unmanaged vtables, CoCreateInstance, or manually defining COM interfaces not in Win32 metadata (e.g. WMI IWbemLocator, IWbemServices).'
argument-hint: 'Describe the COM interface or activation pattern you are working with.'
---

# CsWin32 COM Interop Guide

Struct-based COM interop using CsWin32 patterns — AOT-compatible, no `[ComImport]` or built-in marshalling.

**Paired skill:** [cswin32-interop](../cswin32-interop/SKILL.md) covers general P/Invoke and the blittable signature rules that apply to both `[DllImport]` and COM vtables. This file covers only the COM-specific layer.

## Workflow

1. **Interface in Win32 metadata?** Add the name to `src/Framework/NativeMethods.txt` → CsWin32 generates it. Not in metadata (WMI, Fusion, Setup Configuration) → define a manual struct under its own folder, excluded from source builds via `<Compile Remove>`.
2. **Lifetime: `using ComScope<T> scope = new();`** for every transient COM pointer (`CoCreateInstance`, `QueryInterface`, `IEnumXxx::Next`, factory output, app-local `STDAPI Get*`, etc.). Never write `T* local; try { ... } finally { local->Release(); }` — that's the pre-`ComScope` shape that leaks on every early return. Same for `BSTR` out-params: `using BSTR x = default;`.
3. **Activate** via `ComClassFactory.TryCreate(CLSID, ...)` (AOT-compatible) or `PInvoke.CoCreateInstance` with `IID.Get<T>()` — not `&localGuid`.
4. **Call** via `scope.Pointer->Method(...)`. Pass `ComScope<T>` directly where the API expects `T**` / `void**` — the implicit operator does the address-of.
5. **Match the caller's error contract.** If the top-level consumer swallows COM failure ("no result" == "absent"), helpers return `default` / `false` instead of throwing a `COMException` that's immediately discarded. `ThrowOnFailure` only when the exception will actually propagate or assert a should-never-happen.
6. **Guard with `#if FEATURE_WINDOWSINTEROP`** — add `&& NET` only when the struct uses the static-abstract `IComIID` form exclusively (no net472 dual-target).

## Manual COM Structs (Not in Metadata)

Define each interface in its own file under e.g. `src/Tasks/AssemblyDependency/Fusion/`, `src/Framework/Shared/VisualStudio/`, `src/Framework/Utilities/Wmi/`. Exclude from source builds. Pattern (dual-target — works on net472 + .NET):

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
    readonly ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IAssemblyCache);
    }
#endif

    private readonly void** _lpVtbl;

    // IUnknown at indices 0-2, then interface methods at 3+
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IAssemblyCache* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IAssemblyCache*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }
    // AddRef @1, Release @2, then interface methods at correct slot indices.
}
```

**Rules:**
- `delegate* unmanaged[Stdcall]` for the function-pointer cast — IDL `STDMETHODCALLTYPE` ⇒ `__stdcall` on Win32. Picking the wrong convention silently corrupts the stack.
- `IComIID` has two shapes: static-abstract (`static ref readonly Guid IComIID.Guid`) on .NET 7+, instance (`readonly ref readonly Guid IComIID.Guid`) on net472 / netstandard2.0. CsWin32 picks the right one for its generated structs automatically (see [IComIID across TFMs](#icomiid-across-tfms)); a **manual** struct must spell out both arms itself. For .NET-only structs (WMI), drop the `#else` branch and gate the whole file `#if NET`. For dual-target structs (Fusion), keep both as shown — see `src/Tasks/AssemblyDependency/Fusion/` for the canonical example.
- Use CsWin32-generated `PCWSTR` / `PWSTR` for wide strings (add to `NativeMethods.txt`); raw `char*` only when no typed equivalent exists.
- Vtable slots are exact: index 0 = `QueryInterface`, 1 = `AddRef`, 2 = `Release`, 3+ = interface methods in IDL order. When inheriting, **add the parent interface's method count** before counting your own (e.g. `ISetupConfiguration2.EnumAllInstances` is at slot 6 = 3 IUnknown + 3 v1 + 0).
- CS0592 prevents `[SupportedOSPlatform]` on structs — put it on individual methods.

## Lifetime & Access

**A raw `T*` (COM struct) is forbidden as a field on a non-`ref` type.** Allowed locations: locals in `unsafe` methods, parameters, fields of a `ref struct`. Anywhere else (instance fields of `class` / non-`ref` `struct`) → use `AgileComPointer<T>` — a raw pointer field is an apartment-agility hazard and leaks on finalize-without-Dispose.

### `ComScope<T>` — transient pointers

`ref struct`, `using`'d, calls `Release` on dispose. **The default for everything that doesn't survive the current method.** Implicitly converts to `T**` and `void**`, so out-params write into the scope directly:

```csharp
using ComScope<ISomeInterface> scope = new();
Guid clsid = SomeStruct.CLSID;
Guid iid = IID.Get<ISomeInterface>();
PInvoke.CoCreateInstance(&clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, &iid, scope).ThrowOnFailure();
scope.Pointer->DoThing(...);

using ComScope<IOther> other = new();
Guid otherIid = IOther.IID_IOther;
scope.Pointer->QueryInterface(&otherIid, other).ThrowOnFailure();
```

- Access methods via `scope.Pointer->Method(...)`. Null-check with `scope.IsNull`.
- Applies to *every* COM out-param: `CoCreateInstance`, `QueryInterface`, `IEnumXxx::Next`, factory methods, app-local `STDAPI Get*` (declare the `[DllImport]` with `T** pp`, not `out T*`, so the implicit operator binds).

**Ownership transfer out of a helper.** A helper that acquires a COM pointer can return `ComScope<T>` directly; intermediate pointers stay in their own `using` scopes and Release on the helper's `return`. `default` `ComScope<T>` is null and its `Dispose` is a no-op, so callers don't need an extra null guard:

```csharp
private static ComScope<ISetupConfiguration2> AcquireSetupConfig()
{
    Guid clsid = SetupConfiguration.CLSID_SetupConfiguration;
    Guid iid = ISetupConfiguration2.IID_ISetupConfiguration2;

    ComScope<ISetupConfiguration2> config = new();
    HRESULT hr = PInvoke.CoCreateInstance(&clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, &iid, config);
    if (hr.Succeeded) return config;                 // ownership flows to caller
    if (hr != HRESULT.REGDB_E_CLASSNOTREG) return default;

    using ComScope<ISetupConfiguration> v1 = new();  // intermediate, Released on return
    if (SetupConfiguration.GetSetupConfiguration(v1, IntPtr.Zero) < 0 || v1.IsNull) return default;
    return v1.Pointer->QueryInterface(&iid, config).Succeeded ? config : default;
}

// caller:
using ComScope<ISetupConfiguration2> config = AcquireSetupConfig();
if (!config.IsNull) { /* use config.Pointer */ }
```

### `BSTR` — COM strings

CsWin32-generated; extended in [`Windows/Win32/Foundation/BSTR.cs`](../../../src/Framework/Windows/Win32/Foundation/BSTR.cs) with `IDisposable`. `Dispose` calls `SysFreeString` and zeroes the field in place. **Scope every COM `BSTR` out-param with `using BSTR x = default;`** — no manual `try/finally + SysFreeString`:

```csharp
using BSTR versionBstr = default;
if (instance->GetInstallationVersion(&versionBstr).Failed) return false;
string version = versionBstr.ToString();
// SysFreeString runs at scope exit.
```

In-place-only: `BSTR.Dispose` writes through `Unsafe.AsRef(in this)`, so `using` must be on the storage location, not on a method-returned copy. Same rule as `ComScope<T>`.

### `AgileComPointer<T>` — fields outliving a method

Finalizable managed class. Use whenever a COM pointer is stored in a class field (the only legal way — raw `T*` fields are forbidden, see top of section).

- Registers in the Global Interface Table (thread-agile); finalizer releases if `Dispose` is missed.
- Access via `using ComScope<T> scope = agile.GetInterface();` then `scope.Pointer->Method(...)`. `GetInterface()` round-trips through the GIT — hoist a single scope to the top of a method when several calls share it.
- Constructor `takeOwnership`: pass `false` when the raw pointer is also held by a `ComScope<T>` that will Release (GIT AddRefs independently); pass `true` only when no other code path will Release.
- Dispose via the owner's `Dispose` / `DisposeManagedResources` (`AgileComPointer` is managed, not unmanaged — its finalizer is the safety net, not the primary cleanup).

## Activation

```csharp
// AOT-compatible
if (ComClassFactory.TryCreate(IWbemLocator.CLSID, out var factory, out HRESULT hr))
    using ComScope<IWbemLocator> instance = factory.TryCreateInstance<IWbemLocator>(out hr);

// CoCreateInstance — IID.Get<T>(), not &localGuid
Guid clsid = IWbemLocator.CLSID;
using ComScope<IWbemLocator> locator = new();
hr = PInvoke.CoCreateInstance(&clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.Get<IWbemLocator>(), locator);
```

## Error-Handling Parity When Migrating

Struct-based COM returns raw `HRESULT`; `[ComImport]` and built-in activation threw automatically. Preserve the old throw-vs-return at each call site:

| Old shape | Threw via | Migrated shape |
|---|---|---|
| `new SomeCoClass()` | built-in activation | `PInvoke.CoCreateInstance(...).ThrowOnFailure()` |
| `(IFoo)rcw` cast | `InvalidCastException` on QI | `QueryInterface(&iid, scope).ThrowOnFailure()` |
| `[ComImport]` method, default `PreserveSig=false` | marshaller throws on `FAILED(hr)` | `.ThrowOnFailure()` at the call site |
| `[ComImport]` method with `[PreserveSig]` | caller inspects return | mirror the existing `hr` branch — don't start throwing |

**Factory-method exception:** when the old contract was "return null for invalid input" (e.g. `Create(path)`), keep null-return only for the legitimate-rejection path; activation / QI failures still throw. See [`MetadataReader.cs`](../../../src/Tasks/ManifestUtil/MetadataReader.cs) (throws on activation/QI, returns null on `OpenScope`).

**Don't throw when the caller swallows it.** If the top-level consumer wraps the whole chain in `catch (COMException) { }`, inner helpers must return `default` / `false` / empty instead of constructing a `COMException` only to have it discarded. Throwing here allocates the exception, walks the stack, and hides the HRESULT for no benefit. See [`VisualStudioLocationHelper.AcquireSetupConfiguration2`](../../../src/Framework/VisualStudioLocationHelper.cs).

## Mocking Struct-Based COM in Tests

Struct-based COM calls go through raw vtable pointers, so a managed mock can't be passed where a `T*` is expected. Bridge with the **built-in COM marshaller**: the mock implements the *managed* interface (BCL `System.Runtime.InteropServices.ComTypes.*` when ABI-compatible — true for `ITypeLib` / `ITypeInfo` — else CsWin32's nested `[ComImport] internal interface Interface`), and the test passes a **CCW** (COM-callable wrapper) pointer to the code under test:

```csharp
// MockTypeLib : ComTypes.ITypeLib   (a normal managed class)
IntPtr ccw = Marshal.GetComInterfaceForObject(mock, typeof(ComTypes.ITypeLib));
try { walker.AnalyzeTypeLibrary((ITypeLib*)ccw); }   // struct API; real QueryInterface works
finally { Marshal.Release(ccw); }
```

The CCW exposes a real vtable, so no hand-rolled native vtable is needed and the managed mock framework stays intact. But the mock must now behave like real COM, not a managed object:

- **All `[out]` params are written**, even ones the caller ignores. Passing `null` for a trailing out-param (`GetDocumentation(-1, &name, null, null, null)`) faults the marshaller — supply a real pointer for each.
- **Exception identity is lost.** An HRESULT-returning method resurfaces a thrown exception as a *new* `COMException` (HRESULT preserved, instance not); a `uint`/`void` method with no HRESULT channel (`GetTypeInfoCount`, the `Release*` methods) **swallows it silently** and returns garbage. Assert on type/count, not the injected instance, and don't rely on `void`/`uint` methods to propagate.
- **Throw `COMException` on bad input, don't `Assert`.** An `Assert` throws an `XunitException` that escapes the SUT's `catch (COMException)`; a `COMException` becomes a failing HRESULT the SUT handles normally. (Matters because a swallowed `GetTypeInfoCount` failure can feed an out-of-range index to the next call.)

See [`MockTypeLib.cs`](../../../src/Tasks.UnitTests/MockTypeLib.cs) / [`MockTypeInfo.cs`](../../../src/Tasks.UnitTests/MockTypeInfo.cs) and the CCW bridge in [`ComReferenceWalker_Tests.cs`](../../../src/Tasks.UnitTests/ComReferenceWalker_Tests.cs).

## Strongly-typed handle / token wrappers

When the native side `typedef`s a primitive into a family of "same shape, different meaning" aliases (`corhdr.h`'s `typedef ULONG32 mdToken; typedef mdToken mdAssembly; ...`), mirror that hierarchy with distinct `readonly struct` wrappers holding the single underlying primitive — same pattern as CsWin32's `HANDLE` / `HWND` / `HMODULE`. Stays blittable; `delegate*` casts and arrays marshal at zero cost.

Conversions follow the typedef hierarchy: **implicit** widening to the base (`MdAssembly` → `MdToken`, always safe), **explicit** narrowing (`(MdAssembly)token`, opt-in because the C side can't enforce the kind at the cast site).

**Check the native header for the canonical validation primitives** before defining `IsNil` / `IsValid`. Encoding details often hide in macros that don't grep cleanly. For `mdToken` the encoding is `(TableType << 24) | Rid`:

| C macro | Meaning |
|---|---|
| `TypeFromToken(tk) = tk & 0xff000000` | Table-type tag (high byte) → `CorTokenType` |
| `RidFromToken(tk) = tk & 0x00ffffff`  | Row id (low 24 bits) |
| `IsNilToken(tk) = RidFromToken(tk) == 0` | Nil check — **rid half, not the whole value** |
| `mdAssemblyNil = mdtAssembly = 0x20000000` | Per-type nil is the table-type tag, **not 0** |

A naive `IsNil => Value == 0` would misclassify the `0x20000000` "no assembly in this scope" return from `GetAssemblyFromScope`. Mirror the macro: `IsNil => Rid == 0`. Reference: [`Tokens.cs`](../../../src/Tasks/AssemblyDependency/Metadata/Tokens.cs), [`CorTokenType.cs`](../../../src/Tasks/AssemblyDependency/Metadata/CorTokenType.cs).

## IComIID across TFMs

CsWin32 (since **0.3.287**) emits `IComIID` and attaches it to every generated COM struct on **all** target frameworks — static-abstract `static ref readonly Guid Guid { get; }` on .NET 7+, instance `ref readonly Guid Guid { get; }` on net472 / netstandard2.0. No hand-authored polyfill is needed; the old `src/Framework/Polyfills/IComIID.cs` + `IComIIDPolyfills.cs` files (one `partial` per generated struct) are gone.

When you add a new CsWin32-generated COM type to `NativeMethods.txt`, it carries `IComIID` automatically through `ComScope<T>` — nothing extra to do.

Only **manual** structs (WMI, Fusion, Setup Configuration, CLR metadata) implement `IComIID` by hand, matching the per-TFM shape — see [Manual COM Structs](#manual-com-structs-not-in-metadata). `IID.Get<T>()` ([`IID.cs`](../../../src/Framework/Windows/Win32/IID.cs)) reads it via `T.Guid` on .NET and `default(T).Guid` on net472.

## File Organization

| Location | Contents |
|---|---|
| `src/Framework/Windows/Win32/System/Com/` | `ComScope`, `ComClassFactory`, `AgileComPointer`, `GlobalInterfaceTable` |
| `src/Framework/Windows/Win32/IID.cs` | Generic IID lookup |
| `src/Framework/Utilities/Wmi/`, `src/Framework/Shared/VisualStudio/`, `src/Tasks/AssemblyDependency/Fusion/`, `src/Tasks/AssemblyDependency/Metadata/`, `src/Tasks/TypeLib/` | Manual COM struct interfaces — own folder per API surface |

## CS3016 CLS Compliance

CsWin32 (since **0.3.287**) auto-emits `[CLSCompliant(false)]` on every generated COM struct wrapper that carries CCW thunks (the `[UnmanagedCallersOnly(CallConvs = new[] { ... })]` array argument is what trips CS3016), and adds CS3019 / CS3021 to its own generated-file `#pragma warning disable` list. So a CLS-compliant assembly (`[assembly: CLSCompliant(true)]`) builds clean with no consumer action — the hand-authored `GeneratedInteropClsCompliance.cs` partial and the `.editorconfig` CS3019 suppression for `{**/Windows/**/*.cs}` are gone.

The attribute is only emitted on .NET 5+ TFMs (net472 / netstandard2.0 have no CCW thunks, so no CS3016) and only for `internal` wrappers (the generator default). Don't re-add per-type `[CLSCompliant(false)]` partials or blanket `NoWarn`. See https://github.com/dotnet/roslyn/issues/68526 for the underlying Roslyn limitation that motivated the generator-side fix.
