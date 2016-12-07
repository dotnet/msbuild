// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Tools.List.ProjectToProjectReferences;

namespace Microsoft.DotNet.Tools.List
{
    public class ListCommand : DispatchCommand
    {
        protected override string HelpText => $@"{LocalizableStrings.ListCommandDescription}

{LocalizableStrings.Usage}: dotnet list [options] <object> <command> [[--] <arg>...]]

Options:
  -h|--help  {LocalizableStrings.HelpDefinition}

{LocalizableStrings.Arguments}:
  <object>   {LocalizableStrings.ObjectDefinition}
  <command>  {LocalizableStrings.CommandDefinition}

{LocalizableStrings.ExtraArgs}:
  {LocalizableStrings.ExtraArgumentsDefinition}

{LocalizableStrings.Commands}:
  p2ps       {LocalizableStrings.P2PsDefinition}";

        protected override Dictionary<string, Func<string[], int>> BuiltInCommands => new Dictionary<string, Func<string[], int>>
        {
            ["p2ps"] = ListProjectToProjectReferencesCommand.Run,
        };

        public static int Run(string[] args)
        {
            var cmd = new ListCommand();
            return cmd.Start(args);
        }
    }
}
