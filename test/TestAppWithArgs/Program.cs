using System;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Console.WriteLine($"I was passed {args.Length} args:");
            foreach (var arg in args)
            {
                Console.WriteLine($"arg: [{arg}]");
            }
        }
    }
}
