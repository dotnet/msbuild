using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    public static class CommandParserSupport
    {
        public static Command CreateNewCommandWithoutTemplateInfo(string commandName) => GetNewCommand(commandName, NewCommandVisibleArgs, NewCommandHiddenArgs, DebuggingCommandArgs);

        private static Command GetNewCommand(string commandName, params Option[][] args)
        {
            Option[] combinedArgs = ArrayExtensions.CombineArrays(args);

            return Create.Command(commandName,
                           LocalizableStrings.CommandDescription,
                           Accept.ZeroOrMoreArguments(),    // this can't be ZeroOrOneArguments() because template args would cause errors
                           combinedArgs);
        }

        // Final parser for when there is no template name provided.
        // Unmatched args are errors.
        public static Command CreateNewCommandForNoTemplateName(string commandName)
        {
            Option[] combinedArgs = ArrayExtensions.CombineArrays(NewCommandVisibleArgs, NewCommandHiddenArgs, DebuggingCommandArgs);

            return Create.Command(commandName,
                           LocalizableStrings.CommandDescription,
                           Accept.NoArguments(),
                           true,
                           combinedArgs);
        }

        private static Command GetNewCommandForTemplate(string commandName, string templateName, params Option[][] args)
        {
            Option[] combinedArgs = ArrayExtensions.CombineArrays(args);

            return Create.Command(commandName,
                           LocalizableStrings.CommandDescription,
                           Accept.ExactlyOneArgument().WithSuggestionsFrom(templateName),
                           combinedArgs);
        }

        // Creates a command setup with the args for "new", plus args for the input template parameters.
        public static Command CreateNewCommandWithArgsForTemplate(string commandName, string templateName,
                    IReadOnlyList<ITemplateParameter> parameterDefinitions,
                    IDictionary<string, string> longNameOverrides,
                    IDictionary<string, string> shortNameOverrides,
                    out IReadOnlyDictionary<string, IReadOnlyList<string>> templateParamMap)
        {
            IList<Option> paramOptionList = new List<Option>();
            Option[] allBuiltInArgs = ArrayExtensions.CombineArrays(NewCommandVisibleArgs, NewCommandHiddenArgs, NewCommandReservedArgs, DebuggingCommandArgs);

            HashSet<string> initiallyTakenAliases = VariantsForOptions(allBuiltInArgs);
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

                if (string.Equals(parameter.DataType, "choice", StringComparison.OrdinalIgnoreCase))
                {
                    option = Create.Option(string.Join("|", aliasesForParam), parameter.Documentation,
                                            Accept.ExactlyOneArgument()
                                                //.WithSuggestionsFrom(parameter.Choices.Keys.ToArray())
                                                .With(defaultValue: () => parameter.DefaultValue));
                }
                else if (string.Equals(parameter.DataType, "bool", StringComparison.OrdinalIgnoreCase))
                {
                    option = Create.Option(string.Join("|", aliasesForParam), parameter.Documentation,
                                        Accept.ZeroOrOneArgument()
                                            .WithSuggestionsFrom(new[] { "true", "false" }));

                }
                else
                {
                    option = Create.Option(string.Join("|", aliasesForParam), parameter.Documentation,
                                        Accept.ExactlyOneArgument());
                }

                paramOptionList.Add(option);    // add the option
                canonicalToVariantMap.Add(parameter.Name, aliasesForParam.ToList());   // map the template canonical name to its aliases.
            }

            templateParamMap = canonicalToVariantMap;
            return GetNewCommandForTemplate(commandName, templateName, NewCommandVisibleArgs, NewCommandHiddenArgs, DebuggingCommandArgs, paramOptionList.ToArray());
        }

        private static Option[] NewCommandVisibleArgs
        {
            get
            {
                return new[]
                {
                    Create.Option("-h|--help", LocalizableStrings.DisplaysHelp, Accept.NoArguments()),
                    Create.Option("-l|--list", LocalizableStrings.ListsTemplates, Accept.NoArguments()),
                    Create.Option("-n|--name", LocalizableStrings.NameOfOutput, Accept.ExactlyOneArgument()),
                    Create.Option("-o|--output", LocalizableStrings.OutputPath, Accept.ExactlyOneArgument()),
                    Create.Option("-i|--install", LocalizableStrings.InstallHelp, Accept.OneOrMoreArguments()),
                    Create.Option("-u|--uninstall", LocalizableStrings.UninstallHelp, Accept.OneOrMoreArguments()),
                    Create.Option("--type", LocalizableStrings.ShowsFilteredTemplates, Accept.ExactlyOneArgument()),
                    Create.Option("--force", LocalizableStrings.ForcesTemplateCreation, Accept.NoArguments()),
                    Create.Option("-lang|--language", LocalizableStrings.LanguageParameter,
                                    // TODO: dynamically get the language set for all installed templates.
                                    Accept.ExactlyOneArgument()
                                        .WithSuggestionsFrom("C#", "F#")
                                        .With(defaultValue: () => "C#")),
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
                    Create.Option("--locale", string.Empty, Accept.ExactlyOneArgument()),
                    Create.Option("--quiet", string.Empty, Accept.NoArguments()),
                    Create.Option("-all|--show-all", string.Empty, Accept.NoArguments()),
                    Create.Option("--allow-scripts", string.Empty, Accept.ZeroOrOneArgument()),
                    Create.Option("--baseline", string.Empty, Accept.ExactlyOneArgument()),
                };
            }
        }

        private static Option[] NewCommandReservedArgs
        {
            get
            {
                return new[]
                {
                    Create.Option("-up|--update", string.Empty, Accept.ZeroOrOneArgument()),
                    Create.Option("--skip-update-check", string.Empty, Accept.NoArguments()),
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
                    Create.Option("--debug:reset-config", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:showconfig", string.Empty, Accept.NoArguments()),
                    Create.Option("--debug:emit-timings", string.Empty, Accept.NoArguments()),
                };
            }
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
