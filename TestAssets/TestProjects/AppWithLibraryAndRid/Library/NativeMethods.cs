
using System;
using System.Runtime.InteropServices;

namespace Library
{
    public static class NativeMethod
    {
        [DllImport("libsqlite3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_libversion();
    }
}