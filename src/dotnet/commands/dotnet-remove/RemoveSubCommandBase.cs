// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Remove
{
    public abstract class RemoveSubCommandBase
    {
        protected abstract string CommandName { get; }
        protected abstract string LocalizedDisplayName { get; }
        protected abstract string LocalizedDescription { get; }
        protected abstract string LocalizedHelpText { get; }
        internal abstract void AddCustomOptions(CommandLineApplication app);
        protected abstract IRemoveSubCommand CreateIRemoveSubCommand(string fileOrDirectory);

        internal CommandLineApplication Create(CommandLineApplication parentApp)
        {
            CommandLineApplication app = parentApp.Command(CommandName, throwOnUnexpectedArg: false);
            app.FullName = LocalizedDisplayName;
            app.Description = LocalizedDescription;
            app.HandleRemainingArguments = true;
            app.ArgumentSeparatorHelpText = LocalizedHelpText;

            app.HelpOption("-h|--help");

            AddCustomOptions(app);

            app.OnExecute(() => {
                try
                {
                    if (!parentApp.Arguments.Any())
                    {
                        throw new GracefulException(CommonLocalizableStrings.RequiredArgumentNotPassed, Constants.ProjectOrSolutionArgumentName);
                    }

                    var projectOrDirectory = parentApp.Arguments.First().Value;
                    if (string.IsNullOrEmpty(projectOrDirectory))
                    {
                        projectOrDirectory = PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory());
                    }

                    var removeSubCommand = CreateIRemoveSubCommand(projectOrDirectory);
                    removeSubCommand.Remove(app.RemainingArguments);
                    
                    return 0;
                }
                catch (GracefulException e)
                {
                    Reporter.Error.WriteLine(e.Message.Red());
                    app.ShowHelp();
                    return 1;
                }
            });

            return app;
        }
    }
}
