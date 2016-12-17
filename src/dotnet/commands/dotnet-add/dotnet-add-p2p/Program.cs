// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
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
    internal class AddProjectToProjectReferenceCommand : DotNetSubCommandBase
    {
        private CommandOption _frameworkOption;

        public static DotNetSubCommandBase Create()
        {
            var command = new AddProjectToProjectReferenceCommand()
            {
                Name = "p2p",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText,
            };

            command.HelpOption("-h|--help");

            command._frameworkOption = command.Option(
               $"-f|--framework <{CommonLocalizableStrings.CmdFramework}>",
               LocalizableStrings.CmdFrameworkDescription,
               CommandOptionType.SingleValue);

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            var projects = new ProjectCollection();
            MsbuildProject msbuildProj = MsbuildProject.FromFileOrDirectory(projects, fileOrDirectory);

            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToAdd);
            }

            string frameworkString = _frameworkOption.Value();
            PathUtility.EnsureAllPathsExist(RemainingArguments, CommonLocalizableStrings.ReferenceDoesNotExist);
            List<MsbuildProject> refs = RemainingArguments
                .Select((r) => MsbuildProject.FromFile(projects, r))
                .ToList();

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
                    Reporter.Error.WriteLine(string.Format(
                        CommonLocalizableStrings.ProjectDoesNotTargetFramework,
                        msbuildProj.ProjectRootElement.FullPath,
                        frameworkString));
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

            var relativePathReferences = RemainingArguments.Select((r) =>
                PathUtility.GetRelativePath(msbuildProj.ProjectDirectory, Path.GetFullPath(r))).ToList();

            int numberOfAddedReferences = msbuildProj.AddProjectToProjectReferences(
                _frameworkOption.Value(),
                relativePathReferences);

            if (numberOfAddedReferences != 0)
            {
                msbuildProj.ProjectRootElement.Save();
            }

            return 0;
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
