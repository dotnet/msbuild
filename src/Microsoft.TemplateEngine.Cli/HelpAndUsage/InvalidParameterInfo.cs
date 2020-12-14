using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    /// <summary>
    /// The class represents the information about the invalid template parameter used when executing the command
    /// </summary>
    internal class InvalidParameterInfo
    {
        /// <summary>
        /// Defines the possible reason for the parameter to be invalid
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
        /// the option used in CLI for parameter
        /// </summary>
        internal string InputFormat { get; }
        /// <summary>
        /// The value specified for the parameter in CLI
        /// </summary>
        internal string SpecifiedValue { get; }
        /// <summary>
        /// The canonical name for the parameter
        /// </summary>
        internal string Canonical { get; }
        /// <summary>
        /// The reason why the parameter is invalid
        /// </summary>
        internal Kind ErrorKind { get; }

        /// <summary>
        /// Provides the error string to use for the invalid parameters collection
        /// </summary>
        /// <param name="invalidParameterList">the invalid parameters collection to prepare output for</param>
        /// <returns>the error string for the output</returns>
        internal static string InvalidParameterListToString(IReadOnlyList<InvalidParameterInfo> invalidParameterList)
        {
            if (invalidParameterList.Count == 0)
            {
                return string.Empty;
            }

            string invalidParamsErrorText = LocalizableStrings.InvalidTemplateParameterValues;

            foreach (InvalidParameterInfo invalidParam in invalidParameterList)
            {
                if (invalidParam.ErrorKind == Kind.InvalidParameterValue)
                {
                    invalidParamsErrorText += Environment.NewLine + string.Format(LocalizableStrings.InvalidParameterDetail, invalidParam.InputFormat, invalidParam.SpecifiedValue, invalidParam.Canonical);
                }
                else
                {
                    invalidParamsErrorText += Environment.NewLine + string.Format(LocalizableStrings.InvalidParameterDefault, invalidParam.Canonical, invalidParam.SpecifiedValue);
                }
            }

            return invalidParamsErrorText;
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
    }
}
