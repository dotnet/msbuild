using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LibraryWithRids
{
    public class NativeCode
    {
        [DllImport("libuv", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uv_version_string();

        public static string InvokeNativeCodeAndReturnAString()
        {
            return Marshal.PtrToStringAnsi(uv_version_string());
        }

        public static string GetRidStoredInAssemblyDescriptionAttribute()
        {
            return typeof(NativeCode)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute<AssemblyDescriptionAttribute>()
                ?.Description;
        }
    }
}
