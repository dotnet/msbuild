// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.List
{
    public abstract class ListSubCommandBase
    {
        protected abstract string CommandName { get; }
        protected abstract string LocalizedDisplayName { get; }
        protected abstract string LocalizedDescription { get; }
        protected abstract IListSubCommand CreateIListSubCommand(string fileOrDirectory);

        internal CommandLineApplication Create(CommandLineApplication parentApp)
        {
            CommandLineApplication app = parentApp.Command(CommandName, throwOnUnexpectedArg: false);
            app.FullName = LocalizedDisplayName;
            app.Description = LocalizedDescription;

            app.HelpOption("-h|--help");

            app.OnExecute(() => {
                try
                {
                    if (!parentApp.Arguments.Any())
                    {
                        throw new GracefulException(
                            CommonLocalizableStrings.RequiredArgumentNotPassed,
                            Constants.ProjectOrSolutionArgumentName);
                    }

                    var projectOrDirectory = parentApp.Arguments.First().Value;
                    if (string.IsNullOrEmpty(projectOrDirectory))
                    {
                        projectOrDirectory = PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory());
                    }

                    var listCommand = CreateIListSubCommand(projectOrDirectory);
                    if (listCommand.Items.Count == 0)
                    {
                        Reporter.Output.WriteLine(listCommand.LocalizedErrorMessageNoItemsFound);
                        return 0;
                    }

                    Reporter.Output.WriteLine($"{CommonLocalizableStrings.ProjectReferenceOneOrMore}");
                    Reporter.Output.WriteLine(new string('-', CommonLocalizableStrings.ProjectReferenceOneOrMore.Length));
                    foreach (var item in listCommand.Items)
                    {
                        Reporter.Output.WriteLine(item);
                    }

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
