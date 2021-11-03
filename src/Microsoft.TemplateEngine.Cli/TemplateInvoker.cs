// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Utils;
using CreationResultStatus = Microsoft.TemplateEngine.Edge.Template.CreationResultStatus;
using ITemplateCreationResult = Microsoft.TemplateEngine.Edge.Template.ITemplateCreationResult;
using TemplateCreator = Microsoft.TemplateEngine.Edge.Template.TemplateCreator;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateInvoker
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly Func<string> _inputGetter;
        private readonly NewCommandCallbacks _callbacks;
        private readonly TemplateCreator _templateCreator;
        private readonly PostActionDispatcher _postActionDispatcher;

        internal TemplateInvoker(
            IEngineEnvironmentSettings environment,
            ITelemetryLogger telemetryLogger,
            Func<string> inputGetter,
            NewCommandCallbacks callbacks)
        {
            _environmentSettings = environment;
            _telemetryLogger = telemetryLogger;
            _inputGetter = inputGetter;
            _callbacks = callbacks;

            _templateCreator = new TemplateCreator(_environmentSettings);
            _postActionDispatcher = new PostActionDispatcher(_environmentSettings, _callbacks, _inputGetter);
        }

        internal async Task<NewCommandStatus> InvokeTemplateAsync(TemplateArgs templateArgs, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? templateLanguage = templateArgs.Template.GetLanguage();
            bool isMicrosoftAuthored = string.Equals(templateArgs.Template.Author, "Microsoft", StringComparison.OrdinalIgnoreCase);
            string? framework = null;
            string? auth = null;
            string? templateName = TelemetryHelper.HashWithNormalizedCasing(templateArgs.Template.Identity);

            if (isMicrosoftAuthored)
            {
                templateArgs.TemplateParameters.TryGetValue("Framework", out string? inputFrameworkValue);
                framework = TelemetryHelper.HashWithNormalizedCasing(TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateArgs.Template, "Framework", inputFrameworkValue));

                templateArgs.TemplateParameters.TryGetValue("auth", out string? inputAuthValue);
                auth = TelemetryHelper.HashWithNormalizedCasing(TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateArgs.Template, "auth", inputAuthValue));
            }

            bool success = true;

            try
            {
                return await CreateTemplateAsync(templateArgs, cancellationToken).ConfigureAwait(false);
            }
            catch (ContentGenerationException cx)
            {
                success = false;
                Reporter.Error.WriteLine(cx.Message.Bold().Red());
                if (cx.InnerException != null)
                {
                    Reporter.Error.WriteLine(cx.InnerException.Message.Bold().Red());
                }

                return NewCommandStatus.CreateFailed;
            }
            catch (Exception ex)
            {
                success = false;
                Reporter.Error.WriteLine(ex.Message.Bold().Red());
            }
            finally
            {
                _telemetryLogger.TrackEvent(templateArgs.NewCommandName + TelemetryConstants.CreateEventSuffix, new Dictionary<string, string?>
                    {
                        { TelemetryConstants.Language, templateLanguage },
                        { TelemetryConstants.ArgError, "False" },
                        { TelemetryConstants.Framework, framework },
                        { TelemetryConstants.TemplateName, templateName },
                        { TelemetryConstants.IsTemplateThirdParty, (!isMicrosoftAuthored).ToString() },
                        { TelemetryConstants.CreationResult, success.ToString() },
                        { TelemetryConstants.Auth, auth }
                    });
            }

            return NewCommandStatus.CreateFailed;

        }

        private static string GetChangeString(ChangeKind kind)
        {
            return kind switch
            {
                ChangeKind.Create => LocalizableStrings.Create,
                ChangeKind.Change => LocalizableStrings.Change,
                ChangeKind.Delete => LocalizableStrings.Delete,
                ChangeKind.Overwrite => LocalizableStrings.Overwrite,
                _ => LocalizableStrings.UnknownChangeKind
            };
        }

        private async Task<NewCommandStatus> CreateTemplateAsync(TemplateArgs templateArgs, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            char[] invalidChars = Path.GetInvalidFileNameChars();

            if (templateArgs.Name != null && templateArgs.Name.IndexOfAny(invalidChars) > -1)
            {
                string printableChars = string.Join(", ", invalidChars.Where(x => !char.IsControl(x)).Select(x => $"'{x}'"));
                string nonPrintableChars = string.Join(", ", invalidChars.Where(char.IsControl).Select(x => $"char({(int)x})"));
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.InvalidNameParameter, printableChars, nonPrintableChars).Bold().Red());
                return NewCommandStatus.CreateFailed;
            }

            string? fallbackName = new DirectoryInfo(
                !string.IsNullOrWhiteSpace(templateArgs.OutputPath)
                    ? templateArgs.OutputPath
                    : Directory.GetCurrentDirectory())
                .Name;

            if (string.IsNullOrEmpty(fallbackName) || string.Equals(fallbackName, "/", StringComparison.Ordinal))
            {
                // DirectoryInfo("/").Name on *nix returns "/", as opposed to null or "".
                fallbackName = null;
            }
            // Name returns <disk letter>:\ for root disk folder on Windows - replace invalid chars
            else if (fallbackName.IndexOfAny(invalidChars) > -1)
            {
                Regex pattern = new Regex($"[{Regex.Escape(new string(invalidChars))}]");
                fallbackName = pattern.Replace(fallbackName, "");
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    fallbackName = null;
                }
            }

            ITemplateCreationResult instantiateResult;

            try
            {
                instantiateResult = await _templateCreator.InstantiateAsync(
                    templateArgs.Template,
                    templateArgs.Name,
                    fallbackName,
                    templateArgs.OutputPath,
                    templateArgs.TemplateParameters,
                    templateArgs.IsForceFlagSpecified,
                    templateArgs.BaselineName,
                    templateArgs.IsDryRun,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ContentGenerationException cx)
            {
                Reporter.Error.WriteLine(cx.Message.Bold().Red());
                if (cx.InnerException != null)
                {
                    Reporter.Error.WriteLine(cx.InnerException.Message.Bold().Red());
                }

                return NewCommandStatus.CreateFailed;
            }
            catch (TemplateAuthoringException tae)
            {
                Reporter.Error.WriteLine(tae.Message.Bold().Red());
                return NewCommandStatus.CreateFailed;
            }

            string resultTemplateName = string.IsNullOrEmpty(instantiateResult.TemplateFullName) ? templateArgs.Template.Name : instantiateResult.TemplateFullName;

            switch (instantiateResult.Status)
            {
                case CreationResultStatus.Success:
                    if (!templateArgs.IsDryRun)
                    {
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.CreateSuccessful, resultTemplateName));
                    }
                    else
                    {
                        Reporter.Output.WriteLine(LocalizableStrings.FileActionsWouldHaveBeenTaken);
                        if (instantiateResult.CreationEffects != null)
                        {
                            foreach (IFileChange change in instantiateResult.CreationEffects.FileChanges)
                            {
                                Reporter.Output.WriteLine($"  {GetChangeString(change.ChangeKind)}: {change.TargetRelativePath}");
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(templateArgs.Template.ThirdPartyNotices))
                    {
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.ThirdPartyNotices, templateArgs.Template.ThirdPartyNotices));
                    }

                    return HandlePostActions(instantiateResult, templateArgs);
                case CreationResultStatus.CreateFailed:
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.CreateFailed, resultTemplateName, instantiateResult.ErrorMessage).Bold().Red());
                    return NewCommandStatus.CreateFailed;
                //this is unlikely case as these errors are caught on parse level now
                //TODO: discuss if we need better handling here, then enhance core to return canonical names as array and not parse them from error message
                case CreationResultStatus.MissingMandatoryParam:
                    if (!string.IsNullOrWhiteSpace(instantiateResult.ErrorMessage))
                    {
                        IReadOnlyList<string> missingParamNamesCanonical = instantiateResult.ErrorMessage.Split(new[] { ',' })
                            .Select(x => templateArgs.TryGetAliasForCanonicalName(x, out string? alias) ? alias! : x)
                            .ToList();
                        string fixedMessage = string.Join(", ", missingParamNamesCanonical);
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingRequiredParameter, fixedMessage, resultTemplateName).Bold().Red());
                    }
                    return NewCommandStatus.MissingMandatoryParam;
                case CreationResultStatus.NotFound:
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingTemplateContentDetected, templateArgs).Bold().Red());
                    return NewCommandStatus.NotFound;
                //this is unlikely case as these errors are caught on parse level now, so rely on proper error message from core.
                //TODO: discuss if we need better handling here, then enhance core to return canonical names as array and not parse them from error message
                case CreationResultStatus.InvalidParamValues:
                    Reporter.Error.WriteLine($"{LocalizableStrings.InvalidCommandOptions}: {instantiateResult.ErrorMessage}".Bold().Red());
                    Reporter.Error.WriteLine(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters);
                    Reporter.Error.WriteCommand(CommandExamples.HelpCommandExample(templateArgs.NewCommandName, templateArgs.Template.ShortNameList[0]));
                    return NewCommandStatus.InvalidParamValues;
                case CreationResultStatus.DestructiveChangesDetected:
                    Reporter.Error.WriteLine(LocalizableStrings.DestructiveChangesNotification.Bold().Red());
                    if (instantiateResult.CreationEffects != null)
                    {
                        IReadOnlyList<IFileChange> destructiveChanges = instantiateResult.CreationEffects.FileChanges.Where(x => x.ChangeKind != ChangeKind.Create).ToList();
                        int longestChangeTextLength = destructiveChanges.Max(x => GetChangeString(x.ChangeKind).Length);
                        int padLen = 5 + longestChangeTextLength;

                        foreach (IFileChange change in destructiveChanges)
                        {
                            string changeKind = GetChangeString(change.ChangeKind);
                            Reporter.Error.WriteLine(($"  {changeKind}".PadRight(padLen) + change.TargetRelativePath).Bold().Red());
                        }
                        Reporter.Error.WriteLine();
                    }
                    Reporter.Error.WriteLine(LocalizableStrings.RerunCommandAndPassForceToCreateAnyway.Bold().Red());
                    return NewCommandStatus.DestructiveChangesDetected;
                default:
                    return NewCommandStatus.UnexpectedResult;
            }
        }

        private NewCommandStatus HandlePostActions(ITemplateCreationResult creationResult, TemplateArgs args)
        {
            PostActionExecutionStatus result = _postActionDispatcher.Process(creationResult, args.IsDryRun, args.AllowScripts ?? AllowRunScripts.Prompt);

            return result switch
            {
                PostActionExecutionStatus.Success => NewCommandStatus.Success,
                PostActionExecutionStatus.Failure => NewCommandStatus.PostActionFailed,
                PostActionExecutionStatus.Cancelled => NewCommandStatus.Cancelled,
                PostActionExecutionStatus.Failure | PostActionExecutionStatus.Cancelled => NewCommandStatus.PostActionFailed,
                _ => NewCommandStatus.UnexpectedResult
            };
        }
    }
}
