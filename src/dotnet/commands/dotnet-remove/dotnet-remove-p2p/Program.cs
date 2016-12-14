// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    public class RemoveProjectToProjectReferenceCommand
    {
        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            CommandLineApplication app = parentApp.Command("p2p", throwOnUnexpectedArg: false);
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.HandleRemainingArguments = true;
            app.ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText;

            app.HelpOption("-h|--help");

            CommandOption frameworkOption = app.Option(
                $"-f|--framework <{CommonLocalizableStrings.CmdFramework}>",
                LocalizableStrings.CmdFrameworkDescription,
                CommandOptionType.SingleValue);

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

                    var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), projectOrDirectory);

                    if (app.RemainingArguments.Count == 0)
                    {
                        throw new GracefulException(LocalizableStrings.SpecifyAtLeastOneReferenceToRemove);
                    }

                    List<string> references = app.RemainingArguments;

                    int numberOfRemovedReferences = msbuildProj.RemoveProjectToProjectReferences(
                        frameworkOption.Value(),
                        references);

                    if (numberOfRemovedReferences != 0)
                    {
                        msbuildProj.ProjectRootElement.Save();
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
