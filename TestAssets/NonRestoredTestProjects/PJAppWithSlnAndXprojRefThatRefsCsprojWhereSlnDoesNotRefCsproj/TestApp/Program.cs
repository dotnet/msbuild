using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello from main");
            Console.WriteLine(TestLibrary.Lib.GetMessage());
            Console.WriteLine(subdir.Helper.GetMessage());
        }
    }
}
