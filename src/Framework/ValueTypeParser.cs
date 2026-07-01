// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;

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
            // Reuse MSBuild's canonical boolean parsing (true/false, on/off, yes/no, and '!' negation)
            return ConversionUtilities.ConvertStringToBool(value);
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
                // Path-like types (AbsolutePath/FileInfo/DirectoryInfo) are constructed directly from
                // 'value' without rooting it here, because callers are expected to pass an already-rooted
                // (absolute) path. The engine's parameter-binding path roots the string via
                // TaskEnvironment.GetAbsolutePath before calling Parse, and the TaskItem<T> path derives
                // 'value' from the item's FullPath metadata (also absolute). Constructing FileInfo/
                // DirectoryInfo from a relative string would silently resolve against the current working
                // directory, so we rely on that rooting invariant rather than re-resolving here.

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

                // Everything else (numeric types, char, etc.) is converted via Convert.ChangeType, pinned
                // to InvariantCulture. This is deliberately permissive: a future analyzer is expected to
                // steer authors toward the first-class supported types above and away from relying on this
                // general-purpose conversion for parameter types we don't really intend to support.
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
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
                return absolutePath.Value ?? string.Empty;
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
