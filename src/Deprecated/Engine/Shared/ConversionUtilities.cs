// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Globalization;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class contains only static methods, which are useful throughout many
    /// of the MSBuild classes and don't really belong in any specific class.   
    /// </summary>
    internal static class ConversionUtilities
    {
        /// <summary>
        /// Converts a string to a bool.  We consider "true/false", "on/off", and 
        /// "yes/no" to be valid boolean representations in the XML.
        /// </summary>
        /// <param name="parameterValue">The string to convert.</param>
        /// <returns>Boolean true or false, corresponding to the string.</returns>
        internal static bool ConvertStringToBool(string parameterValue)
        {
            if (ValidBooleanTrue(parameterValue))
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
                error.VerifyThrowArgument(false, "Shared.CannotConvertStringToBool", parameterValue);
                return false;
            }
        }

        /// <summary>
        /// Returns true if the string can be successfully converted to a bool,
        /// such as "on" or "yes"
        /// </summary>
        internal static bool CanConvertStringToBool(string parameterValue)
        {
            return ValidBooleanTrue(parameterValue) || ValidBooleanFalse(parameterValue);
        }

        /// <summary>
        /// Returns true if the string represents a valid MSBuild boolean true value,
        /// such as "on", "!false", "yes"
        /// </summary>
        private static bool ValidBooleanTrue(string parameterValue)
        {
            return (String.Equals(parameterValue, "true", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "on", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "yes", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "!false", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "!off", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "!no", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns true if the string represents a valid MSBuild boolean false value,
        /// such as "!on" "off" "no" "!true"
        /// </summary>
        private static bool ValidBooleanFalse(string parameterValue)
        {
            return (String.Equals(parameterValue, "false", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "off", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "no", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "!true", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "!on", StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(parameterValue, "!yes", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Converts a string like "123.456" into a double. Leading sign is allowed.
        /// </summary>
        internal static double ConvertDecimalToDouble(string number)
        {
            return Double.Parse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat);
        }

        /// <summary>
        /// Converts a hex string like "0xABC" into a double.
        /// </summary>
        internal static double ConvertHexToDouble(string number)
        {
            return (double)Int32.Parse(number.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture.NumberFormat);
        }

        /// <summary>
        /// Converts a string like "123.456" or "0xABC" into a double.
        /// Tries decimal conversion first.
        /// </summary>
        internal static double ConvertDecimalOrHexToDouble(string number)
        {
            if (ConversionUtilities.ValidDecimalNumber(number))
            {
                return ConversionUtilities.ConvertDecimalToDouble(number);
            }
            else if (ConversionUtilities.ValidHexNumber(number))
            {
                return ConversionUtilities.ConvertHexToDouble(number);
            }
            else
            {
                ErrorUtilities.VerifyThrow(false, "Cannot numeric evaluate");
                return 0.0D;
            }
        }

        /// <summary>
        /// Returns true if the string is a valid hex number, like "0xABC"
        /// </summary>
        private static bool ValidHexNumber(string number)
        {
            bool canConvert = false;
            if (number.Length >= 3 && number[0] == '0' && (number[1] == 'x' || number[1] == 'X'))
            {
                int value;
                canConvert = Int32.TryParse(number.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture.NumberFormat, out value);
            }
            return canConvert;
        }

        /// <summary>
        /// Returns true if the string is a valid decimal number, like "-123.456"
        /// </summary>
        private static bool ValidDecimalNumber(string number)
        {
            double value;
            return Double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat, out value);
        }

        /// <summary>
        /// Returns true if the string is a valid decimal or hex number
        /// </summary>
        internal static bool ValidDecimalOrHexNumber(string number)
        {
            return ValidDecimalNumber(number) || ValidHexNumber(number);
        }
    }
}
