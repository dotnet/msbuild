// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Adapted from dotnet/winforms (System.Private.Windows.Core). See:
// https://github.com/dotnet/winforms/blob/main/src/System.Private.Windows.Core/src/Windows/Win32/System/Com/AgileComPointer.cs

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using Windows.Win32.Foundation;

namespace Windows.Win32.System.Com;

/// <summary>
///  Finalizable wrapper for COM pointers stored in managed fields. Stores the
///  interface in the Global Interface Table so it can be retrieved from any
///  thread (thread-agile) and is safely released if the owner is never disposed.
/// </summary>
/// <remarks>
///  <para>
///   Any COM pointer stored in a managed class field should be wrapped in an
///   <see cref="AgileComPointer{TInterface}"/>. Direct retention of a raw
///   <c>TInterface*</c> field is an agility hazard and risks ref-count leaks
///   if the owner is never disposed.
///  </para>
///  <para>
///   Always dispose explicitly; finalization is a fallback. To safely access
///   the underlying COM object, call <see cref="GetInterface()"/> inside a
///   <c>using</c> statement; the returned <see cref="ComScope{T}"/> releases
///   the reference when disposed.
///  </para>
/// </remarks>
internal unsafe class AgileComPointer<TInterface> : IDisposable
    where TInterface : unmanaged, IComIID
{
    private uint _cookie;

    /// <summary>
    ///  Creates an <see cref="AgileComPointer{TInterface}"/> for <paramref name="interface"/>.
    /// </summary>
    /// <param name="interface">The COM interface pointer.</param>
    /// <param name="takeOwnership">
    ///  If <see langword="true"/>, releases the caller's reference after the GIT
    ///  registers (and AddRefs) its own. The caller should not use the pointer after this.
    /// </param>
    [SupportedOSPlatform("windows5.0")]
    public AgileComPointer(TInterface* @interface, bool takeOwnership)
    {
        try
        {
            _cookie = GlobalInterfaceTable.RegisterInterface(@interface);
        }
        catch
        {
            // No need to clean if we couldn't register.
            GC.SuppressFinalize(this);
            throw;
        }
        finally
        {
            if (takeOwnership)
            {
                // The GIT added a ref; release the caller's ref so the GIT entry effectively owns it.
                ((IUnknown*)@interface)->Release();
            }
        }
    }

    /// <summary>
    ///  Gets the default interface. Throws on failure.
    /// </summary>
    [SupportedOSPlatform("windows5.0")]
    public ComScope<TInterface> GetInterface()
    {
        ComScope<TInterface> scope = GlobalInterfaceTable.GetInterface<TInterface>(_cookie, out HRESULT hr);
        hr.ThrowOnFailure();
        return scope;
    }

    /// <summary>
    ///  Tries to get the default interface; returns the <see cref="HRESULT"/>.
    /// </summary>
    [SupportedOSPlatform("windows5.0")]
    public ComScope<TInterface> TryGetInterface(out HRESULT hr)
        => GlobalInterfaceTable.GetInterface<TInterface>(_cookie, out hr);

    /// <summary>
    ///  Gets <typeparamref name="TAsInterface"/> via the registered cookie. Throws on failure.
    /// </summary>
    [SupportedOSPlatform("windows5.0")]
    public ComScope<TAsInterface> GetInterface<TAsInterface>()
        where TAsInterface : unmanaged, IComIID
    {
        ComScope<TAsInterface> scope = TryGetInterface<TAsInterface>(out HRESULT hr);
        hr.ThrowOnFailure();
        return scope;
    }

    /// <summary>
    ///  Tries to get <typeparamref name="TAsInterface"/>; returns the <see cref="HRESULT"/>.
    /// </summary>
    [SupportedOSPlatform("windows5.0")]
    public ComScope<TAsInterface> TryGetInterface<TAsInterface>(out HRESULT hr)
        where TAsInterface : unmanaged, IComIID
        => GlobalInterfaceTable.GetInterface<TAsInterface>(_cookie, out hr);

    [SupportedOSPlatform("windows5.0")]
    ~AgileComPointer()
    {
        Dispose(disposing: false);
    }

    [SupportedOSPlatform("windows5.0")]
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    [SupportedOSPlatform("windows5.0")]
    protected virtual void Dispose(bool disposing)
    {
        // Clear the cookie before revoking, to guard against re-entry.
        // Interlocked.Exchange(ref uint, ...) isn't available on net472, so use the
        // matching int overload via Unsafe.As.
        uint cookie = (uint)Interlocked.Exchange(
            ref Unsafe.As<uint, int>(ref _cookie),
            0);
        if (cookie == 0)
        {
            return;
        }

        GlobalInterfaceTable.RevokeInterface(cookie);
    }
}
