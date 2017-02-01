using System;
using System.Linq;
using System.Reflection;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var thisAssembly = typeof(Program).GetTypeInfo().Assembly;
            var resources = from resourceName in thisAssembly.GetManifestResourceNames()
                            select resourceName;

            var resourceNames = string.Join(",", resources);
            Console.WriteLine($"{resources.Count()} Resources Found: {resourceNames}");
        }
    }
}
