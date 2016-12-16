// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;

namespace Microsoft.DotNet.Cli
{
    public abstract class DotNetTopLevelCommandBase
    {
        protected abstract string CommandName { get; }
        protected abstract string FullCommandNameLocalized { get; }
        internal abstract List<Func<CommandLineApplication, CommandLineApplication>> SubCommands { get; }

        public int RunCommand(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: true)
            {
                Name = $"dotnet {CommandName}",
                FullName = FullCommandNameLocalized,
            };

            app.HelpOption("-h|--help");

            app.Argument(
                Constants.ProjectOrSolutionArgumentName,
                CommonLocalizableStrings.ArgumentsProjectOrSolutionDescription);

            foreach (var subCommandCreator in SubCommands)
            {
                subCommandCreator(app);
            }

            try
            {
                return app.Execute(args);
            }
            catch (GracefulException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                app.ShowHelp();
                return 1;
            }
            catch (CommandParsingException e)
            {
                string errorMessage = e.IsRequireSubCommandMissing
                    ? CommonLocalizableStrings.RequiredCommandNotPassed
                    : e.Message;

                Reporter.Error.WriteLine(errorMessage.Red());
                return 1;
            }
        }
    }
}
