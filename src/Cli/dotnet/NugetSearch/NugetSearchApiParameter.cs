// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
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
            var searchTerm = parseResult.ValueForArgument<string>(ToolSearchCommandParser.SearchTermArgument);

            var skip = GetParsedResultAsInt(parseResult, ToolSearchCommandParser.SkipOption);
            var take = GetParsedResultAsInt(parseResult, ToolSearchCommandParser.TakeOption);
            var prerelease = parseResult.ValueForOption<bool>(ToolSearchCommandParser.PrereleaseOption);

            SearchTerm = searchTerm;
            Skip = skip;
            Take = take;
            Prerelease = prerelease;
        }

        private static int? GetParsedResultAsInt(ParseResult parseResult, Option<string> alias)
        {
            var valueFromParser = parseResult.ValueForOption<string>(alias);
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
