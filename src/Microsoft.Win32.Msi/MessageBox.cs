// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Message box style flags describing buttons, icons, and default buttons.
    /// </summary>
    /// <remarks>
    /// See winuser.h
    /// </remarks>
    [Flags]
    public enum MessageBox : uint
    {
        OK = 0x00000000,
        OKCANCEL = 0x00000001,
        ABORTRETRYIGNORE = 0x00000002,
        YESNOCANCEL = 0x00000003,
        YESNO = 0x00000004,
        RETRYCANCEL = 0x00000005,
        CANCELTRYCONTINUE = 0x00000006,

        ICONHAND = 0x00000010,
        ICONQUESTION = 0x00000020,
        ICONEXCLAMATION = 0x00000030,
        ICONASTERISK = 0x00000040,
        USERICON = 0x00000080,
        ICONWARNING = ICONEXCLAMATION,
        ICONERROR = ICONHAND,
        ICONINFORMATION = ICONASTERISK,
        ICONSTOP = ICONHAND,

        DEFBUTTON1 = 0x00000000,
        DEFBUTTON2 = 0x00000100,
        DEFBUTTON3 = 0x00000200,
        DEFBUTTON4 = 0x00000300,

        APPLMODAL = 0x00000000,
        SYSTEMMODAL = 0x00001000,
        TASKMODAL = 0x00002000,
        HELP = 0x00004000,

        NOFOCUS = 0x00008000,
        SETFOREGROUND = 0x00010000,
        DEFAULT_DESKTOP_ONLY = 0x00020000,

        TOPMOST = 0x00040000,
        RIGHT = 0x00080000,
        RTLREADING = 0x00100000,

        SERVICE_NOTIFICATION = 0x00200000,
        SERVICE_NOTIFICATION_NT3X = 0x00400000,

        TYPEMASK = 0x0000000F,
        ICONMASK = 0x000000F0,
        DEFMASK = 0x00000F00,
        MODEMASK = 0x00003000,
        MISCMASK = 0x0000C000,
    }
}
