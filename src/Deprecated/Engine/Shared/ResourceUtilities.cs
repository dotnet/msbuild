// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Resources;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class contains utility methods for dealing with resources.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal static class ResourceUtilities
    {
        // used to find MSBuild message code prefixes
        private static readonly Regex msbuildMessageCodePattern = new Regex(@"^\s*(?<CODE>MSB\d\d\d\d):\s*(?<MESSAGE>.*)$", RegexOptions.Singleline);

        /// <summary>
        /// Extracts the message code (if any) prefixed to the given string. If a message code pattern is not supplied, the
        /// MSBuild message code pattern is used by default. The message code pattern must contain two named capturing groups
        /// called "CODE" and "MESSAGE" that identify the message code and the message respectively.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <owner>SumedhK</owner>
        /// <param name="messageCodePattern">The Regex used to find the message code (can be null).</param>
        /// <param name="messageWithCode">The string to parse.</param>
        /// <param name="code">[out] The message code, or null if there was no code.</param>
        /// <returns>The string without its message code prefix.</returns>
        internal static string ExtractMessageCode(Regex messageCodePattern, string messageWithCode, out string code)
        {
            code = null;
            string messageOnly = messageWithCode;

            if (messageCodePattern == null)
            {
                messageCodePattern = msbuildMessageCodePattern;
            }

            // NOTE: the Regex class is thread-safe (see MSDN)
            Match messageCode = messageCodePattern.Match(messageWithCode);

            if (messageCode.Success)
            {
                code = messageCode.Groups["CODE"].Value;
                messageOnly = messageCode.Groups["MESSAGE"].Value;
            }

            return messageOnly;
        }

        /// <summary>
        /// Retrieves the MSBuild F1-help keyword for the given resource string. Help keywords are used to index help topics in
        /// host IDEs.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="resourceName">Resource string to get the MSBuild F1-keyword for.</param>
        /// <returns>The MSBuild F1-help keyword string.</returns>
        private static string GetHelpKeyword(string resourceName)
        {
            return "MSBuild." + resourceName;
        }

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        /// 
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <owner>SumedhK</owner>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="args">Optional arguments for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceString(out string code, out string helpKeyword, string resourceName, params object[] args)
        {
            helpKeyword = GetHelpKeyword(resourceName);

            // NOTE: the AssemblyResources.GetString() method is thread-safe
            return ExtractMessageCode(null, FormatString(AssemblyResources.GetString(resourceName), args), out code);
        }

        /// <summary>
        /// Looks up a string in the resources, and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        /// 
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <owner>SumedhK</owner>
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
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <owner>SumedhK</owner>
        /// <param name="unformatted">The string to format.</param>
        /// <param name="args">Optional arguments for formatting the given string.</param>
        /// <returns>The formatted string.</returns>
        internal static string FormatString(string unformatted, params object[] args)
        {
            string formatted = unformatted;

            // NOTE: String.Format() does not allow a null arguments array
            if ((args?.Length > 0))
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
                //
                // See DevDiv Bugs 15210 for more information.
                                
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
        /// <owner>RGoel</owner>
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
                    Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
                    throw new InternalErrorException();
                }
            }
            catch (ArgumentException e)
            {
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
                throw new InternalErrorException(e.Message);
            }
            catch (InvalidOperationException e)
            {
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
                throw new InternalErrorException(e.Message);
            }
            catch (MissingManifestResourceException e)
            {
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
                throw new InternalErrorException(e.Message);
            }
#endif
        }
    }
}
