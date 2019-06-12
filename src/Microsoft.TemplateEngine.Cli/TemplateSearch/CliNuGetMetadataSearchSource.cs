using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliNuGetMetadataSearchSource : NuGetMetadataSearchSource
    {
        public CliNuGetMetadataSearchSource()
            : base()
        {
        }

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
