// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal static class CliTemplateSearchCoordinatorFactory
    {
        internal const string CliHostDataName = "cliHostData";
        private static readonly string[] _hostDataPropertyNames = new[] { "isHidden,", "SymbolInfo", "UsageExamples" };

        private static readonly Func<object, object> CliHostDataReader = (obj) =>
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

        internal static TemplateSearchCoordinator CreateCliTemplateSearchCoordinator(
            IEngineEnvironmentSettings environmentSettings)
        {
            Dictionary<string, Func<object, object>> dataReaders = new Dictionary<string, Func<object, object>>()
            {
                { CliHostDataName, CliHostDataReader }
            };

            return new TemplateSearchCoordinator(environmentSettings, dataReaders);
        }
    }
}
