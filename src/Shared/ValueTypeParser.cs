// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Provides unified parsing and formatting logic for value types used in MSBuild task parameters.
    /// This ensures consistent behavior between TaskItem&lt;T&gt; parsing and TaskExecutionHost parameter binding.
    /// </summary>
    internal static class ValueTypeParser
    {
        /// <summary>
        /// Parses a boolean value using MSBuild's boolean conventions.
        /// Supports: true/on/yes/!false/!off/!no for true, false/off/no/!true/!on/!yes for false.
        /// </summary>
        private static bool ParseBool(string value)
        {
            // MSBuild boolean true values
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "!false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "!off", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "!no", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // MSBuild boolean false values
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "!true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "!on", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "!yes", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw new ArgumentException($"Cannot parse '{value}' as a boolean value.");
        }

        /// <summary>
        /// Parses a string value as the specified type.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <param name="targetType">The type to parse the value as.</param>
        /// <returns>The parsed value as an object.</returns>
        /// <exception cref="ArgumentException">Thrown if the value cannot be parsed as the target type.</exception>
        public static object Parse(string value, Type targetType)
        {
            try
            {
                // Special handling for AbsolutePath
                if (targetType == typeof(AbsolutePath))
                {
                    return new AbsolutePath(value);
                }

                // Special handling for FileInfo
                if (targetType == typeof(FileInfo))
                {
                    return new FileInfo(value);
                }

                // Special handling for DirectoryInfo
                if (targetType == typeof(DirectoryInfo))
                {
                    return new DirectoryInfo(value);
                }

                // Special handling for bool - MSBuild supports various boolean representations
                if (targetType == typeof(bool))
                {
                    return ParseBool(value);
                }

                // Special handling for string (no parsing needed)
                if (targetType == typeof(string))
                {
                    return value;
                }

                // Use TypeCode-based parsing for built-in types
                TypeCode typeCode = Type.GetTypeCode(targetType);
                switch (typeCode)
                {
                    case TypeCode.Int32:
                        return int.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.Int64:
                        return long.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.Double:
                        return double.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.Single:
                        return float.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.Decimal:
                        return decimal.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.Byte:
                        return byte.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.SByte:
                        return sbyte.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.Int16:
                        return short.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.UInt16:
                        return ushort.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.UInt32:
                        return uint.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.UInt64:
                        return ulong.Parse(value, CultureInfo.InvariantCulture);
                    case TypeCode.Char:
                        return char.Parse(value);
                    default:
                        // Fallback to Convert.ChangeType for other types
                        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException || ex is ArgumentException || ex is NotSupportedException)
            {
                throw new ArgumentException($"Cannot parse '{value}' as type {targetType.Name}.", nameof(value), ex);
            }
        }

        /// <summary>
        /// Converts a value to its string representation for use in MSBuild.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The string representation of the value.</returns>
        public static string ToString(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            // AbsolutePath needs special handling because Convert.ChangeType doesn't work with implicit operators
            if (value is AbsolutePath absolutePath)
            {
                return absolutePath.Value;
            }

            // FileInfo and DirectoryInfo need special handling to return their path
            if (value is FileInfo fileInfo)
            {
                return fileInfo.FullName;
            }

            if (value is DirectoryInfo directoryInfo)
            {
                return directoryInfo.FullName;
            }

            // Use InvariantCulture for consistent formatting
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
