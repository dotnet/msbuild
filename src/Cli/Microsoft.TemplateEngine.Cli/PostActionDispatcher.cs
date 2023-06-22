// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    internal enum AllowRunScripts
    {
        No,
        Yes,
        Prompt
    }

    /// <summary>
    /// Indicates post action execution status.
    /// </summary>
    [Flags]
    internal enum PostActionExecutionStatus
    {
        /// <summary>
        /// All post actions executed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// One or more post actions failed.
        /// </summary>
        Failure = 1,

        /// <summary>
        /// User has cancelled post action execution.
        /// </summary>
        Cancelled = 2
    }

    internal class PostActionDispatcher
    {
        private readonly IEngineEnvironmentSettings _environment;
        private readonly Func<string> _inputGetter;

        /// <summary>
        /// Creates the instance.
        /// </summary>
        /// <param name="environment">template engine environment settings.</param>
        /// <param name="inputGetter">the predicate to get user input whether to run the post action.</param>
        internal PostActionDispatcher(IEngineEnvironmentSettings environment, Func<string> inputGetter)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _inputGetter = inputGetter ?? throw new ArgumentNullException(nameof(inputGetter));
        }

        /// <summary>
        /// Process post actions based on <paramref name="creationResult"/>.
        /// </summary>
        /// <param name="creationResult">the result of template creation or dry run.</param>
        /// <param name="isDryRun">true if dry run should be done, false if post actions should be performed.</param>
        /// <param name="canRunScripts">indicates if run script post action is allowed to be run.</param>
        /// <returns>
        /// <see cref="PostActionExecutionStatus"/> containing result of post action execution.<br />
        /// Note that if <see cref="IPostAction.ContinueOnError"/> is set to true, the result will be <see cref="PostActionExecutionStatus.Success"/> even if the post action fails.
        /// If the user cancelled post action with <see cref="IPostAction.ContinueOnError"/> set to true, the result will be <see cref="PostActionExecutionStatus.Cancelled"></see> anyway.<br />
        /// Note that <see cref="PostActionExecutionStatus"/> is a flags enum, and can contain multiple status if multiple post actions failed with different reason.
        /// </returns>
        internal PostActionExecutionStatus Process(ITemplateCreationResult creationResult, bool isDryRun, AllowRunScripts canRunScripts)
        {
            _ = creationResult ?? throw new ArgumentNullException(nameof(creationResult));
            _ = creationResult.CreationEffects ?? throw new ArgumentNullException(nameof(creationResult.CreationEffects));
            if (!isDryRun)
            {
                _ = creationResult.CreationResult ?? throw new ArgumentNullException(nameof(creationResult.CreationResult));
            }
            if (string.IsNullOrWhiteSpace(creationResult.OutputBaseDirectory))
            {
                throw new ArgumentNullException($"{nameof(creationResult.OutputBaseDirectory)} should not be null or whitespace", nameof(creationResult.OutputBaseDirectory));
            }

            IReadOnlyList<IPostAction> postActions = isDryRun
                ? creationResult.CreationEffects.CreationResult.PostActions
                : creationResult.CreationResult!.PostActions;

            if (postActions.Count > 0)
            {
                Reporter.Output.WriteLine();
                Reporter.Output.WriteLine(LocalizableStrings.ProcessingPostActions);
            }
            else
            {
                return PostActionExecutionStatus.Success;
            }

            PostActionExecutionStatus result = PostActionExecutionStatus.Success;
            foreach (IPostAction action in postActions)
            {
                if (isDryRun)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.ActionWouldHaveBeenTakenAutomatically);
                    if (!string.IsNullOrWhiteSpace(action.Description))
                    {
                        Reporter.Output.WriteLine(action.Description.Indent());
                    }
                    continue;
                }

                IPostActionProcessor? actionProcessor = null;
                _environment.Components.TryGetComponent(action.ActionId, out actionProcessor);

                if (actionProcessor == null)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostActionDispatcher_Error_NotSupported, action.ActionId);
                    if (!string.IsNullOrWhiteSpace(action.Description))
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.PostActionDescription, action.Description);
                    }
                    // The host doesn't know how to handle this action, just display manual instructions.
                    DisplayInstructionsForAction(action, useErrorOutput: true);
                    result |= PostActionExecutionStatus.Failure;
                }
                else if (actionProcessor is ProcessStartPostActionProcessor)
                {
                    if (canRunScripts == AllowRunScripts.No)
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.PostActionDispatcher_Error_RunScriptNotAllowed);
                        if (!string.IsNullOrWhiteSpace(action.Description))
                        {
                            Reporter.Error.WriteLine(LocalizableStrings.PostActionDescription, action.Description);
                        }
                        DisplayInstructionsForAction(action, useErrorOutput: true);
                        result |= PostActionExecutionStatus.Cancelled;
                    }
                    else if (canRunScripts == AllowRunScripts.Yes)
                    {
                        result |= ProcessAction(creationResult.CreationEffects, creationResult.CreationResult!, creationResult.OutputBaseDirectory, action, actionProcessor);
                    }
                    else if (canRunScripts == AllowRunScripts.Prompt)
                    {
                        // Ask the user if they want to run the action.
                        // If they do, run it, and return the result.
                        // Otherwise return cancelled, indicating the action was not run.
                        if (AskUserIfActionShouldRun(action))
                        {
                            result |= ProcessAction(creationResult.CreationEffects, creationResult.CreationResult!, creationResult.OutputBaseDirectory, action, actionProcessor);
                        }
                        else
                        {
                            result |= PostActionExecutionStatus.Cancelled;
                        }
                    }
                    // no trailing else - no other cases.
                }
                else // other post action
                {
                    result |= ProcessAction(creationResult.CreationEffects, creationResult.CreationResult!, creationResult.OutputBaseDirectory, action, actionProcessor);
                }
                if (result != PostActionExecutionStatus.Success)
                {
                    if (action.ContinueOnError)
                    {
                        result ^= PostActionExecutionStatus.Failure;
                    }
                    else
                    {
                        break;
                    }
                }
                Reporter.Output.WriteLine();
            }
            return result;
        }

        private bool AskUserIfActionShouldRun(IPostAction action)
        {
            const string YesAnswer = "Y";
            const string NoAnswer = "N";

            Reporter.Output.WriteLine(LocalizableStrings.PostActionPromptHeader);
            if (!string.IsNullOrWhiteSpace(action.Description))
            {
                Reporter.Output.WriteLine(LocalizableStrings.PostActionDescription, action.Description);
            }
            // actual command that will be run by 'Run script' post action
            if (action.Args != null && action.Args.TryGetValue("executable", out string? executable))
            {
                action.Args.TryGetValue("args", out string? commandArgs);
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostActionCommand, $"{executable} {commandArgs}").Bold().Red());
            }
            Reporter.Output.WriteLine(LocalizableStrings.PostActionPromptRequest, YesAnswer, NoAnswer);

            do
            {
                string input = _inputGetter();

                if (input.Equals(YesAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (input.Equals(NoAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Reporter.Output.WriteLine(LocalizableStrings.PostActionInvalidInputRePrompt, input, YesAnswer, NoAnswer);
            }
            while (true);
        }

        private PostActionExecutionStatus ProcessAction(
            ICreationEffects creationEffects,
            ICreationResult creationResult,
            string outputBaseDirectory,
            IPostAction action,
            IPostActionProcessor actionProcessor)
        {
            //catch all exceptions on post action execution
            //post actions can be added using components and it's not sure if they handle exceptions properly
            try
            {
                if (actionProcessor.Process(_environment, action, creationEffects, creationResult, outputBaseDirectory))
                {
                    return PostActionExecutionStatus.Success;
                }

                Reporter.Error.WriteLine(LocalizableStrings.PostActionFailedInstructionHeader);
                DisplayInstructionsForAction(action, useErrorOutput: true);
                return PostActionExecutionStatus.Failure;
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostActionFailedInstructionHeader);
                Reporter.Verbose.WriteLine(LocalizableStrings.Generic_Details, e.ToString());
                DisplayInstructionsForAction(action, useErrorOutput: true);
                return PostActionExecutionStatus.Failure;
            }

        }

        private void DisplayInstructionsForAction(IPostAction action, bool useErrorOutput = false)
        {
            if (string.IsNullOrWhiteSpace(action.ManualInstructions))
            {
                //no manual instructions was written by template author for post action
                return;
            }

            IReporter stream = useErrorOutput ? Reporter.Error : Reporter.Output;
            stream.WriteLine(LocalizableStrings.PostActionInstructions, action.ManualInstructions);

            // if the post action executes the command ('Run script' post action), additionally display command to be executed.
            if (action.Args != null && action.Args.TryGetValue("executable", out string? executable))
            {
                action.Args.TryGetValue("args", out string? commandArgs);
                stream.WriteLine(string.Format(LocalizableStrings.PostActionCommand, $"{executable} {commandArgs}").Bold().Red());
            }
        }
    }
}
