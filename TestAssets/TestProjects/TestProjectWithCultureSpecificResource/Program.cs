using System;
using System.Resources;
using System.Reflection;
using System.Globalization;

namespace TestProjectWithCultureSpecificResource
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var rm = new ResourceManager(
                "TestProjectWithCultureSpecificResource.Strings",
                typeof(Program).GetTypeInfo().Assembly);

            Console.WriteLine(rm.GetString("hello"));
            Console.WriteLine(rm.GetString("hello", new CultureInfo("fr")));
        }
    }
}
