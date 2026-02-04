// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;
using Microsoft.Build.TaskHost.Resources;

namespace Microsoft.Build.TaskHost.Utilities
{
    /// <summary>
    /// This class contains utility methods for dealing with resources.
    /// </summary>
    internal static class ResourceUtilities
    {
        /// <summary>
        /// Extracts the message code (if any) prefixed to the given string.
        /// <![CDATA[
        /// MSBuild codes match "^\s*(?<CODE>MSB\d\d\d\d):\s*(?<MESSAGE>.*)$"
        /// Arbitrary codes match "^\s*(?<CODE>[A-Za-z]+\d+):\s*(?<MESSAGE>.*)$"
        /// ]]>
        /// Thread safe.
        /// </summary>
        /// <param name="msbuildCodeOnly">Whether to match only MSBuild error codes, or any error code.</param>
        /// <param name="message">The string to parse.</param>
        /// <param name="code">[out] The message code, or null if there was no code.</param>
        /// <returns>The string without its message code prefix, if any.</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Scope = "member", Target = "Microsoft.Build.Shared.ResourceUtilities.#ExtractMessageCode(System.Boolean,System.String,System.String&)", Justification = "Unavoidable complexity")]
        internal static string ExtractMessageCode(bool msbuildCodeOnly, string message, out string? code)
        {
            code = null;
            int i = 0;

            while (i < message.Length && Char.IsWhiteSpace(message[i]))
            {
                i++;
            }

            if (msbuildCodeOnly)
            {
                if (
                    message.Length < i + 8 ||
                    message[i] != 'M' ||
                    message[i + 1] != 'S' ||
                    message[i + 2] != 'B' ||
                    message[i + 3] < '0' || message[i + 3] > '9' ||
                    message[i + 4] < '0' || message[i + 4] > '9' ||
                    message[i + 5] < '0' || message[i + 5] > '9' ||
                    message[i + 6] < '0' || message[i + 6] > '9' ||
                    message[i + 7] != ':')
                {
                    return message;
                }

                code = message.Substring(i, 7);

                i += 8;
            }
            else
            {
                int j = i;
                for (; j < message.Length; j++)
                {
                    char c = message[j];
                    if (((c < 'a') || (c > 'z')) && ((c < 'A') || (c > 'Z')))
                    {
                        break;
                    }
                }

                if (j == i)
                {
                    return message; // Should have been at least one letter
                }

                int k = j;

                for (; k < message.Length; k++)
                {
                    char c = message[k];
                    if (c < '0' || c > '9')
                    {
                        break;
                    }
                }

                if (k == j)
                {
                    return message; // Should have been at least one digit
                }

                if (k == message.Length || message[k] != ':')
                {
                    return message;
                }

                code = message.Substring(i, k - i);

                i = k + 1;
            }

            while (i < message.Length && Char.IsWhiteSpace(message[i]))
            {
                i++;
            }

            if (i < message.Length)
            {
                message = message.Substring(i);
            }

            return message;
        }

        /// <summary>
        /// Retrieves the MSBuild F1-help keyword for the given resource string. Help keywords are used to index help topics in
        /// host IDEs.
        /// </summary>
        /// <param name="resourceName">Resource string to get the MSBuild F1-keyword for.</param>
        /// <returns>The MSBuild F1-help keyword string.</returns>
        private static string GetHelpKeyword(string resourceName)
            => "MSBuild." + resourceName;

        /// <summary>
        /// Retrieves the contents of the named resource string.
        /// </summary>
        /// <param name="resourceName">Resource string name.</param>
        /// <returns>Resource string contents.</returns>
        internal static string GetResourceString(string resourceName)
            => AssemblyResources.GetString(resourceName);

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="args">Optional arguments for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        private static string FormatResourceStringStripCodeAndKeyword(out string? code, out string? helpKeyword, string resourceName, params object?[]? args)
        {
            helpKeyword = GetHelpKeyword(resourceName);

            // NOTE: the AssemblyResources.GetString() method is thread-safe
            return ExtractMessageCode(true /* msbuildCodeOnly */, FormatString(GetResourceString(resourceName), args), out code);
        }

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        /// </summary>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">Argument for formatting the resource string.</param>
        private static string FormatResourceStringStripCodeAndKeyword(out string? code, out string? helpKeyword, string resourceName, object? arg1)
        {
            helpKeyword = GetHelpKeyword(resourceName);
            return ExtractMessageCode(true, FormatString(GetResourceString(resourceName), arg1), out code);
        }

        /// <summary>
        /// Looks up a string in the resources, and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="args">Optional arguments for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringStripCodeAndKeyword(string resourceName, params object?[]? args)
            => FormatResourceStringStripCodeAndKeyword(out _, out _, resourceName, args);

        /// <summary>
        /// Looks up a string in the resources, and formats it with the argument passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">Argument for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringStripCodeAndKeyword(string resourceName, object? arg1)
           => FormatResourceStringStripCodeAndKeyword(out _, out _, resourceName, arg1);

        /// <summary>
        /// Formats the given string using the variable arguments passed in.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        ///
        /// Thread safe.
        /// </summary>
        /// <param name="unformatted">The string to format.</param>
        /// <param name="args">Optional arguments for formatting the given string.</param>
        /// <returns>The formatted string.</returns>
        internal static string FormatString(string unformatted, params object?[]? args)
        {
            string formatted = unformatted;

            // NOTE: String.Format() does not allow a null arguments array
            if (args?.Length > 0)
            {
                ValidateArgsIfDebug(args);

                // Format the string, using the variable arguments passed in.
                // NOTE: all String methods are thread-safe
                formatted = string.Format(CultureInfo.CurrentCulture, unformatted, args);
            }

            return formatted;
        }

        // Overloads with 1-3 arguments to avoid array allocations.

        /// <summary>
        /// Formats the given string using the variable arguments passed in.
        /// </summary>
        /// <param name="unformatted">The string to format.</param>
        /// <param name="arg1">Argument for formatting the given string.</param>
        /// <returns>The formatted string.</returns>
        internal static string FormatString(string unformatted, object? arg1)
        {
            ValidateArgsIfDebug([arg1]);
            return string.Format(CultureInfo.CurrentCulture, unformatted, arg1);
        }

        /// <summary>
        /// Formats the given string using the variable arguments passed in.
        /// </summary>
        /// <param name="unformatted">The string to format.</param>
        /// <param name="arg1">First argument for formatting the given string.</param>
        /// <param name="arg2">Second argument for formatting the given string.</param>
        /// <returns>The formatted string.</returns>
        internal static string FormatString(string unformatted, object? arg1, object? arg2)
        {
            ValidateArgsIfDebug([arg1, arg2]);
            return string.Format(CultureInfo.CurrentCulture, unformatted, arg1, arg2);
        }

        [Conditional("DEBUG")]
        private static void ValidateArgsIfDebug(object?[] args)
        {
            // If you accidentally pass some random type in that can't be converted to a string,
            // FormatResourceString calls ToString() which returns the full name of the type!
            foreach (object? param in args)
            {
                // Check it has a real implementation of ToString() and the type is not actually System.String
                if (param != null)
                {
                    if (string.Equals(param.GetType().ToString(), param.ToString(), StringComparison.Ordinal) &&
                        param.GetType() != typeof(string))
                    {
                        ErrorUtilities.ThrowInternalError(
                            "Invalid resource parameter type, was {0}",
                            param.GetType().FullName);
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that a particular resource string actually exists in the string table. This will only be called in debug
        /// builds. It helps catch situations where a dev calls VerifyThrowXXX with a new resource string, but forgets to add the
        /// resource string to the string table, or misspells it!
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to check.</param>
        [Conditional("DEBUG")]
        internal static void VerifyResourceStringExists(string resourceName)
        {
            try
            {
                // Look up the resource string in the engine's string table.
                // NOTE: the AssemblyResources.GetString() method is thread-safe
                string unformattedMessage = AssemblyResources.GetString(resourceName);

                if (unformattedMessage == null)
                {
                    ErrorUtilities.ThrowInternalError("The resource string \"" + resourceName + "\" was not found.");
                }
            }
            catch (ArgumentException e)
            {
#if FEATURE_DEBUG_LAUNCH
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
#endif
                ErrorUtilities.ThrowInternalError(e.Message);
            }
            catch (InvalidOperationException e)
            {
#if FEATURE_DEBUG_LAUNCH
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
#endif
                ErrorUtilities.ThrowInternalError(e.Message);
            }
            catch (MissingManifestResourceException e)
            {
#if FEATURE_DEBUG_LAUNCH
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
#endif
                ErrorUtilities.ThrowInternalError(e.Message);
            }
        }
    }
}
