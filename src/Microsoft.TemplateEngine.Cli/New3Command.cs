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
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
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
        private ExtendedCommandParser _app;
        private HostSpecificTemplateData _hostSpecificTemplateData;
        private IReadOnlyList<IFilteredTemplateInfo> _matchedTemplates;
        private CommandArgument _templateNameArgument;
        private readonly TemplateCreator _templateCreator;
        private readonly SettingsLoader _settingsLoader;
        private readonly AliasRegistry _aliasRegistry;
        private readonly Paths _paths;
        private readonly ExtendedTemplateEngineHost _host;

        private static readonly Regex LocaleFormatRegex = new Regex(@"
                    ^
                        [a-z]{2}
                        (?:-[A-Z]{2})?
                    $"
            , RegexOptions.IgnorePatternWhitespace);
        private bool _forceAmbiguousFlow;
        private readonly Action<IEngineEnvironmentSettings, IInstaller> _onFirstRun;

        public New3Command(string commandName, ITemplateEngineHost host, Action<IEngineEnvironmentSettings, IInstaller> onFirstRun, ExtendedCommandParser app, CommandArgument templateName)
        {
            host = _host = new ExtendedTemplateEngineHost(host, this);
            EnvironmentSettings = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
            _settingsLoader = (SettingsLoader)EnvironmentSettings.SettingsLoader;
            Installer = new Installer(EnvironmentSettings, _settingsLoader.UserTemplateCache);
            _templateCreator = new TemplateCreator(EnvironmentSettings);
            _aliasRegistry = new AliasRegistry(EnvironmentSettings);
            CommandName = commandName;
            _paths = new Paths(EnvironmentSettings);
            _app = app;
            _templateNameArgument = templateName;
            _onFirstRun = onFirstRun;
        }

        public string Alias => _app.InternalParamValue("--alias");

        public bool ExtraArgsHasValue => _app.InternalParamHasValue("--extra-args");

        public string CommandName { get; }

        public IList<string> Install => _app.InternalParamValueList("--install");

        public static IInstaller Installer { get; set; }

        public bool IsForceFlagSpecified => _app.InternalParamHasValue("--force");

        public bool InstallHasValue => _app.InternalParamHasValue("--install");

        public bool IsHelpFlagSpecified => _app.InternalParamHasValue("--help");

        public bool IsListFlagSpecified => _app.InternalParamHasValue("--list");

        public bool IsQuietFlagSpecified => _app.InternalParamHasValue("--quiet");

        public bool IsShowAllFlagSpecified => _app.InternalParamHasValue("--show-all");

        public string TypeFilter => _app.InternalParamValue("--type");

        public string Language => _app.InternalParamValue("--language");

        public string Locale => _app.InternalParamValue("--locale");

        public bool LocaleHasValue => _app.InternalParamHasValue("--locale");

        public string Name => _app.InternalParamValue("--name");

        public string OutputPath => _app.InternalParamValue("--output");

        public string TemplateName => _templateNameArgument.Value;

        public bool SkipUpdateCheck => _app.InternalParamHasValue("--skip-update-check");

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
                else if (string.IsNullOrEmpty(Language) && EnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage))
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
            if (_secondaryFilteredUnambiguousTemplateGroupToUse == null)
            {
                if (_matchedTemplatesWithSecondaryMatchInfo == null)
                {
                    _matchedTemplatesWithSecondaryMatchInfo = FilterTemplatesOnParameters(_matchedTemplates).Where(x => x.IsMatch).ToList();

                    IReadOnlyList<IFilteredTemplateInfo> matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo.Where(x => x.IsParameterMatch).ToList();

                    if (matchesAfterParameterChecks.Count == 0)
                    {   // no param matches, continue additional matching with the list from before param checking.
                        matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo;
                    }

                    if (matchesAfterParameterChecks.Count == 1)
                    {
                        return _secondaryFilteredUnambiguousTemplateGroupToUse = matchesAfterParameterChecks;
                    }
                    else if (string.IsNullOrEmpty(Language) && EnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage))
                    {
                        IReadOnlyList<IFilteredTemplateInfo> languageFiltered = FindTemplatesExplicitlyMatchingLanguage(matchesAfterParameterChecks, defaultLanguage);
                        if (languageFiltered.Count == 1 || AreAllTemplatesSameGroupIdentity(languageFiltered))
                        {
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

        public static int Run(string commandName, ITemplateEngineHost host, Action<IEngineEnvironmentSettings, IInstaller> onFirstRun, string[] args)
        {
            if (args.Any(x => string.Equals(x, "--debug:attach", StringComparison.Ordinal)))
            {
                Console.ReadLine();
            }

            ExtendedCommandParser app = new ExtendedCommandParser()
            {
                Name = $"dotnet {commandName}",
                FullName = LocalizableStrings.CommandDescription
            };
            SetupInternalCommands(app);

            CommandArgument templateName = app.Argument("template", LocalizableStrings.TemplateArgumentHelp);
            New3Command instance = new New3Command(commandName, host, onFirstRun, app, templateName);

            app.OnExecute(instance.ExecuteAsync);

            int result;
            try
            {
                using (Timing.Over("Execute"))
                {
                    result = app.Execute(args);
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
            string fallbackName = new DirectoryInfo(OutputPath ?? Directory.GetCurrentDirectory()).Name;
            TemplateCreationResult instantiateResult;

            try
            {
                instantiateResult = await _templateCreator.InstantiateAsync(template, Name, fallbackName, OutputPath, _app.AllTemplateParams, SkipUpdateCheck, IsForceFlagSpecified).ConfigureAwait(false);
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
                    string invalidParamsError = GetTemplateParameterErrorsMessage(template, out IParameterSet ps, out IReadOnlyList<string> userParamsWithInvalidValues);
                    Reporter.Error.WriteLine(invalidParamsError.Bold().Red());
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{CommandName} {TemplateName}").Bold().Red());
                    break;
                default:
                    break;
            }

            return instantiateResult.Status;
        }

        private string GetTemplateParameterErrorsMessage(ITemplateInfo templateInfo, out IParameterSet allParams, out IReadOnlyList<string> userParamsWithInvalidValues)
        {
            ITemplate template = EnvironmentSettings.SettingsLoader.LoadTemplate(templateInfo);
            ParseTemplateArgs(templateInfo);
            allParams = _templateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName, out IList<string> defaultParamsWithInvalidValues);
            _templateCreator.ResolveUserParameters(template, allParams, _app.AllTemplateParams, out userParamsWithInvalidValues);

            if (userParamsWithInvalidValues.Any())
            {
                string invalidParamsErrorText = LocalizableStrings.InvalidTemplateParameterValues;
                // Lookup the input param formats - userParamsWithInvalidValues has canonical.
                IList<string> inputParamFormats = new List<string>();
                foreach (string canonical in userParamsWithInvalidValues)
                {
                    _app.AllTemplateParams.TryGetValue(canonical, out string specifiedValue);
                    string inputFormat = _app.TemplateParamInputFormat(canonical);
                    invalidParamsErrorText += Environment.NewLine + string.Format(LocalizableStrings.InvalidParameterDetail, inputFormat, specifiedValue, canonical);
                }

                return invalidParamsErrorText;
            }

            return null;
        }

        private string GetLanguageMismatchErrorMessage(string inputLanguage)
        {
            string inputFlagForm;
            if (_app.RemainingArguments.Contains("-lang"))
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

            PostActionDispatcher postActionDispatcher = new PostActionDispatcher(creationResult, EnvironmentSettings.SettingsLoader.Components);
            postActionDispatcher.Process();
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
                else if (_matchedTemplatesWithSecondaryMatchInfo != null && _matchedTemplatesWithSecondaryMatchInfo.Count > 0)
                {
                    templateList = _matchedTemplatesWithSecondaryMatchInfo.Select(x => x.Info);
                }
                else if (_matchedTemplates != null && _matchedTemplates.Where(x => x.IsMatch).Count() > 0)
                {
                    templateList = _matchedTemplates.Where(x => x.IsMatch).Select(x => x.Info);
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

            IEnumerable<IGrouping<string, ITemplateInfo>> grouped = results.GroupBy(x => x.GroupIdentity);
            EnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage);

            Dictionary<ITemplateInfo, string> templatesVersusLanguages = new Dictionary<ITemplateInfo, string>();

            foreach (IGrouping<string, ITemplateInfo> grouping in grouped)
            {
                List<string> languageForDisplay = new List<string>();
                HashSet<string> uniqueLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (ITemplateInfo info in grouping)
                {
                    if (info.Tags != null && info.Tags.TryGetValue("language", out ICacheTag languageTag))
                    {
                        foreach (string lang in languageTag.ChoicesAndDescriptions.Keys)
                        {
                            if (uniqueLanguages.Add(lang))
                            {
                                if (string.IsNullOrEmpty(Language) && string.Equals(defaultLanguage, lang, StringComparison.OrdinalIgnoreCase))
                                {
                                    languageForDisplay.Add($"[{lang}]");
                                }
                                else
                                {
                                    languageForDisplay.Add(lang);
                                }
                            }
                        }
                    }
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

            if (!IsListFlagSpecified)
            {
                Reporter.Output.WriteLine();
                ShowInvocationExamples();

                bool firstDetails = true;
                IList<ITemplateInfo> templatesToShow = TemplatesToShowDetailedHelpAbout.ToList();

                foreach (ITemplateInfo template in templatesToShow)
                {
                    if (firstDetails)
                    {
                        Reporter.Output.WriteLine();
                        firstDetails = false;
                    }

                    ShowTemplateHelp(template, templatesToShow.Count > 1);
                }
            }
        }

        private CreationResultStatus EnterAmbiguousTemplateManipulationFlow()
        {
            if (!string.IsNullOrEmpty(TemplateName) && _matchedTemplates.All(x => x.MatchDisposition.Any(d => d.Location == MatchLocation.Language && d.Kind == MatchKind.Mismatch)))
            {
                string errorMessage = GetLanguageMismatchErrorMessage(Language);
                Reporter.Error.WriteLine(errorMessage.Bold().Red());
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{CommandName} {TemplateName}").Bold().Red());
                return CreationResultStatus.NotFound;
            }

            if (!ValidateRemainingParameters() || (!IsListFlagSpecified && !string.IsNullOrEmpty(TemplateName)))
            {
                bool anyPartialMatchesDisplayed = ShowTemplateNameMismatchHelp();
                DisplayTemplateList();
                return CreationResultStatus.NotFound;
            }

            if (!string.IsNullOrWhiteSpace(Alias))
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                Reporter.Error.WriteLine("  " + _app.TemplateParamInputFormat("--alias").Bold().Red());
                return CreationResultStatus.NotFound;
            }

            if (IsHelpFlagSpecified)
            {
                ShowUsageHelp();
                DisplayTemplateList();
                return CreationResultStatus.Success;
            }
            else
            {
                DisplayTemplateList();

                //If we're showing the list because we were asked to, exit with success, otherwise, exit with failure
                if (IsListFlagSpecified)
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
            Installer.InstallPackages(Install.ToList());
            //TODO: When an installer that directly calls into NuGet is available,
            //  return a more accurate representation of the outcome of the operation
            return CreationResultStatus.Success;
        }

        private CreationResultStatus EnterMaintenanceFlow()
        {
            if (!ValidateRemainingParameters())
            {
                if (IsHelpFlagSpecified)
                {
                    ShowUsageHelp();
                }
                else
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, CommandName).Bold().Red());
                }

                return CreationResultStatus.InvalidParamValues;
            }

            if (InstallHasValue && 
                ((Install.Count > 0) && (Install[0] != null)))
            {
                CreationResultStatus installResult = EnterInstallFlow();

                if (installResult == CreationResultStatus.Success)
                {
                    _settingsLoader.ReloadTemplates();
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
            bool anyValid = false;

            foreach (IFilteredTemplateInfo templateInfo in UnambiguousTemplateGroupToUse)
            {
                ParseTemplateArgs(templateInfo.Info);

                if (AnyRemainingParameters)
                {
                    anyValid = true;
                    _aliasRegistry.SetTemplateAlias(Alias, templateInfo.Info);
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

                ShowTemplateHelp(templateInfo.Info, showImplicitlyHiddenParams);

                anyArgsErrors |= argsError;
            }

            return anyArgsErrors ? CreationResultStatus.InvalidParamValues : CreationResultStatus.Success;
        }

        private async Task<CreationResultStatus> EnterSingularTemplateManipulationFlowAsync()
        {
            if (!string.IsNullOrWhiteSpace(Alias))
            {
                return SetupTemplateAlias();
            }
            else if (IsListFlagSpecified)
            {
                return SingularGroupDisplayTemplateListIfAnyAreValid();
            }
            else if (IsHelpFlagSpecified)
            {
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

            if (argsError)
            {
                if (commandParseFailureMessage != null)
                {
                    Reporter.Error.WriteLine(commandParseFailureMessage.Bold().Red());
                }

                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, $"{CommandName} {TemplateName}").Bold().Red());
                return CreationResultStatus.InvalidParamValues;
            }
            else
            {
                try
                {
                    return await CreateTemplateAsync(highestPrecedenceTemplate.Info).ConfigureAwait(false);
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
                catch (Exception ex)
                {
                    Reporter.Error.WriteLine(ex.Message.Bold().Red());
                }

                return CreationResultStatus.CreateFailed;
            }
        }

        private async Task<CreationResultStatus> EnterTemplateManipulationFlowAsync()
        {
            PerformCoreTemplateQuery();

            if (UnambiguousTemplateGroupToUse != null && UnambiguousTemplateGroupToUse.Any())
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
            //Parse non-template specific arguments
            try
            {
                _app.ParseArgs();
            }
            catch (CommandParserException ex)
            {
                Reporter.Error.WriteLine(ex.Message.Bold().Red());

                if (IsHelpFlagSpecified)
                {
                    ShowUsageHelp();
                }
                else
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, CommandName).Bold().Red());
                }

                return CreationResultStatus.InvalidParamValues;
            }

            if (ExtraArgsHasValue)
            {
                try
                {
                    _app.ParseArgs(_app.InternalParamValueList("--extra-args"));
                }
                catch (CommandParserException ex)
                {
                    Reporter.Error.WriteLine(ex.Message.Bold().Red());

                    if (IsHelpFlagSpecified)
                    {
                        ShowUsageHelp();
                    }
                    else
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunHelpForInformationAboutAcceptedParameters, CommandName).Bold().Red());
                    }

                    return CreationResultStatus.InvalidParamValues;
                }
            }

            ConfigureLocale();
            Initialize();
            bool forceCacheRebuild = _app.RemainingArguments.Any(x => x == "--debug:rebuildcache");
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

        private void ConfigureLocale()
        {
            if (LocaleHasValue)
            {
                string newLocale = Locale;
                if (!ValidateLocaleFormat(newLocale))
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.BadLocaleError, newLocale).Bold().Red());
                }

                EnvironmentSettings.Host.UpdateLocale(newLocale);
                // cache the templates for the new locale
                _settingsLoader.ReloadTemplates();
            }
        }

        // Note: This method explicitly filters out "type" and "language", in addition to other filtering.
        private static IEnumerable<ITemplateParameter> FilterParamsForHelp(IEnumerable<ITemplateParameter> parameterDefinitions, HashSet<string> hiddenParams, bool showImplicitlyHiddenParams = false)
        {
            IEnumerable<ITemplateParameter> filteredParams = parameterDefinitions
                .Where(x => x.Priority != TemplateParameterPriority.Implicit 
                        && !hiddenParams.Contains(x.Name) && !string.Equals(x.Name, "type", StringComparison.OrdinalIgnoreCase) && !string.Equals(x.Name, "language", StringComparison.OrdinalIgnoreCase)
                        && (showImplicitlyHiddenParams || x.DataType != "choice" || x.Choices.Count > 1));    // for filtering "tags"
            return filteredParams;
        }

        private void GenerateUsageForTemplate(ITemplateInfo templateInfo)
        {
            HostSpecificTemplateData hostTemplateData = ReadHostSpecificTemplateData(templateInfo);

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
        }

        private bool Initialize()
        {
            bool ephemeralHiveFlag = _app.RemainingArguments.Any(x => x == "--debug:ephemeral-hive");

            if (ephemeralHiveFlag)
            {
                EnvironmentSettings.Host.VirtualizeDirectory(_paths.User.BaseDir);
            }

            bool reinitFlag = _app.RemainingArguments.Any(x => x == "--debug:reinit");
            if (reinitFlag)
            {
                _paths.Delete(_paths.User.FirstRunCookie);
            }

            // Note: this leaves things in a weird state. Might be related to the localized caches.
            // not sure, need to look into it.
            if (reinitFlag || _app.RemainingArguments.Any(x => x == "--debug:reset-config"))
            {
                _paths.Delete(_paths.User.AliasesFile);
                _paths.Delete(_paths.User.SettingsFile);
                _settingsLoader.UserTemplateCache.DeleteAllLocaleCacheFiles();
                _settingsLoader.ReloadTemplates();
                return false;
            }

            if (!_paths.Exists(_paths.User.BaseDir) || !_paths.Exists(_paths.User.FirstRunCookie))
            {
                if (!IsQuietFlagSpecified)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.GettingReady);
                }

                ConfigureEnvironment();
                _paths.WriteAllText(_paths.User.FirstRunCookie, "");
            }

            if (_app.RemainingArguments.Any(x => x == "--debug:showconfig"))
            {
                ShowConfig();
                return false;
            }

            return true;
        }

        private void ShowParameterHelp(IReadOnlyDictionary<string, string> inputParams, IParameterSet allParams, string additionalInfo, IReadOnlyList<string> invalidParams, HashSet<string> explicitlyHiddenParams, bool showImplicitlyHiddenParams)
        {
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                Reporter.Error.WriteLine(additionalInfo.Bold().Red());
                Reporter.Output.WriteLine();
            }

            IEnumerable<ITemplateParameter> filteredParams = FilterParamsForHelp(allParams.ParameterDefinitions, explicitlyHiddenParams, showImplicitlyHiddenParams);

            if (filteredParams.Any())
            {
                HelpFormatter<ITemplateParameter> formatter = new HelpFormatter<ITemplateParameter>(EnvironmentSettings, filteredParams, 2, null, true);

                formatter.DefineColumn(
                    param =>
                    {
                        // the key is guaranteed to exist
                        IList<string> variants = _app.CanonicalToVariantsTemplateParamMap[param.Name];
                        string options = string.Join("|", variants.Reverse());
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
                            && _app.TemplateParamHasValue(param.Name)
                            && string.IsNullOrEmpty(_app.TemplateParamValue(param.Name)))
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

        // Causes the args to be parsed in the context of the input template.
        private void ParseTemplateArgs(ITemplateInfo templateInfo)
        {
            _app.Reset();
            SetupInternalCommands(_app);

            IReadOnlyList<ITemplateParameter> parameterDefinitions = templateInfo.Parameters;
            _hostSpecificTemplateData = ReadHostSpecificTemplateData(templateInfo);

            IEnumerable<KeyValuePair<string, string>> argParameters = parameterDefinitions
                                                            .Where(x => x.Priority != TemplateParameterPriority.Implicit)
                                                            .OrderBy(x => x.Name)
                                                            .Select(x => new KeyValuePair<string, string>(x.Name, x.DataType));

            _app.SetupTemplateParameters(argParameters, _hostSpecificTemplateData.LongNameOverrides, _hostSpecificTemplateData.ShortNameOverrides);
            _app.ParseArgs(_app.InternalParamValueList("--extra-args"));
        }

        private string DetermineTemplateContext()
        {
            return TypeFilter?.ToLowerInvariant();
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
                WellKnownSearchFilters.LanguageFilter(Language),
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
                    foreach (KeyValuePair<string, string> matchedParamInfo in _app.AllTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string paramValue = matchedParamInfo.Value;

                        if (templateWithFilterInfo.Info.Tags.TryGetValue(paramName, out ICacheTag paramDetails)
                            && (
                                paramDetails.ChoicesAndDescriptions.ContainsKey(paramValue)
                                || paramDetails.ChoicesAndDescriptions.Any(x => x.Value.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase))
                            ))
                        {
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName });
                        }
                        else
                        {
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName });
                        }
                    }

                    foreach (string unmatchedParamName in _app.RemainingParameters.Keys.Where(x => !x.Contains(':')))   // filter debugging params
                    {
                        if (_app.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
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

        private static void SetupInternalCommands(ExtendedCommandParser appExt)
        {
            // visible
            appExt.InternalOption("-l|--list", "--list", LocalizableStrings.ListsTemplates, CommandOptionType.NoValue);
            appExt.InternalOption("-lang|--language", "--language", LocalizableStrings.LanguageParameter, CommandOptionType.SingleValue);
            appExt.InternalOption("-n|--name", "--name", LocalizableStrings.NameOfOutput, CommandOptionType.SingleValue);
            appExt.InternalOption("-o|--output", "--output", LocalizableStrings.OutputPath, CommandOptionType.SingleValue);
            appExt.InternalOption("-h|--help", "--help", LocalizableStrings.DisplaysHelp, CommandOptionType.NoValue);
            appExt.InternalOption("--type", "--type", LocalizableStrings.ShowsFilteredTemplates, CommandOptionType.SingleValue);
            appExt.InternalOption("--force", "--force", LocalizableStrings.ForcesTemplateCreation, CommandOptionType.NoValue);

            // hidden
            appExt.HiddenInternalOption("-a|--alias", "--alias", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("-x|--extra-args", "--extra-args", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--locale", "--locale", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("--quiet", "--quiet", CommandOptionType.NoValue);
            appExt.HiddenInternalOption("-i|--install", "--install", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("-all|--show-all", "--show-all", CommandOptionType.NoValue);

            // reserved but not currently used
            appExt.HiddenInternalOption("-up|--update", "--update", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("-u|--uninstall", "--uninstall", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--skip-update-check", "--skip-update-check", CommandOptionType.NoValue);
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

            foreach (string preferredName in preferredNameList)
            {
                ITemplateInfo template = templateList.FirstOrDefault(x => string.Equals(x.ShortName, preferredName, StringComparison.OrdinalIgnoreCase));
                if (template != null)
                {
                    GenerateUsageForTemplate(template);
                    numShown++;
                }

                templateList.Remove(template);  // remove it so it won't get chosen again
            }

            // show up to 2 examples (total, including the above)
            Random rnd = new Random();
            for (int i = numShown; i < ExamplesToShow && templateList.Any(); i++)
            {
                int index = rnd.Next(0, templateList.Count - 1);
                ITemplateInfo template = templateList[index];
                GenerateUsageForTemplate(template);
                templateList.Remove(template);  // remove it so it won't get chosen again
            }

            // show a help example
            Reporter.Output.WriteLine($"    dotnet {CommandName} --help");
        }

        private void ShowTemplateHelp(ITemplateInfo templateInfo, bool showImplicitlyHiddenParams = false)
        {
            IList<string> languages = null;

            if (templateInfo.Tags != null && templateInfo.Tags.TryGetValue("language", out ICacheTag languageTag))
            {
                languages = languageTag.ChoicesAndDescriptions.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            }

            if (languages != null && languages.Any())
            {
                Reporter.Output.WriteLine($"{templateInfo.Name} ({string.Join(", ", languages)})");
            }
            else
            {
                Reporter.Output.WriteLine(templateInfo.Name);
            }

            if (!string.IsNullOrWhiteSpace(templateInfo.Author))
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.Author, templateInfo.Author));
            }

            if (!string.IsNullOrWhiteSpace(templateInfo.Description))
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.Description, templateInfo.Description));
            }

            string additionalInfo = GetTemplateParameterErrorsMessage(templateInfo, out IParameterSet allParams, out IReadOnlyList<string> userParamsWithInvalidValues);

            HashSet<string> parametersToExplicitlyHide = _hostSpecificTemplateData?.HiddenParameterNames ?? new HashSet<string>();
            ShowParameterHelp(_app.AllTemplateParams, allParams, additionalInfo, userParamsWithInvalidValues, parametersToExplicitlyHide, showImplicitlyHiddenParams);
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
            _app.ShowHelp();
            Reporter.Output.WriteLine();
        }

        private bool AnyRemainingParameters
        {
            get
            {
                return _app.RemainingParameters.Any(x => !x.Key.StartsWith("--debug:"));
            }
        }

        private bool ValidateRemainingParameters()
        {
            if (AnyRemainingParameters)
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                foreach (string flag in _app.RemainingParameters.Keys)
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
