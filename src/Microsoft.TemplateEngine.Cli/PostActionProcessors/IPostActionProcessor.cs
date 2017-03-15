using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public interface IPostActionProcessor : IIdentifiedComponent
    {
        bool Process(IPostAction action, ICreationResult templateCreationResult, string outputBasePath);
    }
}
