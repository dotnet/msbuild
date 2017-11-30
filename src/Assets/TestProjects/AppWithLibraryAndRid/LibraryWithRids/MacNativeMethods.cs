using System;
using System.Runtime.InteropServices;

namespace LibraryWithRids
{
    public static class MacNativeMethods
    {
        [DllImport("libsqlite3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_libversion();
    }
}
