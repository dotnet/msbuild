// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NugetSearch;

namespace Microsoft.DotNet.Tools.Tool.Search
{
    internal class ToolSearchCommand : CommandBase
    {
        private readonly INugetToolSearchApiRequest _nugetToolSearchApiRequest;
        private readonly SearchResultPrinter _searchResultPrinter;

        public ToolSearchCommand(
            ParseResult result,
            INugetToolSearchApiRequest nugetToolSearchApiRequest = null
        )
            : base(result)
        {
            _nugetToolSearchApiRequest = nugetToolSearchApiRequest ?? new NugetToolSearchApiRequest();
            _searchResultPrinter = new SearchResultPrinter(Reporter.Output);
        }

        public override int Execute()
        {
            var isDetailed = _parseResult.GetValueForOption(ToolSearchCommandParser.DetailOption);
            NugetSearchApiParameter nugetSearchApiParameter = new NugetSearchApiParameter(_parseResult);
            IReadOnlyCollection<SearchResultPackage> searchResultPackages =
                NugetSearchApiResultDeserializer.Deserialize(
                    _nugetToolSearchApiRequest.GetResult(nugetSearchApiParameter).GetAwaiter().GetResult());

            _searchResultPrinter.Print(isDetailed, searchResultPackages);

            return 0;
        }
    }
}
