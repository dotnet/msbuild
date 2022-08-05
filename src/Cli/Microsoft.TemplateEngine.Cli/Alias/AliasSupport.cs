// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//

//using System.Text.RegularExpressions;
//using Microsoft.TemplateEngine.Abstractions;
//using Microsoft.TemplateEngine.Cli.TabularOutput;

//namespace Microsoft.TemplateEngine.Cli.Alias
//{
//    internal static class AliasSupport
//    {
//        // Matches on any non-word character (letter, number, or underscore)
//        // Almost the same as \W, except \W has some quirks with unicode characters, and we allow '.'
//        private static readonly Regex InvalidAliasRegex = new Regex("[^a-z0-9_.]", RegexOptions.IgnoreCase);

//        // The first token must be a valid template short name. This naively tests for it by checking the first character.
//        // TODO: make this test more robust.
//        private static readonly Regex ValidFirstTokenRegex = new Regex("^[a-z0-9]", RegexOptions.IgnoreCase);

//        internal static (NewCommandStatus, INewCommandInput?) CoordinateAliasExpansion(
//            INewCommandInput commandInput,
//            AliasRegistry aliasRegistry)
//        {
//            (AliasExpansionStatus aliasExpansionStatus, INewCommandInput? expandedCommandInput) = AliasSupport.TryExpandAliases(commandInput, aliasRegistry);
//            if (aliasExpansionStatus == AliasExpansionStatus.ExpansionError)
//            {
//                Reporter.Output.WriteLine(LocalizableStrings.AliasExpansionError);
//                return (NewCommandStatus.InvalidParamValues, null);
//            }
//            else if (aliasExpansionStatus == AliasExpansionStatus.Expanded && expandedCommandInput != null)
//            {
//                Reporter.Output.WriteLine(string.Format(LocalizableStrings.AliasCommandAfterExpansion, string.Join(" ", expandedCommandInput.Tokens)));

//                if (!expandedCommandInput.ValidateParseError())
//                {
//                    Reporter.Error.WriteLine(LocalizableStrings.AliasExpandedCommandParseError);
//                    return (NewCommandStatus.InvalidParamValues, null);
//                }
//            }

//            // this is both for success and for no action.
//            return (NewCommandStatus.Success, expandedCommandInput);
//        }

//        internal static (AliasExpansionStatus, INewCommandInput?) TryExpandAliases(INewCommandInput commandInput, AliasRegistry aliasRegistry)
//        {
//            List<string> inputTokens = commandInput.Tokens.ToList();
//            inputTokens.RemoveAt(0);    // remove the command name

//            if (aliasRegistry.TryExpandCommandAliases(inputTokens, out IReadOnlyList<string> expandedTokens))
//            {
//                // TryExpandCommandAliases() return value indicates error (cycle) or not error. It doesn't indicate whether or not expansions actually occurred.
//                if (!expandedTokens.SequenceEqual(inputTokens))
//                {
//                    INewCommandInput expandedCommandInput = BaseCommandInput.Parse(expandedTokens.ToArray(), commandInput.CommandName);
//                    return (AliasExpansionStatus.Expanded, expandedCommandInput);
//                }

//                return (AliasExpansionStatus.NoChange, commandInput);
//            }

//            return (AliasExpansionStatus.ExpansionError, null);
//        }

//        internal static NewCommandStatus ManipulateAliasIfValid(AliasRegistry aliasRegistry, string aliasName, List<string> inputTokens, HashSet<string> reservedAliasNames)
//        {
//            if (reservedAliasNames.Contains(aliasName))
//            {
//                Reporter.Output.WriteLine(string.Format(LocalizableStrings.AliasCannotBeShortName, aliasName));
//                return NewCommandStatus.CreateFailed;
//            }
//            else if (InvalidAliasRegex.IsMatch(aliasName))
//            {
//                Reporter.Output.WriteLine(LocalizableStrings.AliasNameContainsInvalidCharacters);
//                return NewCommandStatus.InvalidParamValues;
//            }

//            inputTokens.RemoveAt(0);    // remove the command name
//            IReadOnlyList<string> aliasTokens = FilterForAliasTokens(inputTokens); // remove '-a' or '--alias', and the alias name

//            // The first token refers to a template name, or another alias.
//            if (aliasTokens.Count > 0 && !ValidFirstTokenRegex.IsMatch(aliasTokens[0]))
//            {
//                Reporter.Output.WriteLine(LocalizableStrings.AliasValueFirstArgError);
//                return NewCommandStatus.InvalidParamValues;
//            }

//            // create, update, or delete an alias.
//            return ManipulateAliasValue(aliasName, aliasTokens, aliasRegistry);
//        }

//        internal static NewCommandStatus DisplayAliasValues(IEngineEnvironmentSettings environment, INewCommandInput commandInput, AliasRegistry aliasRegistry, string commandName)
//        {
//            IReadOnlyDictionary<string, IReadOnlyList<string>> aliasesToShow;

//            if (!string.IsNullOrEmpty(commandInput.ShowAliasesAliasName))
//            {
//                if (aliasRegistry.AllAliases.TryGetValue(commandInput.ShowAliasesAliasName, out IReadOnlyList<string>? aliasValue))
//                {
//                    aliasesToShow = new Dictionary<string, IReadOnlyList<string>>()
//                    {
//                        { commandInput.ShowAliasesAliasName, aliasValue }
//                    };
//                }
//                else
//                {
//                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.AliasShowErrorUnknownAlias, commandInput.ShowAliasesAliasName, commandName));
//                    return NewCommandStatus.InvalidParamValues;
//                }
//            }
//            else
//            {
//                aliasesToShow = aliasRegistry.AllAliases;
//                Reporter.Output.WriteLine(LocalizableStrings.AliasShowAllAliasesHeader);
//            }

//            TabularOutput<KeyValuePair<string, IReadOnlyList<string>>> formatter =
//                new TabularOutput<KeyValuePair<string, IReadOnlyList<string>>>(
//                    new TabularOutputSettings(environment.Environment),
//                    aliasesToShow)
//                .DefineColumn(t => t.Key, LocalizableStrings.AliasName, showAlways: true)
//                .DefineColumn(t => string.Join(" ", t.Value), LocalizableStrings.AliasValue, showAlways: true);

//            Reporter.Output.WriteLine(formatter.Layout());
//            return NewCommandStatus.Success;
//        }

//        private static NewCommandStatus ManipulateAliasValue(string aliasName, IReadOnlyList<string> aliasTokens, AliasRegistry aliasRegistry)
//        {
//            AliasManipulationResult result = aliasRegistry.TryCreateOrRemoveAlias(aliasName, aliasTokens);

//            switch (result.Status)
//            {
//                case AliasManipulationStatus.Created:
//                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.AliasCreated, result.AliasName, string.Join(" ", result.AliasTokens)));
//                    return NewCommandStatus.Success;
//                case AliasManipulationStatus.Removed:
//                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.AliasRemoved, result.AliasName, string.Join(" ", result.AliasTokens)));
//                    return NewCommandStatus.Success;
//                case AliasManipulationStatus.RemoveNonExistentFailed:
//                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.AliasRemoveNonExistentFailed, result.AliasName));
//                    return NewCommandStatus.AliasFailed;
//                case AliasManipulationStatus.Updated:
//                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.AliasUpdated, result.AliasName, string.Join(" ", result.AliasTokens)));
//                    return NewCommandStatus.Success;
//                case AliasManipulationStatus.WouldCreateCycle:
//                    Reporter.Output.WriteLine(LocalizableStrings.AliasCycleError);
//                    return NewCommandStatus.AliasFailed;
//                case AliasManipulationStatus.InvalidInput:
//                    Reporter.Output.WriteLine(LocalizableStrings.AliasNotCreatedInvalidInput);
//                    return NewCommandStatus.InvalidParamValues;
//                default:
//                    return NewCommandStatus.OperationNotSpecified;
//            }
//        }

//        private static IReadOnlyList<string> FilterForAliasTokens(IReadOnlyList<string> inputTokens)
//        {
//            List<string> aliasTokens = new List<string>();
//            bool nextIsAliasName = false;
//            bool skipNextToken = false;
//            string? aliasName = null;

//            foreach (string token in inputTokens)
//            {
//                if (nextIsAliasName)
//                {
//                    aliasName = token;
//                    nextIsAliasName = false;
//                }
//                else if (skipNextToken)
//                {
//                    skipNextToken = false;
//                    continue;
//                }
//                else if (string.Equals(token, "-a", StringComparison.Ordinal) || string.Equals(token, "--alias", StringComparison.Ordinal))
//                {
//                    if (!string.IsNullOrEmpty(aliasName))
//                    {
//                        // found multiple alias names, which is invalid.
//                        aliasTokens.Clear();
//                        aliasName = null;
//                        return aliasTokens;
//                    }

//                    nextIsAliasName = true;
//                }
//                else if (token.Equals("--debug:custom-hive", StringComparison.Ordinal))
//                {
//                    skipNextToken = true;
//                }
//                else if (!token.StartsWith("--debug:", StringComparison.Ordinal))
//                {
//                    aliasTokens.Add(token);
//                }
//            }

//            return aliasTokens;
//        }
//    }
//}
