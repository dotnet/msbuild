// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand
    {
        private const string UsageText = @"Usage: dotnet [host-options] [command] [arguments] [common-options]

Arguments:
  [command]             The command to execute
  [arguments]           Arguments to pass to the command
  [host-options]        Options specific to dotnet (host)
  [common-options]      Options common to all commands

Common options:
  -v|--verbose          Enable verbose output
  -h|--help             Show help 

Host options (passed before the command):
  -v|--verbose          Enable verbose output
  --version             Display .NET CLI Version Number
  --info                Display .NET CLI Info

Common Commands:
  new           Initialize a basic .NET project
  restore       Restore dependencies specified in the .NET project
  build         Builds a .NET project
  publish       Publishes a .NET project for deployment (including the runtime)
  run           Compiles and immediately executes a .NET project
  test          Runs unit tests using the test runner specified in the project
  pack          Creates a NuGet package";

        public static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }
            else
            {
                return Cli.Program.Main(new[] { args[0], "--help" });
            }
        }

        public static void PrintHelp()
        {
            PrintVersionHeader();
            Reporter.Output.WriteLine(UsageText);
        }

        public static void PrintVersionHeader()
        {
            var versionString = string.IsNullOrEmpty(Product.Version) ?
                string.Empty :
                $" ({Product.Version})";
            Reporter.Output.WriteLine(Product.LongName + versionString);
        }
    }
}
