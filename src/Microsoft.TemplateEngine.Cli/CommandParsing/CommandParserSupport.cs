// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    internal static class CommandParserSupport
    {
        private static readonly ArgumentsRule ArgumentCannotStartWithDashRule = new ArgumentsRule(option =>
        {
            foreach (var argument in option.Arguments)
            {
                if (argument.StartsWith("-"))
                {
                    return "The argument value cannot start with '-'";
                }
            }
            return null;
        });

        private static HashSet<string> _argsForBuiltInCommands;

        internal static HashSet<string> ArgsForBuiltInCommands
        {
            get
            {
                if (_argsForBuiltInCommands == null)
                {
                    Option[] allBuiltInArgs = ArrayExtensions.CombineArrays(NewCommandVisibleArgs, NewCommandHiddenArgs, DebuggingCommandArgs);

                    _argsForBuiltInCommands = VariantsForOptions(allBuiltInArgs);
                }

                // return a copy so the original doesn't get modified.
                return new HashSet<string>(_argsForBuiltInCommands);
            }
        }

        private static Option[] NewCommandVisibleArgs
        {
            get
            {
                return new[]
                {
                    Create.Option("-h|--help", LocalizableStrings.DisplaysHelp, Accept.NoArguments()),
                    Create.Option(
                        "-l|--list",
                        LocalizableStrings.ListsTemplates,
                        Accept
                            .ZeroOrOneArgument()
                            .And(ArgumentCannotStartWithDashRule)
                            .With(LocalizableStrings.ListsTemplates, "PARTIAL_NAME")),
                    Create.Option("-n|--name", LocalizableStrings.NameOfOutput, Accept.ExactlyOneArgument()),
                    Create.Option("-o|--output", LocalizableStrings.OutputPath, Accept.ExactlyOneArgument()),
                    Create.Option("-i|--install", LocalizableStrings.InstallHelp, Accept.OneOrMoreArguments()),
                    Create.Option("-u|--uninstall", LocalizableStrings.UninstallHelp, Accept.ZeroOrMoreArguments()),
                    Create.Option("--interactive", LocalizableStrings.OptionDescriptionInteractive, Accept.NoArguments()),
                    Create.Option("--nuget-source|--add-source", LocalizableStrings.OptionDescriptionNuGetSource, Accept.OneOrMoreArguments()),
                    Create.Option("--type", LocalizableStrings.OptionDescriptionTypeFilter, Accept.ExactlyOneArgument()),
                    Create.Option("--dry-run", LocalizableStrings.DryRunDescription, Accept.NoArguments()),
                    Create.Option("--force", LocalizableStrings.ForcesTemplateCreation, Accept.NoArguments()),
                    Create.Option("-lang|--language", LocalizableStrings.OptionDescriptionLanguageFilter, Accept.ExactlyOneArgument()),
                    Create.Option("--update-check", LocalizableStrings.UpdateCheckCommandHelp, Accept.NoArguments()),
                    Create.Option("--update-apply", LocalizableStrings.UpdateApplyCommandHelp, Accept.NoArguments()),
                    Create.Option(
                        "--search",
                        LocalizableStrings.OptionDescriptionSearch,
                        Accept
                            .ZeroOrOneArgument()
                            .And(ArgumentCannotStartWithDashRule)
                            .With(LocalizableStrings.OptionDescriptionSearch, "PARTIAL_NAME")),
                    Create.Option("--author", LocalizableStrings.OptionDescriptionAuthorFilter, Accept.ExactlyOneArgument().With(LocalizableStrings.OptionDescriptionAuthorFilter, "AUTHOR")),
                    Create.Option("--package", LocalizableStrings.OptionDescriptionPackageFilter, Accept.ExactlyOneArgument().With(LocalizableStrings.OptionDescriptionPackageFilter, "PACKAGE")),
                    Create.Option("--columns", LocalizableStrings.OptionDescriptionColumns, Accept.ExactlyOneArgument().With(LocalizableStrings.OptionDescriptionColumns, "COLUMNS_LIST")),
                    Create.Option("--columns-all", LocalizableStrings.OptionDescriptionColumnsAll, Accept.NoArguments()),
                    Create.Option("--tag", LocalizableStrings.OptionDescriptionTagFilter, Accept.ExactlyOneArgument().With(LocalizableStrings.OptionDescriptionTagFilter, "TAG")),
                    Create.Option("--no-update-check", LocalizableStrings.OptionDescriptionNoUpdateCheck, Accept.NoArguments()),
                };
            }
        }

        private static Option[] NewCommandHiddenArgs
        {
            get
            {
                return new[]
                {
                    //Create.Option("-a|--alias", LocalizableStrings.AliasHelp, Accept.ExactlyOneArgument()),
                    //Create.Option("--show-alias", LocalizableStrings.ShowAliasesHelp, Accept.ZeroOrOneArgument()),
                    // When these are un-hidden, be sure to set their help values like above.
                    Create.Option("-a|--alias", string.Empty, Accept.ExactlyOneArgument()),
                    Create.Option("--show-alias", string.Empty, Accept.ZeroOrOneArgument()),
                    Create.Option("-x|--extra-args", string.Empty, Accept.OneOrMoreArguments()),
                    Create.Option("--quiet", string.Empty, Accept.NoArguments()),
                    Create.Option("-all|--show-all", string.Empty, Accept.NoArguments()),
                    Create.Option("--allow-scripts", string.Empty, Accept.ZeroOrOneArgument()),
                    Create.Option("--baseline", string.Empty, Accept.ExactlyOneArgument()),
                };
            }
        }

        private static Option[] DebuggingCommandArgs
        {
            get
            {
                return new[]
                {
                    Create.Option("--debug:attach", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:rebuildcache", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:ephemeral-hive", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:reinit", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:showconfig", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:emit-telemetry", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:custom-hive", string.Empty, Accept.ExactlyOneArgument()),
                    Create.Option("--debug:disable-sdk-templates", string.Empty, Accept.NoArguments()),
                };
            }
        }

        // Final parser for when there is no template name provided.
        // Unmatched args are errors.
        internal static Command CreateNewCommandForNoTemplateName(string commandName, bool treatUnmatchedTokensAsErrors = true)
        {
            Option[] combinedArgs = ArrayExtensions.CombineArrays(NewCommandVisibleArgs, NewCommandHiddenArgs, DebuggingCommandArgs);

            return Create.Command(
                commandName,
                LocalizableStrings.CommandDescription,
                Accept.NoArguments(),
                treatUnmatchedTokensAsErrors: treatUnmatchedTokensAsErrors,
                combinedArgs);
        }

        // Creates a command setup with the args for "new", plus args for the input template parameters.
        internal static Command CreateNewCommandWithArgsForTemplate(
            string commandName,
            string templateName,
            IReadOnlyList<ITemplateParameter> parameterDefinitions,
            IDictionary<string, string> longNameOverrides,
            IDictionary<string, string> shortNameOverrides,
            out IReadOnlyDictionary<string, IReadOnlyList<string>> templateParamMap)
        {
            IList<Option> paramOptionList = new List<Option>();
            HashSet<string> initiallyTakenAliases = ArgsForBuiltInCommands;

            Dictionary<string, IReadOnlyList<string>> canonicalToVariantMap = new Dictionary<string, IReadOnlyList<string>>();
            AliasAssignmentCoordinator assignmentCoordinator = new AliasAssignmentCoordinator(parameterDefinitions, longNameOverrides, shortNameOverrides, initiallyTakenAliases);

            if (assignmentCoordinator.InvalidParams.Count > 0)
            {
                string unusableDisplayList = string.Join(", ", assignmentCoordinator.InvalidParams);
                throw new Exception($"Template is malformed. The following parameter names are invalid: {unusableDisplayList}");
            }

            foreach (ITemplateParameter parameter in parameterDefinitions.Where(x => x.Priority != TemplateParameterPriority.Implicit))
            {
                Option option;
                IList<string> aliasesForParam = new List<string>();

                if (assignmentCoordinator.LongNameAssignments.TryGetValue(parameter.Name, out string longVersion))
                {
                    aliasesForParam.Add(longVersion);
                }

                if (assignmentCoordinator.ShortNameAssignments.TryGetValue(parameter.Name, out string shortVersion))
                {
                    aliasesForParam.Add(shortVersion);
                }

                if (!string.IsNullOrEmpty(parameter.DefaultIfOptionWithoutValue))
                {
                    // This switch can be provided with or without a value.
                    // If the user doesn't specify a value, it gets the value of DefaultIfOptionWithoutValue
                    option = Create.Option(string.Join("|", aliasesForParam), parameter.Description, Accept.ZeroOrOneArgument());
                }
                else
                {
                    // User must provide a value if this switch is specified.
                    option = Create.Option(string.Join("|", aliasesForParam), parameter.Description, Accept.ExactlyOneArgument());
                }

                paramOptionList.Add(option);    // add the option
                canonicalToVariantMap.Add(parameter.Name, aliasesForParam.ToList());   // map the template canonical name to its aliases.
            }

            templateParamMap = canonicalToVariantMap;
            return GetNewCommandForTemplate(commandName, templateName, NewCommandVisibleArgs, NewCommandHiddenArgs, DebuggingCommandArgs, paramOptionList.ToArray());
        }

        internal static Command CreateNewCommandWithoutTemplateInfo(string commandName)
        {
            Option[] combinedArgs = ArrayExtensions.CombineArrays(NewCommandVisibleArgs, NewCommandHiddenArgs, DebuggingCommandArgs);
            return Create.Command(
                commandName,
                LocalizableStrings.CommandDescription,
                Accept.ZeroOrOneArgument(),
                treatUnmatchedTokensAsErrors: false,
                combinedArgs);
        }

        private static Command GetNewCommandForTemplate(string commandName, string templateName, params Option[][] args)
        {
            Option[] combinedArgs = ArrayExtensions.CombineArrays(args);

            return Create.Command(
                commandName,
                LocalizableStrings.CommandDescription,
                Accept.ExactlyOneArgument().WithSuggestionsFrom(templateName),
                combinedArgs);
        }

        private static HashSet<string> VariantsForOptions(Option[] options)
        {
            HashSet<string> variants = new HashSet<string>();

            if (options == null)
            {
                return variants;
            }

            for (int i = 0; i < options.Length; i++)
            {
                variants.UnionWith(options[i].RawAliases);
            }

            return variants;
        }
    }
}
