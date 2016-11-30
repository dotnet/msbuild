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
                FullName = ".NET Add Project to Project (p2p) reference Command",
                Description = "Command to add project to project (p2p) reference",
                AllowArgumentSeparator = true,
                ArgumentSeparatorHelpText = "Project to project references to add"
            };

            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument(
                "<PROJECT>",
                "The project file to modify. If a project file is not specified," +
                " it searches the current working directory for an MSBuild file that has" +
                " a file extension that ends in `proj` and uses that file.");

            CommandOption frameworkOption = app.Option(
                "-f|--framework <FRAMEWORK>",
                "Add reference only when targetting a specific framework",
                CommandOptionType.SingleValue);

            CommandOption forceOption = app.Option(
                "--force", 
                "Add reference even if it does not exist, do not convert paths to relative",
                CommandOptionType.NoValue);

            app.OnExecute(() => {
                if (string.IsNullOrEmpty(projectArgument.Value))
                {
                    throw new GracefulException(LocalizableStrings.RequiredArgumentNotPassed, "<Project>");
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
