using System;
using System.Runtime.InteropServices;

namespace LibraryWithRid
{
    public static class NativeMethod
    {
        [DllImport("libsqlite3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_libversion();

        [DllImport("libsqlite3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_dylibversion();

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_dllversion();
    }
}
