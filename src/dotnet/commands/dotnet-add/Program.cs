// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using NuGet.Frameworks;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;

namespace Microsoft.DotNet.Tools.Add
{
    public class AddCommand
    {
        public const string HelpText = @".NET Add Command

Usage: dotnet add [options] <object> <command> [[--] <arg>...]]

Options:
  -h|--help  Show help information

Arguments:
  <object>   The object of the operation. If a project file is not specified, it defaults to the current directory.
  <command>  Command to be executed on <object>.

Args:
  Any extra arguments passed to the command. Use `dotnet add <command> --help` to get help about these arguments.

Commands:
  p2p        Add project to project (p2p) reference to a project";

        private static Dictionary<string, Func<string[], int>> s_builtIns = new Dictionary<string, Func<string[], int>>
        {
            ["p2p"] = AddProjectToProjectReferenceCommand.Run,
        };

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Reporter.Output.WriteLine(HelpText);
                return 0;
            }

            string commandObject;
            string command;
            if (IsValidCommandName(args[0]))
            {
                command = args[0];
                commandObject = GetCurrentDirectoryWithDirSeparator();
                args = args.Skip(1).Prepend(commandObject).ToArray();
            }
            else if (args.Length == 1)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RequiredArgumentNotPassed, "<command>").Red());
                Reporter.Output.WriteLine(HelpText);
                return 1;
            }
            else
            {
                commandObject = args[0];
                command = args[1];

                args = args.Skip(2).Prepend(commandObject).ToArray();
            }

            Func<string[], int> builtin;
            if (s_builtIns.TryGetValue(command, out builtin))
            {
                return builtin(args);
            }

            Reporter.Error.WriteLine(string.Format(LocalizableStrings.RequiredArgumentIsInvalid, "<command>").Red());
            Reporter.Output.WriteLine(HelpText);
            return 1;
        }

        private static bool IsValidCommandName(string s)
        {
            return s_builtIns.ContainsKey(s);
        }

        private static string GetCurrentDirectoryWithDirSeparator()
        {
            string ret = Directory.GetCurrentDirectory();
            if (ret[ret.Length - 1] != Path.DirectorySeparatorChar)
            {
                ret += Path.DirectorySeparatorChar;
            }

            return ret;
        }
    }
}
