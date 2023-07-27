// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    internal class ListProjectToProjectReferencesCommand : CommandBase
    {
        private readonly string _fileOrDirectory;

        public ListProjectToProjectReferencesCommand(
            ParseResult parseResult) : base(parseResult)
        {
            ShowHelpOrErrorIfAppropriate(parseResult);

            _fileOrDirectory = parseResult.GetValue(ListCommandParser.SlnOrProjectArgument);
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
