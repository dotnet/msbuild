using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public enum AllowPostActionsSetting
    {
        No,
        Yes,
        Prompt
    };

    public class PostActionDispatcher
    {
        private readonly TemplateCreationResult _creationResult;
        private readonly IEngineEnvironmentSettings _environment;
        private readonly New3Callbacks _callbacks;
        private readonly AllowPostActionsSetting _canRunScripts;
        private readonly bool _isDryRun;

        public PostActionDispatcher(IEngineEnvironmentSettings environment, New3Callbacks callbacks, TemplateCreationResult creationResult, AllowPostActionsSetting canRunStatus, bool isDryRun)
        {
            _environment = environment;
            _callbacks = callbacks;
            _creationResult = creationResult;
            _canRunScripts = canRunStatus;
            _isDryRun = isDryRun;
        }

        public void Process(Func<string> inputGetter)
        {
            IReadOnlyList<IPostAction> postActions = _creationResult.ResultInfo?.PostActions ?? _creationResult.CreationEffects.CreationResult.PostActions;
            if (postActions.Count > 0)
            {
                Reporter.Output.WriteLine();
                Reporter.Output.WriteLine(LocalizableStrings.ProcessingPostActions);
            }

            foreach (IPostAction action in postActions)
            {
                IPostActionProcessor actionProcessor = null;

                if (action.ActionId != null)
                {
                    _environment.SettingsLoader.Components.TryGetComponent(action.ActionId, out actionProcessor);
                }

                bool result = false;

                if (actionProcessor == null)
                {   // The host doesn't know how to handle this action, just display instructions.
                    result = DisplayInstructionsForAction(action);
                }
                else if (actionProcessor is ProcessStartPostActionProcessor)
                {
                    if (_canRunScripts == AllowPostActionsSetting.No || _isDryRun)
                    {
                        DisplayInstructionsForAction(action);
                        result = false; // post action didn't run, it's an error in the sense that continue on error sees it.
                    }
                    else if (_canRunScripts == AllowPostActionsSetting.Yes)
                    {
                        result = ProcessAction(action, actionProcessor);
                    }
                    else if (_canRunScripts == AllowPostActionsSetting.Prompt)
                    {
                        result = HandlePromptRequired(action, actionProcessor, inputGetter);
                    }
                    // no trailing else - no other cases.
                }
                else
                {
                    if (!_isDryRun)
                    {
                        result = ProcessAction(action, actionProcessor);
                    }
                    else
                    {
                        Reporter.Output.WriteLine(LocalizableStrings.ActionWouldHaveBeenTakenAutomatically);

                        if (!string.IsNullOrWhiteSpace(action.Description))
                        {
                            Reporter.Output.WriteLine("  " + action.Description);
                            result = true;
                        }
                    }

                    if (!result && !string.IsNullOrEmpty(action.ManualInstructions))
                    {
                        Reporter.Output.WriteLine(LocalizableStrings.PostActionFailedInstructionHeader);
                        DisplayInstructionsForAction(action);
                    }
                }

                if (!result && !action.ContinueOnError)
                {
                    break;
                }

                Reporter.Output.WriteLine();
            }
        }

        // If the action is just instructions, display them and be done with the action.
        // Otherwise ask the user if they want to run the action. 
        // If they do, run it, and return the result.
        // Otherwise return false, indicating the action was not run. 
        private bool HandlePromptRequired(IPostAction action, IPostActionProcessor actionProcessor, Func<string> inputGetter)
        {
            if (actionProcessor is InstructionDisplayPostActionProcessor)
            {   // it's just instructions, no need to prompt
                bool result = ProcessAction(action, actionProcessor);
                return result;
            }

            // TODO: determine if this is the proper way to get input.
            bool userWantsToRunAction = AskUserIfActionShouldRun(action, inputGetter);

            if (!userWantsToRunAction)
            {
                return false;
            }

            return ProcessAction(action, actionProcessor);
        }

        private bool AskUserIfActionShouldRun(IPostAction action, Func<string> inputGetter)
        {
            const string YesAnswer = "Y";
            const string NoAnswer = "N";

            Reporter.Output.WriteLine(LocalizableStrings.PostActionPromptHeader);
            DisplayInstructionsForAction(action);

            Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostActionPromptRequest, YesAnswer, NoAnswer));

            do
            {
                string input = inputGetter();

                if (input.Equals(YesAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (input.Equals(NoAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostActionInvalidInputRePrompt, input, YesAnswer, NoAnswer));
            } while (true);
        }

        private bool ProcessAction(IPostAction action, IPostActionProcessor actionProcessor)
        {
            if(actionProcessor is PostActionProcessor2Base actionProcessor2Base)
            {
                actionProcessor2Base.Callbacks = _callbacks;
            }

            if (actionProcessor is IPostActionProcessor2 actionProcessor2 && _creationResult.CreationEffects is ICreationEffects2 creationEffects)
            {
                return actionProcessor2.Process(_environment, action, creationEffects, _creationResult.ResultInfo, _creationResult.OutputBaseDirectory);
            }

            return actionProcessor.Process(_environment, action, _creationResult.ResultInfo, _creationResult.OutputBaseDirectory);
        }

        private bool DisplayInstructionsForAction(IPostAction action)
        {
            IPostActionProcessor instructionProcessor = new InstructionDisplayPostActionProcessor();
            return ProcessAction(action, instructionProcessor);
        }
    }
}
