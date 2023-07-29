// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Command = System.CommandLine.CliCommand;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand : BaseCommand<InstantiateCommandArgs>
    {
        internal static List<InvalidTemplateOptionResult> GetInvalidOptions(IEnumerable<TemplateResult> templates)
        {
            //we need to process errors only for templates that match on language, type or baseline
            IEnumerable<TemplateResult> templatesToAnalyze = templates.Where(template => template.IsTemplateMatch);

            List<InvalidTemplateOptionResult> invalidOptionsList = new();

            //collect the options with invalid names (unmatched tokens)
            IEnumerable<InvalidTemplateOptionResult> unmatchedOptions = templatesToAnalyze.SelectMany(
                template => template.InvalidTemplateOptions
                    .Where(i => i.ErrorKind == InvalidTemplateOptionResult.Kind.InvalidName)).Distinct();

            foreach (InvalidTemplateOptionResult option in unmatchedOptions)
            {
                if (templatesToAnalyze.All(
                    template =>
                        template.InvalidTemplateOptions.Any(x => x.Equals(option))))
                {
                    invalidOptionsList.Add(option);
                }
            }

            //collect the options with invalid values (includes default and default if no option value failures)
            IEnumerable<InvalidTemplateOptionResult> optionsWithInvalidValues = templatesToAnalyze.SelectMany(
                template => template.InvalidTemplateOptions
                        .Where(i => i.ErrorKind == InvalidTemplateOptionResult.Kind.InvalidValue)).Distinct();

            foreach (InvalidTemplateOptionResult option in optionsWithInvalidValues)
            {
                if (templatesToAnalyze.All(
                    template =>
                        template.InvalidTemplateOptions.Any(x => x.Equals(option))
                        //skip templates where option is not available
                        || template.InvalidTemplateOptions.Any(x => x.ErrorKind == InvalidTemplateOptionResult.Kind.InvalidName && x.InputFormat == option.InputFormat)))
                {
                    if (option.IsChoice)
                    {
                        option.CorrectErrorMessageForChoice(templatesToAnalyze);
                    }
                    invalidOptionsList.Add(option);
                }
            }

            return invalidOptionsList;
        }

        internal static List<TemplateResult> CollectTemplateMatchInfo(InstantiateCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, TemplateGroup templateGroup)
        {
            List<TemplateResult> matchInfos = new();
            foreach (CliTemplateInfo template in templateGroup.Templates)
            {
                if (ReparseForTemplate(args, environmentSettings, templatePackageManager, templateGroup, template)
                    is (TemplateCommand command, ParseResult parseResult))
                {
                    matchInfos.Add(TemplateResult.FromParseResult(command, parseResult));
                }
            }
            return matchInfos;
        }

        /// <summary>
        /// Provides the error string to use for the invalid parameters collection.
        /// </summary>
        /// <param name="invalidParameterList">the invalid parameters collection to prepare output for.</param>
        /// <param name="templates">the templates to use to get more information about parameters. Optional - if not provided the possible value for the parameters won't be included to the output.</param>
        /// <returns>the error string for the output.</returns>
        private static string InvalidOptionsListToString(IEnumerable<InvalidTemplateOptionResult> invalidParameterList, IEnumerable<TemplateResult>? templates = null)
        {
            if (!invalidParameterList.Any())
            {
                return string.Empty;
            }
            if (templates != null)
            {
                //we need to check only the templates matching on base criterias
                templates = templates.Where(template => template.IsTemplateMatch);
            }

            StringBuilder invalidParamsErrorText = new(LocalizableStrings.InvalidCommandOptions);
            foreach (InvalidTemplateOptionResult invalidParam in invalidParameterList)
            {
                invalidParamsErrorText.AppendLine();
                if (invalidParam.ErrorKind == InvalidTemplateOptionResult.Kind.InvalidName)
                {
                    invalidParamsErrorText.AppendLine(invalidParam.InputFormat);
                    invalidParamsErrorText.Indent(1).AppendFormat(LocalizableStrings.InvalidParameterNameDetail, invalidParam.InputFormat);
                }
                else if (invalidParam.ErrorKind == InvalidTemplateOptionResult.Kind.InvalidValue)
                {
                    invalidParamsErrorText.AppendLine(invalidParam.InputFormat + ' ' + invalidParam.SpecifiedValue);
                    if (string.IsNullOrWhiteSpace(invalidParam.ErrorMessage))
                    {
                        invalidParamsErrorText.Indent(1).AppendFormat(LocalizableStrings.InvalidParameterDetail, invalidParam.InputFormat, invalidParam.SpecifiedValue);
                    }
                    else
                    {
                        invalidParamsErrorText.Append(invalidParam.ErrorMessage?.IndentLines(1));
                    }
                }
                else
                {
                    invalidParamsErrorText.AppendLine(invalidParam.InputFormat + ' ' + invalidParam.SpecifiedValue);
                    invalidParamsErrorText.Indent(1).AppendFormat(LocalizableStrings.InvalidParameterDefault, invalidParam.InputFormat, invalidParam.SpecifiedValue);
                }
            }
            return invalidParamsErrorText.ToString();
        }

        private static NewCommandStatus HandleNoTemplateFoundResult(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            IReporter reporter)
        {
            List<TemplateResult> matchInfos = CollectTemplateMatchInfo(args, environmentSettings, templatePackageManager, templateGroup);
            //process language, type and baseline errors
            if (!matchInfos.Any(mi => mi.IsTemplateMatch))
            {
                HandleNoMatchOnTemplateBaseOptions(matchInfos, args, templateGroup);
                return NewCommandStatus.NotFound;
            }

            List<InvalidTemplateOptionResult> invalidOptionsList = GetInvalidOptions(matchInfos);
            if (invalidOptionsList.Any())
            {
                reporter.WriteLine(InvalidOptionsListToString(invalidOptionsList, matchInfos).Bold().Red());
            }
            else
            {
                IEnumerable<string> tokens = args.ParseResult.Tokens.Select(t => $"'{t.Value}'");
                reporter.WriteLine(string.Format(LocalizableStrings.Generic_Info_NoMatchingTemplates, string.Join(" ", tokens)).Bold().Red());
            }
            reporter.WriteLine();
            //TODO: if we were not able to match the errors, print all the errors template by template.

            if (templateGroup.ShortNames.Any())
            {
                reporter.WriteLine(LocalizableStrings.InvalidParameterTemplateHint);
                var example = Example
                    .For<NewCommand>(args.ParseResult)
                    .WithArgument(NewCommand.ShortNameArgument, templateGroup.ShortNames[0]);
                var language = matchInfos.Where(mi => mi.Language != null).FirstOrDefault()?.Language;
                if (language != null)
                {
                    example.WithOption(language.Option, language.GetValueOrDefault<string>()!);
                }
                example.WithHelpOption();
                reporter.WriteCommand(example);
            }

            return invalidOptionsList.Any() ? NewCommandStatus.InvalidOption : NewCommandStatus.NotFound;
        }

        private static void HandleNoMatchOnTemplateBaseOptions(IEnumerable<TemplateResult> matchInfos, InstantiateCommandArgs args, TemplateGroup templateGroup)
        {
            CliOption<string> languageOption = SharedOptionsFactory.CreateLanguageOption();
            CliOption<string> typeOption = SharedOptionsFactory.CreateTypeOption();
            CliOption<string> baselineOption = SharedOptionsFactory.CreateBaselineOption();

            Command reparseCommand = new("reparse-only")
            {
                languageOption,
                typeOption,
                baselineOption,
                new CliArgument<string[]>("rem-args")
                {
                    Arity = new ArgumentArity(0, 999)
                }
            };

            ParseResult result = ParserFactory.CreateParser(reparseCommand).Parse(args.RemainingArguments ?? Array.Empty<string>());
            string baseInputParameters = $"'{args.ShortName}'";
            foreach (CliOption<string> option in new[] { languageOption, typeOption, baselineOption })
            {
                if (result.GetResult(option) is { } optionResult && optionResult.IdentifierToken is { } token)
                {
                    baseInputParameters += $", {token.Value}='{optionResult.GetValueOrDefault<string>()}'";
                }
            }

            Reporter.Error.WriteLine(string.Format(LocalizableStrings.Generic_Info_NoMatchingTemplates, baseInputParameters).Bold().Red());
            foreach (var option in new[]
                {
                    new { Option = languageOption, Condition = matchInfos.All(mi => !mi.IsLanguageMatch), AllowedValues = templateGroup.Languages },
                    new { Option = typeOption, Condition = matchInfos.All(mi => !mi.IsTypeMatch), AllowedValues = templateGroup.Types },
                    new { Option = baselineOption, Condition = matchInfos.All(mi => !mi.IsBaselineMatch), AllowedValues = (IReadOnlyList<string?>)templateGroup.Baselines },
                })
            {
                if (option.Condition && result.GetResult(option.Option) is { } optionResult && optionResult.IdentifierToken is { } token)
                {
                    string allowedValues = string.Join(", ", option.AllowedValues.Select(l => $"'{l}'").OrderBy(l => l, StringComparer.OrdinalIgnoreCase));
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplateOptions_Error_AllowedValuesForOptionList, token.Value, allowedValues));
                }
            }

            Reporter.Error.WriteLine();
        }
    }
}
