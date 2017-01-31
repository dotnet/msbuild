using System;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(IncludeThis.GetMessage());
            Console.WriteLine(HelperBuiltIn1.GetMessage());
            Console.WriteLine(HelperBuiltIn2.GetMessage());
        }
    }
}
