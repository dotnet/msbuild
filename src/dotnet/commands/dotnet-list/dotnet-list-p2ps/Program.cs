// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    public class ListProjectToProjectReferencesCommand
    {
        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            CommandLineApplication app = parentApp.Command("p2ps", throwOnUnexpectedArg: false);
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;

            app.HelpOption("-h|--help");

            app.OnExecute(() => {
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

                    var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), projectOrDirectory);

                    var p2ps = msbuildProj.GetProjectToProjectReferences();
                    if (p2ps.Count() == 0)
                    {
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.NoReferencesFound, CommonLocalizableStrings.P2P, projectOrDirectory));
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
                catch (GracefulException e)
                {
                    Reporter.Error.WriteLine(e.Message.Red());
                    app.ShowHelp();
                    return 1;
                }
            });

            return app;
        }
    }
}
