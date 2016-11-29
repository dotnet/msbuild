// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    public class RemoveProjectToProjectReferenceCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "dotnet remove p2p",
                FullName = ".NET Remove Project to Project (p2p) reference Command",
                Description = "Command to remove project to project (p2p) reference",
                AllowArgumentSeparator = true,
                ArgumentSeparatorHelpText = "Project to project references to remove"
            };

            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument(
                "<PROJECT>",
                "The project file to modify. If a project file is not specified," +
                " it searches the current working directory for an MSBuild file that has" +
                " a file extension that ends in `proj` and uses that file.");

            CommandOption frameworkOption = app.Option(
                "-f|--framework <FRAMEWORK>",
                "Remove reference only when targetting a specific framework",
                CommandOptionType.SingleValue);

            app.OnExecute(() => {
                if (string.IsNullOrEmpty(projectArgument.Value))
                {
                    throw new GracefulException(LocalizableStrings.RequiredArgumentNotPassed, "<Project>");
                }

                ProjectRootElement project;
                string projectDir;
                if (File.Exists(projectArgument.Value))
                {
                    project = P2PHelpers.GetProjectFromFileOrThrow(projectArgument.Value);
                    projectDir = new FileInfo(projectArgument.Value).DirectoryName;
                }
                else
                {
                    project = P2PHelpers.GetProjectFromDirectoryOrThrow(projectArgument.Value);
                    projectDir = projectArgument.Value;
                }

                projectDir = PathUtility.EnsureTrailingSlash(projectDir);

                if (app.RemainingArguments.Count == 0)
                {
                    throw new GracefulException(LocalizableStrings.SpecifyAtLeastOneReferenceToRemove);
                }

                List<string> references = app.RemainingArguments;
                
                int numberOfRemovedReferences = P2PHelpers.RemoveProjectToProjectReference(
                    project,
                    frameworkOption.Value(),
                    references);

                if (numberOfRemovedReferences != 0)
                {
                    project.Save();
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
