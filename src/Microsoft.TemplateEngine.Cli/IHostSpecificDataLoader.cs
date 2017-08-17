using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    public interface IHostSpecificDataLoader
    {
        HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo);
    }
}
