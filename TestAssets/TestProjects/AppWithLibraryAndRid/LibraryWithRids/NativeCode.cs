using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LibraryWithRids
{
    public class NativeCode
    {
        public static string InvokeNativeCodeAndReturnAString()
        {
            return Marshal.PtrToStringAnsi(NativeMethod.sqlite3_libversion());
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
