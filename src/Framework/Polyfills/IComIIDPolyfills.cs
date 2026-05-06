// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Polyfills attaching IComIID to CsWin32-generated COM struct wrappers on
// .NET Framework / netstandard2.0. CsWin32 emits the IComIID interface and
// attaches it to generated structs on .NET 7+ (via static-abstract members).
// On older targets it does neither, so we provide:
//
//   1. The IComIID interface itself (instance-based; see IComIID.cs).
//   2. A partial struct per COM type that adds : IComIID and implements
//      Guid by returning the IID_Guid field that CsWin32 always emits.
//
// Each new manual or generated COM type used through ComScope<T> on
// .NET Framework needs an entry here.
//
// See https://github.com/dotnet/winforms/blob/main/src/System.Private.Windows.Core/src/Framework/Windows/Win32/System/Com/IDataObject.cs
// for the same pattern in dotnet/winforms.

#if !NET

using System;

namespace Windows.Win32.System.Com;

internal partial struct IUnknown : IComIID
{
    readonly Guid IComIID.Guid => IID_Guid;
}

internal partial struct IRunningObjectTable : IComIID
{
    readonly Guid IComIID.Guid => IID_Guid;
}

internal partial struct IMoniker : IComIID
{
    readonly Guid IComIID.Guid => IID_Guid;
}

internal partial struct IErrorInfo : IComIID
{
    readonly Guid IComIID.Guid => IID_Guid;
}

#endif
