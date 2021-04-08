using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class InMemoryHostSpecificDataLoader : IHostSpecificDataLoader
    {
        internal InMemoryHostSpecificDataLoader(IReadOnlyDictionary<string, HostSpecificTemplateData> hostSpecificData)
        {
            _hostSpecificData = hostSpecificData;
        }

        // Keys are the template identities, values are the host data to return.
        private IReadOnlyDictionary<string, HostSpecificTemplateData> _hostSpecificData;

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
