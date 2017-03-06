// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public abstract class DotNetTopLevelCommandBase
    {
        protected abstract string CommandName { get; }
        protected abstract string FullCommandNameLocalized { get; }
        protected abstract string ArgumentName { get; }
        protected abstract string ArgumentDescriptionLocalized { get; }
        internal abstract List<Func<DotNetSubCommandBase>> SubCommands { get; }

        public int RunCommand(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var result = Parser.DotnetCommand[CommandName]
                                  .Parse(args);

            Reporter.Verbose.WriteLine(result.Diagram());

            var command = result[CommandName];

            if (command.HasOption("help"))
            {
                result.ShowHelp();
                return 0;
            }

            if (result.Errors.Any())
            {
                Reporter.Error.WriteLine(result.Errors.First().Message.Red());
                return 1;
            }

            var subCommand = SubCommands
                .Select(c => c())
                .FirstOrDefault(c => c.Name == command.AppliedOptions.First().Name);

            var fileOrDirectory = command.AppliedOptions
                                         .First()
                                         .Arguments
                                         .FirstOrDefault();

            try
            {
                return subCommand.Run(fileOrDirectory);
            }
            catch (GracefulException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                subCommand.ShowHelp();
                return 1;
            }
        }
    }
}