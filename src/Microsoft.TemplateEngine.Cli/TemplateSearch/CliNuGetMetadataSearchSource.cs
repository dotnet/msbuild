// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliNuGetMetadataSearchSource : NuGetMetadataSearchSource
    {
        public override Guid Id => new Guid("6EA368C4-8A56-444C-91D1-55150B296BF2");

        protected override IFileMetadataTemplateSearchCache CreateSearchCache(IEngineEnvironmentSettings environmentSettings)
        {
            return new CliNuGetMetadataTemplateSearchCache(environmentSettings, _templateDiscoveryMetadataFile);
        }

        protected override TemplateNameSearchResult CreateNameSearchResult(ITemplateInfo candidateTemplateInfo, PackInfo candidatePackInfo)
        {
            if (_searchCache is CliNuGetMetadataTemplateSearchCache cliSearchCache
                && cliSearchCache.TryGetHostDataForTemplateIdentity(candidateTemplateInfo.Identity, out HostSpecificTemplateData hostData))
            {
                return new CliTemplateNameSearchResult(candidateTemplateInfo, candidatePackInfo, hostData);
            }
            else
            {
                return new CliTemplateNameSearchResult(candidateTemplateInfo, candidatePackInfo, HostSpecificTemplateData.Default);
            }
        }
    }
}
