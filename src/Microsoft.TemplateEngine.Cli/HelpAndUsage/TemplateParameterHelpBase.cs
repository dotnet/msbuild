// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    internal static class TemplateParameterHelpBase
    {
        // Note: This method explicitly filters out "type" and "language", in addition to other filtering.
        internal static IEnumerable<ITemplateParameter> FilterParamsForHelp(IEnumerable<ITemplateParameter> parameterDefinitions, HashSet<string> hiddenParams, bool showImplicitlyHiddenParams = false, bool hasPostActionScriptRunner = false, HashSet<string> parametersToAlwaysShow = null)
        {
            IList<ITemplateParameter> filteredParams = parameterDefinitions
                .Where(x => x.Priority != TemplateParameterPriority.Implicit
                        && !hiddenParams.Contains(x.Name)
                        && string.Equals(x.Type, "parameter", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.Name, "type", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.Name, "language", StringComparison.OrdinalIgnoreCase)
                        && (showImplicitlyHiddenParams || x.DataType != "choice" || x.Choices.Count > 1 || (parametersToAlwaysShow?.Contains(x.Name) ?? false))).ToList();    // for filtering "tags"

            if (hasPostActionScriptRunner)
            {
                ITemplateParameter allowScriptsParam = new TemplateParameter(
                    description: LocalizableStrings.WhetherToAllowScriptsToRun,
                    name: "allow-scripts",
                    type: "parameter",
                    datatype: "choice",
                    defaultValue: "prompt",
                    choices: new Dictionary<string, ParameterChoice>()
                    {
                        { "yes", new ParameterChoice(string.Empty, LocalizableStrings.AllowScriptsYesChoice) },
                        { "no", new ParameterChoice(string.Empty, LocalizableStrings.AllowScriptsNoChoice) },
                        { "prompt", new ParameterChoice(string.Empty, LocalizableStrings.AllowScriptsPromptChoice) }
                    });
                filteredParams.Add(allowScriptsParam);
            }

            return filteredParams;
        }
    }
}
