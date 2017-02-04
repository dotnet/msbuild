using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public class PostActionDispatcher
    {
        private TemplateCreationResult _creationResult;
        private IComponentManager _components;

        public PostActionDispatcher(TemplateCreationResult creationResult, IComponentManager components)
        {
            _creationResult = creationResult;
            _components = components;
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

                if (action.ActionId == null || !_components.TryGetComponent(action.ActionId, out actionProcessor))
                {
                    actionProcessor = new InstructionDisplayPostActionProcessor();
                }

                bool result = actionProcessor.Process(action, _creationResult.ResultInfo, _creationResult.OutputBaseDirectory);

                if (!result && !action.ContinueOnError)
                {   
                    break;
                }
            }
        }
    }
}
