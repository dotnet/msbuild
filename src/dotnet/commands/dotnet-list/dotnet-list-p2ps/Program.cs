// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    public class ListProjectToProjectReferencesCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "dotnet list p2ps",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription
            };

            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument($"<{LocalizableStrings.ProjectArgumentValueName}>", LocalizableStrings.ProjectArgumentDescription);

            app.OnExecute(() => {
                if (string.IsNullOrEmpty(projectArgument.Value))
                {
                    throw new GracefulException(CommonLocalizableStrings.RequiredArgumentNotPassed, $"<{LocalizableStrings.ProjectArgumentValueName}>");
                }

                var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), projectArgument.Value);

                var p2ps = msbuildProj.GetProjectToProjectReferences();
                if (p2ps.Count() == 0)
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.NoReferencesFound, CommonLocalizableStrings.P2P, projectArgument.Value));
                    return 0;
                }

                Reporter.Output.WriteLine($"{CommonLocalizableStrings.ProjectReferenceOneOrMore}");
                Reporter.Output.WriteLine(new string('-', CommonLocalizableStrings.ProjectReferenceOneOrMore.Length));
                foreach (var p2p in p2ps)
                {
                    Reporter.Output.WriteLine(p2p.Include);
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
    }
}
