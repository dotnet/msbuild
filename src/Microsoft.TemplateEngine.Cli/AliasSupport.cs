using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public static class AliasSupport
    {
        public static AliasExpansionStatus TryExpandAliases(INewCommandInput commandInput, AliasRegistry aliasRegistry)
        {
            List<string> inputTokens = commandInput.Tokens.ToList();
            inputTokens.RemoveAt(0);    // remove the command name

            if (aliasRegistry.TryExpandCommandAliases(inputTokens, out IReadOnlyList<string> expandedTokens))
            {
                if (!expandedTokens.SequenceEqual(inputTokens))
                {
                    commandInput.ResetArgs(expandedTokens.ToArray());
                    return AliasExpansionStatus.Expanded;
                }

                return AliasExpansionStatus.NoChange;
            }

            return AliasExpansionStatus.ExpansionError;
        }

        public static CreationResultStatus ManipulateAliasValue(INewCommandInput commandInput, AliasRegistry aliasRegistry)
        {
            List<string> inputTokens = commandInput.Tokens.ToList();
            inputTokens.RemoveAt(0);    // remove the command name
            AliasManipulationResult result = aliasRegistry.TryCreateOrRemoveAlias(inputTokens);
            CreationResultStatus returnStatus = CreationResultStatus.OperationNotSpecified;

            switch (result.Status)
            {
                case AliasManipulationStatus.Created:
                    Reporter.Output.WriteLine(string.Format("Successfully created alias named '{0}' with value '{1}'", result.AliasName, result.AliasValue));
                    returnStatus = CreationResultStatus.Success;
                    break;
                case AliasManipulationStatus.Removed:
                    Reporter.Output.WriteLine(string.Format("Successfully removed alias named '{0}' whose value was '{1}'", result.AliasName, result.AliasValue));
                    returnStatus = CreationResultStatus.Success;
                    break;
                case AliasManipulationStatus.Updated:
                    Reporter.Output.WriteLine(string.Format("Successfully updated alias named '{0}' to value '{1}'", result.AliasName, result.AliasValue));
                    returnStatus = CreationResultStatus.Success;
                    break;
                case AliasManipulationStatus.WouldCreateCycle:
                    Reporter.Output.WriteLine(string.Format("Alias not created. It would have created an alias cycle, resulting in infinite expansion."));
                    returnStatus = CreationResultStatus.CreateFailed;
                    break;
                case AliasManipulationStatus.InvalidInput:
                    Reporter.Output.WriteLine(string.Format("Alias not created. The input was invalid"));
                    returnStatus = CreationResultStatus.InvalidParamValues;
                    break;
            }

            return returnStatus;
        }

        public static CreationResultStatus DisplayAliasValues(IEngineEnvironmentSettings environment, INewCommandInput commandInput, AliasRegistry aliasRegistry)
        {
            IReadOnlyDictionary<string, string> aliasesToShow;

            if (!string.IsNullOrEmpty(commandInput.ShowAliasesAliasName))
            {
                if (aliasRegistry.AllAliases.TryGetValue(commandInput.ShowAliasesAliasName, out string aliasValue))
                {
                    aliasesToShow = new Dictionary<string, string>()
                    {
                        { commandInput.ShowAliasesAliasName, aliasValue }
                    };
                }
                else
                {
                    Reporter.Output.WriteLine(string.Format("Unknown alias name '{0}'\nRun 'dotnet --show-aliases' with no args to show all aliases.", commandInput.ShowAliasesAliasName));
                    return CreationResultStatus.InvalidParamValues;
                }
            }
            else
            {
                aliasesToShow = aliasRegistry.AllAliases;
                Reporter.Output.WriteLine("All Aliases:");
            }

            HelpFormatter<KeyValuePair<string, string>> formatter = new HelpFormatter<KeyValuePair<string, string>>(environment, aliasesToShow, 2, '-', false)
                            .DefineColumn(t => t.Key, LocalizableStrings.AliasName)
                            .DefineColumn(t => t.Value, LocalizableStrings.AliasValue);
            Reporter.Output.WriteLine(formatter.Layout());
            return CreationResultStatus.Success;
        }
    }
}
