// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Search;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadSearchCommandParser
    {
        public static readonly Argument<string> WorkloadIdStubArgument =
            new Argument<string>(LocalizableStrings.WorkloadIdStubArgumentName)
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = LocalizableStrings.WorkloadIdStubArgumentDescription
            };

        public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("search", LocalizableStrings.CommandDescription);
            command.AddArgument(WorkloadIdStubArgument);
            command.AddOption(CommonOptions.HiddenVerbosityOption);
            command.AddOption(VersionOption);

            command.SetHandler((parseResult) => new WorkloadSearchCommand(parseResult).Execute());

            return command;
        }
    }
}
