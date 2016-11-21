// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

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

            CommandArgument projectArgument = app.Argument("<PROJECT>",
                "The project file to modify. If a project file is not specified," +
                " it searches the current working directory for an MSBuild file that has a file extension that ends in `proj` and uses that file.");

            CommandOption frameworkOption = app.Option("-f|--framework <FRAMEWORK>", "Add reference only when targetting a specific framework", CommandOptionType.SingleValue);

            app.OnExecute(() => {
                if (projectArgument.Value == null)
                {
                    throw new GracefulException("Argument <Project> is required.");
                }

                ProjectRootElement project = File.Exists(projectArgument.Value) ?
                                                GetProjectFromFileOrThrow(projectArgument.Value) :
                                                GetProjectFromDirectoryOrThrow(projectArgument.Value);

                if (app.RemainingArguments.Count == 0)
                {
                    throw new GracefulException("You must specify at least one reference to add.");
                }

                int numberOfAddedReferences = AddProjectToProjectReference(
                    project,
                    frameworkOption.Value(),
                    app.RemainingArguments);

                if (numberOfAddedReferences != 0)
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

        // There is ProjectRootElement.TryOpen but it does not work as expected
        // I.e. it returns null for some valid projects
        internal static ProjectRootElement TryOpenProject(string filename)
        {
            try
            {
                return ProjectRootElement.Open(filename, new ProjectCollection(), preserveFormatting: true);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }
        }

        internal static ProjectRootElement GetProjectFromFileOrThrow(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new GracefulException($"Provided project `{filename}` does not exist.");
            }

            var project = TryOpenProject(filename);
            if (project == null)
            {
                throw new GracefulException($"Invalid project `{filename}`.");
            }

            return project;
        }

        // TODO: improve errors
        internal static ProjectRootElement GetProjectFromDirectoryOrThrow(string directory)
        {
            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(directory);
            }
            catch (ArgumentException)
            {
                throw new GracefulException($"Could not find project or directory `{directory}`.");
            }

            if (!dir.Exists)
            {
                throw new GracefulException($"Could not find project or directory `{directory}`.");
            }

            FileInfo[] files = dir.GetFiles("*proj");
            if (files.Length == 0)
            {
                throw new GracefulException($"Could not find any project in `{directory}`.");
            }

            if (files.Length > 1)
            {
                throw new GracefulException("Found more than one project in the current directory. Please specify which one to use.");
            }

            FileInfo projectFile = files.First();

            if (!projectFile.Exists)
            {
                throw new GracefulException($"Could not find any project in `{directory}`.");
            }

            var ret = TryOpenProject(projectFile.FullName);
            if (ret == null)
            {
                throw new GracefulException($"Found a project `{projectFile.FullName}` but it is invalid.");
            }

            return ret;
        }

        internal static int AddProjectToProjectReference(ProjectRootElement root, string framework, IEnumerable<string> refs)
        {
            int numberOfAddedReferences = 0;
            const string ProjectItemElementType = "ProjectReference";

            ProjectItemGroupElement ig = null;
            foreach (var @ref in refs)
            {
                if (root.HasExistingItemWithCondition(framework, @ref))
                {
                    Reporter.Output.WriteLine($"Project already has a reference to `{@ref}`.");
                    continue;
                }

                numberOfAddedReferences++;
                ig = ig ?? root.FindUniformOrCreateItemGroupWithCondition(ProjectItemElementType, framework);
                ig.AppendChild(root.CreateItemElement(ProjectItemElementType, @ref));

                Reporter.Output.WriteLine($"Reference `{@ref}` added to the project.");
            }

            return numberOfAddedReferences;
        }
    }
}
