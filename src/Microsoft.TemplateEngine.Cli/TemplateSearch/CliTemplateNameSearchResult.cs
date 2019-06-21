using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    public class CliTemplateNameSearchResult : TemplateNameSearchResult
    {
        public CliTemplateNameSearchResult(ITemplateInfo template, PackInfo packInfo, HostSpecificTemplateData hostSpecificData)
            : base(template, packInfo)
        {
            HostSpecificTemplateData = hostSpecificData;
        }

        public HostSpecificTemplateData HostSpecificTemplateData { get; }
    }
}
