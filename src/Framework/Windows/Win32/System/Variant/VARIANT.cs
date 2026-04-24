// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com.StructuredStorage;
using static Windows.Win32.System.Variant.VARENUM;

namespace Windows.Win32.System.Variant;

internal unsafe partial struct VARIANT : IDisposable
{
    // See WinForms sources for additional VARIANT functionality when needed.

    /// <summary>
    ///  Gets a value indicating whether this <see cref="VARIANT"/> is empty.
    /// </summary>
    public bool IsEmpty => vt == VT_EMPTY && data.llVal == 0;

    /// <summary>
    ///  Gets the <see cref="VARENUM"/> type of this <see cref="VARIANT"/>.
    /// </summary>
    public VARENUM Type => vt & VT_TYPEMASK;

    /// <summary>
    ///  Gets a value indicating whether this <see cref="VARIANT"/> is a by-reference value.
    /// </summary>
    public bool Byref => vt.HasFlag(VT_BYREF);

    /// <summary>
    ///  Gets a reference to the <see cref="VARENUM"/> value type field.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Use <see cref="Type"/> to read the type as some of the bits overlap with <see cref="VT_DECIMAL"/> data.
    ///  </para>
    /// </remarks>
    [UnscopedRef]
    public ref VARENUM vt => ref Anonymous.Anonymous.vt;

    /// <summary>
    ///  Gets a reference to the data union of this <see cref="VARIANT"/>.
    /// </summary>
    [UnscopedRef]
    public ref _Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union data => ref Anonymous.Anonymous.Anonymous;

    /// <summary>
    ///  Releases resources used by this <see cref="VARIANT"/>.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public void Dispose() => Clear();

    /// <summary>
    ///  Clears the value of this <see cref="VARIANT"/>, releasing any associated resources.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public void Clear()
    {
        // PropVariantClear is essentially a superset of VariantClear it calls CoTaskMemFree on the following types:
        //
        //     - VT_LPWSTR, VT_LPSTR, VT_CLSID (psvVal)
        //     - VT_BSTR_BLOB (bstrblobVal.pData)
        //     - VT_CF (pclipdata->pClipData, pclipdata)
        //     - VT_BLOB, VT_BLOB_OBJECT (blob.pData)
        //     - VT_STREAM, VT_STREAMED_OBJECT (pStream)
        //     - VT_VERSIONED_STREAM (pVersionedStream->pStream, pVersionedStream)
        //     - VT_STORAGE, VT_STORED_OBJECT (pStorage)
        //
        // If the VARTYPE is a VT_VECTOR, the contents are cleared as above and CoTaskMemFree is also called on
        // cabstr.pElems.
        //
        // https://learn.microsoft.com/windows/win32/api/oleauto/nf-oleauto-variantclear#remarks
        //
        //     - VT_BSTR (SysFreeString)
        //     - VT_DISPATCH / VT_UNKOWN (->Release(), if not VT_BYREF)

        if (IsEmpty)
        {
            return;
        }

        fixed (void* t = &this)
        {
            PInvoke.PropVariantClear((PROPVARIANT*)t);
        }

        vt = VT_EMPTY;
        data = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BSTR(VARIANT value) =>
        value.vt == VT_BSTR ? value.data.bstrVal : ThrowInvalidCast<BSTR>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T ThrowInvalidCast<T>() => throw new InvalidCastException();
}
