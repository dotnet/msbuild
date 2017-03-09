using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LibraryWithRids
{
    public class NativeCode
    {
        public static string InvokeNativeCodeAndReturnAString()
        {
            switch(GetRidStoredInAssemblyDescriptionAttribute())
            {
                case "'ubuntu.16.04-x64'":
                case "'ubuntu.14.04-x64'":
                    return Marshal.PtrToStringAnsi(LinuxNativeMethods.sqlite3_libversion());
                case "'osx.10.11-x64'": 
                case "'osx.10.12-x64'":
                    return Marshal.PtrToStringAnsi(MacNativeMethods.sqlite3_libversion());
                case "'win10-x64'":
                case "'win81-x64'":
                    return Marshal.PtrToStringAnsi(WindowsNativeMethods.sqlite3_libversion());
                default:
                    return "Unexpected RID. Cannot find sqlite3.";
            }            
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
