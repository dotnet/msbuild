using System;
using System.Runtime.InteropServices;

namespace LibraryWithRid
{
    public static class LinuxNativeMethods
    {
        [DllImport("libsqlite3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_libversion();
    }
}
