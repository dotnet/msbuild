// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;

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

    /// <summary>
    /// The class combines information from<see cref="ITemplateParameter"/> and <see cref="HostSpecificTemplateData"/>.
    /// Choice parameters are implemented in separate class <see cref="ChoiceTemplateParameter"/>.
    /// </summary>
    internal class CliTemplateParameter
    {
        private readonly List<string> _shortNameOverrides = new();

        private readonly List<string> _longNameOverrides = new();

        private readonly TemplateParameterPrecedence _precedence;

        internal CliTemplateParameter(ITemplateParameter parameter, HostSpecificTemplateData data)
        {
            Name = parameter.Name;
            Description = parameter.Description ?? string.Empty;
            Type = ParseType(parameter.DataType);
            DefaultValue = parameter.DefaultValue;
            DataType = parameter.DataType;
            if (Type == ParameterType.Boolean && string.Equals(parameter.DefaultIfOptionWithoutValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                //ignore, parser is doing this behavior by default
            }
            else
            {
                DefaultIfOptionWithoutValue = parameter.DefaultIfOptionWithoutValue;
            }
            IsRequired = parameter.Precedence.PrecedenceDefinition == PrecedenceDefinition.Required && parameter.DefaultValue == null;
            IsHidden =
                parameter.Precedence.PrecedenceDefinition == PrecedenceDefinition.Implicit
                || parameter.Precedence.PrecedenceDefinition == PrecedenceDefinition.Disabled
                || data.HiddenParameterNames.Contains(parameter.Name);

            AlwaysShow = data.ParametersToAlwaysShow.Contains(parameter.Name);
            AllowMultipleValues = parameter.AllowMultipleValues;

            if (data.ShortNameOverrides.ContainsKey(parameter.Name))
            {
                _shortNameOverrides.Add(data.ShortNameOverrides[parameter.Name]);
            }
            if (data.LongNameOverrides.ContainsKey(parameter.Name))
            {
                _longNameOverrides.Add(data.LongNameOverrides[parameter.Name]);
            }
            _precedence = parameter.Precedence;
        }

        /// <summary>
        /// Unit test constructor.
        /// </summary>
        internal CliTemplateParameter(
            string name,
            ParameterType type = ParameterType.String,
            IEnumerable<string>? shortNameOverrides = null,
            IEnumerable<string>? longNameOverrides = null)
        {
            Name = name;
            Type = type;
            _shortNameOverrides = shortNameOverrides?.ToList() ?? new List<string>();
            _longNameOverrides = longNameOverrides?.ToList() ?? new List<string>();

            Description = string.Empty;
            DefaultValue = string.Empty;
            DefaultIfOptionWithoutValue = string.Empty;
            DataType = ParameterTypeToString(Type);
            _precedence = TemplateParameterPrecedence.Default;
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        internal CliTemplateParameter(CliTemplateParameter other)
        {
            Name = other.Name;
            Type = other.Type;
            Description = other.Description;
            DataType = other.DataType;
            DefaultValue = other.DefaultValue;
            IsRequired = other.IsRequired;
            IsHidden = other.IsHidden;
            AlwaysShow = other.AlwaysShow;
            _shortNameOverrides = other.ShortNameOverrides.ToList();
            _longNameOverrides = other.LongNameOverrides.ToList();
            DefaultIfOptionWithoutValue = other.DefaultIfOptionWithoutValue;
            AllowMultipleValues = other.AllowMultipleValues;
            _precedence = other._precedence;
        }

        internal string Name { get; private set; }

        internal string Description { get; private set; }

        internal virtual ParameterType Type { get; private set; }

        internal string DataType { get; private set; }

        internal string? DefaultValue { get; private set; }

        internal bool IsRequired { get; private set; }

        internal bool IsHidden { get; private set; }

        internal bool AlwaysShow { get; private set; }

        internal IReadOnlyList<string> ShortNameOverrides => _shortNameOverrides;

        internal IReadOnlyList<string> LongNameOverrides => _longNameOverrides;

        internal string? DefaultIfOptionWithoutValue { get; private set; }

        protected bool AllowMultipleValues { get; private init; }

        /// <summary>
        /// Creates <see cref="CliOption"/> for template parameter.
        /// </summary>
        /// <param name="aliases">aliases to be used for option.</param>
        internal CliOption GetOption(IReadOnlySet<string> aliases)
        {
            CliOption option = GetBaseOption(aliases);
            option.Hidden = IsHidden;

            //if parameter is required, the default value is ignored.
            //the user should always specify the parameter, so the default value is not even shown.
            if (!IsRequired)
            {
                if (!string.IsNullOrWhiteSpace(DefaultValue)
                    || (Type == ParameterType.String || Type == ParameterType.Choice) && DefaultValue != null)
                {
                    switch (option)
                    {
                        case CliOption<string> stringOption:
                            stringOption.DefaultValueFactory = (_) => DefaultValue;
                            break;
                        case CliOption<bool> booleanOption:
                            booleanOption.DefaultValueFactory = (_) => bool.Parse(DefaultValue);
                            break;
                        case CliOption<long> integerOption:
                            if (Type == ParameterType.Hex)
                            {
                                integerOption.DefaultValueFactory = (_) => Convert.ToInt64(DefaultValue, 16);
                            }
                            else
                            {
                                integerOption.DefaultValueFactory = (_) => long.Parse(DefaultValue);
                            }
                            break;
                        case CliOption<float> floatOption:
                            floatOption.DefaultValueFactory = (_) => float.Parse(DefaultValue);
                            break;
                        case CliOption<double> doubleOption:
                            doubleOption.DefaultValueFactory = (_) => double.Parse(DefaultValue);
                            break;
                        default:
                            Debug.Fail($"Unexpected Option type: {option.GetType()}");
                            break;
                    }
                }
            }
            option.Description = GetOptionDescription();
            return option;
        }

        /// <summary>
        /// Returns a function to display option usage.
        /// </summary>
        internal virtual Func<HelpContext, string?>? GetCustomFirstColumnText(TemplateOption o)
        {
            //not customized
            return null;
        }

        /// <summary>
        /// Returns a function to display option description.
        /// </summary>
        internal Func<HelpContext, string?>? GetCustomSecondColumnText()
        {
            return (context) =>
            {
                return GetOptionDescription();
            };
        }

        protected virtual CliOption GetBaseOption(IReadOnlySet<string> aliases)
        {
            string name = GetName(aliases);
            CliOption cliOption = Type switch
            {
                ParameterType.Boolean => new CliOption<bool>(name)
                {
                    Arity = new ArgumentArity(0, 1)
                },
                ParameterType.Integer => new CliOption<long>(name)
                {
                    CustomParser = result => GetParseArgument(this, ConvertValueToInt)(result),
                    Arity = new ArgumentArity(string.IsNullOrWhiteSpace(DefaultIfOptionWithoutValue) ? 1 : 0, 1)
                },
                ParameterType.String => new CliOption<string>(name)
                {
                    CustomParser = result => GetParseArgument(this, ConvertValueToString)(result),
                    Arity = new ArgumentArity(DefaultIfOptionWithoutValue == null ? 1 : 0, 1)
                },
                ParameterType.Float => new CliOption<double>(name)
                {
                    CustomParser = result => GetParseArgument(this, ConvertValueToFloat)(result),
                    Arity = new ArgumentArity(string.IsNullOrWhiteSpace(DefaultIfOptionWithoutValue) ? 1 : 0, 1)
                },
                ParameterType.Hex => new CliOption<long>(name)
                {
                    CustomParser = result => GetParseArgument(this, ConvertValueToHex)(result),
                    Arity = new ArgumentArity(string.IsNullOrWhiteSpace(DefaultIfOptionWithoutValue) ? 1 : 0, 1)
                },
                _ => throw new Exception($"Unexpected value for {nameof(ParameterType)}: {Type}.")
            };
            AddAliases(cliOption, aliases);
            return cliOption;
        }

        /// <summary>
        /// Returns the longest alias without prefix.
        /// This is how System.CommandLine used to choose Name from aliases before Name and Aliases separation.
        /// </summary>
        protected string GetName(IReadOnlySet<string> aliases)
        {
            string name = "-";

            foreach (string alias in aliases)
            {
                if ((alias.Length - GetPrefixLength(alias)) > (name.Length - GetPrefixLength(name)))
                {
                    name = alias;
                }
            }

            return name;

            static int GetPrefixLength(string alias)
            {
                if (alias[0] == '-')
                {
                    return alias.Length > 1 && alias[1] == '-' ? 2 : 1;
                }
                else if (alias[0] == '/')
                {
                    return 1;
                }

                return 0;
            }
        }

        protected void AddAliases(CliOption option, IReadOnlySet<string> aliases)
        {
            foreach (string alias in aliases)
            {
                if (alias != option.Name)
                {
                    option.Aliases.Add(alias);
                }
            }
        }

        private static string ParameterTypeToString(ParameterType dataType)
        {
            return dataType switch
            {
                ParameterType.Boolean => "bool",
                ParameterType.Choice => "choice",
                ParameterType.Float => "float",
                ParameterType.Hex => "hex",
                ParameterType.Integer => "integer",
                _ => "text",
            };
        }

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

        private static Func<ArgumentResult, T> GetParseArgument<T>(CliTemplateParameter parameter, Func<string?, (bool, T)> convert)
        {
            return (argumentResult) =>
            {
                if (argumentResult.Parent is not OptionResult or)
                {
                    throw new NotSupportedException("The method should be only used with option.");
                }

                if (argumentResult.Tokens.Count == 0)
                {
                    if (or.Implicit)
                    {
                        if (parameter.DefaultValue != null)
                        {
                            (bool parsed, T value) = convert(parameter.DefaultValue);
                            if (parsed)
                            {
                                return value;
                            }

                            //Cannot parse default value '{0}' for option '{1}' as expected type '{2}'.
                            argumentResult.AddError(string.Format(
                                LocalizableStrings.ParseTemplateOption_Error_InvalidDefaultValue,
                                parameter.DefaultValue,
                                or.IdentifierToken?.Value,
                                typeof(T).Name));

                            //https://github.com/dotnet/command-line-api/blob/5eca6545a0196124cc1a66d8bd43db8945f1f1b7/src/System.CommandLine/Argument%7BT%7D.cs#L99-L113
                            //system-command-line can handle null.
                            return default!;
                        }
                        //Default value for argument missing for option: '{0}'.
                        argumentResult.AddError(string.Format(LocalizableStrings.ParseTemplateOption_Error_MissingDefaultValue, or.IdentifierToken?.Value));
                        return default!;
                    }
                    if (parameter.DefaultIfOptionWithoutValue != null)
                    {
                        (bool parsed, T value) = convert(parameter.DefaultIfOptionWithoutValue);
                        if (parsed)
                        {
                            return value;
                        }
                        //Cannot parse default if option without value '{0}' for option '{1}' as expected type '{2}'.
                        argumentResult.AddError(string.Format(
                            LocalizableStrings.ParseTemplateOption_Error_InvalidDefaultIfNoOptionValue,
                            parameter.DefaultIfOptionWithoutValue,
                            or.IdentifierToken?.Value,
                            typeof(T).Name));
                        return default!;
                    }
                    //Required argument missing for option: '{0}'.
                    argumentResult.AddError(string.Format(LocalizableStrings.ParseTemplateOption_Error_MissingDefaultIfNoOptionValue, or.IdentifierToken?.Value));
                    return default!;
                }
                else if (argumentResult.Tokens.Count == 1)
                {
                    (bool parsed, T value) = convert(argumentResult.Tokens[0].Value);
                    if (parsed)
                    {
                        return value;
                    }
                    //Cannot parse argument '{0}' for option '{1}' as expected type '{2}'.
                    argumentResult.AddError(string.Format(
                        LocalizableStrings.ParseTemplateOption_Error_InvalidArgument,
                        argumentResult.Tokens[0].Value,
                        or.IdentifierToken?.Value,
                        typeof(T).Name));
                    return default!;
                }
                else
                {
                    //Using more than 1 argument is not allowed for '{0}', used: {1}.
                    argumentResult.AddError(string.Format(LocalizableStrings.ParseTemplateOption_Error_InvalidCount, or.IdentifierToken?.Value, argumentResult.Tokens.Count));
                    return default!;
                }
            };
        }

        private static string? GetPrecedenceInfo(TemplateParameterPrecedence precedence)
        {
            switch (precedence.PrecedenceDefinition)
            {
                case PrecedenceDefinition.ConditionalyRequired:
                    return string.Format(HelpStrings.Text_RequiredCondition, precedence.IsRequiredCondition);
                case PrecedenceDefinition.ConditionalyDisabled:
                    return string.Format(HelpStrings.Text_EnabledCondition, precedence.IsEnabledCondition);
                case PrecedenceDefinition.Disabled:
                    return HelpStrings.Text_Disabled;
                case PrecedenceDefinition.Required:
                    return (HelpStrings.Text_Required);
                case PrecedenceDefinition.Optional:
                case PrecedenceDefinition.Implicit:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private (bool, string) ConvertValueToString(string? value)
        {
            return (true, value ?? string.Empty);
        }

        private (bool, long) ConvertValueToInt(string? value)
        {
            if (long.TryParse(value, out long result))
            {
                return (true, result);
            }
            return (false, default);
        }

        private (bool, double) ConvertValueToFloat(string? value)
        {
            if (Utils.ParserExtensions.DoubleTryParseСurrentOrInvariant(value, out double convertedFloat))
            {
                return (true, convertedFloat);
            }
            return (false, default);
        }

        private (bool, long) ConvertValueToHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (false, default);
            }

            if (value.Length < 3)
            {
                return (false, default);
            }

            if (!string.Equals(value.Substring(0, 2), "0x", StringComparison.OrdinalIgnoreCase))
            {
                return (false, default);
            }

            if (long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long convertedHex))
            {
                return (true, convertedHex);
            }
            return (false, default);
        }

        private string GetOptionDescription()
        {
            StringBuilder displayValue = new(255);
            displayValue.AppendLine(Description);

            string? precedenceValue = GetPrecedenceInfo(_precedence);
            if (!string.IsNullOrEmpty(precedenceValue))
            {
                displayValue.AppendLine(precedenceValue);
            }

            if (this is ChoiceTemplateParameter choice)
            {
                displayValue.AppendLine(string.Format(HelpStrings.RowHeader_Type, string.IsNullOrWhiteSpace(DataType) ? "choice" : DataType));
                int longestChoiceLength = choice.Choices.Keys.Max(x => x.Length);
                foreach (KeyValuePair<string, ParameterChoice> choiceInfo in choice.Choices)
                {
                    const string Indent = "  ";
                    displayValue.Append(Indent + choiceInfo.Key.PadRight(longestChoiceLength + Indent.Length));
                    if (!string.IsNullOrWhiteSpace(choiceInfo.Value.Description))
                    {
                        displayValue.AppendLine(choiceInfo.Value.Description.Replace("\r\n", " ").Replace("\n", " "));
                    }
                    else
                    {
                        displayValue.AppendLine();
                    }
                }
            }
            else
            {
                displayValue.AppendLine(string.Format(HelpStrings.RowHeader_Type, string.IsNullOrWhiteSpace(DataType) ? "string" : DataType));
            }
            if (AllowMultipleValues)
            {
                displayValue.AppendLine(string.Format(HelpStrings.RowHeader_AllowMultiValue, AllowMultipleValues));
            }
            //display the default value if there is one
            if (!string.IsNullOrWhiteSpace(DefaultValue))
            {
                displayValue.AppendLine(string.Format(HelpStrings.RowHeader_DefaultValue, DefaultValue));
            }

            if (!string.IsNullOrWhiteSpace(DefaultIfOptionWithoutValue))
            {
                // default if option is provided without a value should not be displayed if:
                // - it is bool parameter with "DefaultIfOptionWithoutValue": "true"
                // - it is not bool parameter (int, string, etc) and default value coincides with "DefaultIfOptionWithoutValue"
                if (Type == ParameterType.Boolean)
                {
                    if (!string.Equals(DefaultIfOptionWithoutValue, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        displayValue.AppendLine(string.Format(HelpStrings.RowHeader_DefaultIfOptionWithoutValue, DefaultIfOptionWithoutValue));
                    }
                }
                else
                {
                    if (!string.Equals(DefaultIfOptionWithoutValue, DefaultValue, StringComparison.Ordinal))
                    {
                        displayValue.AppendLine(string.Format(HelpStrings.RowHeader_DefaultIfOptionWithoutValue, DefaultIfOptionWithoutValue));
                    }
                }
            }

            return displayValue.ToString();
        }
    }
}
