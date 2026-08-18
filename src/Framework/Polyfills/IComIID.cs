// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Polyfill IComIID for .NET Framework / netstandard2.0. CsWin32 emits this
// interface (with static-abstract members) on .NET 7+ and attaches it to
// every generated COM struct. On older targets we provide an instance-based
// version, plus per-struct partials in this folder that attach it to
// CsWin32-generated structs (see Win32/System/Com/IComIIDPolyfills.cs).
//
// IComIID is a known case that always needs polyfilling for .NET Framework
// support — every new COM struct used through ComScope<T> needs both pieces.

#if !NET

using System;

namespace Windows.Win32;

/// <summary>
///  Common interface for COM interface wrapping structs.
///  On .NET 7+, CsWin32 generates this as a static-abstract interface and
///  attaches it to generated structs automatically. On older targets we use
///  an instance-based shape and attach it via partial declarations.
/// </summary>
internal interface IComIID
{
    Guid Guid { get; }
}

#endif
