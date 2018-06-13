// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    public static class TemplateParameterHelpBase
    {
        // Note: This method explicitly filters out "type" and "language", in addition to other filtering.
        public static IEnumerable<ITemplateParameter> FilterParamsForHelp(IEnumerable<ITemplateParameter> parameterDefinitions, HashSet<string> hiddenParams, bool showImplicitlyHiddenParams = false, bool hasPostActionScriptRunner = false, HashSet<string> parametersToAlwaysShow = null)
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
                ITemplateParameter allowScriptsParam = new TemplateParameter()
                {
                    Documentation = LocalizableStrings.WhetherToAllowScriptsToRun,
                    Name = "allow-scripts",
                    DataType = "choice",
                    DefaultValue = "prompt",
                    Choices = new Dictionary<string, string>()
                    {
                        { "yes", LocalizableStrings.AllowScriptsYesChoice },
                        { "no", LocalizableStrings.AllowScriptsNoChoice },
                        { "prompt", LocalizableStrings.AllowScriptsPromptChoice }
                    }
                };

                if (allowScriptsParam is IAllowDefaultIfOptionWithoutValue allowScriptsParamWithNoValueDefault)
                {
                    allowScriptsParamWithNoValueDefault.DefaultIfOptionWithoutValue = null;
                    filteredParams.Add(allowScriptsParamWithNoValueDefault as TemplateParameter);
                }
                else
                {
                    filteredParams.Add(allowScriptsParam);
                }
            }

            return filteredParams;
        }
    }
}
