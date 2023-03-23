// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;

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
