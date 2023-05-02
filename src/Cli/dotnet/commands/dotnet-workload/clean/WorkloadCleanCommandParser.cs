// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Clean;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Clean.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCleanCommandParser
    {

        public static readonly Option<bool> CleanAllOption = new Option<bool>("--all", LocalizableStrings.CleanAllOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new Command("clean", LocalizableStrings.CommandDescription);

            command.AddOption(CleanAllOption);

            command.SetHandler((parseResult) => new WorkloadCleanCommand(parseResult).Execute());

            return command;
        }
    }
}
