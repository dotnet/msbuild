// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli
{
    public class New3Command
    {
        private static readonly IReadOnlyCollection<MatchLocation> NameFields = new HashSet<MatchLocation> { MatchLocation.Name, MatchLocation.ShortName, MatchLocation.Alias };
        private HostSpecificTemplateData _hostSpecificTemplateData;
        private IReadOnlyList<IFilteredTemplateInfo> _matchedTemplates;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly TemplateCreator _templateCreator;
        private readonly SettingsLoader _settingsLoader;
        private readonly AliasRegistry _aliasRegistry;
        private readonly Paths _paths;
        private readonly ExtendedTemplateEngineHost _host;
        private readonly INewCommandInput _commandInput;

        private static readonly Regex LocaleFormatRegex = new Regex(@"
                    ^
                        [a-z]{2}
                        (?:-[A-Z]{2})?
                    $"
            , RegexOptions.IgnorePatternWhitespace);
        private bool _forceAmbiguousFlow;
        private readonly Action<IEngineEnvironmentSettings, IInstaller> _onFirstRun;

        public New3Command(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, Action<IEngineEnvironmentSettings, IInstaller> onFirstRun, INewCommandInput commandInput)
        {
            _telemetryLogger = telemetryLogger;
            host = _host = new ExtendedTemplateEngineHost(host, this);
            EnvironmentSettings = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
            _settingsLoader = (SettingsLoader)EnvironmentSettings.SettingsLoader;
            Installer = new Installer(EnvironmentSettings);
            _templateCreator = new TemplateCreator(EnvironmentSettings);
            _aliasRegistry = new AliasRegistry(EnvironmentSettings);
            CommandName = commandName;
            _paths = new Paths(EnvironmentSettings);
            _onFirstRun = onFirstRun;

            _commandInput = commandInput;
        }

        public string CommandName { get; }

        public static IInstaller Installer { get; set; }

        public string TemplateName => _commandInput.TemplateName;

        public string OutputPath => _commandInput.OutputPath;

        private static bool AreAllTemplatesSameGroupIdentity(IEnumerable<IFilteredTemplateInfo> templateList)
        {
            return templateList.AllAreTheSame((x) => x.Info.GroupIdentity, StringComparer.OrdinalIgnoreCase);
        }

        private static IFilteredTemplateInfo FindHighestPrecedenceTemplateIfAllSameGroupIdentity(IReadOnlyList<IFilteredTemplateInfo> templateList)
        {
            if (!AreAllTemplatesSameGroupIdentity(templateList))
            {
                return null;
            }

            IFilteredTemplateInfo highestPrecedenceTemplate = null;

            foreach (IFilteredTemplateInfo template in templateList)
            {
                if (highestPrecedenceTemplate == null)
                {
                    highestPrecedenceTemplate = template;
                }
                else if (template.Info.Precedence > highestPrecedenceTemplate.Info.Precedence)
                {
                    highestPrecedenceTemplate = template;
                }
            }

            return highestPrecedenceTemplate;
        }

        private IReadOnlyList<IFilteredTemplateInfo> _unambiguousTemplateGroupToUse;

        // If a template group & language can be unambiguously determined, return those templates.
        // Otherwise return null.
        public IReadOnlyList<IFilteredTemplateInfo> UnambiguousTemplateGroupToUse
        {
            get
            {
                if (_unambiguousTemplateGroupToUse != null)
                {
                    return _unambiguousTemplateGroupToUse;
                }

                if (_forceAmbiguousFlow || _matchedTemplates == null || _matchedTemplates.Count == 0)
                {
                    return null;
                }

                if (_matchedTemplates.Count == 1)
                {
                    return _unambiguousTemplateGroupToUse = _matchedTemplates;
                }
                else if (string.IsNullOrEmpty(_commandInput.Language) && EnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage))
                {
                    IReadOnlyList<IFilteredTemplateInfo> languageMatchedTemplates = FindTemplatesExplicitlyMatchingLanguage(_matchedTemplates, defaultLanguage);

                    if (languageMatchedTemplates.Count == 1)
                    {
                        return _unambiguousTemplateGroupToUse = languageMatchedTemplates;
                    }
                }

                return _unambiguousTemplateGroupToUse = UseSecondaryCriteriaToDisambiguateTemplateMatches();
            }
        }

        // Returns the templates from the input list whose language is the input language.
        private static IReadOnlyList<IFilteredTemplateInfo> FindTemplatesExplicitlyMatchingLanguage(IEnumerable<IFilteredTemplateInfo> listToFilter, string language)
        {
            List<IFilteredTemplateInfo> languageMatches = new List<IFilteredTemplateInfo>();

            if (string.IsNullOrEmpty(language))
            {
                return languageMatches;
            }

            foreach (IFilteredTemplateInfo info in listToFilter)
            {
                // ChoicesAndDescriptions is invoked as case insensitive 
                if (info.Info.Tags == null ||
                    info.Info.Tags.TryGetValue("language", out ICacheTag languageTag) &&
                    languageTag.ChoicesAndDescriptions.ContainsKey(language))
                {
                    languageMatches.Add(info);
                }
            }

            return languageMatches;
        }

        // If a template group can be uniquely identified using secondary criteria, it's stored here.
        IReadOnlyList<IFilteredTemplateInfo> _secondaryFilteredUnambiguousTemplateGroupToUse;

        // The list of templates, including both primary and secondary criteria results
        private IReadOnlyList<IFilteredTemplateInfo> _matchedTemplatesWithSecondaryMatchInfo;

        // Does additional checks on the _matchedTemplates.
        // Stores the match details in _matchedTemplatesWithSecondaryMatchInfo
        // If there is exactly 1 template group & language that matches on secondary info, return it. Return null otherwise.
        private IReadOnlyList<IFilteredTemplateInfo> UseSecondaryCriteriaToDisambiguateTemplateMatches()
        {
            if (_secondaryFilteredUnambiguousTemplateGroupToUse == null && !string.IsNullOrEmpty(TemplateName))
            {
                if (_matchedTemplatesWithSecondaryMatchInfo == null)
                {
                    _matchedTemplatesWithSecondaryMatchInfo = FilterTemplatesOnParameters(_matchedTemplates).Where(x => x.IsMatch).ToList();

                    IReadOnlyList<IFilteredTemplateInfo> matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo.Where(x => x.IsParameterMatch).ToList();
                    if (_matchedTemplatesWithSecondaryMatchInfo.Any(x => x.HasAmbiguousParameterMatch))
                    {
                        matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo;
                    }

                    if (matchesAfterParameterChecks.Count == 0)
                    {   // no param matches, continue additional matching with the list from before param checking (but with the param match dispositions)
                        matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo;
                    }

                    if (matchesAfterParameterChecks.Count == 1)
                    {
                        return _secondaryFilteredUnambiguousTemplateGroupToUse = matchesAfterParameterChecks;
                    }
                    else if (string.IsNullOrEmpty(_commandInput.Language) && EnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage))
                    {
                        IReadOnlyList<IFilteredTemplateInfo> languageFiltered = FindTemplatesExplicitlyMatchingLanguage(matchesAfterParameterChecks, defaultLanguage);

                        if (languageFiltered.Count == 1)
                        {
                            return _secondaryFilteredUnambiguousTemplateGroupToUse = languageFiltered;
                        }
                        else if (AreAllTemplatesSameGroupIdentity(languageFiltered))
                        {
                            IReadOnlyList<IFilteredTemplateInfo> languageFilteredMatchesAfterParameterChecks = languageFiltered.Where(x => x.IsParameterMatch).ToList();
                            if (languageFilteredMatchesAfterParameterChecks.Count > 0 && !languageFiltered.Any(x => x.HasAmbiguousParameterMatch))
                            {
                                return _secondaryFilteredUnambiguousTemplateGroupToUse = languageFilteredMatchesAfterParameterChecks;
                            }

                            return _secondaryFilteredUnambiguousTemplateGroupToUse = languageFiltered;
                        }
                    }
                    else if (AreAllTemplatesSameGroupIdentity(matchesAfterParameterChecks))
                    {
                        return _secondaryFilteredUnambiguousTemplateGroupToUse = matchesAfterParameterChecks;
                    }
                }
            }

            return _secondaryFilteredUnambiguousTemplateGroupToUse;
        }

        public EngineEnvironmentSettings EnvironmentSettings { get; private set; }

        public static int Run(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, Action<IEngineEnvironmentSettings, IInstaller> onFirstRun, string[] args)
        {
            if (args.Any(x => string.Equals(x, "--debug:attach", StringComparison.Ordinal)))
            {
                Console.ReadLine();
            }

            if (args.Length == 0)
            {
                telemetryLogger.TrackEvent(commandName + "-CalledWithNoArgs");
            }

            // new parser
            INewCommandInput commandInput = new NewCommandInputCli(commandName);
            // old parser
            //INewCommandInput commandInput = ExtendedCommandParserSupport.SetupParser(commandName);

            New3Command instance = new New3Command(commandName, host, telemetryLogger, onFirstRun, commandInput);

            commandInput.OnExecute(instance.ExecuteAsync);

            int result;
            try
            {
                using (Timing.Over("Execute"))
                {
                    result = commandInput.Execute(args);
                }
            }
            catch (Exception ex)
            {
                AggregateException ax = ex as AggregateException;

                while (ax != null && ax.InnerExceptions.Count == 1)
                {
                    ex = ax.InnerException;
                    ax = ex as AggregateException;
                }

                Reporter.Error.WriteLine(ex.Message.Bold().Red());

                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    ax = ex as AggregateException;

                    while (ax != null && ax.InnerExceptions.Count == 1)
                    {
                        ex = ax.InnerException;
                        ax = ex as AggregateException;
                    }

                    Reporter.Error.WriteLine(ex.Message.Bold().Red());
                }

                Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                result = 1;
            }

            return result;
        }

        private void ConfigureEnvironment()
        {
            _onFirstRun?.Invoke(EnvironmentSettings, Installer);

            foreach (Type type in typeof(New3Command).GetTypeInfo().Assembly.GetTypes())
            {
                EnvironmentSettings.SettingsLoader.Components.Register(type);
            }
        }

        private async Task<CreationResultStatus> CreateTemplateAsync(ITemplateInfo template)
        {
            string fallbackName = new DirectoryInfo(_commandInput.OutputPath ?? Directory.GetCurrentDirectory()).Name;

            if (string.IsNullOrEmpty(fallbackName))
            {
                fallbackName = null;
            }

            TemplateCreationResult instantiateResult;

            try
            {
                instantiateResult = await _templateCreator.InstantiateAsync(template, _commandInput.Name, fallbackName, _commandInput.OutputPath, _commandInput.AllTemplateParams, _commandInput.SkipUpdateCheck, _commandInput.IsForceFlagSpecified).ConfigureAwait(false);
            }
            catch (ContentGenerationException cx)
            {
                Reporter.Error.WriteLine(cx.Message.Bold().Red());
                if(cx.InnerException != null)
                {
                    Reporter.Error.WriteLine(cx.InnerException.Message.Bold().Red());
                }

                return CreationResultStatus.CreateFailed;
            }
            catch (TemplateAuthoringException tae)
            {
                Reporter.Error.WriteLine(tae.Message.Bold().Red());
                return CreationResultStatus.CreateFailed;
            }

            string resultTemplateName = string.IsNullOrEmpty(instantiateResult.TemplateFullName) ? TemplateName : instantiateResult.TemplateFullName;

            switch (instantiateResult.Status)
            {
                case CreationResultStatus.Success:
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.CreateSuccessful, resultTemplateName));

                    if(!string.IsNullOrEmpty(template.ThirdPartyNotices))
                    {
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.ThirdPartyNotices, template.ThirdPartyNotices));
                    }

                    HandlePostActions(instantiateResult);
                    break;
                case CreationResultStatus.CreateFailed:
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.CreateFailed, resultTemplateName, instantiateResult.Message).Bold().Red());
                    break;
                case CreationResultStatus.MissingMandatoryParam:
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingRequiredParameter, instantiateResult.Message, resultTemplateName).Bold().Red());
                    break;
                case CreationResultStatus.OperationNotSpecified:
                    break;
                case CreationResultStatus.InvalidParamValues:
                    IReadOnlyList<InvalidParameterInfo> invalidParameterList = GetTemplateUsageInformation(template, out IParameterSet ps, out IReadOnlyList<string> userParamsWithInvalidValues, out bool hasPostActionScriptRunner);
                    string invalidParamsError = InvalidParameterInfo.InvalidParameterListToString(invalidParameterList);
                    Reporter.Error.WriteLine(invalidParamsError.Bold().Red());
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{CommandName} {TemplateName}").Bold().Red());
                    break;
                default:
                    break;
            }

            return instantiateResult.Status;
        }

        private IReadOnlyList<InvalidParameterInfo> GetTemplateUsageInformation(ITemplateInfo templateInfo, out IParameterSet allParams, out IReadOnlyList<string> userParamsWithInvalidValues, out bool hasPostActionScriptRunner)
        {
            ITemplate template = EnvironmentSettings.SettingsLoader.LoadTemplate(templateInfo);
            ParseTemplateArgs(templateInfo);
            allParams = _templateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName ?? "testName", out IList<string> defaultParamsWithInvalidValues);
            _templateCreator.ResolveUserParameters(template, allParams, _commandInput.AllTemplateParams, out userParamsWithInvalidValues);
            hasPostActionScriptRunner = CheckIfTemplateHasScriptRunningPostActions(template);

            List<InvalidParameterInfo> invalidParameters = new List<InvalidParameterInfo>();

            if (userParamsWithInvalidValues.Any())
            {
                // Lookup the input param formats - userParamsWithInvalidValues has canonical.
                IList<string> inputParamFormats = new List<string>();
                foreach (string canonical in userParamsWithInvalidValues)
                {
                    _commandInput.AllTemplateParams.TryGetValue(canonical, out string specifiedValue);
                    string inputFormat = _commandInput.TemplateParamInputFormat(canonical);
                    InvalidParameterInfo invalidParam = new InvalidParameterInfo(inputFormat, specifiedValue, canonical);
                    invalidParameters.Add(invalidParam);
                }
            }

            return invalidParameters;
        }

        private bool CheckIfTemplateHasScriptRunningPostActions(ITemplate template)
        {
            // use a throwaway set of params for getting the creation effects - it makes changes to them.
            string targetDir = _commandInput.OutputPath ?? EnvironmentSettings.Host.FileSystem.GetCurrentDirectory();
            IParameterSet paramsForCreationEffects = _templateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName ?? "testName", out IList<string> throwaway);
            _templateCreator.ResolveUserParameters(template, paramsForCreationEffects, _commandInput.AllTemplateParams, out IReadOnlyList<string> userParamsWithInvalidValues);
            ICreationEffects creationEffects = template.Generator.GetCreationEffects(EnvironmentSettings, template, paramsForCreationEffects, EnvironmentSettings.SettingsLoader.Components, targetDir);
            return creationEffects.CreationResult.PostActions.Any(x => x.ActionId == ProcessStartPostActionProcessor.ActionProcessorId);
        }

        private string GetLanguageMismatchErrorMessage(string inputLanguage)
        {
            string inputFlagForm;
            if (_commandInput.RemainingArguments.Contains("-lang"))
            {
                inputFlagForm = "-lang";
            }
            else
            {
                inputFlagForm = "--language";
            }

            string invalidLanguageErrorText = LocalizableStrings.InvalidTemplateParameterValues;
            invalidLanguageErrorText += Environment.NewLine + string.Format(LocalizableStrings.InvalidParameterDetail, inputFlagForm, inputLanguage, "language");
            return invalidLanguageErrorText;
        }

        private void HandlePostActions(TemplateCreationResult creationResult)
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

            PostActionDispatcher postActionDispatcher = new PostActionDispatcher(EnvironmentSettings, creationResult, scriptRunSettings);
            postActionDispatcher.Process(() => Console.ReadLine());
        }

        // Checks the result of TemplatesToDisplayInfoAbout()
        // If they all have the same group identity, return them.
        // Otherwise retun an empty list.
        private IEnumerable<ITemplateInfo> TemplatesToShowDetailedHelpAbout
        {
            get
            {
                IReadOnlyList<ITemplateInfo> candidateTemplates = TemplatesToDisplayInfoAbout;
                Func<ITemplateInfo, string> groupIdentitySelector = (x) => x.GroupIdentity;

                if (candidateTemplates.AllAreTheSame(groupIdentitySelector, StringComparer.OrdinalIgnoreCase))
                {
                    return candidateTemplates;
                }

                return new List<ITemplateInfo>();
            }
        }

        // If there are secondary matches, return them
        // Else if there are primary matches, return them
        // Otherwise return all templates in the current context
        private IReadOnlyList<ITemplateInfo> TemplatesToDisplayInfoAbout
        {
            get
            {
                IEnumerable<ITemplateInfo> templateList;

                if (UnambiguousTemplateGroupToUse != null && UnambiguousTemplateGroupToUse.Count > 0)
                {
                    templateList = UnambiguousTemplateGroupToUse.Select(x => x.Info);
                }
                else if (!string.IsNullOrEmpty(TemplateName) && _matchedTemplatesWithSecondaryMatchInfo != null && _matchedTemplatesWithSecondaryMatchInfo.Count > 0)
                {   // without template name, it's not reasonable to do secondary matching
                    templateList = _matchedTemplatesWithSecondaryMatchInfo.Select(x => x.Info);
                }
                else if (_matchedTemplates != null && _matchedTemplates.Any(x => x.IsMatch))
                {
                    templateList = _matchedTemplates.Where(x => x.IsMatch).Select(x => x.Info);
                }
                else if (_matchedTemplates != null && _matchedTemplates.Any(X => X.IsPartialMatch))
                {
                    templateList = _matchedTemplates.Where(x => x.IsPartialMatch).Select(x => x.Info);
                }
                else
                {
                    templateList = PerformAllTemplatesInContextQuery().Where(x => x.IsMatch).Select(x => x.Info);
                }

                return templateList.ToList();
            }
        }

        private void DisplayTemplateList()
        {
            IReadOnlyList<ITemplateInfo> results = TemplatesToDisplayInfoAbout;
            IEnumerable<IGrouping<string, ITemplateInfo>> grouped = results.GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity));
            EnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage);
            Dictionary<ITemplateInfo, string> templatesVersusLanguages = new Dictionary<ITemplateInfo, string>();

            foreach (IGrouping<string, ITemplateInfo> grouping in grouped)
            {
                List<string> languageForDisplay = new List<string>();
                HashSet<string> uniqueLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string defaultLanguageDisplay = string.Empty;

                foreach (ITemplateInfo info in grouping)
                {
                    if (info.Tags != null && info.Tags.TryGetValue("language", out ICacheTag languageTag))
                    {
                        foreach (string lang in languageTag.ChoicesAndDescriptions.Keys)
                        {
                            if (uniqueLanguages.Add(lang))
                            {
                                if (string.IsNullOrEmpty(_commandInput.Language) && string.Equals(defaultLanguage, lang, StringComparison.OrdinalIgnoreCase))
                                {
                                    defaultLanguageDisplay = $"[{lang}]";
                                }
                                else
                                {
                                    languageForDisplay.Add(lang);
                                }
                            }
                        }
                    }
                }

                languageForDisplay.Sort(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(defaultLanguageDisplay))
                {
                    languageForDisplay.Insert(0, defaultLanguageDisplay);
                }

                templatesVersusLanguages[grouping.First()] = string.Join(", ", languageForDisplay);
            }

            HelpFormatter<KeyValuePair<ITemplateInfo, string>> formatter = HelpFormatter.For(EnvironmentSettings, templatesVersusLanguages, 6, '-', false)
                .DefineColumn(t => t.Key.Name, LocalizableStrings.Templates)
                .DefineColumn(t => t.Key.ShortName, LocalizableStrings.ShortName)
                .DefineColumn(t => t.Value, out object languageColumn, LocalizableStrings.Language)
                .DefineColumn(t => t.Key.Classifications != null ? string.Join("/", t.Key.Classifications) : null, out object tagsColumn, LocalizableStrings.Tags)
                .OrderByDescending(languageColumn, new NullOrEmptyIsLastStringComparer())
                .OrderBy(tagsColumn);
            Reporter.Output.WriteLine(formatter.Layout());

            if (!_commandInput.IsListFlagSpecified)
            {
                Reporter.Output.WriteLine();
                ShowInvocationExamples();
                IList<ITemplateInfo> templatesToShow = TemplatesToShowDetailedHelpAbout.ToList();
                ShowTemplateGroupHelp(templatesToShow);
            }
        }

        private CreationResultStatus EnterAmbiguousTemplateManipulationFlow()
        {
            if (!string.IsNullOrEmpty(TemplateName)
                && _matchedTemplates.Count > 0
                && _matchedTemplates.All(x => x.MatchDisposition.Any(d => d.Location == MatchLocation.Language && d.Kind == MatchKind.Mismatch)))
            {
                string errorMessage = GetLanguageMismatchErrorMessage(_commandInput.Language);
                Reporter.Error.WriteLine(errorMessage.Bold().Red());
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{CommandName} {TemplateName}").Bold().Red());
                return CreationResultStatus.NotFound;
            }

            if (!ValidateRemainingParameters() || (!_commandInput.IsListFlagSpecified && !string.IsNullOrEmpty(TemplateName)))
            {
                bool anyPartialMatchesDisplayed = ShowTemplateNameMismatchHelp();
                DisplayTemplateList();
                return CreationResultStatus.NotFound;
            }

            if (!string.IsNullOrWhiteSpace(_commandInput.Alias))
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                Reporter.Error.WriteLine("  " + _commandInput.TemplateParamInputFormat("--alias").Bold().Red());
                return CreationResultStatus.NotFound;
            }

            if (_commandInput.IsHelpFlagSpecified)
            {
                _telemetryLogger.TrackEvent(CommandName + "-Help");
                ShowUsageHelp();
                DisplayTemplateList();
                return CreationResultStatus.Success;
            }
            else
            {
                DisplayTemplateList();

                //If we're showing the list because we were asked to, exit with success, otherwise, exit with failure
                if (_commandInput.IsListFlagSpecified)
                {
                    return CreationResultStatus.Success;
                }
                else
                {
                    return CreationResultStatus.OperationNotSpecified;
                }
            }
        }

        private CreationResultStatus EnterInstallFlow()
        {
            _telemetryLogger.TrackEvent(CommandName + "-Install", new Dictionary<string, string> { { "CountOfThingsToInstall", _commandInput.ToInstallList.Count.ToString() } });

            Installer.InstallPackages(_commandInput.ToInstallList);

            //TODO: When an installer that directly calls into NuGet is available,
            //  return a more accurate representation of the outcome of the operation
            return CreationResultStatus.Success;
        }

        private CreationResultStatus EnterMaintenanceFlow()
        {
            if (!ValidateRemainingParameters())
            {
                if (_commandInput.IsHelpFlagSpecified)
                {
                    _telemetryLogger.TrackEvent(CommandName + "-Help");
                    ShowUsageHelp();
                }
                else
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, CommandName).Bold().Red());
                }

                return CreationResultStatus.InvalidParamValues;
            }

            if (_commandInput.ToInstallList != null && _commandInput.ToInstallList.Count > 0 && _commandInput.ToInstallList[0] != null)
            {
                CreationResultStatus installResult = EnterInstallFlow();

                if (installResult == CreationResultStatus.Success)
                {
                    _settingsLoader.Reload();
                    PerformCoreTemplateQuery();
                    DisplayTemplateList();
                }

                return installResult;
            }

            //No other cases specified, we've fallen through to "Usage help + List"
            ShowUsageHelp();
            PerformCoreTemplateQuery();
            DisplayTemplateList();

            return CreationResultStatus.Success;
        }

        // Setup the alias for the templates in the unambiguous group which don't have parameter problems.
        private CreationResultStatus SetupTemplateAlias()
        {
            _telemetryLogger.TrackEvent(CommandName + "-CreateAlias");
            bool anyValid = false;

            foreach (IFilteredTemplateInfo templateInfo in UnambiguousTemplateGroupToUse)
            {
                ParseTemplateArgs(templateInfo.Info);

                if (AnyRemainingParameters)
                {
                    anyValid = true;
                    _aliasRegistry.SetTemplateAlias(_commandInput.Alias, templateInfo.Info);
                }
            }

            if (!anyValid)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{CommandName} {TemplateName}").Bold().Red());
                return CreationResultStatus.InvalidParamValues;
            }

            Reporter.Output.WriteLine(LocalizableStrings.AliasCreated);
            return CreationResultStatus.Success;
        }

        private CreationResultStatus SingularGroupDisplayTemplateListIfAnyAreValid()
        {
            bool anyValid = false;

            foreach (IFilteredTemplateInfo templateInfo in UnambiguousTemplateGroupToUse)
            {
                ParseTemplateArgs(templateInfo.Info);

                if (!AnyRemainingParameters)
                {
                    anyValid = true;
                    break;
                }
            }

            if (!anyValid)
            {
                IFilteredTemplateInfo highestPrecedenceTemplate = FindHighestPrecedenceTemplateIfAllSameGroupIdentity(UnambiguousTemplateGroupToUse);

                if (highestPrecedenceTemplate != null)
                {
                    ParseTemplateArgs(highestPrecedenceTemplate.Info);
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, CommandName).Bold().Red());
                    return CreationResultStatus.InvalidParamValues;
                }
            }

            DisplayTemplateList();
            return CreationResultStatus.Success;
        }

        private CreationResultStatus DisplayTemplateHelpForSingularGroup()
        {
            bool anyArgsErrors = false;
            ShowUsageHelp();
            bool showImplicitlyHiddenParams = UnambiguousTemplateGroupToUse.Count > 1;

            IList<ITemplateInfo> templatesToShowHelpOn = new List<ITemplateInfo>();

            foreach (IFilteredTemplateInfo templateInfo in UnambiguousTemplateGroupToUse)
            {
                bool argsError = false;
                string commandParseFailureMessage = null;

                try
                {
                    ParseTemplateArgs(templateInfo.Info);
                }
                catch (CommandParserException ex)
                {
                    commandParseFailureMessage = ex.Message;
                    argsError = true;
                }

                if (!argsError)
                {   // Validate outputs the invalid switch errors.
                    argsError = !ValidateRemainingParameters();
                }

                if (commandParseFailureMessage != null)
                {
                    Reporter.Error.WriteLine(commandParseFailureMessage.Bold().Red());
                }

                templatesToShowHelpOn.Add(templateInfo.Info);
                anyArgsErrors |= argsError;
            }

            // maybe
            ShowTemplateGroupHelp(templatesToShowHelpOn, showImplicitlyHiddenParams);

            return anyArgsErrors ? CreationResultStatus.InvalidParamValues : CreationResultStatus.Success;
        }

        private async Task<CreationResultStatus> EnterSingularTemplateManipulationFlowAsync()
        {
            if (!string.IsNullOrWhiteSpace(_commandInput.Alias))
            {
                return SetupTemplateAlias();
            }
            else if (_commandInput.IsListFlagSpecified)
            {
                return SingularGroupDisplayTemplateListIfAnyAreValid();
            }
            else if (_commandInput.IsHelpFlagSpecified)
            {
                _telemetryLogger.TrackEvent(CommandName + "-Help");
                return DisplayTemplateHelpForSingularGroup();
            }

            bool argsError = false;
            string commandParseFailureMessage = null;
            IFilteredTemplateInfo highestPrecedenceTemplate = FindHighestPrecedenceTemplateIfAllSameGroupIdentity(UnambiguousTemplateGroupToUse);
            try
            {
                ParseTemplateArgs(highestPrecedenceTemplate.Info);
            }
            catch (CommandParserException ex)
            {
                commandParseFailureMessage = ex.Message;
                argsError = true;
            }

            if (!argsError)
            {
                argsError = !ValidateRemainingParameters();
            }

            highestPrecedenceTemplate.Info.Tags.TryGetValue("language", out ICacheTag language);
            _commandInput.AllTemplateParams.TryGetValue("framework", out string framework);
            _commandInput.AllTemplateParams.TryGetValue("auth", out string auth);
            bool isMicrosoftAuthored = string.Equals(highestPrecedenceTemplate.Info.Author, "Microsoft", StringComparison.OrdinalIgnoreCase);
            string templateName = isMicrosoftAuthored ? highestPrecedenceTemplate.Info.Identity : "(3rd Party)";

            if (!isMicrosoftAuthored)
            {
                auth = null;
            }

            if (argsError)
            {
                _telemetryLogger.TrackEvent(CommandName + "CreateTemplate", new Dictionary<string, string>
                {
                    { "language", language?.ChoicesAndDescriptions.Keys.FirstOrDefault() },
                    { "argument-error", "true" },
                    { "framework", framework },
                    { "template-name", templateName },
                    { "auth", auth }
                });

                if (commandParseFailureMessage != null)
                {
                    Reporter.Error.WriteLine(commandParseFailureMessage.Bold().Red());
                }

                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{CommandName} {TemplateName}").Bold().Red());
                return CreationResultStatus.InvalidParamValues;
            }
            else
            {
                bool success = true;

                try
                {
                    return await CreateTemplateAsync(highestPrecedenceTemplate.Info).ConfigureAwait(false);
                }
                catch (ContentGenerationException cx)
                {
                    success = false;
                    Reporter.Error.WriteLine(cx.Message.Bold().Red());
                    if(cx.InnerException != null)
                    {
                        Reporter.Error.WriteLine(cx.InnerException.Message.Bold().Red());
                    }

                    return CreationResultStatus.CreateFailed;
                }
                catch (Exception ex)
                {
                    success = false;
                    Reporter.Error.WriteLine(ex.Message.Bold().Red());
                }
                finally
                {
                    _telemetryLogger.TrackEvent(CommandName + "CreateTemplate", new Dictionary<string, string>
                    {
                        { "language", language?.ChoicesAndDescriptions.Keys.FirstOrDefault() },
                        { "argument-error", "false" },
                        { "framework", framework },
                        { "template-name", templateName },
                        { "create-success", success.ToString() },
                        { "auth", auth }
                    });
                }

                return CreationResultStatus.CreateFailed;
            }
        }

        private async Task<CreationResultStatus> EnterTemplateManipulationFlowAsync()
        {
            PerformCoreTemplateQuery();

            if (UnambiguousTemplateGroupToUse != null && UnambiguousTemplateGroupToUse.Any()
                && !UnambiguousTemplateGroupToUse.Any(x => x.HasAmbiguousParameterMatch))
            {
                // unambiguous templates should all have the same dispositions
                if (UnambiguousTemplateGroupToUse[0].MatchDisposition.Any(x => x.Kind == MatchKind.Exact && x.Location != MatchLocation.Context && x.Location != MatchLocation.Language))
                {
                    return await EnterSingularTemplateManipulationFlowAsync().ConfigureAwait(false);
                }
                else if(EnvironmentSettings.Host.OnConfirmPartialMatch(UnambiguousTemplateGroupToUse[0].Info.Name))
                {   // unambiguous templates will all have the same name
                    return await EnterSingularTemplateManipulationFlowAsync().ConfigureAwait(false);
                }
                else
                {
                    return CreationResultStatus.Cancelled;
                }
            }

            return EnterAmbiguousTemplateManipulationFlow();
        }

        private async Task<CreationResultStatus> ExecuteAsync()
        {
            if (_commandInput.HasParseError)
            {
                ValidateRemainingParameters();

                // TODO: get a meaningful error message from the parser
                if (_commandInput.IsHelpFlagSpecified)
                {
                    _telemetryLogger.TrackEvent(CommandName + "-Help");
                    ShowUsageHelp();
                }
                else
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, CommandName).Bold().Red());
                }

                return CreationResultStatus.InvalidParamValues;
            }

            if (!ConfigureLocale())
            {
                return CreationResultStatus.InvalidParamValues;
            }

            Initialize();
            bool forceCacheRebuild = _commandInput.HasDebuggingFlag("--debug:rebuildcache");
            _settingsLoader.RebuildCacheFromSettingsIfNotCurrent(forceCacheRebuild);

            try
            {
                if (string.IsNullOrWhiteSpace(TemplateName))
                {
                    return EnterMaintenanceFlow();
                }

                return await EnterTemplateManipulationFlowAsync().ConfigureAwait(false);
            }
            catch (TemplateAuthoringException tae)
            {
                Reporter.Error.WriteLine(tae.Message.Bold().Red());
                return CreationResultStatus.CreateFailed;
            }
        }

        private bool ConfigureLocale()
        {
            if (!string.IsNullOrEmpty(_commandInput.Locale))
            {
                string newLocale = _commandInput.Locale;
                if (!ValidateLocaleFormat(newLocale))
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.BadLocaleError, newLocale).Bold().Red());
                    return false;
                }

                EnvironmentSettings.Host.UpdateLocale(newLocale);
                // cache the templates for the new locale
                _settingsLoader.Reload();
            }

            return true;
        }

        // Note: This method explicitly filters out "type" and "language", in addition to other filtering.
        private static IEnumerable<ITemplateParameter> FilterParamsForHelp(IEnumerable<ITemplateParameter> parameterDefinitions, HashSet<string> hiddenParams, bool showImplicitlyHiddenParams = false, bool hasPostActionScriptRunner = false)
        {
            IList<ITemplateParameter> filteredParams = parameterDefinitions
                .Where(x => x.Priority != TemplateParameterPriority.Implicit 
                        && !hiddenParams.Contains(x.Name) && !string.Equals(x.Name, "type", StringComparison.OrdinalIgnoreCase) && !string.Equals(x.Name, "language", StringComparison.OrdinalIgnoreCase)
                        && (showImplicitlyHiddenParams || x.DataType != "choice" || x.Choices.Count > 1)).ToList();    // for filtering "tags"

            if (hasPostActionScriptRunner)
            {
                ITemplateParameter allowScriptsParam = new TemplateParameter()
                {
                    Documentation = LocalizableStrings.WhetherToAllowScriptsToRun,
                    Name = "allow-scripts",
                    DataType = "choice",
                    DefaultValue = "prompt",
                    Choices = new Dictionary<string, string>()
                    {
                        { "yes", LocalizableStrings.AllowScriptsYesChoice },
                        { "no", LocalizableStrings.AllowScriptsNoChoice },
                        { "prompt", LocalizableStrings.AllowScriptsPromptChoice }
                    }
                };

                filteredParams.Add(allowScriptsParam);
            }

            return filteredParams;
        }

        private bool GenerateUsageForTemplate(ITemplateInfo templateInfo)
        {
            HostSpecificTemplateData hostTemplateData = ReadHostSpecificTemplateData(templateInfo);

            if(hostTemplateData.UsageExamples != null)
            {
                if(hostTemplateData.UsageExamples.Count == 0)
                {
                    return false;
                }

                Reporter.Output.WriteLine($"    dotnet {CommandName} {templateInfo.ShortName} {hostTemplateData.UsageExamples[0]}");
                return true;
            }

            Reporter.Output.Write($"    dotnet {CommandName} {templateInfo.ShortName}");
            IReadOnlyList<ITemplateParameter> allParameterDefinitions = templateInfo.Parameters;
            IEnumerable<ITemplateParameter> filteredParams = FilterParamsForHelp(allParameterDefinitions, hostTemplateData.HiddenParameterNames);

            foreach (ITemplateParameter parameter in filteredParams)
            {
                if (string.Equals(parameter.DataType, "bool", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parameter.DefaultValue, "false", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string displayParameter = hostTemplateData.DisplayNameForParameter(parameter.Name);

                Reporter.Output.Write($" --{displayParameter}");

                if (!string.IsNullOrEmpty(parameter.DefaultValue) && !string.Equals(parameter.DataType, "bool", StringComparison.OrdinalIgnoreCase))
                {
                    Reporter.Output.Write($" {parameter.DefaultValue}");
                }
            }

            Reporter.Output.WriteLine();
            return true;
        }

        private bool Initialize()
        {
            bool ephemeralHiveFlag = _commandInput.HasDebuggingFlag("--debug:ephemeral-hive");

            if (ephemeralHiveFlag)
            {
                EnvironmentSettings.Host.VirtualizeDirectory(_paths.User.BaseDir);
            }

            bool reinitFlag = _commandInput.HasDebuggingFlag("--debug:reinit");
            if (reinitFlag)
            {
                _paths.Delete(_paths.User.FirstRunCookie);
            }

            // Note: this leaves things in a weird state. Might be related to the localized caches.
            // not sure, need to look into it.
            if (reinitFlag || _commandInput.HasDebuggingFlag("--debug:reset-config"))
            {
                _paths.Delete(_paths.User.AliasesFile);
                _paths.Delete(_paths.User.SettingsFile);
                _settingsLoader.UserTemplateCache.DeleteAllLocaleCacheFiles();
                _settingsLoader.Reload();
                return false;
            }

            if (!_paths.Exists(_paths.User.BaseDir) || !_paths.Exists(_paths.User.FirstRunCookie))
            {
                if (!_commandInput.IsQuietFlagSpecified)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.GettingReady);
                }

                ConfigureEnvironment();
                _paths.WriteAllText(_paths.User.FirstRunCookie, "");
            }

            if (_commandInput.HasDebuggingFlag("--debug:showconfig"))
            {
                ShowConfig();
                return false;
            }

            return true;
        }

        private void ShowParameterHelp(IReadOnlyDictionary<string, string> inputParams, IParameterSet allParams, string additionalInfo, IReadOnlyList<string> invalidParams, HashSet<string> explicitlyHiddenParams, bool showImplicitlyHiddenParams, bool hasPostActionScriptRunner)
        {
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                Reporter.Error.WriteLine(additionalInfo.Bold().Red());
                Reporter.Output.WriteLine();
            }

            IEnumerable<ITemplateParameter> filteredParams = FilterParamsForHelp(allParams.ParameterDefinitions, explicitlyHiddenParams, showImplicitlyHiddenParams, hasPostActionScriptRunner);

            if (filteredParams.Any())
            {
                HelpFormatter<ITemplateParameter> formatter = new HelpFormatter<ITemplateParameter>(EnvironmentSettings, filteredParams, 2, null, true);

                formatter.DefineColumn(
                    param =>
                    {
                        string options;
                        if (string.Equals(param.Name, "allow-scripts", StringComparison.OrdinalIgnoreCase))
                        {
                            options = "--" + param.Name;
                        }
                        else
                        {
                            // the key is guaranteed to exist
                            IList<string> variants = _commandInput.VariantsForCanonical(param.Name).ToList();
                            options = string.Join("|", variants.Reverse());
                        }

                        return "  " + options;
                    },
                    LocalizableStrings.Options
                );

                formatter.DefineColumn(delegate (ITemplateParameter param)
                {
                    StringBuilder displayValue = new StringBuilder(255);
                    displayValue.AppendLine(param.Documentation);

                    if (string.Equals(param.DataType, "choice", StringComparison.OrdinalIgnoreCase))
                    {
                        int longestChoiceLength = param.Choices.Keys.Max(x => x.Length);

                        foreach (KeyValuePair<string, string> choiceInfo in param.Choices)
                        {
                            displayValue.Append("    " + choiceInfo.Key.PadRight(longestChoiceLength + 4));

                            if (!string.IsNullOrWhiteSpace(choiceInfo.Value))
                            {
                                displayValue.AppendLine("- " + choiceInfo.Value);
                            }
                        }
                    }
                    else
                    {
                        displayValue.Append(param.DataType ?? "string");
                        displayValue.AppendLine(" - " + param.Priority.ToString());
                    }

                    // display the configured value if there is one
                    string configuredValue = null;
                    if (allParams.ResolvedValues.TryGetValue(param, out object resolvedValueObject))
                    {
                        string resolvedValue = resolvedValueObject as string;

                        if (!string.IsNullOrEmpty(resolvedValue)
                            && !string.IsNullOrEmpty(param.DefaultValue)
                            && !string.Equals(param.DefaultValue, resolvedValue))
                        {
                            configuredValue = resolvedValue;
                        }
                    }

                    if (string.IsNullOrEmpty(configuredValue))
                    {
                        // this will catch when the user inputs the default value. The above deliberately skips it on the resolved values.
                        if (string.Equals(param.DataType, "bool", StringComparison.OrdinalIgnoreCase)
                            && _commandInput.TemplateParamHasValue(param.Name)
                            && string.IsNullOrEmpty(_commandInput.TemplateParamValue(param.Name)))
                        {
                            configuredValue = "true";
                        }
                        else
                        {
                            inputParams.TryGetValue(param.Name, out configuredValue);
                        }
                    }

                    if (!string.IsNullOrEmpty(configuredValue))
                    {
                        string realValue = configuredValue;

                        if (invalidParams.Contains(param.Name))
                        {
                            realValue = realValue.Bold().Red();
                        }
                        else if (allParams.TryGetRuntimeValue(EnvironmentSettings, param.Name, out object runtimeVal) && runtimeVal != null)
                        {
                            realValue = runtimeVal.ToString();
                        }

                        displayValue.AppendLine(string.Format(LocalizableStrings.ConfiguredValue, realValue));
                    }

                    // display the default value if there is one
                    if (!string.IsNullOrEmpty(param.DefaultValue))
                    {
                        displayValue.AppendLine(string.Format(LocalizableStrings.DefaultValue, param.DefaultValue));
                    }

                    return displayValue.ToString();
                }, string.Empty);

                Reporter.Output.WriteLine(formatter.Layout());
            }
            else
            {
                Reporter.Output.WriteLine(LocalizableStrings.NoParameters);
            }
        }

        private void ParseTemplateArgs(ITemplateInfo templateInfo)
        {
            _hostSpecificTemplateData = ReadHostSpecificTemplateData(templateInfo);
            _commandInput.ReparseForTemplate(templateInfo, _hostSpecificTemplateData);
        }

        private string DetermineTemplateContext()
        {
            return _commandInput.TypeFilter?.ToLowerInvariant();
        }

        private void PerformCoreTemplateQuery()
        {
            string context = DetermineTemplateContext();

            IReadOnlyCollection<IFilteredTemplateInfo> templates = _settingsLoader.UserTemplateCache.List
            (
                false,
                WellKnownSearchFilters.AliasFilter(TemplateName),
                WellKnownSearchFilters.NameFilter(TemplateName),
                WellKnownSearchFilters.ClassificationsFilter(TemplateName),
                WellKnownSearchFilters.LanguageFilter(_commandInput.Language),
                WellKnownSearchFilters.ContextFilter(context)
            );

            IReadOnlyList<IFilteredTemplateInfo> matchedTemplates = templates.Where(x => x.IsMatch).ToList();

            if (matchedTemplates.Count == 0)
            {
                matchedTemplates = templates.Where(x => x.IsPartialMatch).ToList();
                _forceAmbiguousFlow = true;
            }
            else
            {
                IReadOnlyList<IFilteredTemplateInfo> matchesWithExactDispositionsInNameFields = matchedTemplates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();

                if (matchesWithExactDispositionsInNameFields.Count > 0)
                {
                    matchedTemplates = matchesWithExactDispositionsInNameFields;
                }
            }

            _matchedTemplates = matchedTemplates;
        }

        private IReadOnlyList<IFilteredTemplateInfo> FilterTemplatesOnParameters(IReadOnlyList<IFilteredTemplateInfo> templatesToFilter)
        {
            List<IFilteredTemplateInfo> filterResults = new List<IFilteredTemplateInfo>();

            foreach (IFilteredTemplateInfo templateWithFilterInfo in templatesToFilter)
            {
                List<MatchInfo> dispositionForTemplate = templateWithFilterInfo.MatchDisposition.ToList();

                try
                {
                    ParseTemplateArgs(templateWithFilterInfo.Info);

                    // params are already parsed. But choice values aren't checked
                    foreach (KeyValuePair<string, string> matchedParamInfo in _commandInput.AllTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string paramValue = matchedParamInfo.Value;

                        if (templateWithFilterInfo.Info.Tags.TryGetValue(paramName, out ICacheTag paramDetails))
                        {
                            // key is the value user should provide, value is description
                            if (paramDetails.ChoicesAndDescriptions.ContainsKey(paramValue))
                            {
                                dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName });
                            }
                            else
                            {
                                int startsWithCount = paramDetails.ChoicesAndDescriptions.Count(x => x.Key.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase));
                                if (startsWithCount == 1)
                                {
                                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName });
                                }
                                else if (startsWithCount > 1)
                                {
                                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.AmbiguousParameterValue, ChoiceIfLocationIsOtherChoice = paramName });
                                }
                                else
                                {
                                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName });
                                }
                            }
                        }
                        else if (templateWithFilterInfo.Info.CacheParameters.ContainsKey(paramName))
                        {
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName });
                        }
                        else
                        {
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName });
                        }
                    }

                    foreach (string unmatchedParamName in _commandInput.RemainingParameters.Keys.Where(x => !x.Contains(':')))   // filter debugging params
                    {
                        if (_commandInput.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
                        {   // the name is a known template param, it must have not parsed due to an invalid value
                            //
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                        else
                        {   // the name is not known
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                    }
                }
                catch
                {   // if we do actually throw, add a non-match
                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.Unspecified, Kind = MatchKind.Unspecified });
                }

                filterResults.Add(new FilteredTemplateInfo(templateWithFilterInfo.Info, dispositionForTemplate));
            }

            return filterResults;
        }

        // Lists all the templates, filtered only by the context (item, project, etc)
        private IReadOnlyCollection<IFilteredTemplateInfo> PerformAllTemplatesInContextQuery()
        {
            string context = DetermineTemplateContext();

            IReadOnlyCollection<IFilteredTemplateInfo> templates = _settingsLoader.UserTemplateCache.List
            (
                false,
                WellKnownSearchFilters.ContextFilter(context),
                WellKnownSearchFilters.NameFilter(string.Empty)
            );

            return templates;
        }

        private HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            if (EnvironmentSettings.SettingsLoader.TryGetFileFromIdAndPath(templateInfo.HostConfigMountPointId, templateInfo.HostConfigPlace, out IFile file))
            {
                JObject jsonData;
                using (Stream s = file.OpenRead())
                using (TextReader tr = new StreamReader(s, true))
                using (JsonReader r = new JsonTextReader(tr))
                {
                    jsonData = JObject.Load(r);
                }

                return jsonData.ToObject<HostSpecificTemplateData>();
            }

            return HostSpecificTemplateData.Default;
        }

        private void ShowConfig()
        {
            Reporter.Output.WriteLine(LocalizableStrings.CurrentConfiguration);
            Reporter.Output.WriteLine(" ");
            TableFormatter.Print(EnvironmentSettings.SettingsLoader.MountPoints, LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<MountPointInfo, object>>
            {
                {LocalizableStrings.MountPoints, x => x.Place},
                {LocalizableStrings.Id, x => x.MountPointId},
                {LocalizableStrings.Parent, x => x.ParentMountPointId},
                {LocalizableStrings.Factory, x => x.MountPointFactoryId}
            });

            TableFormatter.Print(EnvironmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IMountPointFactory, object>>
            {
                {LocalizableStrings.MountPointFactories, x => x.Id},
                {LocalizableStrings.Type, x => x.GetType().FullName},
                {LocalizableStrings.Assembly, x => x.GetType().GetTypeInfo().Assembly.FullName}
            });

            TableFormatter.Print(EnvironmentSettings.SettingsLoader.Components.OfType<IGenerator>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IGenerator, object>>
            {
                {LocalizableStrings.Generators, x => x.Id},
                {LocalizableStrings.Type, x => x.GetType().FullName},
                {LocalizableStrings.Assembly, x => x.GetType().GetTypeInfo().Assembly.FullName}
            });
        }

        private void ShowInvocationExamples()
        {
            const int ExamplesToShow = 2;
            IReadOnlyList<string> preferredNameList = new List<string>() { "mvc" };
            int numShown = 0;

            if (_matchedTemplates.Count == 0)
            {
                return;
            }

            List<ITemplateInfo> templateList = _matchedTemplates.Select(x => x.Info).ToList();
            Reporter.Output.WriteLine("Examples:");
            HashSet<string> usedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string preferredName in preferredNameList)
            {
                ITemplateInfo template = templateList.FirstOrDefault(x => string.Equals(x.ShortName, preferredName, StringComparison.OrdinalIgnoreCase));

                if (template != null)
                {
                    string identity = string.IsNullOrWhiteSpace(template.GroupIdentity) ? string.IsNullOrWhiteSpace(template.Identity) ? string.Empty : template.Identity : template.GroupIdentity;
                    if (usedGroupIds.Add(identity))
                    {
                        GenerateUsageForTemplate(template);
                        numShown++;
                    }
                }

                templateList.Remove(template);  // remove it so it won't get chosen again
            }

            // show up to 2 examples (total, including the above)
            Random rnd = new Random();
            for (int i = numShown; i < ExamplesToShow && templateList.Count > 0; i++)
            {
                int index = rnd.Next(0, templateList.Count - 1);
                ITemplateInfo template = templateList[index];
                string identity = string.IsNullOrWhiteSpace(template.GroupIdentity) ? string.IsNullOrWhiteSpace(template.Identity) ? string.Empty : template.Identity : template.GroupIdentity;
                if (usedGroupIds.Add(identity) && !GenerateUsageForTemplate(template))
                {
                    --i;
                }

                templateList.Remove(template);  // remove it so it won't get chosen again
            }

            // show a help example
            Reporter.Output.WriteLine($"    dotnet {CommandName} --help");
        }

        private void ShowTemplateGroupHelp(IList<ITemplateInfo> templateGroup, bool showImplicitlyHiddenParams = false)
        {
            if (templateGroup.Count == 0)
            {
                return;
            }

            // Use the highest precedence template for most of the output
            ITemplateInfo preferredTemplate = templateGroup.OrderByDescending(x => x.Precedence).First();

            // use all templates to get the language choices
            HashSet<string> languages = new HashSet<string>();
            foreach (ITemplateInfo templateInfo in templateGroup)
            {
                if (templateInfo.Tags != null && templateInfo.Tags.TryGetValue("language", out ICacheTag languageTag))
                {
                    languages.UnionWith(languageTag.ChoicesAndDescriptions.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).ToList());
                }
            }

            if (languages != null && languages.Any())
            {
                Reporter.Output.WriteLine($"{preferredTemplate.Name} ({string.Join(", ", languages)})");
            }
            else
            {
                Reporter.Output.WriteLine(preferredTemplate.Name);
            }

            if (!string.IsNullOrWhiteSpace(preferredTemplate.Author))
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.Author, preferredTemplate.Author));
            }

            if (!string.IsNullOrWhiteSpace(preferredTemplate.Description))
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.Description, preferredTemplate.Description));
            }

            if (!string.IsNullOrEmpty(preferredTemplate.ThirdPartyNotices))
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.ThirdPartyNotices, preferredTemplate.ThirdPartyNotices));
            }

            HashSet<string> groupUserParamsWithInvalidValues = new HashSet<string>();
            HashSet<string> groupParametersToExplicitlyHide = new HashSet<string>();
            bool groupHasPostActionScriptRunner = false;
            List<IParameterSet> parameterSetsForAllTemplatesInGroup = new List<IParameterSet>();
            IDictionary<string, InvalidParameterInfo> invalidParametersForGroup = new Dictionary<string, InvalidParameterInfo>();
            bool firstInList = true;

            foreach (ITemplateInfo templateInfo in templateGroup)
            {
                IReadOnlyList<InvalidParameterInfo> invalidParamsForTemplate = GetTemplateUsageInformation(templateInfo, out IParameterSet allParamsForTemplate, out IReadOnlyList<string> userParamsWithInvalidValues, out bool hasPostActionScriptRunner);

                if (firstInList)
                {
                    invalidParametersForGroup = invalidParamsForTemplate.ToDictionary(x => x.Canonical, x => x);
                    firstInList = false;
                }
                else
                {
                    invalidParametersForGroup = InvalidParameterInfo.IntersectWithExisting(invalidParametersForGroup, invalidParamsForTemplate);
                }

                HashSet<string> parametersToExplicitlyHide = _hostSpecificTemplateData?.HiddenParameterNames ?? new HashSet<string>();

                groupUserParamsWithInvalidValues.IntersectWith(userParamsWithInvalidValues);    // intersect because if the value is valid for any version, it's valid.
                groupParametersToExplicitlyHide.UnionWith(parametersToExplicitlyHide);
                groupHasPostActionScriptRunner |= hasPostActionScriptRunner;
                parameterSetsForAllTemplatesInGroup.Add(allParamsForTemplate);
            }

            IParameterSet allGroupParameters = new TemplateGroupParameterSet(parameterSetsForAllTemplatesInGroup);
            string parameterErrors = InvalidParameterInfo.InvalidParameterListToString(invalidParametersForGroup.Values.ToList());
            ShowParameterHelp(_commandInput.AllTemplateParams, allGroupParameters, parameterErrors, groupUserParamsWithInvalidValues.ToList(), groupParametersToExplicitlyHide, showImplicitlyHiddenParams, groupHasPostActionScriptRunner);
        }

        // Returns true if any partial matches were displayed, false otherwise
        private bool ShowTemplateNameMismatchHelp()
        {
            IDictionary<string, IFilteredTemplateInfo> contextProblemMatches = new Dictionary<string, IFilteredTemplateInfo>();
            IDictionary<string, IFilteredTemplateInfo> remainingPartialMatches = new Dictionary<string, IFilteredTemplateInfo>();

            // this filtering / grouping ignores language differences.
            foreach (IFilteredTemplateInfo template in _matchedTemplates)
            {
                if (contextProblemMatches.ContainsKey(template.Info.Name) || remainingPartialMatches.ContainsKey(template.Info.Name))
                {
                    continue;
                }

                if (template.MatchDisposition.Any(x => x.Location == MatchLocation.Context && x.Kind != MatchKind.Exact))
                {
                    contextProblemMatches.Add(template.Info.Name, template);
                }
                else if(template.MatchDisposition.Any(t => t.Location != MatchLocation.Context && t.Kind != MatchKind.Mismatch && t.Kind != MatchKind.Unspecified))
                {
                    remainingPartialMatches.Add(template.Info.Name, template);
                }
            }

            if (contextProblemMatches.Keys.Count + remainingPartialMatches.Keys.Count > 1)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AmbiguousInputTemplateName, TemplateName));
            }
            else if (contextProblemMatches.Keys.Count + remainingPartialMatches.Keys.Count == 0)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoTemplatesMatchName, TemplateName));
                Reporter.Error.WriteLine();
                return false;
            }

            foreach (IFilteredTemplateInfo template in contextProblemMatches.Values)
            {
                if (template.Info.Tags != null && template.Info.Tags.TryGetValue("type", out ICacheTag typeTag))
                {
                    MatchInfo? matchInfo = WellKnownSearchFilters.ContextFilter(DetermineTemplateContext())(template.Info, null);
                    if ((matchInfo?.Kind ?? MatchKind.Mismatch) == MatchKind.Mismatch)
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplateNotValidGivenTheSpecifiedFilter, template.Info.Name).Bold().Red());
                    }
                }
                else
                {   // this really shouldn't ever happen. But better to have a generic error than quietly ignore the partial match.
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.GenericPlaceholderTemplateContextError, template.Info.Name).Bold().Red());
                }
            }

            if (remainingPartialMatches.Keys.Count > 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.TemplateMultiplePartialNameMatches.Bold().Red());
            }

            Reporter.Error.WriteLine();
            return true;
        }

        private void ShowUsageHelp()
        {
            Reporter.Output.WriteLine(_commandInput.HelpText);
            Reporter.Output.WriteLine();
        }

        private bool AnyRemainingParameters
        {
            get
            {
                // should not have to check for "--debug:" anymore, with the new parser setup
                return _commandInput.RemainingParameters.Any(); //.Any(x => !x.Key.StartsWith("--debug:"));
            }
        }

        private bool ValidateRemainingParameters()
        {
            if (AnyRemainingParameters)
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                foreach (string flag in _commandInput.RemainingParameters.Keys)
                {
                    Reporter.Error.WriteLine($"  {flag}".Bold().Red());
                }

                return false;
            }

            return true;
        }

        private static bool ValidateLocaleFormat(string localeToCheck)
        {
            return LocaleFormatRegex.IsMatch(localeToCheck);
        }
    }
}
