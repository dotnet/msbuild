// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

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

            CommandLineApplication command = new CommandLineApplication(throwOnUnexpectedArg: true)
            {
                Name = $"dotnet {CommandName}",
                FullName = FullCommandNameLocalized,
            };

            command.HelpOption("-h|--help");

            command.Argument(ArgumentName, ArgumentDescriptionLocalized);

            foreach (var subCommandCreator in SubCommands)
            {
                var subCommand = subCommandCreator();
                command.AddCommand(subCommand);

                subCommand.OnExecute(() => {
                    try
                    {
                        if (!command.Arguments.Any())
                        {
                            throw new GracefulException(CommonLocalizableStrings.RequiredArgumentNotPassed, ArgumentDescriptionLocalized);
                        }

                        var projectOrDirectory = command.Arguments.First().Value;
                        if (string.IsNullOrEmpty(projectOrDirectory))
                        {
                            projectOrDirectory = PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory());
                        }

                        return subCommand.Run(projectOrDirectory);
                    }
                    catch (GracefulException e)
                    {
                        Reporter.Error.WriteLine(e.Message.Red());
                        subCommand.ShowHelp();
                        return 1;
                    }
                });
            }

            try
            {
                return command.Execute(args);
            }
            catch (GracefulException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                command.ShowHelp();
                return 1;
            }
            catch (CommandParsingException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                return 1;
            }
        }
    }
}
