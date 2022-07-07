// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadInstallCommandParser
    {
        public static readonly Argument<IEnumerable<string>> WorkloadIdArgument =
            new Argument<IEnumerable<string>>(LocalizableStrings.WorkloadIdArgumentName)
            {
                Arity = ArgumentArity.OneOrMore,
                Description = LocalizableStrings.WorkloadIdArgumentDescription
            };

        public static readonly Option<bool> SkipSignCheckOption =
            new Option<bool>("--skip-sign-check", LocalizableStrings.SkipSignCheckOptionDescription)
            {
                IsHidden = true
            };

        public static readonly Option<bool> SkipManifestUpdateOption = new Option<bool>("--skip-manifest-update", LocalizableStrings.SkipManifestUpdateOptionDescription);

        public static readonly Option<string> TempDirOption = new Option<string>("--temp-dir", LocalizableStrings.TempDirOptionDescription);


        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("install", LocalizableStrings.CommandDescription);

            command.AddArgument(WorkloadIdArgument);
            AddWorkloadInstallCommandOptions(command);

            command.SetHandler((parseResult) => new WorkloadInstallCommand(parseResult).Execute());

            return command;
        }

        internal static void AddWorkloadInstallCommandOptions(Command command)
        {
            InstallingWorkloadCommandParser.AddWorkloadInstallCommandOptions(command);

            command.AddOption(SkipManifestUpdateOption);
            command.AddOption(TempDirOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(SkipSignCheckOption);
        }
    }
}
