using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal interface IPostActionProcessor : IIdentifiedComponent
    {
        bool Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationResult templateCreationResult, string outputBasePath);
    }

    internal interface IPostActionProcessor2 : IIdentifiedComponent
    {
        bool Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects2 creationEffects, ICreationResult templateCreationResult, string outputBasePath);
    }
}
