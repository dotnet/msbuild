// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Resources;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Text;
using System.ComponentModel;

#if BUILDINGAPPXTASKS
namespace Microsoft.Build.AppxPackage.Shared
#else
namespace Microsoft.Build.Shared
#endif
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
        internal static string ExtractMessageCode(bool msbuildCodeOnly, string message, out string code)
        {
#if !BUILDINGAPPXTASKS
            ErrorUtilities.VerifyThrowInternalNull(message, "message");
#endif

            code = null;
            int i = 0;

            while (i < message.Length && Char.IsWhiteSpace(message[i]))
            {
                i++;
            }

#if !BUILDINGAPPXTASKS
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
                    message[i + 7] != ':'
                    )
                {
                    return message;
                }

                code = message.Substring(i, 7);

                i = i + 8;
            }
            else
#endif
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
                message = message.Substring(i, message.Length - i);
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
        {
            return ("MSBuild." + resourceName);
        }

#if !BUILDINGAPPXTASKS
        /// <summary>
        /// Retrieves the contents of the named resource string.
        /// </summary>
        /// <param name="resourceName">Resource string name.</param>
        /// <returns>Resource string contents.</returns>
        internal static string GetResourceString(string resourceName)
        {
            string result = AssemblyResources.GetString(resourceName);
            return result;
        }

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        /// 
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="args">Optional arguments for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceString(out string code, out string helpKeyword, string resourceName, params object[] args)
        {
            helpKeyword = GetHelpKeyword(resourceName);

            // NOTE: the AssemblyResources.GetString() method is thread-safe
            return ExtractMessageCode(true /* msbuildCodeOnly */, FormatString(GetResourceString(resourceName), args), out code);
        }

        [Obsolete("Use GetResourceString instead.", true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static string FormatResourceString(string resourceName)
        {   // Avoids an accidental dependency on FormatResourceString(string, params object[])
            return null;
        }

        /// <summary>
        /// Looks up a string in the resources, and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        /// 
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="args">Optional arguments for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceString(string resourceName, params object[] args)
        {
            string code;
            string helpKeyword;

            return FormatResourceString(out code, out helpKeyword, resourceName, args);
        }

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
        internal static string FormatString(string unformatted, params object[] args)
        {
            string formatted = unformatted;

            // NOTE: String.Format() does not allow a null arguments array
            if ((args != null) && (args.Length > 0))
            {
#if DEBUG

#if VALIDATERESOURCESTRINGS
                // The code below reveals many places in our codebase where
                // we're not using all of the data given to us to format
                // strings -- but there are too many to presently fix.
                // Rather than toss away the code, we should later build it
                // and fix each offending resource (or the code processing
                // the resource).

                // String.Format() will throw a FormatException if args does
                // not have enough elements to match each format parameter.
                // However, it provides no feedback in the case when args contains
                // more elements than necessary to replace each format 
                // parameter.  We'd like to know if we're providing too much
                // data in cases like these, so we'll fail if this code runs.
                                                
                // We create an array with one fewer element
                object[] trimmedArgs = new object[args.Length - 1];
                Array.Copy(args, 0, trimmedArgs, 0, args.Length - 1);

                bool caughtFormatException = false;
                try
                {
                    // This will throw if there aren't enough elements in trimmedArgs...
                    String.Format(CultureInfo.CurrentCulture, unformatted, trimmedArgs);
                }
                catch (FormatException)
                {
                    caughtFormatException = true;
                }

                // If we didn't catch an exception above, then some of the elements
                // of args were unnecessary when formatting unformatted...
                Debug.Assert
                (
                    caughtFormatException,
                    String.Format("The provided format string '{0}' had fewer format parameters than the number of format args, '{1}'.", unformatted, args.Length)
                );
#endif
                // If you accidentally pass some random type in that can't be converted to a string, 
                // FormatResourceString calls ToString() which returns the full name of the type!
                foreach (object param in args)
                {
                    // Check it has a real implementation of ToString()
                    if (param != null)
                    {
                        if (String.Equals(param.GetType().ToString(), param.ToString(), StringComparison.Ordinal))
                        {
                            ErrorUtilities.ThrowInternalError("Invalid resource parameter type, was {0}", param.GetType().FullName);
                        }
                    }
                }
#endif
                // Format the string, using the variable arguments passed in.
                // NOTE: all String methods are thread-safe
                formatted = String.Format(CultureInfo.CurrentCulture, unformatted, args);
            }

            return formatted;
        }

        /// <summary>
        /// Verifies that a particular resource string actually exists in the string table. This will only be called in debug
        /// builds. It helps catch situations where a dev calls VerifyThrowXXX with a new resource string, but forgets to add the
        /// resource string to the string table, or misspells it!
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to check.</param>
        internal static void VerifyResourceStringExists(string resourceName)
        {
#if DEBUG
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
#endif
        }
#endif
    }
}
