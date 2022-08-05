// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    public static class CliHostSearchCacheData
    {
        public const string DataName = "cliHostData";
        private static readonly string[] _hostDataPropertyNames = new[] { "isHidden", "SymbolInfo", "UsageExamples" };

        public static Func<object, object> Reader => (obj) =>
        {
            JObject? cacheObject = obj as JObject;
            if (cacheObject == null)
            {
                return HostSpecificTemplateData.Default;
            }
            try
            {
                if (_hostDataPropertyNames.Contains(cacheObject.Properties().First().Name, StringComparer.OrdinalIgnoreCase))
                {
                    return new HostSpecificTemplateData(cacheObject);
                }

                //fallback to old behavior
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
            }
            catch (Exception ex)
            {
                Reporter.Verbose.WriteLine($"Error deserializing the cli host specific template data {cacheObject}, details:{ex}");
            }
            return HostSpecificTemplateData.Default;
        };
    }
}
