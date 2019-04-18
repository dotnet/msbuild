// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    internal class ListProjectToProjectReferencesCommand : CommandBase
    {
        private readonly string _fileOrDirectory;

        public ListProjectToProjectReferencesCommand(
            AppliedOption appliedCommand,
            ParseResult parseResult) : base()
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }
            if (parseResult == null)
            {
                throw new ArgumentNullException(nameof(parseResult));
            }

            // If showing help, replace the parent's argument rule so that this command's argument
            // only shows `PROJECT` instead of `PROJECT | SOLUTION`.
            if (parseResult.AppliedCommand().IsHelpRequested() &&
                (parseResult.Command().Parent is ListCommandParser.ListCommand parent))
            {
                parent.SetArgumentsRule(
                    Accept.ZeroOrOneArgument()
                        .With(
                            name: CommonLocalizableStrings.ProjectArgumentName,
                            description: CommonLocalizableStrings.ProjectArgumentDescription)
                        .DefaultToCurrentDirectory()
                );
            }

            ShowHelpOrErrorIfAppropriate(parseResult);

            _fileOrDirectory = appliedCommand.Arguments.Single();
        }

        public override int Execute()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), _fileOrDirectory, false);

            var p2ps = msbuildProj.GetProjectToProjectReferences();
            if (!p2ps.Any())
            {
                Reporter.Output.WriteLine(string.Format(
                                              CommonLocalizableStrings.NoReferencesFound,
                                              CommonLocalizableStrings.P2P,
                                              _fileOrDirectory));
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
