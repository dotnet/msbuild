// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        private const string HelpText = @".NET Command Line Interface
Usage: dotnet [common-options] [command] [arguments]

Arguments:
  [command]     The command to execute
  [arguments]   Arguments to pass to the command

Common Options (passed before the command):
  -v|--verbose  Enable verbose output

Common Commands:
  new           Initialize a basic .NET project
  restore       Restore dependencies specified in the .NET project
  compile       Compiles a .NET project
  publish       Publishes a .NET project for deployment (including the runtime)
  run           Compiles and immediately executes a .NET project
  repl          Launch an interactive session (read, eval, print, loop)
  pack          Creates a NuGet package";

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            // CommandLineApplication is a bit restrictive, so we parse things ourselves here. Individual apps should use CLA.
            var verbose = false;
            var success = true;    
            var command = string.Empty;
            var lastArg = 0;
            for (; lastArg < args.Length; lastArg++)
            {
                if (IsArg(args[lastArg], "v", "verbose"))
                {
                    verbose = true;
                }
                else if (IsArg(args[lastArg], "h", "help"))
                {
                    PrintHelp();
                    return 0;
                }
                else if (args[lastArg].StartsWith("-"))
                {
                    Reporter.Error.WriteLine($"Unknown option: {args[lastArg]}");
                    success = false;
                }
                else
                {
                    // It's the command, and we're done!
                    command = args[lastArg];
                    break;
                }
            }
            if (!success) {
                PrintHelp();
                return 1;
            }

            var appArgs = (lastArg + 1) >= args.Length ? Enumerable.Empty<string>() : args.Skip(lastArg + 1).ToArray();

            if (string.IsNullOrEmpty(command) || command.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                return RunHelpCommand(appArgs);
            }

            return Command.Create("dotnet-" + command, appArgs, new NuGetFramework("DNXCore", Version.Parse("5.0")))
                .EnvironmentVariable(CommandContext.Variables.Verbose, verbose.ToString())
                .EnvironmentVariable(CommandContext.Variables.AnsiPassThru, bool.TrueString)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute()
                .ExitCode;
        }

        private static int RunHelpCommand(IEnumerable<string> appArgs)
        {
            if (appArgs.Any())
            {
                return Command.Create("dotnet-" + appArgs.First(), "--help")
                    .ForwardStdErr()
                    .ForwardStdOut()
                    .Execute()
                    .ExitCode;
            }
            else
            {
                PrintHelp();
                return 0;
            }
        }

        private static void PrintHelp()
        {
            Reporter.Output.WriteLine(HelpText);
        }

        private static bool IsArg(string candidate, string shortName, string longName)
        {
            return candidate.Equals("-" + shortName) || candidate.Equals("--" + longName);
        }
    }
}
