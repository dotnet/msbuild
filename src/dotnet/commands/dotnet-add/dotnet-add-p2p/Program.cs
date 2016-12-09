// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

                var projects = new ProjectCollection();
                var msbuildProj = MsbuildProject.FromFileOrDirectory(projects, projectArgument.Value);

                if (app.RemainingArguments.Count == 0)
                {
                    throw new GracefulException(LocalizableStrings.SpecifyAtLeastOneReferenceToAdd);
                }

                string frameworkString = frameworkOption.Value();
                List<string> references = app.RemainingArguments;
                if (!forceOption.HasValue())
                {
                    MsbuildProject.EnsureAllReferencesExist(references);
                    IEnumerable<MsbuildProject> refs = references.Select((r) => MsbuildProject.FromFile(projects, r));

                    if (frameworkString == null)
                    {
                        foreach (var tfm in msbuildProj.GetTargetFrameworks())
                        {
                            foreach (var @ref in refs)
                            {
                                if (!@ref.CanWorkOnFramework(tfm))
                                {
                                    Reporter.Error.Write(GetProjectNotCompatibleWithFrameworksDisplayString(
                                            @ref,
                                            msbuildProj.GetTargetFrameworks().Select((fx) => fx.GetShortFolderName())));
                                    return 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        var framework = NuGetFramework.Parse(frameworkString);
                        if (!msbuildProj.IsTargettingFramework(framework))
                        {
                            Reporter.Error.WriteLine(string.Format(CommonLocalizableStrings.ProjectDoesNotTargetFramework, msbuildProj.ProjectRootElement.FullPath, frameworkString));
                            return 1;
                        }

                        foreach (var @ref in refs)
                        {
                            if (!@ref.CanWorkOnFramework(framework))
                            {
                                Reporter.Error.Write(GetProjectNotCompatibleWithFrameworksDisplayString(
                                    @ref,
                                    new string[] { frameworkString }));
                                return 1;
                            }
                        }
                    }

                    msbuildProj.ConvertPathsToRelative(ref references);
                }
                
                int numberOfAddedReferences = msbuildProj.AddProjectToProjectReferences(
                    frameworkOption.Value(),
                    references);

                if (numberOfAddedReferences != 0)
                {
                    msbuildProj.ProjectRootElement.Save();
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

        private static string GetProjectNotCompatibleWithFrameworksDisplayString(MsbuildProject project, IEnumerable<string> frameworksDisplayStrings)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CommonLocalizableStrings.ProjectNotCompatibleWithFrameworks, project.ProjectRootElement.FullPath));
            foreach (var tfm in frameworksDisplayStrings)
            {
                sb.AppendLine($"    - {tfm}");
            }

            return sb.ToString();
        }
    }
}
