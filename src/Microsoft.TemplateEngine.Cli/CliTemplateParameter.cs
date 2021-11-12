// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    internal enum ParameterType
    {
        Boolean,
        Choice,
        Float,
        Integer,
        Hex,
        String
    }

    internal class CliTemplateParameter
    {
        private List<string> _shortNameOverrides = new List<string>();

        private List<string> _longNameOverrides = new List<string>();

        private Dictionary<string, ParameterChoice> _choices = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);

        internal CliTemplateParameter(ITemplateParameter parameter, HostSpecificTemplateData data)
        {
            Name = parameter.Name;
            Description = parameter.Description ?? string.Empty;
            Type = ParseType(parameter.DataType);
            DefaultValue = parameter.DefaultValue;
            DefaultIfOptionWithoutValue = parameter.DefaultIfOptionWithoutValue;
            IsRequired = parameter.Priority == TemplateParameterPriority.Required && parameter.DefaultValue == null;
            IsHidden = parameter.Priority == TemplateParameterPriority.Implicit || data.HiddenParameterNames.Contains(parameter.Name);

            if (parameter.Choices != null)
            {
                _choices = parameter.Choices.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            }
            if (data.ShortNameOverrides.ContainsKey(parameter.Name))
            {
                _shortNameOverrides.Add(data.ShortNameOverrides[parameter.Name]);
            }
            if (data.LongNameOverrides.ContainsKey(parameter.Name))
            {
                _longNameOverrides.Add(data.LongNameOverrides[parameter.Name]);
            }
        }

        /// <summary>
        /// Unit test constructor.
        /// </summary>
        internal CliTemplateParameter(
            string name,
            ParameterType type = ParameterType.String,
            IEnumerable<string>? shortNameOverrides = null,
            IEnumerable<string>? longNameOverrides = null,
            int precedence = 0)
        {
            Name = name;
            Type = type;
            _shortNameOverrides = shortNameOverrides?.ToList() ?? new List<string>();
            _longNameOverrides = longNameOverrides?.ToList() ?? new List<string>();

            Description = string.Empty;
            DefaultValue = string.Empty;
            DefaultIfOptionWithoutValue = string.Empty;
        }

        internal string Name { get; private set; }

        internal string Description { get; private set; }

        internal ParameterType Type { get; private set; }

        internal string? DefaultValue { get; private set; }

        internal bool IsRequired { get; private set; }

        internal bool IsHidden { get; private set; }

        internal IReadOnlyDictionary<string, ParameterChoice>? Choices => _choices;

        internal IReadOnlyList<string> ShortNameOverrides => _shortNameOverrides;

        internal IReadOnlyList<string> LongNameOverrides => _longNameOverrides;

        internal string? DefaultIfOptionWithoutValue { get; private set; }

        private static ParameterType ParseType(string dataType)
        {
            return dataType switch
            {
                "bool" => ParameterType.Boolean,
                "boolean" => ParameterType.Boolean,
                "choice" => ParameterType.Choice,
                "float" => ParameterType.Float,
                "int" => ParameterType.Integer,
                "integer" => ParameterType.Integer,
                "hex" => ParameterType.Hex,
                _ => ParameterType.String
            };
        }
    }
}
