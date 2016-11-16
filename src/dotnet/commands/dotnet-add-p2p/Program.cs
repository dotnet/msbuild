// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;

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
                Description = "Command to add project to project (p2p) reference"
            };
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument("<PROJECT>",
                "The MSBuild project file to modify. If a project file is not specified," +
                " it searches the current working directory for an MSBuild file that has a file extension that ends in `proj` and uses that file.");

            CommandOption frameworkOption = app.Option("-f|--framework <FRAMEWORK>", "Add reference only when targetting a specific framework", CommandOptionType.SingleValue);
            CommandOption referenceOption = app.Option("-r|--reference <REFERENCE>", "Add project to project <REFERENCE> to <PROJECT>", CommandOptionType.MultipleValue);


            app.OnExecute(() => {
                ProjectRootElement project = projectArgument.Value != null ?
                                                GetProjectFromFileOrThrow(projectArgument.Value) :
                                                GetProjectFromCurrentDirectoryOrThrow();

                if (referenceOption.Values.Count == 0)
                {
                    throw new GracefulException("You must specify at least one reference to add.");
                }

                AddProjectToProjectReference(project, frameworkOption.Value(), referenceOption.Values);

                //project.Save();

                return 0;
            });

            try
            {
                return app.Execute(args);
            }
            catch (GracefulException e)
            {
                Reporter.Error.WriteLine(e.Message);
                return 1;
            }
        }

        // There is ProjectRootElement.TryOpen but it does not work as expected
        // I.e. it returns null for some valid projects
        public static ProjectRootElement TryOpenProject(string filename)
        {
            try
            {
                return ProjectRootElement.Open(filename);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }
        }

        public static ProjectRootElement GetProjectFromFileOrThrow(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new GracefulException($"Provided project `{filename}` does not exist.");
            }

            var project = TryOpenProject(filename);
            if (project == null)
            {
                throw new GracefulException($"Invalid MSBuild project `{filename}`.");
            }

            return project;
        }

        public static ProjectRootElement GetProjectFromCurrentDirectoryOrThrow()
        {
            DirectoryInfo currDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            FileInfo[] files = currDir.GetFiles("*proj");
            if (files.Length == 0)
            {
                throw new GracefulException("Could not find any MSBuild project in the current directory.");
            }

            if (files.Length > 1)
            {
                throw new GracefulException("Found more than one MSBuild project in the current directory. Please specify which one to use.");
            }

            FileInfo projectFile = files.First();

            if (!projectFile.Exists)
            {
                throw new GracefulException("Could not find any project in the current directory.");
            }

            var ret = TryOpenProject(projectFile.FullName);
            if (ret == null)
            {
                throw new GracefulException($"Found an MSBuild project `{projectFile.FullName}` in the current directory but it is invalid.");
            }

            return ret;
        }

        public static Func<T, bool> AndPred<T>(params Func<T, bool>[] preds)
        {
            return (el) => preds.All((pred) => pred == null || pred(el));
        }

        public static string GetFrameworkConditionString(string framework)
        {
            return $" '$(TargetFramework)' == '{framework}' ";
        }

        public static Func<T, bool> FrameworkPred<T>(string framework) where T : ProjectElement
        {
            if (string.IsNullOrEmpty(framework))
            {
                return (ig) => {
                    var condChain = ig.ConditionChain();
                    return condChain.Count == 0;
                };
            }

            string conditionStr = GetFrameworkConditionString(framework).Trim();
            return (ig) => {
                var condChain = ig.ConditionChain();
                return condChain.Count == 1 && condChain.First().Trim() == conditionStr;
            };
        }

        public static Func<ProjectItemGroupElement, bool> UniformItemElementTypePred(string projectItemElementType)
        {
            return (ig) => ig.Items.All((it) => it.ItemType == projectItemElementType);
        }

        public static Func<ProjectItemElement, bool> IncludePred(string include)
        {
            return (it) => it.Include == include;
        }

        public static ProjectItemElement[] FindExistingItemsWithCondition(ProjectRootElement root, string framework, string include)
        {
            return root.Items
                       .Where(
                            AndPred(
                                FrameworkPred<ProjectItemElement>(framework),
                                IncludePred(include)))
                       .ToArray();
        }

        public static ProjectItemGroupElement FindExistingUniformItemGroupWithCondition(ProjectRootElement root, string projectItemElementType, string framework)
        {
            return root.ItemGroupsReversed
                            .FirstOrDefault(
                                AndPred(
                                    // When adding more predicates which operate on ItemGroup.Condition
                                    // some slightly more advanced logic need to be used:
                                    // i.e. ConditionPred(FrameworkConditionPred(framework), RuntimeConditionPred(runtime))
                                    //   FrameworkConditionPred and RuntimeConditionPred would need to operate on a single condition
                                    //   and ConditionPred would need to check if whole Condition Chain is satisfied
                                    FrameworkPred<ProjectItemGroupElement>(framework),
                                    UniformItemElementTypePred(projectItemElementType)));
        }

        public static ProjectItemGroupElement FindUniformOrCreateItemGroupWithCondition(ProjectRootElement root, string projectItemElementType, string framework)
        {
            var lastMatchingItemGroup = FindExistingUniformItemGroupWithCondition(root, projectItemElementType, framework);

            if (lastMatchingItemGroup != null)
            {
                return lastMatchingItemGroup;
            }

            ProjectItemGroupElement ret = root.CreateItemGroupElement();
            ret.Condition = GetFrameworkConditionString(framework);
            root.AppendChild(ret);
            return ret;
        }

        public static void AddProjectToProjectReference(ProjectRootElement root, string framework, IEnumerable<string> refs)
        {
            const string ProjectItemElementType = "ProjectReference";

            ProjectItemGroupElement ig = null;
            foreach (var @ref in refs)
            {
                if (FindExistingItemsWithCondition(root, framework, @ref).Length == 0)
                {
                    Reporter.Output.WriteLine($"Item {ProjectItemElementType} including `{@ref}` is already present.");
                    continue;
                }

                ig = ig ?? FindUniformOrCreateItemGroupWithCondition(root, ProjectItemElementType, framework);
                ig.AppendChild(root.CreateItemElement(ProjectItemElementType, @ref));

                Reporter.Output.WriteLine($"Item {ProjectItemElementType} including `{@ref}` added to project.");
            }
        }
    }
}
