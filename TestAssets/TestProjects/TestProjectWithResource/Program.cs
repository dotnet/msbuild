using System;
using System.Resources;
using System.Reflection;

namespace TestProjectWithResource
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var rm = new ResourceManager(
                "TestProjectWithResource.Strings",
                typeof(Program).GetTypeInfo().Assembly);

            Console.WriteLine(rm.GetString("hello"));
        }
    }
}
