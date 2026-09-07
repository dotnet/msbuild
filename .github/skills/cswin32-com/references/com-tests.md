# Testing struct-based COM

## Match the test host

The existing [ComReferenceWalker_Tests](../../../../src/Tasks.UnitTests/ComReferenceWalker_Tests.cs) are gated by `FEATURE_APPDOMAIN`. Their managed-mock bridge uses the built-in COM marshaller; it is not a Native AOT testing recipe.

Use [test instructions](../../../instructions/tests.instructions.md) and [running-unit-tests](../../running-unit-tests/SKILL.md) for the actual runner/TFM. Select a native ABI/lifetime scenario or an existing managed bridge according to what the changed code needs to prove.

## Managed mock to native pointer

The walker accepts a struct pointer, not a managed interface implementation. Its tests obtain a COM-callable wrapper (CCW) for a managed mock implementing the ABI-compatible BCL interface:

```csharp
IntPtr ccw = Marshal.GetComInterfaceForObject(
    mock, typeof(System.Runtime.InteropServices.ComTypes.ITypeLib));
try
{
    walker.AnalyzeTypeLibrary((ITypeLib*)ccw);
}
finally
{
    Marshal.Release(ccw);
}
```

This is the unsafe call-site pattern from the existing suite; `ITypeLib*` denotes the Win32 struct, not the managed BCL interface. Resolve the namespace from the actual declarations.

The pointer exposes a real COM vtable and QueryInterface behavior. The acquired reference must be released, including when the walker fails.

For another interface, verify the managed declaration's ABI or an available generated managed projection. Do not assume every CsWin32 struct exposes a particular nested `Interface` type under every generator configuration.

## Mock ABI and out parameters

[MockTypeInfo](../../../../src/Tasks.UnitTests/MockTypeInfo.cs) deliberately implements the BCL interface rather than also implementing a differently shaped interface with the same IID. Competing declarations can cause QueryInterface to expose the wrong ABI; pointer-sized versus 32-bit parameters are not interchangeable.

A managed out parameter may require a real output address even when the native caller is uninterested in the result. The type-library tests need valid storage for the trailing `GetDocumentation` outputs. Do not generalize a native API's optional-pointer convention to the managed CCW projection without checking it.

Scope any BSTRs or other newly owned outputs, including ignored ones. Preserve the helpers that account for acquired/released native handles.

## Fault injection and assertions

The current `FaultInjectionMainLib` scenario injects `COMException` instances. Its important assertions are that exceptions do not escape the walker, the recorded problem contract is appropriate, and all tracked handles are released.

Crossing COM can change exception identity and HRESULT-to-managed-type mapping. Do not assert reference identity merely because a managed mock originally threw a particular object.

The existing suite distinguishes HRESULT-reporting methods from methods such as `GetTypeInfoCount` and `Release*`, which cannot report failure through an HRESULT return. Its injected failures may produce no recorded problem through those latter paths. These are observations about this bridge and test host, not universal guarantees about all COM runtimes.

[MockTypeLib.GetTypeInfo](../../../../src/Tasks.UnitTests/MockTypeLib.cs) returns a failing COM contract for an out-of-range index rather than relying on an assertion exception. This matters when an earlier non-HRESULT failure leaves the caller with an unusable count.

Choose evidence for the changed behavior: successful calls, relevant failure paths, actual reference/handle release, correct slot/signature, or apartment access. A mock round trip alone does not prove a real COM server's behavior or Native AOT compatibility.
