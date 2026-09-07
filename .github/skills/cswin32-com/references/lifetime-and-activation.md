# COM lifetime and activation

## Choose by ownership

| Pointer use | Usual representation |
|---|---|
| Borrowed input valid for the call | Raw pointer, without an extra Release |
| Newly acquired temporary interface reference | `using ComScope<T>` |
| Reference transferred from a helper | Return an owning scope; caller disposes it |
| Application-owned reference retained beyond the method | `AgileComPointer<T>` where its GIT/apartment contract is appropriate |
| Foundational COM infrastructure | Follow its explicit lifetime design; do not recursively wrap the GIT in itself |

The constructors of [ComScope<T>](../../../../src/Framework/Windows/Win32/System/Com/ComScope.cs) take ownership without adding a reference. Wrapping a borrowed pointer and disposing it can release someone else's reference.

The scope is a ref struct. Its pointer-to-pointer operators write into its own storage; disposal clears that storage and calls `IUnknown.Release`. Use the scope itself as the out destination:

```csharp
using ComScope<IWbemLocator> locator = new();
Guid clsid = IWbemLocator.CLSID;
Guid iid = IID.Get<IWbemLocator>();
PInvoke.CoCreateInstance(
    &clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, &iid, locator).ThrowOnFailure();
```

This is an unsafe, Windows-supported call-site fragment with a throwing error contract. [IID.Get<T>](../../../../src/Framework/Windows/Win32/IID.cs) returns a **Guid**, not a Guid pointer. Use the repository's local-GUID/address pattern for the raw API.

Access through `scope.Pointer->Method(...)`; use `IsNull` when the API can legitimately produce no interface. Do not copy an owning scope and dispose both copies, or dispose a temporary copy instead of the actual storage.

`try/finally` is a valid cleanup mechanism and runs on early return. Prefer `ComScope` for clarity and established ownership, not because `finally` inherently leaks.

## Factories and activation choice

[ComClassFactory](../../../../src/Framework/Windows/Win32/System/Com/ComClassFactory.cs) is itself disposable and holds a native class-factory reference. After successfully acquiring it, dispose both layers:

```csharp
using (factory)
{
    using ComScope<IWbemLocator> locator =
        factory.TryCreateInstance<IWbemLocator>(out HRESULT hr);
    hr.ThrowOnFailure();
    // Use locator.Pointer within this scope.
}
```

Handle a failed `TryCreate`/`TryCreateFromModule` according to the caller's contract. A using declaration is not a valid unbraced `if` body; use a block. Check the instance-creation HRESULT before using the pointer.

Activation choices are not interchangeable:

- `CoCreateInstance` / `TryCreate` use COM registration.
- `TryCreateFromModule` obtains a factory from a module export.
- The CLR metadata path has a particular shim/hosting constraint. [MetadataReader](../../../../src/Tasks/ManifestUtil/MetadataReader.cs) uses `TryCreateFromModule` with `ClrDllGetClassObjectInternalExportName` rather than passing its metadata CLSID to ordinary `CoCreateInstance`.

Preserve that CLR-hosted activation distinction; merely replacing one activation API with another AOT-compatible API can still regress a native host.

## Returning an acquired reference

A helper may return `ComScope<T>` when ownership clearly transfers to its caller. Do not put the returned owner in a helper-local using that releases it before return; intermediate interfaces can remain in separate using scopes.

The null/default scope disposes safely. Release any acquired output before abandoning it on failure, and avoid overwriting an already-owned pointer with another COM out call.

[VisualStudioLocationHelper.AcquireSetupConfiguration2](../../../../src/Framework/VisualStudioLocationHelper.cs) is the current example: it tries registered activation, falls back to the app-local setup helper for class-not-registered, disposes abandoned outputs, and transfers the successful result. Refer to that implementation rather than keeping an abbreviated copy that drops failure cleanup.

## BSTR storage

The [BSTR partial](../../../../src/Framework/Windows/Win32/Foundation/BSTR.cs) implements `IDisposable`; disposal uses `Marshal.FreeBSTR` and clears the instance in place.

```csharp
using BSTR versionBstr = default;
HRESULT hr = instance->GetInstallationVersion(&versionBstr);
hr.ThrowOnFailure();
string version = versionBstr.ToString();
```

Use the appropriate return/throw branch instead of `ThrowOnFailure` when the caller treats a failed query as absence. Dispose the actual local/storage location, not a by-value copy. For a newly owned BSTR out-parameter, scope its lifetime even if a subsequent query fails.

## Retained references and GIT access

[AgileComPointer<T>](../../../../src/Framework/Windows/Win32/System/Com/AgileComPointer.cs) registers an interface in the [Global Interface Table](../../../../src/Framework/Windows/Win32/System/Com/GlobalInterfaceTable.cs). It has a finalizer as a safety net, but the owner should dispose it explicitly through its normal managed-resource cleanup.

- `takeOwnership: false`: appropriate when a `ComScope` will release the caller's reference; the GIT holds its own reference.
- `takeOwnership: true`: transfer the caller's reference, with no other path responsible for releasing it.
- Acquire a temporary scope with `GetInterface()` (throwing) or `TryGetInterface(out HRESULT)` for explicit HRESULT handling.
- A GIT lookup is not free. Reuse one acquired scope for several calls within a method rather than repeatedly fetching the same interface.

Do not treat "all raw fields are forbidden" as a language rule or a safe automated refactor. `ComClassFactory` and `GlobalInterfaceTable` are foundational owners with raw storage; wrapping their internals blindly can break initialization/lifetime design.

## Error parity

| Prior behavior | Preserve |
|---|---|
| Activation failed by throwing | A corresponding failure reaches the caller |
| A managed interface cast failed | QI failure is handled at the same contract boundary |
| Non-preserve-signature COM method | Raw HRESULT failure does not silently become success |
| Preserve-signature method | Existing special-code, retry, return, or throw behavior |
| Legitimate missing/invalid object | The established absence result, without hiding unrelated failures |

HRESULT-to-exception mapping can preserve an error code without preserving exception identity or always producing the same managed type. Assert the contract required by the caller.

Setup discovery deliberately returns an empty result for absent COM setup information; [VisualStudioLocationHelper](../../../../src/Framework/VisualStudioLocationHelper.cs) checks HRESULTs instead of constructing discarded exceptions. [MetadataReader](../../../../src/Tasks/ManifestUtil/MetadataReader.cs) instead throws on activation/QI failures and treats its per-file `OpenScope` rejection as absence.

These are distinct local contracts, not permission to replace every caught COM exception with a default value. Account for all callers, loop termination, later operations skipped by an exception, and cleanup.
