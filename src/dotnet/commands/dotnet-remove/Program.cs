// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Remove.PackageReference;
using Microsoft.DotNet.Tools.Remove.ProjectToProjectReference;

namespace Microsoft.DotNet.Tools.Remove
{
    public class RemoveCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "remove";
        protected override string FullCommandNameLocalized => LocalizableStrings.NetRemoveCommand;
        protected override string ArgumentName => Constants.ProjectArgumentName;
        protected override string ArgumentDescriptionLocalized => CommonLocalizableStrings.ArgumentsProjectDescription;

        internal override Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands =>
            new Dictionary<string, Func<AppliedOption, CommandBase>>
            {
                { "reference", o => new RemoveProjectToProjectReferenceCommand(o) },
                { "package", o => new RemovePackageReferenceCommand(o) }
            };

        public static int Run(string[] args)
        {
            var command = new RemoveCommand();
            return command.RunCommand(args);
        }
    }
}