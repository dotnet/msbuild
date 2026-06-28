// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from dotnet/sdk to provide COM object activation without
// Activator.CreateInstance (AOT-compatible).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
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
    private const string DllGetClassObjectExportName = "DllGetClassObject";

    /// <summary>
    ///  Name of the non-standard class-object entry point exported by the .NET
    ///  Framework CLR (<c>clr.dll</c>). Pass to
    ///  <see cref="TryCreateFromModule(string, Guid, string, out ComClassFactory, out HRESULT)"/>
    ///  to activate CLR-hosted CLSIDs without triggering the <c>mscoree.dll</c> shim.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <c>CLSID_CorMetaDataDispenser</c> and the other CLR-hosted legacy COM CLSIDs are registered
    ///   with <c>mscoree.dll</c> as their <c>InprocServer32</c>. A raw <c>CoCreateInstance</c> therefore
    ///   loads the shim, which then calls <c>LoadLibraryShim</c> to bind a runtime before delegating
    ///   activation to that runtime's class factory.
    ///  </para>
    ///  <para>
    ///   In hosts where the CLR was loaded through the native hosting APIs
    ///   (<c>CLRCreateInstance</c> / <c>ICLRRuntimeHost</c>) rather than the standard
    ///   <c>mscoree</c> entry point that runs when a managed <c>.exe</c> is launched
    ///   normally, the shim's bound-runtime state is not initialized and
    ///   <c>LoadLibraryShim</c> fails with <c>CLR_E_SHIM_RUNTIMELOAD (0x80131700)</c>.
    ///   (One concrete example is a native test harness that embeds MSBuild in-process
    ///   via <c>BuildManager</c>.) Calling <c>DllGetClassObjectInternal</c> on the
    ///   already-loaded <c>clr.dll</c> bypasses the shim entirely and delegates straight
    ///   to the CLR's class factory — which is what the CLR's own managed-COM activation
    ///   does internally for CLSIDs on its hosted-CLSID list.
    ///  </para>
    /// </remarks>
    internal const string ClrDllGetClassObjectInternalExportName = "DllGetClassObjectInternal";

    private IClassFactory* _classFactory;

    private ComClassFactory(IClassFactory* classFactory)
    {
        _classFactory = classFactory;
    }

    /// <summary>
    ///  Attempts to get a class factory for the given COM class ID via the standard
    ///  <c>CoGetClassObject</c> path. Goes through the COM registry and may load a
    ///  server DLL.
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
    ///  Attempts to get a class factory by calling the named module's standard
    ///  <c>DllGetClassObject</c> export directly, bypassing the COM registry and
    ///  any shim that <c>CoGetClassObject</c> would normally invoke.
    /// </summary>
    /// <param name="moduleName">
    ///  Module to resolve. May be a bare DLL name. The method first tries
    ///  <c>GetModuleHandle</c> (no refcount, no DLL-search path) and falls back to
    ///  <c>LoadLibrary</c> only if the module is not already loaded.
    /// </param>
    /// <param name="classId">CLSID of the COM class to activate.</param>
    /// <param name="factory">On success, the wrapped class factory.</param>
    /// <param name="result">HRESULT from the underlying call.</param>
    public static bool TryCreateFromModule(
        string moduleName,
        Guid classId,
        [NotNullWhen(true)] out ComClassFactory? factory,
        out HRESULT result)
        => TryCreateFromModule(moduleName, classId, DllGetClassObjectExportName, out factory, out result);

    /// <param name="exportName">
    ///  Name of the class-object entry point exported by the module. Pass
    ///  <see cref="ClrDllGetClassObjectInternalExportName"/> to activate
    ///  CLR-hosted CLSIDs without triggering the <c>mscoree.dll</c> shim.
    /// </param>
    /// <inheritdoc cref="TryCreateFromModule(string, Guid, out ComClassFactory, out HRESULT)"/>
#pragma warning disable CS1573 // analyzer doesn't see params merged from <inheritdoc>
    public static bool TryCreateFromModule(
        string moduleName,
        Guid classId,
        string exportName,
        [NotNullWhen(true)] out ComClassFactory? factory,
        out HRESULT result)
#pragma warning restore CS1573
    {
        factory = null;
        result = HRESULT.S_OK;

        // Prefer GetModuleHandle: it asserts "module must already be loaded" (true for
        // any DLL implementing a CLSID we want to activate in this process), does not
        // touch the loader refcount, and sidesteps DLL-search-order concerns. Fall back
        // to LoadLibrary only when the module isn't already mapped.
        HMODULE module;
        bool ownsModuleRef;
        fixed (char* pModuleName = moduleName)
        {
            module = PInvoke.GetModuleHandle(pModuleName);
            if (module.IsNull)
            {
                module = PInvoke.LoadLibrary(pModuleName);
                ownsModuleRef = true;
            }
            else
            {
                ownsModuleRef = false;
            }
        }

        if (module.IsNull)
        {
            result = (HRESULT)Marshal.GetHRForLastWin32Error();
            if (result.Succeeded)
            {
                result = (HRESULT)unchecked((int)0x80004005); // E_FAIL
            }

            return false;
        }

        // On success we keep the class factory alive and the module must remain loaded
        // for its vtable to be callable. If we acquired the only ref via LoadLibrary,
        // intentionally leak it: a single unmatched ref over the lifetime of this
        // ComClassFactory is cheaper than tracking the HMODULE through Dispose.
        bool keepModuleLoaded = false;
        try
        {
            FARPROC proc = PInvoke.GetProcAddress(module, exportName);
            if (proc.IsNull)
            {
                // GetProcAddress is not required to call SetLastError; the returned
                // HRESULT can therefore be S_OK on failure. Force a failing code so
                // callers that branch on result.Failed see a deterministic value.
                result = (HRESULT)Marshal.GetHRForLastWin32Error();
                if (result.Succeeded)
                {
                    result = (HRESULT)unchecked((int)0x80004005); // E_FAIL
                }

                return false;
            }

            IClassFactory* classFactory;
            Guid iid = typeof(IClassFactory).GUID;

            // DllGetClassObject is STDAPI (__stdcall) in COM headers; on x86 that
            // matters, on x64 there's a single AMD64 calling convention.
            result = ((delegate* unmanaged[Stdcall]<Guid*, Guid*, void**, HRESULT>)proc.Value)(
                &classId,
                &iid,
                (void**)&classFactory);

            if (result.Failed || classFactory is null)
            {
                return false;
            }

            factory = new ComClassFactory(classFactory);
            keepModuleLoaded = true;
            return true;
        }
        finally
        {
            if (!keepModuleLoaded && ownsModuleRef)
            {
                PInvoke.FreeLibrary(module);
            }
        }
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
