// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand
    {
        private const string Indent = "  ";

        public IEnumerable<HelpSectionDelegate> CustomHelpLayout()
        {
            yield return (context) => WriteHelp(context, context.ParseResult);
        }

        public void WriteHelp(HelpContext context, ParseResult parseResult)
        {
            InstantiateCommandArgs instantiateCommandArgs = new InstantiateCommandArgs(this, parseResult);
            if (string.IsNullOrWhiteSpace(instantiateCommandArgs.ShortName))
            {
                WriteCustomInstantiateHelp(context);
                return;
            }

            IEngineEnvironmentSettings environmentSettings = GetEnvironmentSettingsFromArgs(instantiateCommandArgs);

            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            HostSpecificDataLoader hostSpecificDataLoader = new HostSpecificDataLoader(environmentSettings);

            //TODO: consider use cache only for help
            var selectedTemplateGroups = Task.Run(
                async () => await GetMatchingTemplateGroupsAsync(
                    instantiateCommandArgs,
                    templatePackageManager,
                    hostSpecificDataLoader,
                    default).ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();

            if (!selectedTemplateGroups.Any())
            {
                //help do not return error exit code, so we write error to StdOut instead
                HandleNoMatchingTemplateGroup(instantiateCommandArgs, Reporter.Output);
                return;
            }
            if (selectedTemplateGroups.Take(2).Count() > 1)
            {
                HandleAmbiguousTemplateGroup(environmentSettings, templatePackageManager, selectedTemplateGroups, Reporter.Output);
                return;
            }

            TemplateGroup templateGroup = selectedTemplateGroups.Single();
            IEnumerable<TemplateCommand> matchingTemplates =
                GetMatchingTemplates(
                    instantiateCommandArgs,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup);

            if (!matchingTemplates.Any())
            {
                //output is handled in HandleNoTemplateFoundResult
                HandleNoTemplateFoundResult(instantiateCommandArgs, environmentSettings, templatePackageManager, templateGroup, Reporter.Output);
                return;
            }

            if (!VerifyMatchingTemplates(
                environmentSettings,
                matchingTemplates,
                Reporter.Output,
                out IEnumerable<TemplateCommand>? templatesToShow))
            {
                //error
                //output is handled in VerifyMatchingTemplates
                return;
            }

            var preferredTemplate = templatesToShow.OrderByDescending(x => x.Template.Precedence).First();

            ShowTemplateDetailHeaders(preferredTemplate.Template, context.Output);
            //we need to show all possible short names (not just the one specified)
            ShowUsage(templateGroup.ShortNames, context);
            ShowCommandOptions(templatesToShow, preferredTemplate, context);
            ShowTemplateSpecificOptions(templatesToShow, context);
            ShowHintForOtherTemplates(templateGroup, preferredTemplate.Template, instantiateCommandArgs.CommandName, context.Output);
        }

        internal static bool VerifyMatchingTemplates(
            IEngineEnvironmentSettings environmentSettings,
            IEnumerable<TemplateCommand> matchingTemplates,
            Reporter reporter,
            [NotNullWhen(true)]
            out IEnumerable<TemplateCommand>? filteredTemplates)
        {
            filteredTemplates = matchingTemplates;

            //if more than one language, this is an error - handle it
            IEnumerable<string?> languages = matchingTemplates.Select(c => c.Template.GetLanguage()).Distinct();
            if (languages.Count() > 1)
            {
                var defaultLanguage = environmentSettings.GetDefaultLanguage();
                if (languages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase))
                {
                    var templatesForDefaultLanguage = filteredTemplates.Where(c => string.Equals(c.Template.GetLanguage(), defaultLanguage, StringComparison.OrdinalIgnoreCase));
                    if (templatesForDefaultLanguage.Any())
                    {
                        filteredTemplates = templatesForDefaultLanguage;
                    }
                    else
                    {
                        HandleAmbiguousLanguage(
                            environmentSettings,
                            matchingTemplates.Select(c => c.Template),
                            reporter);

                        filteredTemplates = null;
                        return false;
                    }
                }
                else
                {
                    HandleAmbiguousLanguage(
                        environmentSettings,
                        matchingTemplates.Select(c => c.Template),
                        reporter);

                    filteredTemplates = null;
                    return false;
                }
            }

            //if more than one type, this is an error - handle it
            IEnumerable<string?> types = filteredTemplates.Select(c => c.Template.GetTemplateType()).Distinct();
            if (types.Count() > 1)
            {
                HandleAmbiguousType(
                    environmentSettings,
                    matchingTemplates.Select(c => c.Template),
                    reporter);
                filteredTemplates = null;
                return false;
            }
            return true;
        }

        internal static void ShowTemplateDetailHeaders(CliTemplateInfo preferredTemplate, TextWriter writer)
        {
            string? language = preferredTemplate.GetLanguage();

            if (!string.IsNullOrWhiteSpace(language))
            {
                writer.WriteLine($"{preferredTemplate.Name} ({language})");
            }
            else
            {
                writer.WriteLine(preferredTemplate.Name);
            }

            if (!string.IsNullOrWhiteSpace(preferredTemplate.Author))
            {
                writer.WriteLine(HelpStrings.RowHeader_TemplateAuthor, preferredTemplate.Author);
            }

            if (!string.IsNullOrWhiteSpace(preferredTemplate.Description))
            {
                writer.WriteLine(HelpStrings.RowHeader_Description, preferredTemplate.Description);
            }

            if (!string.IsNullOrEmpty(preferredTemplate.ThirdPartyNotices))
            {
                writer.WriteLine(HelpStrings.Info_TemplateThirdPartyNotice, preferredTemplate.ThirdPartyNotices);
            }
            writer.WriteLine();
        }

        internal static void ShowHintForOtherTemplates(TemplateGroup templateGroup, CliTemplateInfo preferredtemplate, string commandName, TextWriter writer)
        {
            //other languages
            if (templateGroup.Languages.Count <= 1)
            {
                return;
            }

            string? preferredLanguage = preferredtemplate.GetLanguage();

            List<string> supportedLanguages = new List<string>();
            foreach (string? language in templateGroup.Languages)
            {
                if (string.IsNullOrWhiteSpace(language))
                {
                    continue;
                }
                if (!language.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    supportedLanguages.Add(language);
                }
            }

            if (!supportedLanguages.Any())
            {
                return;
            }
            supportedLanguages.Sort(StringComparer.OrdinalIgnoreCase);
            writer.WriteLine(HelpStrings.Hint_HelpForOtherLanguages, string.Join(", ", supportedLanguages));
            writer.WriteLine(CommandExamples.HelpCommandExample(commandName, templateGroup.ShortNames[0], supportedLanguages.First()).Indent());
            writer.WriteLine();

            //other types
            if (templateGroup.Types.Count <= 1)
            {
                return;
            }

            string? preferredType = preferredtemplate.GetTemplateType();

            List<string> supportedTypes = new List<string>();
            foreach (string? type in templateGroup.Types)
            {
                if (string.IsNullOrWhiteSpace(type))
                {
                    continue;
                }
                if (!type.Equals(preferredType, StringComparison.OrdinalIgnoreCase))
                {
                    supportedTypes.Add(type);
                }
            }

            if (!supportedTypes.Any())
            {
                return;
            }
            supportedTypes.Sort(StringComparer.OrdinalIgnoreCase);
            writer.WriteLine(HelpStrings.Hint_HelpForOtherTypes, string.Join(", ", supportedTypes));
            writer.WriteLine(CommandExamples.HelpCommandExample(commandName, templateGroup.ShortNames[0], type: supportedTypes.First()).Indent());
            writer.WriteLine();
        }

        internal static void ShowTemplateSpecificOptions(
            IEnumerable<TemplateCommand> templates,
            HelpContext context)
        {
            IEnumerable<TemplateOption> optionsToShow = CollectOptionsToShow(templates, context);

            context.Output.WriteLine(HelpStrings.SectionHeader_TemplateSpecificOptions);
            if (!optionsToShow.Any())
            {
                context.Output.WriteLine(HelpStrings.Text_NoTemplateOptions.Indent());
                return;
            }

            IEnumerable<TwoColumnHelpRow> optionsToWrite = optionsToShow.Select(o => context.HelpBuilder.GetTwoColumnRow(o.Option, context));
            context.HelpBuilder.WriteColumns(optionsToWrite.ToArray(), context);
            context.Output.WriteLine();
        }

        internal static void ShowCommandOptions(
            IEnumerable<TemplateCommand> templatesToShow,
            TemplateCommand preferredTemplate,
            HelpContext context)
        {
            List<Option> optionsToShow = new List<Option>()
            {
                preferredTemplate.NameOption,
                preferredTemplate.OutputOption,
                preferredTemplate.DryRunOption,
                preferredTemplate.ForceOption,
                preferredTemplate.NoUpdateCheckOption
            };

            foreach (var template in templatesToShow)
            {
                if (template.LanguageOption != null)
                {
                    optionsToShow.Add(template.LanguageOption);
                    break;
                }
            }
            foreach (var template in templatesToShow)
            {
                if (template.TypeOption != null)
                {
                    optionsToShow.Add(template.TypeOption);
                    break;
                }
            }
            foreach (var template in templatesToShow)
            {
                if (template.AllowScriptsOption != null)
                {
                    optionsToShow.Add(template.AllowScriptsOption);
                    break;
                }
            }

            context.Output.WriteLine(context.HelpBuilder.LocalizationResources.HelpOptionsTitle());
            IEnumerable<TwoColumnHelpRow> optionsToWrite = optionsToShow.Select(o => context.HelpBuilder.GetTwoColumnRow(o, context));
            context.HelpBuilder.WriteColumns(optionsToWrite.ToArray(), context);
            context.Output.WriteLine();
        }

        internal IEnumerable<TemplateCommand> GetMatchingTemplates(
            InstantiateCommandArgs instantiateCommandArgs,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup)
        {
            List<TemplateCommand> matchingTemplates = new();

            //unlike instantiation we need to try all the templates
            //however we try them in precedence order
            //so the highest priority one is first
            foreach (IGrouping<int, CliTemplateInfo> templateGrouping in templateGroup.Templates.GroupBy(g => g.Precedence).OrderByDescending(g => g.Key))
            {
                foreach (CliTemplateInfo template in templateGrouping)
                {
                    if (ReparseForTemplate(
                        instantiateCommandArgs,
                        environmentSettings,
                        templatePackageManager,
                        templateGroup,
                        template)
                        is (TemplateCommand command, ParseResult parseResult))
                    {
                        if (!parseResult.Errors.Any())
                        {
                            matchingTemplates.Add(command);
                        }
                    }
                }
            }

            return matchingTemplates;
        }

        internal void ShowUsage(IReadOnlyList<string> shortNames, HelpContext context)
        {
            List<string> usageParts = new List<string>();

            Command? command = this;
            while (command is not null)
            {
                if (!string.IsNullOrWhiteSpace(command.Name))
                {
                    usageParts.Add(command.Name);
                }
                command = command.Parents.FirstOrDefault(c => c is Command) as Command;
            }

            usageParts.Reverse();
            context.Output.WriteLine(context.HelpBuilder.LocalizationResources.HelpUsageTitle());
            foreach (string shortName in shortNames)
            {
                var parts = usageParts.Concat(
                    new[]
                    {
                        shortName,
                        context.HelpBuilder.LocalizationResources.HelpUsageOptions(),
                        HelpStrings.Text_UsageTemplateOptionsPart
                    });
                context.Output.WriteLine(Indent + string.Join(" ", parts));
            }
            context.Output.WriteLine();
        }

        /// <summary>
        /// Ensure <paramref name="templates"/> are sorted in priority order
        /// The highest priority should come first.
        /// </summary>
        private static IEnumerable<TemplateOption> CollectOptionsToShow(IEnumerable<TemplateCommand> templates, HelpContext context)
        {
            Dictionary<string, (CliTemplateParameter Parameter, IReadOnlyList<string> Aliases)> parametersToShow = new();

            //templates are in priority order
            //in case parameters are different in different templates
            //highest priority ones wins
            //except the choice parameter, where we merge possible values
            foreach (TemplateCommand command in templates)
            {
                foreach (CliTemplateParameter currentParam in command.Template.CliParameters.Values)
                {
                    if (currentParam.IsHidden && !currentParam.AlwaysShow)
                    {
                        continue;
                    }

                    if (parametersToShow.TryGetValue(currentParam.Name, out var existingParam))
                    {
                        if (currentParam is ChoiceTemplateParameter currentChoiceParam
                            && existingParam.Parameter is ChoiceTemplateParameter existingChoiceParam)
                        {
                            if (existingChoiceParam is CombinedChoiceTemplateParameter combinedParam)
                            {
                                combinedParam.MergeChoices(currentChoiceParam);
                            }
                            else
                            {
                                var combinedChoice = new CombinedChoiceTemplateParameter(existingChoiceParam);
                                combinedChoice.MergeChoices(currentChoiceParam);
                                parametersToShow[currentParam.Name] = (combinedChoice, existingParam.Aliases);
                            }
                        }
                    }
                    else
                    {
                        var aliases = command.TemplateOptions[currentParam.Name].Aliases.OrderByDescending(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
                        parametersToShow[currentParam.Name] = (currentParam, aliases);
                    }
                }
            }

            var optionsToShow = parametersToShow.Values.Select(p => new TemplateOption(p.Parameter, p.Aliases)).ToList();
            foreach (var option in optionsToShow)
            {
                context.HelpBuilder.CustomizeSymbol(
                    option.Option,
                    firstColumnText: option.TemplateParameter.GetCustomFirstColumnText(option),
                    secondColumnText: option.TemplateParameter.GetCustomSecondColumnText());
            }
            return optionsToShow;
        }

        private void WriteCustomInstantiateHelp(HelpContext context)
        {
            HelpBuilder.Default.SynopsisSection()(context);
            context.Output.WriteLine();
            CustomUsageSection(context);
            HelpBuilder.Default.CommandArgumentsSection()(context);
            context.Output.WriteLine();
            HelpBuilder.Default.OptionsSection()(context);
            context.Output.WriteLine();
            HelpBuilder.Default.SubcommandsSection()(context);
            context.Output.WriteLine();
            HelpBuilder.Default.AdditionalArgumentsSection()(context);
            context.Output.WriteLine();
        }

        private void CustomUsageSection(HelpContext context)
        {
            context.Output.WriteLine(context.HelpBuilder.LocalizationResources.HelpUsageTitle());
            context.Output.WriteLine(Indent + string.Join(" ", GetUsageParts(context, this, showSubcommands: false)));
            context.Output.WriteLine(Indent + string.Join(" ", GetUsageParts(context, this, showParentArguments: false, showArguments: false)));
        }
    }
}
