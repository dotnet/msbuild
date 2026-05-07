// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// CsWin32 generates these internal COM interface structs with
// [MarshalAs(UnmanagedType.SafeArray, SafeArraySubTypes = new[] { ... })] attributes
// whose array argument is not CLS-compliant, producing CS3016 warnings under
// [assembly: CLSCompliant(true)]. Mark each generated type [CLSCompliant(false)]
// here via a partial declaration so the warning is expressed semantically rather
// than suppressed wholesale.
//
// A [SuppressMessage] in a global-suppressions file is not an option: the C#
// compiler does not honor SuppressMessageAttribute for CSxxxx warnings, only
// for analyzer diagnostics. See https://github.com/dotnet/roslyn/issues/68526.
//
// While this approach works, having the attribute on the generated types produces CS3019
// warnings (as the attribute doesn't make sense on internals). We disable this in the
// .editorconfig, for anything in the CsWin32 subfolders.

using System;

namespace Windows.Win32.System.Com
{
    [CLSCompliant(false)]
    internal partial struct IClassFactory
    {
    }

    [CLSCompliant(false)]
    internal partial struct ISequentialStream
    {
    }

    [CLSCompliant(false)]
    internal partial struct IStream
    {
    }

    [CLSCompliant(false)]
    internal partial struct ITypeComp
    {
    }

    [CLSCompliant(false)]
    internal partial struct ITypeInfo
    {
    }

    [CLSCompliant(false)]
    internal partial struct ITypeLib
    {
    }
}

namespace Windows.Win32.System.Com.StructuredStorage
{
    [CLSCompliant(false)]
    internal partial struct IEnumSTATSTG
    {
    }

    [CLSCompliant(false)]
    internal partial struct IStorage
    {
    }
}

namespace Windows.Win32.System.Ole
{
    [CLSCompliant(false)]
    internal partial struct IRecordInfo
    {
    }
}

namespace Windows.Win32.System.Diagnostics.Debug.Extensions
{
    [CLSCompliant(false)]
    internal partial struct IDebugClient
    {
    }

    [CLSCompliant(false)]
    internal partial struct IDebugClient4
    {
    }

    [CLSCompliant(false)]
    internal partial struct IDebugBreakpoint
    {
    }

    [CLSCompliant(false)]
    internal partial struct IDebugOutputCallbacks
    {
    }

    [CLSCompliant(false)]
    internal partial struct IDebugInputCallbacks
    {
    }

    [CLSCompliant(false)]
    internal partial struct IDebugEventCallbacks
    {
    }
}
