# Manual COM interfaces and native tokens

## Before defining a struct

Check [NativeMethods.txt](../../../../src/Framework/NativeMethods.txt), the [generator configuration](../../../../src/Framework/NativeMethods.json), and [package pin](../../../../eng/dependabot/Directory.Packages.props). Whether an interface is available depends on the selected metadata/generator, not a permanent list in this guide.

Existing manual surfaces include [Fusion](../../../../src/Tasks/AssemblyDependency/Fusion), [WMI](../../../../src/Framework/Utilities/Wmi), [Setup Configuration](../../../../src/Framework/Shared/VisualStudio), and [CLR metadata](../../../../src/Tasks/AssemblyDependency/Metadata). Follow the owning project's compile exclusions and the [P/Invoke ABI/platform reference](../../cswin32-interop/references/abi-and-platforms.md).

Keep one interface per file where that is the local pattern. Use the native IDL/header as the ABI authority: inheritance, GUID, slot order, calling convention, widths, indirection, and ownership all matter.

## IComIID and target frameworks

[IID.Get<T>](../../../../src/Framework/Windows/Win32/IID.cs) uses the generated instance-based `IComIID.Guid` shape for Framework/Standard targets and the static shape for the modern runtime target. Inspect the current generated contract if changing target frameworks.

Generated COM structs should not require hand-maintained per-type `IComIID` partials. Manual structs still implement the appropriate shape. The existing dual-target [IAssemblyCache](../../../../src/Tasks/AssemblyDependency/Fusion/IAssemblyCache.cs) uses:

```csharp
public static readonly Guid IID_IAssemblyCache =
    new(0xE707DCDE, 0xD1CD, 0x11D2, 0xBA, 0xB9, 0x00, 0xC0, 0x4F, 0x8E, 0xCE, 0xAE);

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
```

This is a fragment of that existing unsafe struct, not a new interface to duplicate. WMI's manually defined interfaces are excluded from other runtime targets by the [Framework project](../../../../src/Framework/Microsoft.Build.Framework.csproj); do not infer dual-target support merely from a similar vtable.

## Vtable calls

IUnknown occupies slots 0 (`QueryInterface`), 1 (`AddRef`), and 2 (`Release`). Count inherited methods before adding derived methods. [ISetupConfiguration2.EnumAllInstances](../../../../src/Framework/Shared/VisualStudio/ISetupConfiguration2.cs) is at slot 6: three IUnknown methods plus three parent-interface methods.

The existing `IAssemblyCache.QueryInterface` shape is:

```csharp
private readonly void** _lpVtbl;

public HRESULT QueryInterface(Guid* riid, void** ppvObject)
{
    fixed (IAssemblyCache* pThis = &this)
    {
        return ((delegate* unmanaged[Stdcall]<IAssemblyCache*, Guid*, void**, HRESULT>)
            _lpVtbl[0])(pThis, riid, ppvObject);
    }
}
```

Match `STDMETHODCALLTYPE`/the actual native calling convention. In particular, confusing conventions on x86 can corrupt the stack. `AddRef` and `Release` return native ULONG (`uint`), not HRESULT.

Prefer generated typed pointers and option enums when they accurately model the native declaration. Preserve BSTR versus null-terminated-string ownership and parameter semantics; similar pointer representations do not imply interchangeable lifetimes.

`SupportedOSPlatformAttribute` is valid on structs. Use it or member annotations according to the boundary; do not work around a nonexistent blanket CS0592 restriction.

## Metadata token wrappers

[Tokens.cs](../../../../src/Tasks/AssemblyDependency/Metadata/Tokens.cs) and [CorTokenType.cs](../../../../src/Tasks/AssemblyDependency/Metadata/CorTokenType.cs) preserve the CLR token ABI with distinct readonly wrappers over a single `uint`.

| Native operation | Meaning |
|---|---|
| `TypeFromToken(tk)` | High-byte table tag: `tk & 0xff000000` |
| `RidFromToken(tk)` | Low 24-bit row identifier: `tk & 0x00ffffff` |
| `IsNilToken(tk)` | The row identifier is zero |
| `mdAssemblyNil` | Assembly tag with zero row ID (`0x20000000`), not the all-zero token |

The critical distinction is `IsNil => Rid == 0`, not `Value == 0`. A typed token can be nil while retaining its table tag.

Preserve the typedef hierarchy: widening from `MdAssembly` to `MdToken` is implicit; narrowing is explicit and the repository's constructor validates the expected table tag. `default(MdAssembly)` does not run that constructor and is not the canonical typed nil; use `MdAssembly.Nil` when synthesizing it.

Native writes through pointer/array storage bypass managed constructors, so keep the wrapper layout ABI-compatible and validate managed-created values at the managed boundary. Check the native header's macros before inventing `IsValid` or nil rules for another token family.

## Generated CLS diagnostics

If generated COM/CCW declarations produce CLS-compliance diagnostics, inspect the pinned generator's output and applicable TFM before adding consumer partials or suppressions. Do not assume an old `IComIID` polyfill or per-type CLS workaround is still required.

Keep the distinction between generated-code fixes and hand-authored interface changes. [The Roslyn discussion](https://github.com/dotnet/roslyn/issues/68526) is background for the attribute/CLS issue, not proof that a particular generator version needs a blanket workaround. Preserve relevant assembly policy and require evidence for any suppression.
