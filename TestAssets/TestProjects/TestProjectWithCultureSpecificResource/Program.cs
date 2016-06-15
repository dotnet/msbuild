using System;
using System.Resources;
using System.Reflection;
using System.Globalization;

namespace TestProjectWithCultureSpecificResource
{
    public class Program
    {
        // This method is consumed by load context tests
        public static string GetMessage()
        {
            var rm = new ResourceManager(
                "TestProjectWithCultureSpecificResource.Strings",
                typeof(Program).GetTypeInfo().Assembly);

            return rm.GetString("hello") + Environment.NewLine + rm.GetString("hello", new CultureInfo("fr"));
        }

        public static void Main(string[] args)
        {
            Console.WriteLine(GetMessage());
        }
    }
}
