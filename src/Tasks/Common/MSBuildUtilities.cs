// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Internal utilties copied from microsoft/MSBuild repo.
    /// </summary>
    class MSBuildUtilities
    {
        /// <summary>
        /// Converts a string to a bool.  We consider "true/false", "on/off", and 
        /// "yes/no" to be valid boolean representations in the XML.
        /// Modified from its original version to not throw, but return a default value.
        /// </summary>
        /// <param name="parameterValue">The string to convert.</param>
        /// <returns>Boolean true or false, corresponding to the string.</returns>
        internal static bool ConvertStringToBool(string parameterValue, bool defaultValue = false)
        {
            if (String.IsNullOrEmpty(parameterValue))
            {
                return defaultValue;
            }
            else if (ValidBooleanTrue(parameterValue))
            {
                return true;
            }
            else if (ValidBooleanFalse(parameterValue))
            {
                return false;
            }
            else
            {
                // Unsupported boolean representation.
                return defaultValue;
            }
        }

        /// <summary>
        /// Returns true if the string represents a valid MSBuild boolean true value,
        /// such as "on", "!false", "yes"
        /// </summary>
        private static bool ValidBooleanTrue(string parameterValue)
        {
            return ((String.Compare(parameterValue, "true", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "on", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "yes", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "!false", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "!off", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "!no", StringComparison.OrdinalIgnoreCase) == 0));
        }

        /// <summary>
        /// Returns true if the string represents a valid MSBuild boolean false value,
        /// such as "!on" "off" "no" "!true"
        /// </summary>
        private static bool ValidBooleanFalse(string parameterValue)
        {
            return ((String.Compare(parameterValue, "false", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "off", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "no", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "!true", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "!on", StringComparison.OrdinalIgnoreCase) == 0) ||
                    (String.Compare(parameterValue, "!yes", StringComparison.OrdinalIgnoreCase) == 0));
        }
    }
}
