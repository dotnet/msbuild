using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace TestAppThatWaits
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.CancelKeyPress += HandleCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;

            Console.WriteLine(Process.GetCurrentProcess().Id);
            Console.Out.Flush();

            Thread.Sleep(Timeout.Infinite);
        }

        static void HandleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Interrupted!");
            AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
            Environment.Exit(42);
        }

        static void HandleProcessExit(object sender, EventArgs args)
        {
            Console.WriteLine("Terminating!");
            Environment.ExitCode = 43;
        }
    }
}
