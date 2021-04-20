// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    /// <summary>
    /// The class represents the information about the invalid template parameter used when executing the command.
    /// </summary>
    internal class InvalidParameterInfo : IEquatable<InvalidParameterInfo>
    {
        /// <summary>
        /// Defines the possible reason for the parameter to be invalid.
        /// </summary>
        internal enum Kind
        {
            /// <summary>
            /// The parameter name is invalid
            /// </summary>
            InvalidParameterName,

            /// <summary>
            /// The value is invalid
            /// </summary>
            InvalidParameterValue,

            /// <summary>
            /// The default name is invalid
            /// </summary>
            InvalidDefaultValue,

            /// <summary>
            /// The value provided leads to ambiguous choice (for choice parameters only)
            /// </summary>
            AmbiguousParameterValue
        }

        internal InvalidParameterInfo(Kind kind, string inputFormat, string specifiedValue, string canonical)
        {
            ErrorKind = kind;
            InputFormat = inputFormat;
            SpecifiedValue = specifiedValue;
            Canonical = canonical;
        }

        /// <summary>
        /// the option used in CLI for parameter.
        /// </summary>
        internal string InputFormat { get; }

        /// <summary>
        /// The value specified for the parameter in CLI.
        /// </summary>
        internal string SpecifiedValue { get; }

        /// <summary>
        /// The canonical name for the parameter.
        /// </summary>
        internal string Canonical { get; }

        /// <summary>
        /// The reason why the parameter is invalid.
        /// </summary>
        internal Kind ErrorKind { get; }

        /// <summary>
        /// Provides the error string to use for the invalid parameters collection.
        /// </summary>
        /// <param name="invalidParameterList">the invalid parameters collection to prepare output for.</param>
        /// <param name="templateGroup">the template group to use to get more information about parameters. Optional - if not provided the possible value for the parameters won't be included to the output.</param>
        /// <returns>the error string for the output.</returns>
        internal static string InvalidParameterListToString(IEnumerable<InvalidParameterInfo> invalidParameterList, TemplateGroup templateGroup = null)
        {
            if (!invalidParameterList.Any())
            {
                return string.Empty;
            }

            StringBuilder invalidParamsErrorText = new StringBuilder(LocalizableStrings.InvalidTemplateParameterValues);
            const int padWidth = 3;
            invalidParamsErrorText.AppendLine();
            foreach (InvalidParameterInfo invalidParam in invalidParameterList)
            {
                if (invalidParam.ErrorKind == Kind.InvalidParameterName)
                {
                    invalidParamsErrorText.AppendLine(invalidParam.InputFormat);
                    invalidParamsErrorText.Append(' ', padWidth).AppendLine(string.Format(LocalizableStrings.InvalidParameterNameDetail, invalidParam.InputFormat));
                }
                else if (invalidParam.ErrorKind == Kind.AmbiguousParameterValue)
                {
                    invalidParamsErrorText.AppendLine(invalidParam.InputFormat + ' ' + invalidParam.SpecifiedValue);
                    string header = string.Format(LocalizableStrings.AmbiguousParameterDetail, invalidParam.InputFormat, invalidParam.SpecifiedValue);
                    if (templateGroup != null)
                    {
                        DisplayValidValues(invalidParamsErrorText, header, templateGroup.GetAmbiguousValuesForChoiceParameter(invalidParam.Canonical, invalidParam.SpecifiedValue), padWidth);
                    }
                    else
                    {
                        invalidParamsErrorText.Append(' ', padWidth).AppendLine(header);
                    }
                }
                else if (invalidParam.ErrorKind == Kind.InvalidParameterValue)
                {
                    invalidParamsErrorText.AppendLine(invalidParam.InputFormat + ' ' + invalidParam.SpecifiedValue);
                    string header = string.Format(LocalizableStrings.InvalidParameterDetail, invalidParam.InputFormat, invalidParam.SpecifiedValue);
                    if (templateGroup != null)
                    {
                        DisplayValidValues(invalidParamsErrorText, header, templateGroup.GetValidValuesForChoiceParameter(invalidParam.Canonical), padWidth);
                    }
                    else
                    {
                        invalidParamsErrorText.Append(' ', padWidth).AppendLine(header);
                    }
                }
                else
                {
                    invalidParamsErrorText.AppendLine(invalidParam.InputFormat + ' ' + invalidParam.SpecifiedValue);
                    invalidParamsErrorText.Append(' ', padWidth).AppendLine(string.Format(LocalizableStrings.InvalidParameterDefault, invalidParam.InputFormat, invalidParam.SpecifiedValue));
                }
            }
            return invalidParamsErrorText.ToString();
        }

        private static void DisplayValidValues(StringBuilder text, string header, IDictionary<string, ParameterChoice> possibleValues, int padWidth)
        {
            text.Append(' ', padWidth).Append(header);

            if (!possibleValues.Any())
            {
                return;
            }

            text.Append(' ').AppendLine(LocalizableStrings.PossibleValuesHeader);
            int longestChoiceLength = possibleValues.Keys.Max(x => x.Length);
            foreach (KeyValuePair<string, ParameterChoice> choiceInfo in possibleValues)
            {
                text.Append(' ', padWidth * 2).Append(choiceInfo.Key.PadRight(longestChoiceLength + padWidth));

                if (!string.IsNullOrWhiteSpace(choiceInfo.Value.Description))
                {
                    text.Append("- " + choiceInfo.Value.Description);
                }

                text.AppendLine();
            }
        }

        internal static IDictionary<string, InvalidParameterInfo> IntersectWithExisting(IDictionary<string, InvalidParameterInfo> existing, IReadOnlyList<InvalidParameterInfo> newInfo)
        {
            Dictionary<string, InvalidParameterInfo> intersection = new Dictionary<string, InvalidParameterInfo>();

            foreach (InvalidParameterInfo info in newInfo)
            {
                if (existing.ContainsKey(info.Canonical))
                {
                    intersection.Add(info.Canonical, info);
                }
            }

            return intersection;
        }

        public override bool Equals(object obj)
        {
            if (obj is InvalidParameterInfo info)
            {
                //checking canonical name and kind is enough for invalid parameters to be the same
                return Canonical.Equals(info.Canonical, StringComparison.OrdinalIgnoreCase) && ErrorKind == info.ErrorKind;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return new { a = Canonical?.ToLowerInvariant(), ErrorKind }.GetHashCode();
        }

        public bool Equals(InvalidParameterInfo other)
        {
            return Canonical.Equals(other.Canonical, StringComparison.OrdinalIgnoreCase) && ErrorKind == other.ErrorKind;
        }
    }
}
