// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal static class CliTemplateSearchCoordinatorFactory
    {
        internal static TemplateSearchCoordinator CreateCliTemplateSearchCoordinator(
            IEngineEnvironmentSettings environmentSettings)
        {
            Dictionary<string, Func<object, object>> dataReaders = new()
            {
                { CliHostSearchCacheData.DataName, CliHostSearchCacheData.Reader }
            };

            return new TemplateSearchCoordinator(environmentSettings, dataReaders);
        }
    }
}
