// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
//using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
//using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
//using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    internal class New3Command
    {
        private const string CommandName = "new3";
        private static readonly string HostIdentifier = "dotnetcli";
        private static readonly string HostVersion = typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString();
        private static readonly IReadOnlyCollection<MatchLocation> NameFields = new HashSet<MatchLocation> { MatchLocation.Name, MatchLocation.ShortName, MatchLocation.Alias };
        private static DefaultTemplateEngineHost Host;
        private ExtendedCommandParser _app;
        private HostSpecificTemplateData _hostSpecificTemplateData;
        private IReadOnlyList<IFilteredTemplateInfo> _matchedTemplates;
        private CommandArgument _templateName;
        private ITemplateInfo _unambiguousTemplateToUse;

        private static readonly Regex LocaleFormatRegex = new Regex(@"
                    ^
                        [a-z]{2}
                        (?:-[A-Z]{2})?
                    $"
            , RegexOptions.IgnorePatternWhitespace);
        private bool _forceAmbiguousFlow;

        public New3Command(ExtendedCommandParser app, CommandArgument templateNames)
        {
            _app = app;
            _templateName = templateNames;
        }

        public string Alias => _app.InternalParamValue("--alias");

        public bool DebugAttachHasValue => _app.RemainingArguments.Any(x => x.Equals("--debug:attach", StringComparison.Ordinal));

        public bool ExtraArgsHasValue => _app.InternalParamHasValue("--extra-args");

        public IList<string> Install => _app.InternalParamValueList("--install");

        public static IInstaller Installer { get; set; } = new Installer();

        public bool InstallHasValue => _app.InternalParamHasValue("--install");

        public bool IsHelpFlagSpecified => _app.InternalParamHasValue("--help");

        public bool IsListFlagSpecified => _app.InternalParamHasValue("--list");

        public bool IsQuietFlagSpecified => _app.InternalParamHasValue("--quiet");

        public bool IsShowAllFlagSpecified => _app.InternalParamHasValue("--show-all");

        public string Language => _app.InternalParamValue("--language");

        public string Locale => _app.InternalParamValue("--locale");

        public bool LocaleHasValue => _app.InternalParamHasValue("--locale");

        public string Name => _app.InternalParamValue("--name");

        public string OutputPath => _app.InternalParamValue("--output");

        public bool SkipUpdateCheck => _app.InternalParamHasValue("--skip-update-check");

        public ITemplateInfo UnambiguousTemplateToUse
        {
            get
            {
                if (_unambiguousTemplateToUse != null)
                {
                    return _unambiguousTemplateToUse;
                }

                if (_forceAmbiguousFlow || _matchedTemplates == null || _matchedTemplates.Count == 0)
                {
                    return null;
                }

                if (_matchedTemplates.Count == 1)
                {
                    return _unambiguousTemplateToUse = _matchedTemplates.First().Info;
                }

                if (EngineEnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage))
                {
                    IFilteredTemplateInfo match = null;

                    foreach(IFilteredTemplateInfo info in _matchedTemplates)
                    {
                        if (info.Info.Tags == null || info.Info.Tags.TryGetValue("language", out string specificLanguage) && string.Equals(specificLanguage, defaultLanguage, StringComparison.OrdinalIgnoreCase))
                        {
                            if(match == null)
                            {
                                match = info;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }

                    return _unambiguousTemplateToUse = match?.Info;
                }

                return null;
            }
        }

        public static int Run(string[] args)
        {
            ConfigureHost();
            ExtendedCommandParser app = new ExtendedCommandParser()
            {
                Name = "dotnet new",
                FullName = LocalizableStrings.CommandDescription
            };
            SetupInternalCommands(app);

            CommandArgument templateName = app.Argument("template", LocalizableStrings.TemplateArgumentHelp);
            New3Command instance = new New3Command(app, templateName);

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
            string[] packageList;

            if (Paths.Global.DefaultInstallPackageList.FileExists())
            {
                packageList = Paths.Global.DefaultInstallPackageList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (packageList.Length > 0)
                {
                    Installer.InstallPackages(packageList);
                }
            }

            if (Paths.Global.DefaultInstallTemplateList.FileExists())
            {
                packageList = Paths.Global.DefaultInstallTemplateList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (packageList.Length > 0)
                {
                    Installer.InstallPackages(packageList);
                }
            }

            string templatesDir = Path.Combine(Paths.Global.BaseDir, "Templates");

            if (templatesDir.Exists())
            {
                IEnumerable<string> layoutIncludedPackages = Host.FileSystem.EnumerateFiles(templatesDir, "*.nupkg", SearchOption.TopDirectoryOnly);
                Installer.InstallPackages(layoutIncludedPackages);
            }
        }

        private static void ConfigureHost()
        {
            Dictionary<Guid, Func<Type>> builtIns = new Dictionary<Guid, Func<Type>>
            {
                //{ new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3"), () => typeof(RunnableProjectGenerator) },
                //{ new Guid("3147965A-08E5-4523-B869-02C8E9A8AAA1"), () => typeof(BalancedNestingConfig) },
                //{ new Guid("3E8BCBF0-D631-45BA-A12D-FBF1DE03AA38"), () => typeof(ConditionalConfig) },
                //{ new Guid("A1E27A4B-9608-47F1-B3B8-F70DF62DC521"), () => typeof(FlagsConfig) },
                //{ new Guid("3FAE1942-7257-4247-B44D-2DDE07CB4A4A"), () => typeof(IncludeConfig) },
                //{ new Guid("3D33B3BF-F40E-43EB-A14D-F40516F880CD"), () => typeof(RegionConfig) },
                //{ new Guid("62DB7F1F-A10E-46F0-953F-A28A03A81CD1"), () => typeof(ReplacementConfig) },
                //{ new Guid("370996FE-2943-4AED-B2F6-EC03F0B75B4A"), () => typeof(ConstantMacro) },
                //{ new Guid("BB625F71-6404-4550-98AF-B2E546F46C5F"), () => typeof(EvaluateMacro) },
                //{ new Guid("10919008-4E13-4FA8-825C-3B4DA855578E"), () => typeof(GuidMacro) },
                //{ new Guid("F2B423D7-3C23-4489-816A-41D8D2A98596"), () => typeof(NowMacro) },
                //{ new Guid("011E8DC1-8544-4360-9B40-65FD916049B7"), () => typeof(RandomMacro) },
                //{ new Guid("8A4D4937-E23F-426D-8398-3BDBD1873ADB"), () => typeof(RegexMacro) },
                //{ new Guid("B57D64E0-9B4F-4ABE-9366-711170FD5294"), () => typeof(SwitchMacro) },
                //{ new Guid("10919118-4E13-4FA9-825C-3B4DA855578E"), () => typeof(CaseChangeMacro) }
            };

            Host = new DefaultTemplateEngineHost(HostIdentifier, HostVersion, CultureInfo.CurrentCulture.Name, new Dictionary<string, string> { { "prefs:language", "C#" } }, builtIns.ToList());
            EngineEnvironmentSettings.Host = Host;
        }

        private async Task<int> CreateTemplateAsync(ITemplateInfo template)
        {
            string fallbackName = new DirectoryInfo(OutputPath ?? Directory.GetCurrentDirectory()).Name;
            TemplateCreationResult instantiateResult = await TemplateCreator.InstantiateAsync(template, Name, fallbackName, OutputPath, _app.AllTemplateParams, SkipUpdateCheck).ConfigureAwait(false);
            string resultTemplateName = string.IsNullOrEmpty(instantiateResult.TemplateFullName) ? _templateName.Value : instantiateResult.TemplateFullName;

            switch (instantiateResult.Status)
            {
                case CreationResultStatus.CreateSucceeded:
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.CreateSuccessful, resultTemplateName));
                    break;
                case CreationResultStatus.CreateFailed:
                    break;
                case CreationResultStatus.MissingMandatoryParam:
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.MissingRequiredParameter, instantiateResult.Message, resultTemplateName).Bold().Red());
                    break;
                case CreationResultStatus.InvalidParamValues:
                    ShowTemplateHelp();
                    break;
                default:
                    break;
            }

            return instantiateResult.ResultCode;
        }

        private void DisplayTemplateList()
        {
            IEnumerable<ITemplateInfo> results = _matchedTemplates.Where(x => x.IsMatch).Select(x => x.Info);

            IEnumerable<IGrouping<string, ITemplateInfo>> grouped = results.GroupBy(x => x.GroupIdentity);
            EngineEnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out string defaultLanguage);

            Dictionary<ITemplateInfo, string> templatesVersusLanguages = new Dictionary<ITemplateInfo, string>();

            foreach (IGrouping<string, ITemplateInfo> grouping in grouped)
            {
                List<string> languages = new List<string>();

                foreach (ITemplateInfo info in grouping)
                {
                    if (info.Tags != null && info.Tags.TryGetValue("language", out string lang))
                    {
                        if (string.IsNullOrEmpty(Language) && string.Equals(defaultLanguage, lang, StringComparison.OrdinalIgnoreCase))
                        {
                            lang = $"[{lang}]";
                        }

                        languages.Add(lang);
                    }
                }

                templatesVersusLanguages[grouping.First()] = string.Join(", ", languages);
            }

            HelpFormatter<KeyValuePair<ITemplateInfo, string>> formatter = new HelpFormatter<KeyValuePair<ITemplateInfo, string>>(templatesVersusLanguages, 6, '-', false)
                .DefineColumn(t => t.Key.Name, LocalizableStrings.Templates)
                .DefineColumn(t => t.Key.ShortName, LocalizableStrings.ShortName)
                .DefineColumn(t => t.Value, out object languageColumn, LocalizableStrings.Language)
                .DefineColumn(t => t.Key.Classifications != null ? string.Join("/", t.Key.Classifications) : null, out object tagsColumn, LocalizableStrings.Tags)
                .OrderByDescending(languageColumn, new NullOrEmptyIsLastStringComparer())
                .OrderBy(tagsColumn);
            Reporter.Output.WriteLine(formatter.Layout());

            if (!IsListFlagSpecified)
            {
                ShowInvocationExamples();
            }
        }

        private Task<int> EnterAmbiguousTemplateManipulationFlowAsync()
        {
            if (!ValidateRemainingParameters())
            {
                ShowUsageHelp();
                return Task.FromResult(-1);
            }    

            if (!string.IsNullOrWhiteSpace(Alias))
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                Reporter.Error.WriteLine("  " + _app.TemplateParamInputFormat("--alias").Bold().Red());
                return Task.FromResult(-1);
            }

            if (IsHelpFlagSpecified)
            {
                ShowUsageHelp();
                DisplayTemplateList();
                return Task.FromResult(0);
            }
            else
            {
                DisplayTemplateList();

                //If we're showing the list because we were asked to, exit with success, otherwise, exit with failure
                if (IsListFlagSpecified)
                {
                    return Task.FromResult(0);
                }
                else
                {
                    return Task.FromResult(-1);
                }
            }
        }

        private Task<int> EnterInstallFlowAsync()
        {
            Installer.InstallPackages(Install.ToList());
            //TODO: When an installer that directly calls into NuGet is available,
            //  return a more accurate representation of the outcome of the operation
            return Task.FromResult(0);
        }

        private async Task<int> EnterMaintenanceFlowAsync()
        {
            if (!ValidateRemainingParameters())
            {
                ShowUsageHelp();
                return -1;
            }

            if (InstallHasValue)
            {
                int installResult = await EnterInstallFlowAsync().ConfigureAwait(false);

                if(installResult == 0)
                {                    
                    await PerformCoreTemplateQueryAsync().ConfigureAwait(false);
                    DisplayTemplateList();
                }

                return installResult;
            }

            //No other cases specified, we've fallen through to "Usage help + List"
            ShowUsageHelp();
            await PerformCoreTemplateQueryAsync().ConfigureAwait(false);
            DisplayTemplateList();

            return 0;
        }

        private async Task<int> EnterSingularTemplateManipulationFlowAsync()
        {
            if (!string.IsNullOrWhiteSpace(Alias))
            {
                if (!ValidateRemainingParameters())
                {
                    ShowUsageHelp();
                    return -1;
                }

                AliasRegistry.SetTemplateAlias(Alias, UnambiguousTemplateToUse);
                Reporter.Output.WriteLine(LocalizableStrings.AliasCreated);
                return 0;
            }
            else if(IsListFlagSpecified)
            {
                if (!ValidateRemainingParameters())
                {
                    ShowUsageHelp();
                    return -1;
                }

                DisplayTemplateList();
                return 0;
            }
            
            //If we've made it here, we need the actual template's args
            ParseTemplateArgs(UnambiguousTemplateToUse);
            bool argsError = !ValidateRemainingParameters();

            if (argsError || IsHelpFlagSpecified)
            {
                ShowUsageHelp();
                ShowTemplateHelp();
                return argsError ? -1 : 0;
            }
            else
            {
                return await CreateTemplateAsync(UnambiguousTemplateToUse).ConfigureAwait(false);
            }
        }

        private async Task<int> EnterTemplateManipulationFlowAsync()
        {
            await PerformCoreTemplateQueryAsync().ConfigureAwait(false);

            if (UnambiguousTemplateToUse != null)
            {
                return await EnterSingularTemplateManipulationFlowAsync().ConfigureAwait(false);
            }

            return await EnterAmbiguousTemplateManipulationFlowAsync().ConfigureAwait(false);
        }

        private async Task<int> ExecuteAsync()
        {
            //Parse non-template specific arguments
            _app.ParseArgs();
            if (ExtraArgsHasValue)
            {
                _app.ParseArgs(_app.InternalParamValueList("--extra-args"));
            }

            if (DebugAttachHasValue)
            {
                Console.ReadLine();
            }

            ConfigureLocale();
            Initialize();

            if (string.IsNullOrWhiteSpace(_templateName.Value))
            {
                return await EnterMaintenanceFlowAsync().ConfigureAwait(false);
            }

            return await EnterTemplateManipulationFlowAsync().ConfigureAwait(false);
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

                Host.UpdateLocale(newLocale);
            }
        }

        private IEnumerable<ITemplateParameter> FilterParamsForHelp(IParameterSet allParams, HashSet<string> hiddenParams)
        {
            IEnumerable<ITemplateParameter> filteredParams = allParams.ParameterDefinitions
                .Where(x => x.Priority != TemplateParameterPriority.Implicit && !hiddenParams.Contains(x.Name));
            return filteredParams;
        }

        private void GenerateUsageForTemplate(ITemplateInfo templateInfo)
        {
            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);
            IParameterSet allParams = template.Generator.GetParametersForTemplate(template);
            HostSpecificTemplateData hostTemplateData = ReadHostSpecificTemplateData(template);

            Reporter.Output.Write($"    dotnet {CommandName} {template.ShortName}");
            IEnumerable<ITemplateParameter> filteredParams = FilterParamsForHelp(allParams, hostTemplateData.HiddenParameterNames);

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
            bool reinitFlag = _app.RemainingArguments.Any(x => x == "--debug:reinit");
            if (reinitFlag)
            {
                Paths.User.FirstRunCookie.Delete();
            }

            // Note: this leaves things in a weird state. Might be related to the localized caches.
            // not sure, need to look into it.
            if (reinitFlag || _app.RemainingArguments.Any(x => x == "--debug:reset-config"))
            {
                Paths.User.AliasesFile.Delete();
                Paths.User.SettingsFile.Delete();
                TemplateCache.DeleteAllLocaleCacheFiles();
                return false;
            }

            if (!Paths.User.BaseDir.Exists() || !Paths.User.FirstRunCookie.Exists())
            {
                if (!IsQuietFlagSpecified)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.GettingReady);
                }

                ConfigureEnvironment();
                Paths.User.FirstRunCookie.WriteAllText("");
            }

            if (_app.RemainingArguments.Any(x => x == "--debug:showconfig"))
            {
                ShowConfig();
                return false;
            }

            return true;
        }

        private void ParameterHelp(IParameterSet allParams, string additionalInfo, HashSet<string> hiddenParams)
        {
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                Reporter.Error.WriteLine(additionalInfo.Bold().Red());
                Reporter.Output.WriteLine();
            }

            IEnumerable<ITemplateParameter> filteredParams = FilterParamsForHelp(allParams, hiddenParams);

            if (filteredParams.Any())
            {
                HelpFormatter<ITemplateParameter> formatter = new HelpFormatter<ITemplateParameter>(filteredParams, 2, null, true);

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
                        displayValue.AppendLine(string.Join(", ", param.Choices));
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
                            _app.AllTemplateParams.TryGetValue(param.Name, out configuredValue);
                        }
                    }

                    if (!string.IsNullOrEmpty(configuredValue))
                    {
                        displayValue.AppendLine(string.Format(LocalizableStrings.ConfiguredValue, configuredValue));
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
            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);
            IParameterSet allParams = template.Generator.GetParametersForTemplate(template);
            _hostSpecificTemplateData = ReadHostSpecificTemplateData(template);
            _app.SetupTemplateParameters(allParams, _hostSpecificTemplateData.LongNameOverrides, _hostSpecificTemplateData.ShortNameOverrides);

            // re-parse after setting up the template params
            _app.ParseArgs(_app.InternalParamValueList("--extra-args"));
        }

        private Task PerformCoreTemplateQueryAsync()
        {
            string outputPath = OutputPath ?? Host.FileSystem.GetCurrentDirectory();

            string context = null;
            if (!IsShowAllFlagSpecified)
            {
                if (Host.FileSystem.DirectoryExists(outputPath) && Host.FileSystem.EnumerateFiles(outputPath, "*.*proj", SearchOption.TopDirectoryOnly).Any())
                {
                    context = "item";
                }
                else
                {
                    context = "project";
                }
            }

            //Perform the core query to search for templates
            IReadOnlyCollection<IFilteredTemplateInfo> templates = TemplateCreator.List
            (
                false,
                WellKnownSearchFilters.AliasFilter(_templateName.Value),
                WellKnownSearchFilters.NameFilter(_templateName.Value),
                WellKnownSearchFilters.ClassificationsFilter(_templateName.Value),
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

            return Task.FromResult(true);
        }

        private static HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplate templateInfo)
        {
            if (SettingsLoader.TryGetFileFromIdAndPath(templateInfo.HostConfigMountPointId, templateInfo.HostConfigPlace, out IFile file))
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
            appExt.InternalOption("-all|--show-all", "--show-all", LocalizableStrings.ShowsAllTemplates, CommandOptionType.NoValue);

            // hidden
            appExt.HiddenInternalOption("-a|--alias", "--alias", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("-x|--extra-args", "--extra-args", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--locale", "--locale", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("--quiet", "--quiet", CommandOptionType.NoValue);
            appExt.HiddenInternalOption("-i|--install", "--install", CommandOptionType.MultipleValue);

            // reserved but not currently used
            appExt.HiddenInternalOption("-up|--update", "--update", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("-u|--uninstall", "--uninstall", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--skip-update-check", "--skip-update-check", CommandOptionType.NoValue);
        }

        private static void ShowConfig()
        {
            Reporter.Output.WriteLine(LocalizableStrings.CurrentConfiguration);
            Reporter.Output.WriteLine(" ");
            TableFormatter.Print(SettingsLoader.MountPoints, LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<MountPointInfo, object>>
            {
                {LocalizableStrings.MountPoints, x => x.Place},
                {LocalizableStrings.Id, x => x.MountPointId},
                {LocalizableStrings.Parent, x => x.ParentMountPointId},
                {LocalizableStrings.Factory, x => x.MountPointFactoryId}
            });

            TableFormatter.Print(SettingsLoader.Components.OfType<IMountPointFactory>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IMountPointFactory, object>>
            {
                {LocalizableStrings.MountPointFactories, x => x.Id},
                {LocalizableStrings.Type, x => x.GetType().FullName},
                {LocalizableStrings.Assembly, x => x.GetType().GetTypeInfo().Assembly.FullName}
            });

            TableFormatter.Print(SettingsLoader.Components.OfType<IGenerator>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IGenerator, object>>
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

        private void ShowTemplateHelp()
        {
            if (UnambiguousTemplateToUse.Tags != null && UnambiguousTemplateToUse.Tags.TryGetValue("language", out string templateLang) && !string.IsNullOrWhiteSpace(templateLang))
            {
                Reporter.Output.WriteLine($"{UnambiguousTemplateToUse.Name} ({templateLang})");
            }
            else
            {
                Reporter.Output.WriteLine(UnambiguousTemplateToUse.Name);
            }

            if (!string.IsNullOrWhiteSpace(UnambiguousTemplateToUse.Author))
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.Author, UnambiguousTemplateToUse.Author));
            }

            if (!string.IsNullOrWhiteSpace(UnambiguousTemplateToUse.Description))
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.Description, UnambiguousTemplateToUse.Description));
            }

            ITemplate template = SettingsLoader.LoadTemplate(UnambiguousTemplateToUse);
            IParameterSet allParams = TemplateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName, out IList<string> defaultParamsWithInvalidValues);
            TemplateCreator.ResolveUserParameters(template, allParams, _app.AllTemplateParams, out IList<string> userParamsWithInvalidValues);

            string additionalInfo = null;
            if (userParamsWithInvalidValues.Any())
            {
                // Lookup the input param formats - userParamsWithInvalidValues has canonical.
                IList<string> inputParamFormats = new List<string>();
                foreach (string canonical in userParamsWithInvalidValues)
                {
                    string inputFormat = _app.TemplateParamInputFormat(canonical);
                    inputParamFormats.Add(inputFormat);
                }
                string badParams = string.Join(", ", inputParamFormats);

                additionalInfo = string.Format(LocalizableStrings.InvalidParameterValues, badParams, template.Name);
            }

            ParameterHelp(allParams, additionalInfo, _hostSpecificTemplateData.HiddenParameterNames);
        }

        private void ShowUsageHelp()
        {
            _app.ShowHelp();
            Reporter.Output.WriteLine();
        }

        private bool ValidateRemainingParameters()
        {
            if (_app.RemainingParameters.Any(x => !x.Key.StartsWith("--debug:")))
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
