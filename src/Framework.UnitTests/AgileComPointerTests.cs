// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WINDOWSINTEROP

using System;
using System.Runtime.Versioning;
using Shouldly;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests;

/// <summary>
/// Tests for <see cref="GlobalInterfaceTable"/> and <see cref="AgileComPointer{TInterface}"/>.
/// Uses <see cref="IRunningObjectTable"/> obtained via <c>PInvoke.GetRunningObjectTable</c>
/// as a real COM object to register, since it is always available on Windows and is
/// already wired up with <see cref="IComIID"/> via the existing polyfill list.
/// </summary>
[SupportedOSPlatform("windows5.0")]
public unsafe class AgileComPointerTests
{
    private static IRunningObjectTable* CreateRot()
    {
        IRunningObjectTable* rot;
        HRESULT hr = PInvoke.GetRunningObjectTable(0, &rot);
        hr.ThrowOnFailure();
        return rot;
    }

    [WindowsOnlyFact]
    public void GlobalInterfaceTable_RegisterAndGet_ReturnsInterface()
    {
        IRunningObjectTable* rot = CreateRot();
        try
        {
            uint cookie = GlobalInterfaceTable.RegisterInterface(rot);
            cookie.ShouldNotBe(0u);

            using (ComScope<IRunningObjectTable> scope = GlobalInterfaceTable.GetInterface<IRunningObjectTable>(cookie, out HRESULT hr))
            {
                hr.ShouldBe(HRESULT.S_OK);
                scope.IsNull.ShouldBeFalse();
            }

            GlobalInterfaceTable.RevokeInterface(cookie).ShouldBe(HRESULT.S_OK);
        }
        finally
        {
            ((IUnknown*)rot)->Release();
        }
    }

    [WindowsOnlyFact]
    public void GlobalInterfaceTable_GetAfterRevoke_Fails()
    {
        IRunningObjectTable* rot = CreateRot();
        uint cookie;
        try
        {
            cookie = GlobalInterfaceTable.RegisterInterface(rot);
            GlobalInterfaceTable.RevokeInterface(cookie).ShouldBe(HRESULT.S_OK);
        }
        finally
        {
            ((IUnknown*)rot)->Release();
        }

        using ComScope<IRunningObjectTable> scope = GlobalInterfaceTable.GetInterface<IRunningObjectTable>(cookie, out HRESULT hr);
        hr.Failed.ShouldBeTrue();
        scope.IsNull.ShouldBeTrue();
    }

    [WindowsOnlyFact]
    public void AgileComPointer_GetInterface_ReturnsLiveInterface()
    {
        IRunningObjectTable* rot = CreateRot();
        using AgileComPointer<IRunningObjectTable> agile = new(rot, takeOwnership: true);

        using ComScope<IRunningObjectTable> scope = agile.GetInterface();
        scope.IsNull.ShouldBeFalse();
    }

    [WindowsOnlyFact]
    public void AgileComPointer_TakeOwnership_ReleasesCallerReference()
    {
        // When takeOwnership: true, the AgileComPointer releases the caller's ref count.
        // The interface stays alive because the GIT now holds the ref.
        IRunningObjectTable* rot = CreateRot();
        using AgileComPointer<IRunningObjectTable> agile = new(rot, takeOwnership: true);

        // Should still be able to obtain a usable scope after caller's ref was released.
        using ComScope<IRunningObjectTable> scope = agile.GetInterface();
        scope.IsNull.ShouldBeFalse();
    }

    [WindowsOnlyFact]
    public void AgileComPointer_NoOwnership_PreservesCallerReference()
    {
        // When takeOwnership: false, the caller still owns its ref and must release.
        IRunningObjectTable* rot = CreateRot();
        try
        {
            // Capture the baseline refcount via an AddRef/Release pair. ROT is a process
            // singleton and may report a fixed count regardless of actual references, so
            // we use the AddRef return as the reference value rather than asserting a
            // hard-coded number.
            uint baseline = ((IUnknown*)rot)->AddRef();
            ((IUnknown*)rot)->Release();

            using (AgileComPointer<IRunningObjectTable> agile = new(rot, takeOwnership: false))
            {
                using ComScope<IRunningObjectTable> scope = agile.GetInterface();
                scope.IsNull.ShouldBeFalse();
            }

            // After AgileComPointer disposes (revoking its GIT entry and releasing its
            // own ref), the caller's reference must still be intact. AddRef/Release
            // round-trip should report the same baseline — anything else means the ref
            // was double-released (AV / underflow) or AgileComPointer leaked a ref.
            uint afterAdd = ((IUnknown*)rot)->AddRef();
            uint remaining = ((IUnknown*)rot)->Release();
            afterAdd.ShouldBe(baseline);
            remaining.ShouldBe(baseline);

            // Release the caller's outstanding reference.
            ((IUnknown*)rot)->Release();
        }
        catch
        {
            ((IUnknown*)rot)->Release();
            throw;
        }
    }

    [WindowsOnlyFact]
    public void AgileComPointer_Dispose_RevokesFromGlobalInterfaceTable()
    {
        IRunningObjectTable* rot = CreateRot();
        AgileComPointer<IRunningObjectTable> agile = new(rot, takeOwnership: true);

        // Before dispose: GetInterface succeeds.
        using (ComScope<IRunningObjectTable> scope = agile.TryGetInterface(out HRESULT hr))
        {
            hr.Succeeded.ShouldBeTrue();
        }

        agile.Dispose();

        // After dispose: GetInterface fails (cookie is gone).
        using (ComScope<IRunningObjectTable> scope = agile.TryGetInterface(out HRESULT hr))
        {
            hr.Failed.ShouldBeTrue();
        }
    }

    [WindowsOnlyFact]
    public void AgileComPointer_Dispose_IsIdempotent()
    {
        IRunningObjectTable* rot = CreateRot();
        AgileComPointer<IRunningObjectTable> agile = new(rot, takeOwnership: true);

        agile.Dispose();
        Should.NotThrow(() => agile.Dispose());
    }

    [WindowsOnlyFact]
    public void AgileComPointer_QueryForIUnknown_Succeeds()
    {
        IRunningObjectTable* rot = CreateRot();
        using AgileComPointer<IRunningObjectTable> agile = new(rot, takeOwnership: true);

        using ComScope<IUnknown> unk = agile.GetInterface<IUnknown>();
        unk.IsNull.ShouldBeFalse();
    }
}
#endif
