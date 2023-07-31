// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
            var isDetailed = _parseResult.GetValue(ToolSearchCommandParser.DetailOption);
            NugetSearchApiParameter nugetSearchApiParameter = new NugetSearchApiParameter(_parseResult);
            IReadOnlyCollection<SearchResultPackage> searchResultPackages =
                NugetSearchApiResultDeserializer.Deserialize(
                    _nugetToolSearchApiRequest.GetResult(nugetSearchApiParameter).GetAwaiter().GetResult());

            _searchResultPrinter.Print(isDetailed, searchResultPackages);

            return 0;
        }
    }
}
