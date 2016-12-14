// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Tools.Add.ProjectToProjectReference
{
    public class AddProjectToProjectReferenceCommand
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

            app.OnExecute(() =>
            {
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

                    var projects = new ProjectCollection();
                    var msbuildProj = MsbuildProject.FromFileOrDirectory(projects, projectOrDirectory);

                    if (app.RemainingArguments.Count == 0)
                    {
                        throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToAdd);
                    }

                    string frameworkString = frameworkOption.Value();
                    List<string> references = app.RemainingArguments;
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

                    int numberOfAddedReferences = msbuildProj.AddProjectToProjectReferences(
                        frameworkOption.Value(),
                        references);

                    if (numberOfAddedReferences != 0)
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
