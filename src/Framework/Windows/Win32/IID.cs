// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from dotnet/sdk to provide IID lookup for CsWin32 struct-based COM interfaces.

using System;
using System.Runtime.CompilerServices;

namespace Windows.Win32;

internal static unsafe class IID
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid Get<T>() where T : unmanaged, IComIID
    {
#if NETFRAMEWORK || NETSTANDARD
        // On .NET Framework and netstandard2.0, IComIID is instance-based.
        return default(T).Guid;
#else
        // On .NET, CsWin32 generates IComIID with static abstract Guid property.
        return T.Guid;
#endif
    }
}
