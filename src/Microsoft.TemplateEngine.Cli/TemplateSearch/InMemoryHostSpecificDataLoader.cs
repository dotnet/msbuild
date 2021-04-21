// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class InMemoryHostSpecificDataLoader : IHostSpecificDataLoader
    {
        // Keys are the template identities, values are the host data to return.
        private IReadOnlyDictionary<string, HostSpecificTemplateData> _hostSpecificData;

        internal InMemoryHostSpecificDataLoader(IReadOnlyDictionary<string, HostSpecificTemplateData> hostSpecificData)
        {
            _hostSpecificData = hostSpecificData;
        }

        public HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            if (_hostSpecificData.TryGetValue(templateInfo.Identity, out HostSpecificTemplateData data))
            {
                return data;
            }

            return HostSpecificTemplateData.Default;
        }
    }
}
