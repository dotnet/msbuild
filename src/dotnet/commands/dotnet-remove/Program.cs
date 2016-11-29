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
using Microsoft.DotNet.Tools.Remove.ProjectToProjectReference;

namespace Microsoft.DotNet.Tools.Remove
{
    public class RemoveCommand : DispatchCommand
    {
        protected override string HelpText => @".NET Remove Command;

Usage: dotnet remove [options] <object> <command> [[--] <arg>...]]

Options:
  -h|--help  Show help information

Arguments:
  <object>   The object of the operation. If a project file is not specified, it defaults to the current directory.
  <command>  Command to be executed on <object>.

Args:
  Any extra arguments passed to the command. Use `dotnet add <command> --help` to get help about these arguments.

Commands:
  p2p        Remove project to project (p2p) reference from a project";

        protected override Dictionary<string, Func<string[], int>> BuiltInCommands => new Dictionary<string, Func<string[], int>>
        {
            ["p2p"] = RemoveProjectToProjectReferenceCommand.Run,
        };
    }
}
