// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Windows.Win32.System.Diagnostics.Debug;

namespace Windows.Win32.Foundation;

internal partial struct HRESULT
{
    /// <summary>
    ///  Convert a Windows error to an <see cref="HRESULT"/>. [HRESULT_FROM_WIN32]
    /// </summary>
    public static explicit operator HRESULT(WIN32_ERROR error) =>
        // https://learn.microsoft.com/windows/win32/api/winerror/nf-winerror-hresult_from_win32
        // return (HRESULT)(x) <= 0 ? (HRESULT)(x) : (HRESULT) (((x) & 0x0000FFFF) | (FACILITY_WIN32 << 16) | 0x80000000);
        (HRESULT)(int)((int)error <= 0 ? (int)error : (((int)error & 0x0000FFFF) | ((int)FACILITY_CODE.FACILITY_WIN32 << 16) | 0x80000000));

    /// <summary>
    ///  Create an <see cref="HRESULT"/> from the last Windows error.
    /// </summary>
    public static HRESULT FromLastError() => (HRESULT)(WIN32_ERROR)Marshal.GetLastWin32Error();
}
