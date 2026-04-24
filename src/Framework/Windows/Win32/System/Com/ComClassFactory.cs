// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from dotnet/sdk to provide COM object activation without
// Activator.CreateInstance (AOT-compatible).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;

namespace Windows.Win32.System.Com;

/// <summary>
///  Wraps a native <see cref="IClassFactory"/> pointer to create COM objects
///  without going through <see cref="Activator.CreateInstance(Type)"/> or
///  <see cref="Type.GetTypeFromCLSID(Guid)"/>.
/// </summary>
[SupportedOSPlatform("windows6.1")]
internal sealed unsafe class ComClassFactory : IDisposable
{
    private IClassFactory* _classFactory;

    private ComClassFactory(IClassFactory* classFactory)
    {
        _classFactory = classFactory;
    }

    /// <summary>
    ///  Attempts to get a class factory for the given COM class ID.
    /// </summary>
    public static bool TryCreate(
        Guid classId,
        [NotNullWhen(true)] out ComClassFactory? factory,
        out HRESULT result)
    {
        IClassFactory* classFactory;
        Guid iid = typeof(IClassFactory).GUID;
        result = PInvoke.CoGetClassObject(
            &classId,
            CLSCTX.CLSCTX_INPROC_SERVER,
            null,
            &iid,
            (void**)&classFactory);

        if (result.Failed || classFactory is null)
        {
            factory = null;
            return false;
        }

        factory = new ComClassFactory(classFactory);
        return true;
    }

    /// <summary>
    ///  Creates an instance of the COM class via the class factory.
    /// </summary>
    public ComScope<TInterface> TryCreateInstance<TInterface>(out HRESULT result)
        where TInterface : unmanaged, IComIID
    {
        Guid iid = IID.Get<TInterface>();
        ComScope<TInterface> scope = default;
        result = _classFactory->CreateInstance(null, &iid, scope);
        return scope;
    }

    public void Dispose()
    {
        if (_classFactory is not null)
        {
            _classFactory->Release();
            _classFactory = null;
        }
    }
}
