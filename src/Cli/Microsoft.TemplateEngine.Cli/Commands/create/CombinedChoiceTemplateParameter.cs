// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// This class implements combining choice parameters from different template
    /// This may happen for help in template group with different choices in templates.
    /// Used for displaying help.
    /// </summary>
    internal class CombinedChoiceTemplateParameter : ChoiceTemplateParameter
    {
        private Dictionary<string, ParameterChoice> _combinedParameters = new(StringComparer.OrdinalIgnoreCase);

        internal CombinedChoiceTemplateParameter(ChoiceTemplateParameter parameter) : base(parameter)
        {
            foreach (var choice in parameter.Choices)
            {
                if (!_combinedParameters.ContainsKey(choice.Key))
                {
                    _combinedParameters[choice.Key] = choice.Value;
                }
            }
        }

        internal override IReadOnlyDictionary<string, ParameterChoice> Choices => _combinedParameters;

        internal void MergeChoices(ChoiceTemplateParameter parameter)
        {
            if (parameter.Type != ParameterType.Choice)
            {
                throw new ArgumentException($"{nameof(parameter)} should have {nameof(parameter.Type)} {nameof(ParameterType.Choice)}");
            }
            if (parameter.Choices == null)
            {
                throw new ArgumentException($"{nameof(parameter)} should have {nameof(parameter.Choices)}");
            }

            foreach (var choice in parameter.Choices)
            {
                if (!_combinedParameters.ContainsKey(choice.Key))
                {
                    _combinedParameters[choice.Key] = choice.Value;
                }
            }
        }
    }
}
