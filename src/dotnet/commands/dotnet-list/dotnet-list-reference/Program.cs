// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using System.Linq;

namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    internal class ListProjectToProjectReferencesCommand : DotNetSubCommandBase
    {
        public static DotNetSubCommandBase Create()
        {
            var command = new ListProjectToProjectReferencesCommand()
            {
                Name = "reference",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
            };

            command.HelpOption("-h|--help");

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), fileOrDirectory);

            var p2ps = msbuildProj.GetProjectToProjectReferences();
            if (!p2ps.Any())
            {
                Reporter.Output.WriteLine(string.Format(
                    CommonLocalizableStrings.NoReferencesFound,
                    CommonLocalizableStrings.P2P,
                    fileOrDirectory));
                return 0;
            }

            Reporter.Output.WriteLine($"{CommonLocalizableStrings.ProjectReferenceOneOrMore}");
            Reporter.Output.WriteLine(new string('-', CommonLocalizableStrings.ProjectReferenceOneOrMore.Length));
            foreach (var p2p in p2ps)
            {
                Reporter.Output.WriteLine(p2p.Include);
            }

            return 0;
        }
    }
}
