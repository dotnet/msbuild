// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Remove.ProjectFromSolution;
using Microsoft.DotNet.Tools.Remove.ProjectToProjectReference;

namespace Microsoft.DotNet.Tools.Remove
{
    public class RemoveCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "remove";
        protected override string FullCommandNameLocalized => LocalizableStrings.NetRemoveCommand;
        internal override List<Func<DotNetSubCommandBase>> SubCommands =>
            new List<Func<DotNetSubCommandBase>>
            {
                RemoveProjectFromSolutionCommand.Create,
                RemoveProjectToProjectReferenceCommand.Create,
            };

        public static int Run(string[] args)
        {
            var command = new RemoveCommand();
            return command.RunCommand(args);
        }
    }
}
