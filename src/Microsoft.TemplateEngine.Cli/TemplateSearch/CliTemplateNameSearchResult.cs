using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal class CliTemplateNameSearchResult : TemplateNameSearchResult
    {
        internal CliTemplateNameSearchResult(ITemplateInfo template, PackInfo packInfo, HostSpecificTemplateData hostSpecificData)
            : base(template, packInfo)
        {
            HostSpecificTemplateData = hostSpecificData;
        }

        internal HostSpecificTemplateData HostSpecificTemplateData { get; }
    }
}
