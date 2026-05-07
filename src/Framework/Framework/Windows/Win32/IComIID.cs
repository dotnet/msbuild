// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file is only compiled for .NET Framework (net472) where static abstract
// interface members are not available. On .NET 7+, CsWin32 generates IComIID
// with static abstract members directly.

using System;

namespace Windows.Win32;

/// <summary>
///  Common interface for COM interface wrapping structs.
///  On .NET 7+, this is provided by CsWin32 as a static abstract interface.
///  On .NET Framework, we provide this instance-based version.
/// </summary>
internal interface IComIID
{
    Guid Guid { get; }
}
