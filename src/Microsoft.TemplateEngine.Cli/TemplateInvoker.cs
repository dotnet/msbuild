// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Utils;
using CreationResultStatus = Microsoft.TemplateEngine.Edge.Template.CreationResultStatus;
using TemplateCreator = Microsoft.TemplateEngine.Edge.Template.TemplateCreator;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateInvoker
    {
        private readonly IEngineEnvironmentSettings _environment;
        private readonly INewCommandInput _commandInput;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly string _commandName;
        private readonly Func<string> _inputGetter;
        private readonly New3Callbacks _callbacks;

        private readonly TemplateCreator _templateCreator;
        private readonly IHostSpecificDataLoader _hostDataLoader;

        internal TemplateInvoker(IEngineEnvironmentSettings environment, INewCommandInput commandInput, ITelemetryLogger telemetryLogger, string commandName, Func<string> inputGetter, New3Callbacks callbacks)
        {
            _environment = environment;
            _commandInput = commandInput;
            _telemetryLogger = telemetryLogger;
            _commandName = commandName;
            _inputGetter = inputGetter;
            _callbacks = callbacks;

            _templateCreator = new TemplateCreator(_environment);
            _hostDataLoader = new HostSpecificDataLoader(_environment.SettingsLoader);
        }

        internal static bool CheckForArgsError(ITemplateMatchInfo template)
        {
            bool argsError;
            IEnumerable<string> invalidParams = template.GetInvalidParameterNames();

            if (invalidParams.Any())
            {
                HelpForTemplateResolution.DisplayInvalidParameters(invalidParams);
                argsError = true;
            }
            else
            {
                argsError = false;
            }

            return argsError;
        }

        internal async Task<New3CommandStatus> InvokeTemplate(ITemplateMatchInfo templateToInvoke)
        {
            string? templateLanguage = templateToInvoke.Info.GetLanguage();
            bool isMicrosoftAuthored = string.Equals(templateToInvoke.Info.Author, "Microsoft", StringComparison.OrdinalIgnoreCase);
            string? framework = null;
            string? auth = null;
            string templateName = TelemetryHelper.HashWithNormalizedCasing(templateToInvoke.Info.Identity);

            if (isMicrosoftAuthored)
            {
                _commandInput.InputTemplateParams.TryGetValue("Framework", out string? inputFrameworkValue);
                framework = TelemetryHelper.HashWithNormalizedCasing(TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateToInvoke.Info, "Framework", inputFrameworkValue));

                _commandInput.InputTemplateParams.TryGetValue("auth", out string? inputAuthValue);
                auth = TelemetryHelper.HashWithNormalizedCasing(TelemetryHelper.GetCanonicalValueForChoiceParamOrDefault(templateToInvoke.Info, "auth", inputAuthValue));
            }

            bool argsError = CheckForArgsError(templateToInvoke);
            if (argsError)
            {
                _telemetryLogger.TrackEvent(_commandName + TelemetryConstants.CreateEventSuffix, new Dictionary<string, string?>
                {
                    { TelemetryConstants.Language, templateLanguage },
                    { TelemetryConstants.ArgError, "True" },
                    { TelemetryConstants.Framework, framework },
                    { TelemetryConstants.TemplateName, templateName },
                    { TelemetryConstants.IsTemplateThirdParty, (!isMicrosoftAuthored).ToString() },
                    { TelemetryConstants.Auth, auth }
                });

                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{_commandName} {_commandInput.TemplateName}").Bold().Red());
                return New3CommandStatus.InvalidParamValues;
            }
            else
            {
                bool success = true;

                try
                {
                    return await CreateTemplateAsync(templateToInvoke).ConfigureAwait(false);
                }
                catch (ContentGenerationException cx)
                {
                    success = false;
                    Reporter.Error.WriteLine(cx.Message.Bold().Red());
                    if (cx.InnerException != null)
                    {
                        Reporter.Error.WriteLine(cx.InnerException.Message.Bold().Red());
                    }

                    return New3CommandStatus.CreateFailed;
                }
                catch (Exception ex)
                {
                    success = false;
                    Reporter.Error.WriteLine(ex.Message.Bold().Red());
                }
                finally
                {
                    _telemetryLogger.TrackEvent(_commandName + TelemetryConstants.CreateEventSuffix, new Dictionary<string, string?>
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

                return New3CommandStatus.CreateFailed;
            }
        }

        private static string GetChangeString(ChangeKind kind)
        {
            return kind switch
            {
                ChangeKind.Change => LocalizableStrings.Change,
                ChangeKind.Delete => LocalizableStrings.Delete,
                ChangeKind.Overwrite => LocalizableStrings.Overwrite,
                _ => LocalizableStrings.UnknownChangeKind
            };
        }

        // Attempts to invoke the template.
        // Warning: The _commandInput cannot be assumed to be in a state that is parsed for the template being invoked.
        //      So be sure to only get template-agnostic information from it. Anything specific to the template must be gotten from the ITemplateMatchInfo
        //      Or do a reparse if necessary (currently occurs in one error case).
        private async Task<New3CommandStatus> CreateTemplateAsync(ITemplateMatchInfo templateMatchDetails)
        {
            ITemplateInfo template = templateMatchDetails.Info;

            char[] invalidChars = Path.GetInvalidFileNameChars();

            if (_commandInput.Name != null && _commandInput.Name.IndexOfAny(invalidChars) > -1)
            {
                string printableChars = string.Join(", ", invalidChars.Where(x => !char.IsControl(x)).Select(x => $"'{x}'"));
                string nonPrintableChars = string.Join(", ", invalidChars.Where(char.IsControl).Select(x => $"char({(int)x})"));
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.InvalidNameParameter, printableChars, nonPrintableChars).Bold().Red());
                return New3CommandStatus.CreateFailed;
            }

            string? fallbackName = new DirectoryInfo(
                !string.IsNullOrWhiteSpace(_commandInput.OutputPath)
                    ? _commandInput.OutputPath
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

            Edge.Template.ITemplateCreationResult instantiateResult;

            try
            {
                instantiateResult = await _templateCreator.InstantiateAsync(
                    template,
                    _commandInput.Name,
                    fallbackName,
                    _commandInput.OutputPath,
                    templateMatchDetails.GetValidTemplateParameters(),
                    _commandInput.IsForceFlagSpecified,
                    _commandInput.BaselineName,
                    _commandInput.IsDryRun)
                    .ConfigureAwait(false);
            }
            catch (ContentGenerationException cx)
            {
                Reporter.Error.WriteLine(cx.Message.Bold().Red());
                if (cx.InnerException != null)
                {
                    Reporter.Error.WriteLine(cx.InnerException.Message.Bold().Red());
                }

                return New3CommandStatus.CreateFailed;
            }
            catch (TemplateAuthoringException tae)
            {
                Reporter.Error.WriteLine(tae.Message.Bold().Red());
                return New3CommandStatus.CreateFailed;
            }

            string resultTemplateName = string.IsNullOrEmpty(instantiateResult.TemplateFullName) ? _commandInput.TemplateName : instantiateResult.TemplateFullName;

            switch (instantiateResult.Status)
            {
                case CreationResultStatus.Success:
                    if (!_commandInput.IsDryRun)
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
                                Reporter.Output.WriteLine($"  {change.ChangeKind}: {change.TargetRelativePath}");
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(template.ThirdPartyNotices))
                    {
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.ThirdPartyNotices, template.ThirdPartyNotices));
                    }

                    HandlePostActions(instantiateResult);
                    return New3CommandStatus.Success;
                case CreationResultStatus.CreateFailed:
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.CreateFailed, resultTemplateName, instantiateResult.ErrorMessage).Bold().Red());
                    return New3CommandStatus.CreateFailed;
                case CreationResultStatus.MissingMandatoryParam:
                    if (string.Equals(instantiateResult.ErrorMessage, "--name", StringComparison.Ordinal))
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingRequiredParameter, instantiateResult.ErrorMessage, resultTemplateName).Bold().Red());
                    }
                    else if (!string.IsNullOrWhiteSpace(instantiateResult.ErrorMessage))
                    {
                        // TODO: rework to avoid having to reparse.
                        // The canonical info could be in the ITemplateMatchInfo, but currently isn't.
                        TemplateResolver.ParseTemplateArgs(template, _hostDataLoader, _commandInput);

                        IReadOnlyList<string> missingParamNamesCanonical = instantiateResult.ErrorMessage.Split(new[] { ',' })
                            .Select(x => _commandInput.VariantsForCanonical(x.Trim())
                                                        .DefaultIfEmpty(x.Trim()).First())
                            .ToList();
                        string fixedMessage = string.Join(", ", missingParamNamesCanonical);
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingRequiredParameter, fixedMessage, resultTemplateName).Bold().Red());
                    }
                    return New3CommandStatus.MissingMandatoryParam;
                case CreationResultStatus.NotFound:
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingTemplateContentDetected, _commandName).Bold().Red());
                    return New3CommandStatus.NotFound;
                case CreationResultStatus.InvalidParamValues:
                    TemplateUsageInformation? usageInformation = TemplateUsageHelp.GetTemplateUsageInformation(template, _environment, _commandInput, _hostDataLoader, _templateCreator);

                    if (usageInformation != null)
                    {
                        string invalidParamsError = InvalidParameterInfo.InvalidParameterListToString(usageInformation.Value.InvalidParameters);
                        Reporter.Error.WriteLine(invalidParamsError.Bold().Red());
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{_commandName} {_commandInput.TemplateName}").Bold().Red());
                    }
                    else
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingTemplateContentDetected, _commandName).Bold().Red());
                        return New3CommandStatus.NotFound;
                    }
                    return New3CommandStatus.InvalidParamValues;
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
                    return New3CommandStatus.DestructiveChangesDetected;
                default:
                    return New3CommandStatus.UnexpectedResult;
            }
        }

        private void HandlePostActions(Edge.Template.ITemplateCreationResult creationResult)
        {
            if (creationResult.Status != CreationResultStatus.Success)
            {
                return;
            }

            AllowPostActionsSetting scriptRunSettings;

            if (string.IsNullOrEmpty(_commandInput.AllowScriptsToRun) || string.Equals(_commandInput.AllowScriptsToRun, "prompt", StringComparison.OrdinalIgnoreCase))
            {
                scriptRunSettings = AllowPostActionsSetting.Prompt;
            }
            else if (string.Equals(_commandInput.AllowScriptsToRun, "yes", StringComparison.OrdinalIgnoreCase))
            {
                scriptRunSettings = AllowPostActionsSetting.Yes;
            }
            else if (string.Equals(_commandInput.AllowScriptsToRun, "no", StringComparison.OrdinalIgnoreCase))
            {
                scriptRunSettings = AllowPostActionsSetting.No;
            }
            else
            {
                scriptRunSettings = AllowPostActionsSetting.Prompt;
            }

            PostActionDispatcher postActionDispatcher = new PostActionDispatcher(_environment, _callbacks, creationResult, scriptRunSettings, _commandInput.IsDryRun);
            postActionDispatcher.Process(_inputGetter);
        }
    }
}
