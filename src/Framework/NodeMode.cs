// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Enumeration of the various node modes that MSBuild.exe can run in.
    /// </summary>
    internal enum NodeMode
    {
        /// <summary>
        /// Normal out-of-process node.
        /// </summary>
        OutOfProcNode = 1,

        /// <summary>
        /// Out-of-process task host node.
        /// </summary>
        OutOfProcTaskHostNode = 2,

        /// <summary>
        /// Out-of-process RAR (ResolveAssemblyReference) service node.
        /// </summary>
        OutOfProcRarNode = 3,

        /// <summary>
        /// Out-of-process server node.
        /// </summary>
        OutOfProcServerNode = 8,
    }

    /// <summary>
    /// Helper methods for the NodeMode enum.
    /// </summary>
    internal static partial class NodeModeHelper
    {
        /// <summary>
        /// Converts a NodeMode value to a command line argument string.
        /// </summary>
        /// <param name="nodeMode">The node mode to convert</param>
        /// <returns>The command line argument string (e.g., "/nodemode:1")</returns>
        public static string ToCommandLineArgument(NodeMode nodeMode) => $"/nodemode:{(int)nodeMode}";

        /// <summary>
        /// Tries to parse a node mode value from a string, supporting both integer values and enum names (case-insensitive).
        /// </summary>
        /// <param name="value">The value to parse (can be an integer or enum name)</param>
        /// <param name="nodeMode">The parsed NodeMode value if successful</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParse(string value, [NotNullWhen(true)] out NodeMode? nodeMode)
        {
#if NET
            return TryParseCore(value.AsSpan(), out nodeMode);
#else
            return TryParseCore(value, out nodeMode);
#endif
        }

#if NET
        /// <summary>
        /// Tries to parse a node mode value from a span, supporting both integer values and enum names (case-insensitive).
        /// </summary>
        /// <param name="value">The value to parse (can be an integer or enum name)</param>
        /// <param name="nodeMode">The parsed NodeMode value if successful</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParse(ReadOnlySpan<char> value, [NotNullWhen(true)] out NodeMode? nodeMode)
        {
            return TryParseCore(value, out nodeMode);
        }
#endif

#if NET
        private static bool TryParseCore(ReadOnlySpan<char> value, [NotNullWhen(true)] out NodeMode? nodeMode)
#else
        private static bool TryParseCore(string value, [NotNullWhen(true)] out NodeMode? nodeMode)
#endif
        {
            nodeMode = null;

#if NET
            if (value.IsEmpty || value.IsWhiteSpace())
            {
                return false;
            }
#else
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
#endif

            // First try to parse as an integer for backward compatibility
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                // Validate that the integer corresponds to a valid enum value
                if (Enum.IsDefined(typeof(NodeMode), intValue))
                {
                    nodeMode = (NodeMode)intValue;
                    return true;
                }

                return false;
            }

            // Try to parse as an enum name (case-insensitive)
            if (Enum.TryParse(value, ignoreCase: true, out NodeMode enumValue) && Enum.IsDefined(typeof(NodeMode), enumValue))
            {
                nodeMode = enumValue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the NodeMode from a command line string using regex pattern matching.
        /// </summary>
        /// <param name="commandLine">The command line to parse. Note that this can't be a span because generated regex don't have a Span Match overload</param>
        /// <returns>The NodeMode if found, otherwise null</returns>
        public static NodeMode? ExtractFromCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return null;
            }
#if NET
            // Use compiled regex for better performance on .NET
            var match = CommandLineNodeModeRegex().Match(commandLine);
#else
            var match = CommandLineNodeModeRegexInstance.Match(commandLine);
#endif

            if (!match.Success)
            {
                return null;
            }

            string nodeModeValue = match.Groups["nodemode"].Value;

#if NET
            if (TryParse(nodeModeValue.AsSpan(), out NodeMode? nodeMode))
#else
            if (TryParse(nodeModeValue, out NodeMode? nodeMode))
#endif
            {
                return nodeMode;
            }

            return null;
        }

#if NET
        [System.Text.RegularExpressions.GeneratedRegex(@"/nodemode:(?<nodemode>[a-zA-Z0-9]+)(?:\s|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
        private static partial System.Text.RegularExpressions.Regex CommandLineNodeModeRegex();
#else
        private static readonly System.Text.RegularExpressions.Regex CommandLineNodeModeRegexInstance = new(
            @"/nodemode:(?<nodemode>[a-zA-Z0-9]+)(?:\s|$)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
#endif
    }
}
