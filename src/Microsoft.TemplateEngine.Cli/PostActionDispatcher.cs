using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public class PostActionDispatcher
    {
        private readonly TemplateCreationResult _creationResult;
        private readonly IEngineEnvironmentSettings _settings;

        public PostActionDispatcher(IEngineEnvironmentSettings settings, TemplateCreationResult creationResult)
        {
            _settings = settings;
            _creationResult = creationResult;
        }

        public void Process()
        {
            if (_creationResult.ResultInfo.PostActions.Count > 0)
            {
                Reporter.Output.WriteLine();
                Reporter.Output.WriteLine("Processing Post Actions");
            }

            foreach (IPostAction action in _creationResult.ResultInfo.PostActions)
            {
                IPostActionProcessor actionProcessor = null;

                if (action.ActionId == null || !_settings.SettingsLoader.Components.TryGetComponent(action.ActionId, out actionProcessor) || actionProcessor == null)
                {
                    actionProcessor = new InstructionDisplayPostActionProcessor();
                }

                bool result = actionProcessor.Process(_settings, action, _creationResult.ResultInfo, _creationResult.OutputBaseDirectory);

                if (!result && !action.ContinueOnError)
                {   
                    break;
                }
            }
        }
    }
}
