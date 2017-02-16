using System;
using System.Runtime.InteropServices;

namespace LibraryWithRid
{
    public static class WindowsNativeMethods
    {
        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_libversion();
    }
}
