// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.NugetSearch
{
    internal class NugetSearchApiParameter
    {
        public NugetSearchApiParameter(
            string searchTerm = null,
            int? skip = null,
            int? take = null,
            bool prerelease = false)
        {
            SearchTerm = searchTerm;
            Skip = skip;
            Take = take;
            Prerelease = prerelease;
        }

        public string SearchTerm { get; }
        public int? Skip { get; }
        public int? Take { get; }
        public bool Prerelease { get; }

        public NugetSearchApiParameter(ParseResult parseResult)
        {
            var searchTerm = parseResult.GetValue(ToolSearchCommandParser.SearchTermArgument);

            var skip = GetParsedResultAsInt(parseResult, ToolSearchCommandParser.SkipOption);
            var take = GetParsedResultAsInt(parseResult, ToolSearchCommandParser.TakeOption);
            var prerelease = parseResult.GetValue(ToolSearchCommandParser.PrereleaseOption);

            SearchTerm = searchTerm;
            Skip = skip;
            Take = take;
            Prerelease = prerelease;
        }

        private static int? GetParsedResultAsInt(ParseResult parseResult, CliOption<string> alias)
        {
            var valueFromParser = parseResult.GetValue(alias);
            if (string.IsNullOrWhiteSpace(valueFromParser))
            {
                return null;
            }

            if (int.TryParse(valueFromParser, out int i))
            {
                return i;
            }
            else
            {
                throw new GracefulException(
                    string.Format(
                        Tools.Tool.Search.LocalizableStrings.InvalidInputTypeInteger,
                        alias));
            }
        }
    }
}
