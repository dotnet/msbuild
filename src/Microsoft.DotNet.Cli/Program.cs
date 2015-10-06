using System;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                // Handle missing args
                Console.Error.WriteLine("TODO: Help");
                return 1;
            }

            return Command.Create("dotnet-" + args[0], args.Skip(1))
                .ForwardStdErr(Console.Error)
                .ForwardStdOut(Console.Out)
                .RunAsync()
                .Result
                .ExitCode;
        }
    }
}
