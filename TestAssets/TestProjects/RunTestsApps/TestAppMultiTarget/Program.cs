using System;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
#if NET451
            Console.WriteLine("NET451");
#else
            Console.WriteLine("CoreCLR");
#endif
        }
    }
}
