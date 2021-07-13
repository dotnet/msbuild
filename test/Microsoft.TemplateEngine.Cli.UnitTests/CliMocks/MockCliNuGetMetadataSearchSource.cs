// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal class MockCliNuGetMetadataSearchSource : CliNuGetMetadataSearchSource
    {
        private static TemplateDiscoveryMetadata _templateDiscoveryMetadata;

        public MockCliNuGetMetadataSearchSource()
                    : base()
        {
        }

        // Only exists on the mock, to give specific search data
        public static void SetupMockData(TemplateDiscoveryMetadata templateDiscoveryMetadata)
        {
            _templateDiscoveryMetadata = templateDiscoveryMetadata;
        }

        public override Task<bool> TryConfigureAsync(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IManagedTemplatePackage> existingTemplatePackages, CancellationToken cancellationToken)
        {
            IFileMetadataTemplateSearchCache searchCache = CreateSearchCache(environmentSettings);
            NupkgHigherVersionInstalledPackFilter packFilter = new NupkgHigherVersionInstalledPackFilter(existingTemplatePackages);
            Configure(searchCache, packFilter);

            return Task.FromResult(true);
        }

        protected override IFileMetadataTemplateSearchCache CreateSearchCache(IEngineEnvironmentSettings environmentSettings)
        {
            if (_templateDiscoveryMetadata == null)
            {
                throw new Exception("MockCliNuGetMetadataSearchSource must be initialized with SetupMockData()");
            }

            // setup a mock search cache using _templateDiscoveryMetadata
            MockFileMetadataTemplateSearchCache searchCache = new MockFileMetadataTemplateSearchCache(environmentSettings, null);
            searchCache.SetupMockData(_templateDiscoveryMetadata);

            return searchCache;
        }
    }
}
