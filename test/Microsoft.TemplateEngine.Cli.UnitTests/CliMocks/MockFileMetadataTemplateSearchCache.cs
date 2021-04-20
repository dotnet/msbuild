// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal class MockFileMetadataTemplateSearchCache : CliNuGetMetadataTemplateSearchCache
    {
        public MockFileMetadataTemplateSearchCache(IEngineEnvironmentSettings environmentSettings, string pathToMetadata)
            : base(environmentSettings, pathToMetadata)
        {
        }

        public void SetupMockData(TemplateDiscoveryMetadata templateDiscoveryMetadata)
        {
            TemplateDiscoveryMetadata = templateDiscoveryMetadata;
        }

        // Does nothing. Prevents the base versions from being called.
        protected override void EnsureInitialized()
        {
            if (IsInitialized)
            {
                return;
            }

            TemplateToPackMap = TemplateToPackMap.FromPackToTemplateDictionary(TemplateDiscoveryMetadata.PackToTemplateMap);

            SetupCliHostSpecificData();

            IsInitialized = true;
        }
    }
}
