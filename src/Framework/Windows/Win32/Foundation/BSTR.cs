// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Partial extension for CsWin32-generated BSTR struct to add IDisposable
// and safe construction from managed strings.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Windows.Win32.Foundation;

internal readonly unsafe partial struct BSTR : IDisposable
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="BSTR"/> struct.
    /// </summary>
    /// <param name="value">The managed string to convert to a <see cref="BSTR"/>.</param>
    public BSTR(string value)
     : this((char*)Marshal.StringToBSTR(value)) { }

    /// <summary>
    ///  Gets a value indicating whether the <see cref="BSTR"/> is null.
    /// </summary>
    public bool IsNull => Value is null;

    /// <summary>
    ///  Frees the underlying BSTR and clears this instance to <c>default</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Disposing is only safe when the caller disposes the <see cref="BSTR"/> in place
    ///   (e.g. via <c>using</c> on a local or stackalloc'd field). Do not dispose a by-value copy.
    ///  </para>
    /// </remarks>
    public void Dispose()
    {
        Marshal.FreeBSTR((nint)Value);
        Unsafe.AsRef(in this) = default;
    }
}
