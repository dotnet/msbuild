// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
            _fileOrDirectory = parseResult.GetValue(AddCommandParser.ProjectArgument);
        }

        public override int Execute()
        {
            var projects = new ProjectCollection();
            bool interactive = _parseResult.GetValue(AddProjectToProjectReferenceParser.InteractiveOption);
            MsbuildProject msbuildProj = MsbuildProject.FromFileOrDirectory(
                projects,
                _fileOrDirectory,
                interactive);

            var frameworkString = _parseResult.GetValue(AddProjectToProjectReferenceParser.FrameworkOption);

            var arguments = _parseResult.GetValue(AddProjectToProjectReferenceParser.ProjectPathArgument).ToList().AsReadOnly();
            PathUtility.EnsureAllPathsExist(arguments,
                CommonLocalizableStrings.CouldNotFindProjectOrDirectory, true);
            List<MsbuildProject> refs =
                arguments
                    .Select((r) => MsbuildProject.FromFileOrDirectory(projects, r, interactive))
                    .ToList();

            if (string.IsNullOrEmpty(frameworkString))
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
