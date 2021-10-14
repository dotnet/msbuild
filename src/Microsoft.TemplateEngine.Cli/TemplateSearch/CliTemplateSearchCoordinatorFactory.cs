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
        internal static TemplateSearchCoordinator CreateCliTemplateSearchCoordinator(
            IEngineEnvironmentSettings environmentSettings)
        {
            Dictionary<string, Func<object, object>> dataReaders = new Dictionary<string, Func<object, object>>()
            {
                { CliHostSearchCacheData.DataName, CliHostSearchCacheData.Reader }
            };

            return new TemplateSearchCoordinator(environmentSettings, dataReaders);
        }
    }
}
