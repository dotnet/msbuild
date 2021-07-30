// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
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

        public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption();

        public static readonly Option<string> VersionOption = WorkloadInstallCommandParser.VersionOption;

        public static Command GetCommand()
        {
            var command = new Command("search", LocalizableStrings.CommandDescription);
            command.AddArgument(WorkloadIdStubArgument);
            command.AddOption(VerbosityOption);
            command.AddOption(VersionOption);
            return command;
        }
    }
}
