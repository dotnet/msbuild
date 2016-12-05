// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Add.ProjectToProjectReference
{
    public class AddProjectToProjectReferenceCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "dotnet add p2p",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                AllowArgumentSeparator = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText
            };

            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument(
                $"<{LocalizableStrings.CmdProject}>",
                LocalizableStrings.CmdProjectDescription);

            CommandOption frameworkOption = app.Option(
                $"-f|--framework <{LocalizableStrings.CmdFramework}>",
                LocalizableStrings.CmdFrameworkDescription,
                CommandOptionType.SingleValue);

            CommandOption forceOption = app.Option(
                "--force", 
                LocalizableStrings.CmdForceDescription,
                CommandOptionType.NoValue);

            app.OnExecute(() => {
                if (string.IsNullOrEmpty(projectArgument.Value))
                {
                    throw new GracefulException(CommonLocalizableStrings.RequiredArgumentNotPassed, $"<{LocalizableStrings.ProjectException}>");
                }

                var msbuildProj = MsbuildProject.FromFileOrDirectory(projectArgument.Value);

                if (app.RemainingArguments.Count == 0)
                {
                    throw new GracefulException(LocalizableStrings.SpecifyAtLeastOneReferenceToAdd);
                }

                List<string> references = app.RemainingArguments;
                if (!forceOption.HasValue())
                {
                    MsbuildProject.EnsureAllReferencesExist(references);
                    msbuildProj.ConvertPathsToRelative(ref references);
                }
                
                int numberOfAddedReferences = msbuildProj.AddProjectToProjectReferences(
                    frameworkOption.Value(),
                    references);

                if (numberOfAddedReferences != 0)
                {
                    msbuildProj.Project.Save();
                }

                return 0;
            });

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
        }
    }
}
