// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Add.PackageReference;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;

namespace Microsoft.DotNet.Tools.Add
{
    public class AddCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "add";
        protected override string FullCommandNameLocalized => LocalizableStrings.NetAddCommand;
        protected override string ArgumentName => Constants.ProjectArgumentName;
        protected override string ArgumentDescriptionLocalized => CommonLocalizableStrings.ArgumentsProjectDescription;
        internal override List<Func<DotNetSubCommandBase>> SubCommands =>
            new List<Func<DotNetSubCommandBase>>
            {
                AddProjectToProjectReferenceCommand.Create,
                AddPackageReferenceCommand.Create,
            };

        public static int Run(string[] args)
        {
            var command = new AddCommand();
            return command.RunCommand(args);
        }
    }
}