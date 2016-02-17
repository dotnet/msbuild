using System;
using System.Reflection;

namespace TestAppWithContentPackage
{
    public class Program
    {
        public static int Main(string[] args)
        {
            foreach (var name in Assembly.GetEntryAssembly().GetManifestResourceNames())
            {
                Console.WriteLine(name);
            }
            Console.WriteLine(typeof(Foo).FullName);
            Console.WriteLine(typeof(MyNamespace.Util).FullName);
            return 0;
        }
    }
}
