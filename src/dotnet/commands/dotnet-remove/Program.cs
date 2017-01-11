// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
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
        internal override List<Func<DotNetSubCommandBase>> SubCommands =>
            new List<Func<DotNetSubCommandBase>>
            {
                RemoveProjectToProjectReferenceCommand.Create,
                RemovePackageReferenceCommand.Create
            };

        public static int Run(string[] args)
        {
            var command = new RemoveCommand();
            return command.RunCommand(args);
        }
    }
}
