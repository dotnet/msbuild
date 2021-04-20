// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliNuGetMetadataTemplateSearchCache : FileMetadataTemplateSearchCache
    {
        protected IReadOnlyDictionary<string, HostSpecificTemplateData> _cliHostSpecificData { get; set; }

        internal CliNuGetMetadataTemplateSearchCache(IEngineEnvironmentSettings environmentSettings, string pathToMetadata)
            : base(environmentSettings, pathToMetadata)
        {
        }

        protected override NuGetSearchCacheConfig SetupSearchCacheConfig()
        {
            return new CliNuGetSearchCacheConfig(_pathToMetadta);
        }

        protected override void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            base.EnsureInitialized();

            SetupCliHostSpecificData();
        }

        protected void SetupCliHostSpecificData()
        {
            try
            {
                if (_templateDiscoveryMetadata.AdditionalData.TryGetValue(CliNuGetSearchCacheConfig.CliHostDataName, out object cliHostDataObject))
                {
                    _cliHostSpecificData = (Dictionary<string, HostSpecificTemplateData>)cliHostDataObject;
                    return;
                }
            }
            catch
            {
                // It's ok for the host data to not exist, or not be properly read.
            }

            // set a default for when there isn't any in the discovery metadata, or when there's an exception.
            _cliHostSpecificData = new Dictionary<string, HostSpecificTemplateData>();
        }

        internal IReadOnlyDictionary<string, HostSpecificTemplateData> GetHostDataForTemplateIdentities(IReadOnlyList<string> identities)
        {
            EnsureInitialized();

            Dictionary<string, HostSpecificTemplateData> map = new Dictionary<string, HostSpecificTemplateData>();

            foreach (string templateIdentity in identities)
            {
                if (_cliHostSpecificData.TryGetValue(templateIdentity, out HostSpecificTemplateData hostData))
                {
                    map[templateIdentity] = hostData;
                }
            }

            return map;
        }

        internal bool TryGetHostDataForTemplateIdentity(string identity, out HostSpecificTemplateData hostData)
        {
            EnsureInitialized();

            return _cliHostSpecificData.TryGetValue(identity, out hostData);
        }
    }
}
