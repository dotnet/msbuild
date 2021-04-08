using System;
using System.Collections.Generic;
using Microsoft.TemplateSearch.Common;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliNuGetSearchCacheConfig : NuGetSearchCacheConfig
    {
        internal static readonly string CliHostDataName = "cliHostData";

        internal CliNuGetSearchCacheConfig(string templateDiscoveryFileName)
            : base(templateDiscoveryFileName)
        {
            _additionalDataReaders[CliHostDataName] = CliHostDataReader;
        }

        private static readonly Func<JObject, object> CliHostDataReader = (cacheObject) =>
        {
            try
            {
                return cacheObject.ToObject<Dictionary<string, HostSpecificTemplateData>>();
            }
            catch (Exception ex)
            {
                throw new Exception("Error deserializing the cli host specific template data.", ex);
            }
        };
    }
}
