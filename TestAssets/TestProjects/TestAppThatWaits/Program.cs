using System;
using System.Diagnostics;
using System.Threading;

namespace TestAppThatWaits
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.CancelKeyPress += HandleCancelKeyPress;
            Console.WriteLine(Process.GetCurrentProcess().Id);
            Console.Out.Flush();
            Console.Read();
            Thread.Sleep(10000);
        }

        static void HandleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Interrupted!");
            Environment.Exit(42);
        }
    }
}
