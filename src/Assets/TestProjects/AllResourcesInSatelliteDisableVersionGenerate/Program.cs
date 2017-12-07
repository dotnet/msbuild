using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace AllResourcesInSatellite
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");

            var resources = new ResourceManager("AllResourcesInSatellite.Strings", typeof(Program).GetTypeInfo().Assembly);

            Console.WriteLine(resources.GetString("HelloWorld"));
        }
    }
}
