// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable disable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Installer.Windows
{
    // Decompiled from WUA typelib
    [ComImport]
    [Guid("ADE87BF7-7B56-4275-8FAB-B9B0E591844B")]
    [TypeLibType(4304)]
    public interface ISystemInformation
    {
        [DispId(1610743809)]
        string OemHardwareSupportLink
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [DispId(1610743809)]
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(1610743810)]
        bool RebootRequired
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [DispId(1610743810)]
            get;
        }
    }

    [ComImport]
    [Guid("ADE87BF7-7B56-4275-8FAB-B9B0E591844B")]
    [CoClass(typeof(SystemInformationClass))]
    public interface SystemInformation : ISystemInformation
    {
    }

    [ComImport]
    [Guid("C01B9BA0-BEA7-41BA-B604-D0A36F469133")]
    [TypeLibType(2)]
    [ClassInterface(ClassInterfaceType.None)]
    public class SystemInformationClass : ISystemInformation, SystemInformation
    {
        [DispId(1610743809)]
        public extern virtual string OemHardwareSupportLink
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [DispId(1610743809)]
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(1610743810)]
        public extern virtual bool RebootRequired
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [DispId(1610743810)]
            get;
        }
    }
}
