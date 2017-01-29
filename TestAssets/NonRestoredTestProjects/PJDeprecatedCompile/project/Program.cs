using System;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(IncludeThis.GetMessage());
            Console.WriteLine(Helper1.GetMessage());
            Console.WriteLine(Helper2.GetMessage());
        }
    }
}
