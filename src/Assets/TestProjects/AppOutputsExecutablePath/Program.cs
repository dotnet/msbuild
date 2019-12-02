using System;
using System.Diagnostics;

namespace foo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(Process.GetCurrentProcess().MainModule.FileName);
        }
    }
}
