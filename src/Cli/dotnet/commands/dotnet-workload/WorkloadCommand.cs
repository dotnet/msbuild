// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.DotNet.Workloads.Workload.Repair;
using Microsoft.DotNet.Workloads.Workload.Restore;
using Microsoft.DotNet.Workloads.Workload.Search;
using Microsoft.DotNet.Workloads.Workload.Uninstall;
using Microsoft.DotNet.Workloads.Workload.Update;

namespace Microsoft.DotNet.Workloads.Workload
{
    public class WorkloadCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "workload";
        protected override string FullCommandNameLocalized => LocalizableStrings.InstallFullCommandNameLocalized;
        protected override string ArgumentName => Constants.ProjectArgumentName;
        protected override string ArgumentDescriptionLocalized => CommonLocalizableStrings.ProjectArgumentDescription;

        internal override Dictionary<string, Func<ParseResult, CommandBase>> SubCommands =>
            new Dictionary<string, Func<ParseResult, CommandBase>>
            {
                ["install"] =
                appliedOption => new WorkloadInstallCommand(
                    ParseResult),
                ["uninstall"] =
                appliedOption => new WorkloadUninstallCommand(
                    ParseResult),
                ["update"] =
                appliedOption => new WorkloadUpdateCommand(
                    ParseResult),
                ["list"] =
                appliedOption => new WorkloadListCommand(
                    ParseResult),
                ["restore"] =
                appliedOption => new WorkloadRestoreCommand(
                    ParseResult),
                ["search"] =
                appliedOption => new WorkloadSearchCommand(
                    ParseResult),
                ["repair"] =
                appliedOption => new WorkloadRepairCommand(
                    ParseResult)
            };

        public static int Run(string[] args)
        {
            var command = new WorkloadCommand();
            return command.RunCommand(args);
        }
    }
}
