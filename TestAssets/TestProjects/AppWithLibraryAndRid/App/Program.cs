using System;

namespace App
{
    class Program
    {
        static void Main(string[] args)
        {
            var valueFromNativeDependency = Library.NativeCode.InvokeNativeCodeAndReturnAString();

            var compileTimeRid = Library.NativeCode.GetRidStoredInAssemblyDescriptionAttribute();

            var valueFromPortableDependency = LibraryWithoutRid.PortableClass.GetHelloWorld();

            Console.WriteLine($"{valueFromNativeDependency} {compileTimeRid} {valueFromPortableDependency}");
        }
    }
}
