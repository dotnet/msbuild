// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.Diagnostics.CodeAnalysis;
using System.Resources;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand
    {
        private const string Indent = "  ";
        private static Lazy<ResourceManager> _resourceManager = new(
            () => new ResourceManager("System.CommandLine.Properties.Resources", typeof(System.CommandLine.CliSymbol).Assembly));

        public static void WriteHelp(HelpContext context, InstantiateCommandArgs instantiateCommandArgs, IEngineEnvironmentSettings environmentSettings)
        {
            if (string.IsNullOrWhiteSpace(instantiateCommandArgs.ShortName))
            {
                WriteCustomInstantiateHelp(context, instantiateCommandArgs.Command);
                return;
            }

            using TemplatePackageManager templatePackageManager = new(environmentSettings);
            HostSpecificDataLoader hostSpecificDataLoader = new(environmentSettings);

            //TODO: consider use cache only for help
            IEnumerable<TemplateGroup> allTemplateGroups = Task.Run(
                async () => await GetTemplateGroupsAsync(
                    templatePackageManager,
                    hostSpecificDataLoader,
                    default).ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();

            IEnumerable<TemplateGroup> selectedTemplateGroups = allTemplateGroups.Where(template => template.ShortNames.Contains(instantiateCommandArgs.ShortName));

            if (!selectedTemplateGroups.Any())
            {
                //help do not return error exit code, so we write error to StdOut instead
                HandleNoMatchingTemplateGroup(instantiateCommandArgs, allTemplateGroups, Reporter.Output);
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

            TemplateCommand preferredTemplate = templatesToShow.OrderByDescending(x => x.Template.Precedence).First();

            ShowTemplateDetailHeaders(preferredTemplate.Template, context.Output);
            //we need to show all possible short names (not just the one specified)
            ShowUsage(instantiateCommandArgs.Command, templateGroup.ShortNames, context);
            ShowCommandOptions(templatesToShow, context);
            ShowTemplateSpecificOptions(templatesToShow, context);
            ShowHintForOtherTemplates(templateGroup, preferredTemplate.Template, instantiateCommandArgs, context.Output);
        }

        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
        {
            yield return (context) =>
            {
                InstantiateCommandArgs instantiateCommandArgs = new(this, context.ParseResult);
                using IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(instantiateCommandArgs, context.ParseResult);
                WriteHelp(context, instantiateCommandArgs, environmentSettings);
            };
        }

        internal static bool VerifyMatchingTemplates(
            IEngineEnvironmentSettings environmentSettings,
            IEnumerable<TemplateCommand> matchingTemplates,
            IReporter reporter,
            [NotNullWhen(true)]
            out IEnumerable<TemplateCommand>? filteredTemplates)
        {
            filteredTemplates = matchingTemplates;

            //if more than one language, this is an error - handle it
            IEnumerable<string?> languages = matchingTemplates.Select(c => c.Template.GetLanguage()).Distinct();
            if (languages.Count() > 1)
            {
                string? defaultLanguage = environmentSettings.GetDefaultLanguage();
                if (languages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase))
                {
                    IEnumerable<TemplateCommand> templatesForDefaultLanguage = filteredTemplates.Where(c => string.Equals(c.Template.GetLanguage(), defaultLanguage, StringComparison.OrdinalIgnoreCase));
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

        internal static void ShowHintForOtherTemplates(TemplateGroup templateGroup, CliTemplateInfo preferredtemplate, InstantiateCommandArgs args, TextWriter writer)
        {
            //other languages
            if (templateGroup.Languages.Count <= 1)
            {
                return;
            }

            string? preferredLanguage = preferredtemplate.GetLanguage();

            List<string> supportedLanguages = new();
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
            writer.WriteLine(
                Example
                    .For<NewCommand>(args.ParseResult)
                    .WithArgument(NewCommand.ShortNameArgument, templateGroup.ShortNames[0])
                    .WithHelpOption()
                    .WithOption(SharedOptionsFactory.CreateLanguageOption(), supportedLanguages.First())
                    .ToString().Indent());
            writer.WriteLine();

            //other types
            if (templateGroup.Types.Count <= 1)
            {
                return;
            }

            string? preferredType = preferredtemplate.GetTemplateType();

            List<string> supportedTypes = new();
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
            writer.WriteLine(Example
                .For<NewCommand>(args.ParseResult)
                .WithArgument(NewCommand.ShortNameArgument, templateGroup.ShortNames[0])
                .WithHelpOption()
                .WithOption(SharedOptionsFactory.CreateTypeOption(), supportedTypes.First())
                .ToString().Indent());
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

            IEnumerable<TwoColumnHelpRow> optionsToWrite = optionsToShow.Select(o =>
            {
                o.Option.EnsureHelpName();

                return context.HelpBuilder.GetTwoColumnRow(o.Option, context);
            });
            context.HelpBuilder.WriteColumns(optionsToWrite.ToArray(), context);
            context.Output.WriteLine();
        }

        internal static void ShowCommandOptions(
            IEnumerable<TemplateCommand> templatesToShow,
            HelpContext context)
        {
            List<CliOption> optionsToShow = new()
            {
                SharedOptions.NameOption,
                SharedOptions.OutputOption,
                SharedOptions.DryRunOption,
                SharedOptions.ForceOption,
                SharedOptions.NoUpdateCheckOption,
                SharedOptions.ProjectPathOption
            };

            foreach (TemplateCommand template in templatesToShow)
            {
                if (template.LanguageOption != null)
                {
                    optionsToShow.Add(template.LanguageOption);
                    break;
                }
            }
            foreach (TemplateCommand template in templatesToShow)
            {
                if (template.TypeOption != null)
                {
                    optionsToShow.Add(template.TypeOption);
                    break;
                }
            }
            foreach (TemplateCommand template in templatesToShow)
            {
                if (template.AllowScriptsOption != null)
                {
                    optionsToShow.Add(template.AllowScriptsOption);
                    break;
                }
            }

            foreach (CliOption cliOption in optionsToShow)
            {
                cliOption.EnsureHelpName();
            }

            context.Output.WriteLine(HelpOptionsTitle());
            IEnumerable<TwoColumnHelpRow> optionsToWrite = optionsToShow.Select(o => context.HelpBuilder.GetTwoColumnRow(o, context));
            context.HelpBuilder.WriteColumns(optionsToWrite.ToArray(), context);
            context.Output.WriteLine();
        }

        internal static IEnumerable<TemplateCommand> GetMatchingTemplates(
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

        internal static void ShowUsage(CliCommand? command, IReadOnlyList<string> shortNames, HelpContext context)
        {
            List<string> usageParts = new();
            while (command is not null)
            {
                if (!string.IsNullOrWhiteSpace(command.Name))
                {
                    usageParts.Add(command.Name);
                }
                command = command.Parents.FirstOrDefault(c => c is CliCommand) as CliCommand;
            }

            usageParts.Reverse();
            context.Output.WriteLine(HelpUsageTitle());
            foreach (string shortName in shortNames)
            {
                IEnumerable<string> parts = usageParts.Concat(
                    new[]
                    {
                        shortName,
                        HelpUsageOptions(),
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
            HashSet<TemplateOption> optionsToShow = new();
            //templates are in priority order
            //in case parameters are different in different templates
            //highest priority ones wins
            //except the choice parameter, where we merge possible values
            foreach (TemplateCommand command in templates)
            {
                foreach (TemplateOption currentOption in command.TemplateOptions.Values)
                {
                    if (currentOption.TemplateParameter.IsHidden && !currentOption.TemplateParameter.AlwaysShow)
                    {
                        continue;
                    }

                    if (optionsToShow.TryGetValue(currentOption, out TemplateOption? existingOption))
                    {
                        if (currentOption.TemplateParameter is ChoiceTemplateParameter currentChoiceParam
                            && existingOption.TemplateParameter is ChoiceTemplateParameter)
                        {
                            existingOption.MergeChoices(currentChoiceParam);
                        }
                    }
                    else
                    {
                        optionsToShow.Add(currentOption);
                    }
                }
            }

            foreach (TemplateOption option in optionsToShow)
            {
                context.HelpBuilder.CustomizeSymbol(
                    option.Option,
                    firstColumnText: option.TemplateParameter.GetCustomFirstColumnText(option),
                    secondColumnText: option.TemplateParameter.GetCustomSecondColumnText());
            }
            return optionsToShow;
        }

        private static void WriteCustomInstantiateHelp(HelpContext context, CliCommand command)
        {
            //unhide arguments of NewCommand. They are hidden not to appear in subcommands help.
            foreach (CliArgument arg in command.Arguments)
            {
                arg.Hidden = false;
            }

            HelpBuilder.Default.SynopsisSection()(context);
            context.Output.WriteLine();
            CustomUsageSection(context, command);
            context.Output.WriteLine();
            HelpBuilder.Default.CommandArgumentsSection()(context);
            context.Output.WriteLine();
            HelpBuilder.Default.OptionsSection()(context);
            HelpBuilder.Default.SubcommandsSection()(context);
            context.Output.WriteLine();
        }

        private static void CustomUsageSection(HelpContext context, CliCommand command)
        {
            context.Output.WriteLine(HelpUsageTitle());
            context.Output.WriteLine(Indent + string.Join(" ", GetCustomUsageParts(context, command, showSubcommands: false)));

            if (command is NewCommand)
            {
                context.Output.WriteLine(Indent + string.Join(" ", GetCustomUsageParts(context, command, showArguments: false)));
            }
        }

        private static IEnumerable<string> GetCustomUsageParts(
            HelpContext context,
            CliCommand command,
            bool showSubcommands = true,
            bool showArguments = true,
            bool showOptions = true)
        {
            List<CliCommand> parentCommands = new();
            CliCommand? nextCommand = command;
            while (nextCommand is not null)
            {
                parentCommands.Add(nextCommand);
                nextCommand = nextCommand.Parents.FirstOrDefault(c => c is CliCommand) as CliCommand;
            }
            parentCommands.Reverse();

            foreach (CliCommand parentCommand in parentCommands)
            {
                yield return parentCommand.Name;
            }
            if (showArguments)
            {
                yield return CommandLineUtils.FormatArgumentUsage(command.Arguments.ToArray());
            }

            if (showSubcommands)
            {
                yield return HelpUsageCommand();
            }

            if (showOptions)
            {
                yield return HelpUsageOptions();
            }
        }

        private static string HelpUsageOptions() => _resourceManager.Value.GetString("HelpUsageOptions")!;

        private static string HelpUsageCommand() => _resourceManager.Value.GetString("HelpUsageCommand")!;

        private static string HelpUsageTitle() => _resourceManager.Value.GetString("HelpUsageTitle")!;

        private static string HelpOptionsTitle() => _resourceManager.Value.GetString("HelpOptionsTitle")!;
    }
}
