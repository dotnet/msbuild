// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools
{
    public abstract class DispatchCommand
    {
        protected abstract string HelpText { get; }
        protected abstract Dictionary<string, Func<string[], int>> BuiltInCommands { get; }

        public int Run(string[] args)
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
                Reporter.Error.WriteLine(string.Format(CommonLocalizableStrings.RequiredArgumentNotPassed, "<command>").Red());
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
            if (BuiltInCommands.TryGetValue(command, out builtin))
            {
                return builtin(args);
            }

            Reporter.Error.WriteLine(string.Format(CommonLocalizableStrings.RequiredArgumentIsInvalid, "<command>").Red());
            Reporter.Output.WriteLine(HelpText);
            return 1;
        }

        private bool IsValidCommandName(string s)
        {
            return BuiltInCommands.ContainsKey(s);
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
