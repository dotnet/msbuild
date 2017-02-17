using System;

namespace App
{
    class Program
    {
        static void Main(string[] args)
        {
            var libraryWithRidNativeOutput = LibraryWithRid.NativeCode.InvokeNativeCodeAndReturnAString();

            var libraryWithRidsNativeOutput = LibraryWithRid.NativeCode.InvokeNativeCodeAndReturnAString();

            var libraryWithRidCompileTimeRid = LibraryWithRid.NativeCode.GetRidStoredInAssemblyDescriptionAttribute();

            var libraryWithRidsCompileTimeRid = LibraryWithRids.NativeCode.GetRidStoredInAssemblyDescriptionAttribute();

            var libraryWithRidStatus = $"{libraryWithRidNativeOutput} {libraryWithRidCompileTimeRid}";

            var libraryWithRidsStatus = $"{libraryWithRidsNativeOutput} {libraryWithRidsCompileTimeRid}";

            var portableLibraryStatus = LibraryWithoutRid.PortableClass.GetHelloWorld();

            Console.WriteLine($"{libraryWithRidStatus} {libraryWithRidsStatus} {portableLibraryStatus}");
        }
    }
}
