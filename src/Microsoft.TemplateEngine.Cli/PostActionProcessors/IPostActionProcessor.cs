using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public interface IPostActionProcessor : IIdentifiedComponent
    {
        bool Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationResult templateCreationResult, string outputBasePath);
    }

    public interface IPostActionProcessor2 : IIdentifiedComponent
    {
        bool Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects2 creationEffects, string outputBasePath);
    }
}
