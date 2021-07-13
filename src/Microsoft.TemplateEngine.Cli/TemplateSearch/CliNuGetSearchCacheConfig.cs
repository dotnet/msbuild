// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliNuGetSearchCacheConfig : NuGetSearchCacheConfig
    {
        internal const string CliHostDataName = "cliHostData";

        private static readonly Func<JObject, object> CliHostDataReader = (cacheObject) =>
        {
            Dictionary<string, HostSpecificTemplateData> cliData = new Dictionary<string, HostSpecificTemplateData>();
            foreach (JProperty data in cacheObject.Properties())
            {
                try
                {
                    cliData[data.Name] = new HostSpecificTemplateData(data.Value as JObject);
                }
                catch (Exception ex)
                {
                    Reporter.Verbose.WriteLine($"Error deserializing the cli host specific template data for template {data.Name}, details:{ex}");
                }
            }
            return cliData;
        };

        internal CliNuGetSearchCacheConfig(string templateDiscoveryFileName)
                    : base(templateDiscoveryFileName)
        {
            AdditionalDataReaders[CliHostDataName] = CliHostDataReader;
        }
    }
}
