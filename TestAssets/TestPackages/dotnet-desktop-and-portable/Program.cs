using System;
using System.Reflection;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
#if NET451
            Console.WriteLine($"Hello {string.Join(" ", args)} From .NETFramework,Version=v4.5.1");
#elif NETCOREAPP1_0
            Console.WriteLine($"Hello {string.Join(" ", args)} From .NETCoreApp,Version=v1.0");
#endif
            var currentAssemblyPath = typeof(ConsoleApplication.Program).GetTypeInfo().Assembly.Location;
            Console.WriteLine($"Current Assembly Directory - {currentAssemblyPath}");
        }
    }
}
