// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateSearch.Common;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliNuGetSearchCacheConfig : NuGetSearchCacheConfig
    {
        internal const string CliHostDataName = "cliHostData";

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

        internal CliNuGetSearchCacheConfig(string templateDiscoveryFileName)
                    : base(templateDiscoveryFileName)
        {
            AdditionalDataReaders[CliHostDataName] = CliHostDataReader;
        }
    }
}
