// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Add.ProjectToProjectReference
{
    internal class AddProjectToProjectReferenceCommand : CommandBase
    {
        private readonly string _fileOrDirectory;

        public AddProjectToProjectReferenceCommand(ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.ValueForArgument<string>(AddCommandParser.ProjectArgument);
        }

        public override int Execute()
        {
            var projects = new ProjectCollection();
            bool interactive = _parseResult.HasOption(AddProjectToProjectReferenceParser.InteractiveOption);
            MsbuildProject msbuildProj = MsbuildProject.FromFileOrDirectory(
                projects,
                _fileOrDirectory,
                interactive);

            var frameworkString = _parseResult.ValueForOption<string>(AddProjectToProjectReferenceParser.FrameworkOption);

            var arguments = _parseResult.ValueForArgument<IReadOnlyCollection<string>>(AddProjectToProjectReferenceParser.ProjectPathArgument);
            PathUtility.EnsureAllPathsExist(arguments,
                CommonLocalizableStrings.CouldNotFindProjectOrDirectory, true);
            List<MsbuildProject> refs =
                arguments
                    .Select((r) => MsbuildProject.FromFileOrDirectory(projects, r, interactive))
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
                if (!msbuildProj.IsTargetingFramework(framework))
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

            var relativePathReferences = refs.Select((r) =>
                                                        Path.GetRelativePath(
                                                            msbuildProj.ProjectDirectory,
                                                            r.ProjectRootElement.FullPath)).ToList();

            int numberOfAddedReferences = msbuildProj.AddProjectToProjectReferences(
                frameworkString,
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
