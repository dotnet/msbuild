// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Add;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Tools.Add.ProjectToProjectReference
{
    public class AddProjectToProjectReference : IAddSubCommand
    {
        private CommandOption _frameworkOption;
        private MsbuildProject _msbuildProj;
        private ProjectCollection _projects;

        internal AddProjectToProjectReference(string fileOrDirectory, CommandOption frameworkOption)
        {
            _projects = new ProjectCollection();
            _msbuildProj = MsbuildProject.FromFileOrDirectory(_projects, fileOrDirectory);
            _frameworkOption = frameworkOption;
        }

        public int Add(List<string> references)
        {
            if (references.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToAdd);
            }

            string frameworkString = _frameworkOption.Value();
            PathUtility.EnsureAllPathsExist(references, CommonLocalizableStrings.ReferenceDoesNotExist);
            IEnumerable<MsbuildProject> refs = references.Select((r) => MsbuildProject.FromFile(_projects, r));

            if (frameworkString == null)
            {
                foreach (var tfm in _msbuildProj.GetTargetFrameworks())
                {
                    foreach (var @ref in refs)
                    {
                        if (!@ref.CanWorkOnFramework(tfm))
                        {
                            Reporter.Error.Write(GetProjectNotCompatibleWithFrameworksDisplayString(
                                    @ref,
                                    _msbuildProj.GetTargetFrameworks().Select((fx) => fx.GetShortFolderName())));
                            return 1;
                        }
                    }
                }
            }
            else
            {
                var framework = NuGetFramework.Parse(frameworkString);
                if (!_msbuildProj.IsTargettingFramework(framework))
                {
                    Reporter.Error.WriteLine(string.Format(
                        CommonLocalizableStrings.ProjectDoesNotTargetFramework,
                        _msbuildProj.ProjectRootElement.FullPath,
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

            var relativePathReferences = references.Select((r) =>
                PathUtility.GetRelativePath(_msbuildProj.ProjectDirectory, Path.GetFullPath(r))).ToList();

            int numberOfAddedReferences = _msbuildProj.AddProjectToProjectReferences(
                _frameworkOption.Value(),
                relativePathReferences);

            if (numberOfAddedReferences != 0)
            {
                _msbuildProj.ProjectRootElement.Save();
            }

            return 0;
        }

        private string GetProjectNotCompatibleWithFrameworksDisplayString(MsbuildProject project, IEnumerable<string> frameworksDisplayStrings)
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

    public class AddProjectToProjectReferenceCommand : AddSubCommandBase
    {
        private CommandOption _frameworkOption;

        protected override string CommandName => "p2p";
        protected override string LocalizedDisplayName => LocalizableStrings.AppFullName;
        protected override string LocalizedDescription => LocalizableStrings.AppDescription;
        protected override string LocalizedHelpText => LocalizableStrings.AppHelpText;

        internal override void AddCustomOptions(CommandLineApplication app)
        {
            _frameworkOption = app.Option(
                $"-f|--framework <{CommonLocalizableStrings.CmdFramework}>",
                LocalizableStrings.CmdFrameworkDescription,
                CommandOptionType.SingleValue);
        }

        protected override IAddSubCommand CreateIAddSubCommand(string fileOrDirectory)
        {
            return new AddProjectToProjectReference(fileOrDirectory, _frameworkOption);
        }

        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            var addSubCommand = new AddProjectToProjectReferenceCommand();
            return addSubCommand.Create(parentApp);
        }
    }
}
