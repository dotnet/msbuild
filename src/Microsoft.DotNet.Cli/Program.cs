using System;
using System.Diagnostics;
using System.IO;
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
                PrintCommandList();
                return 1;
            }

            if (args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length > 1)
                {
                    return Command.Create("dotnet-" + args[1], "--help")
                        .ForwardStdErr()
                        .ForwardStdOut()
                        .Execute()
                        .ExitCode;
                }
                else
                {
                    PrintCommandList();
                    return 0;
                }
            }
            else
            {
                return Command.Create("dotnet-" + args[0], args.Skip(1))
                    .ForwardStdErr()
                    .ForwardStdOut()
                    .Execute()
                    .ExitCode;
            }
        }

        private static void PrintCommandList()
        {
            Console.WriteLine("Some dotnet Commands (use 'dotnet help <command>' to get help):");
            Console.WriteLine("* compile - Compiles code");
            Console.WriteLine("* publish - Publishes a project to a self-contained application");
            Console.WriteLine("* run - Publishes and immediately runs a project");
        }
    }
}
