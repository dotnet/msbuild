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
        public const string CommandName = "dotnet-add";
        public const string UsageText = @"Usage: dotnet add [command] [arguments]

Arguments:
  [command]             The command to execute
  [arguments]           Arguments to pass to the command

Commands:
  p2p           Add project to project (p2p) reference to a project";

        private static Dictionary<string, Func<string[], int>> s_builtIns = new Dictionary<string, Func<string[], int>>
        {
            ["p2p"] = AddProjectToProjectReferenceCommand.Run,
        };

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Reporter.Output.WriteLine(UsageText);
                return 1;
            }

            if (args[0].StartsWith("-"))
            {
                Reporter.Error.WriteLine($"Unknown option: {args[0]}");
                Reporter.Output.WriteLine(UsageText);
                return 1;
            }

            string command = args[0];
            Func<string[], int> builtIn;
            args = args.Skip(1).ToArray();
            if (s_builtIns.TryGetValue(command, out builtIn))
            {
                return builtIn(args);
            }
            else
            {
                CommandResult result = Command.Create(
                        $"{CommandName}-{command}",
                        args,
                        FrameworkConstants.CommonFrameworks.NetStandardApp15)
                    .Execute();
                return result.ExitCode;
            }
        }
    }
}
