// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.Linq;

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

                string frameworkString = frameworkOption.Value();
                List<string> references = app.RemainingArguments;
                if (!forceOption.HasValue())
                {
                    MsbuildProject.EnsureAllReferencesExist(references);
                    IEnumerable<MsbuildProject> refs = references.Select((r) => MsbuildProject.FromFile(r));

                    if (frameworkString == null)
                    {
                        foreach (var tfm in msbuildProj.GetTargetFrameworks())
                        {
                            foreach (var r in refs)
                            {
                                if (!r.CanWorkOnFramework(tfm))
                                {
                                    throw new GracefulException(string.Format(CommonLocalizableStrings.ProjectNotCompatibleWithFramework, r.ProjectRoot.FullPath, GetFrameworkDisplayString(tfm)));
                                }
                            }
                        }
                    }
                    else
                    {
                        var framework = NuGetFramework.Parse(frameworkString);
                        if (!msbuildProj.TargetsFramework(framework))
                        {
                            throw new GracefulException(string.Format(CommonLocalizableStrings.ProjectDoesNotTargetFramework, msbuildProj.ProjectRoot.FullPath, GetFrameworkDisplayString(framework)));
                        }

                        foreach (var r in refs)
                        {
                            if (!r.CanWorkOnFramework(framework))
                            {
                                throw new GracefulException(string.Format(CommonLocalizableStrings.ProjectNotCompatibleWithFramework, r.ProjectRoot.FullPath, GetFrameworkDisplayString(framework)));
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
                    msbuildProj.ProjectRoot.Save();
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

        private static string GetFrameworkDisplayString(NuGetFramework framework)
        {
            return framework.GetShortFolderName();
        }
    }
}
