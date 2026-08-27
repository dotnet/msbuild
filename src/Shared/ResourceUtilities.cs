// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Resources;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for dealing with resources.
    /// </summary>
    internal static class ResourceUtilities
    {
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
        internal static string FormatResourceStringStripCodeAndKeyword(out string? code, out string? helpKeyword, string resourceName, params object?[] args)
        {
            helpKeyword = GetHelpKeyword(resourceName);
            string message = MessageFormatter.Format(GetResourceString(resourceName), args);

            return MessageParser.TryParseMSBuildCode(message, out code, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        // Overloads with 0-3 arguments to avoid array allocations.

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringStripCodeAndKeyword(out string? code, out string? helpKeyword, string resourceName)
        {
            helpKeyword = GetHelpKeyword(resourceName);
            string message = GetResourceString(resourceName);

            return MessageParser.TryParseMSBuildCode(message, out code, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        /// </summary>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">Argument for formatting the resource string.</param>
        internal static string FormatResourceStringStripCodeAndKeyword(out string? code, out string? helpKeyword, string resourceName, object? arg1)
        {
            helpKeyword = GetHelpKeyword(resourceName);
            string message = MessageFormatter.Format(GetResourceString(resourceName), arg1);

            return MessageParser.TryParseMSBuildCode(message, out code, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        /// </summary>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">First argument for formatting the resource string.</param>
        /// <param name="arg2">Second argument for formatting the resource string.</param>
        internal static string FormatResourceStringStripCodeAndKeyword(out string? code, out string? helpKeyword, string resourceName, object? arg1, object? arg2)
        {
            helpKeyword = GetHelpKeyword(resourceName);
            string message = MessageFormatter.Format(GetResourceString(resourceName), arg1, arg2);

            return MessageParser.TryParseMSBuildCode(message, out code, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        /// <summary>
        /// Loads the specified string resource and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they too are returned.
        /// </summary>
        /// <param name="code">[out] The MSBuild message code, or null.</param>
        /// <param name="helpKeyword">[out] The MSBuild F1-help keyword for the host IDE, or null.</param>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">First argument for formatting the resource string.</param>
        /// <param name="arg2">Second argument for formatting the resource string.</param>
        /// <param name="arg3">Third argument for formatting the resource string.</param>
        internal static string FormatResourceStringStripCodeAndKeyword(out string? code, out string? helpKeyword, string resourceName, object? arg1, object? arg2, object? arg3)
        {
            helpKeyword = GetHelpKeyword(resourceName);
            string message = MessageFormatter.Format(GetResourceString(resourceName), arg1, arg2, arg3);

            return MessageParser.TryParseMSBuildCode(message, out code, out string? strippedMessage)
                ? strippedMessage
                : message;
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
        internal static string FormatResourceStringStripCodeAndKeyword(string resourceName, params object?[] args)
        {
            string message = MessageFormatter.Format(GetResourceString(resourceName), args);

            return MessageParser.TryStripMSBuildCode(message, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        // Overloads with 0-3 arguments to avoid array allocations.

        /// <summary>
        /// Looks up a string in the resources. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to load.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringStripCodeAndKeyword(string resourceName)
        {
            string message = GetResourceString(resourceName);

            return MessageParser.TryStripMSBuildCode(message, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        /// <summary>
        /// Looks up a string in the resources, and formats it with the argument passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">Argument for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringStripCodeAndKeyword(string resourceName, object? arg1)
        {
            string message = MessageFormatter.Format(GetResourceString(resourceName), arg1);

            return MessageParser.TryStripMSBuildCode(message, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        /// <summary>
        /// Looks up a string in the resources, and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">First argument for formatting the resource string.</param>
        /// <param name="arg2">Second argument for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringStripCodeAndKeyword(string resourceName, object? arg1, object? arg2)
        {
            string message = MessageFormatter.Format(GetResourceString(resourceName), arg1, arg2);

            return MessageParser.TryStripMSBuildCode(message, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        /// <summary>
        /// Looks up a string in the resources, and formats it with the arguments passed in. If the string resource has an MSBuild
        /// message code and help keyword associated with it, they are discarded.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">First argument for formatting the resource string.</param>
        /// <param name="arg2">Second argument for formatting the resource string.</param>
        /// <param name="arg3">Third argument for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringStripCodeAndKeyword(string resourceName, object? arg1, object? arg2, object? arg3)
        {
            string message = MessageFormatter.Format(GetResourceString(resourceName), arg1, arg2, arg3);

            return MessageParser.TryStripMSBuildCode(message, out string? strippedMessage)
                ? strippedMessage
                : message;
        }

        /// <summary>
        /// Formats the resource string with the given arguments.
        /// Ignores error codes and keywords.
        /// </summary>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="args">Optional arguments for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        /// <remarks>the AssemblyResources.GetString() method is thread-safe.</remarks>
        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resourceName, params object?[] args)
            => MessageFormatter.Format(GetResourceString(resourceName), args);

        // Overloads with 0-3 arguments to avoid array allocations.

        /// <summary>
        /// Formats the resource string.
        /// Ignores error codes and keywords.
        /// </summary>
        /// <param name="resourceName">Resource string to load.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resourceName)
            => GetResourceString(resourceName);

        /// <summary>
        /// Formats the resource string with the given argument.
        /// Ignores error codes and keywords.
        /// </summary>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">Argument for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resourceName, object? arg1)
            => MessageFormatter.Format(GetResourceString(resourceName), arg1);

        /// <summary>
        /// Formats the resource string with the given arguments.
        /// Ignores error codes and keywords.
        /// </summary>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">First argument for formatting the resource string.</param>
        /// <param name="arg2">Second argument for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resourceName, object? arg1, object? arg2)
            => MessageFormatter.Format(GetResourceString(resourceName), arg1, arg2);

        /// <summary>
        /// Formats the resource string with the given arguments.
        /// Ignores error codes and keywords.
        /// </summary>
        /// <param name="resourceName">Resource string to load.</param>
        /// <param name="arg1">First argument for formatting the resource string.</param>
        /// <param name="arg2">Second argument for formatting the resource string.</param>
        /// <param name="arg3">Third argument for formatting the resource string.</param>
        /// <returns>The formatted resource string.</returns>
        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resourceName, object? arg1, object? arg2, object? arg3)
            => MessageFormatter.Format(GetResourceString(resourceName), arg1, arg2, arg3);

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

                Assumed.NotNull(unformattedMessage, $"The resource string \"{resourceName}\" was not found.");
            }
            catch (ArgumentException e)
            {
#if FEATURE_DEBUG_LAUNCH
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
#endif
                InternalError.Throw(e.Message);
            }
            catch (InvalidOperationException e)
            {
#if FEATURE_DEBUG_LAUNCH
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
#endif
                InternalError.Throw(e.Message);
            }
            catch (MissingManifestResourceException e)
            {
#if FEATURE_DEBUG_LAUNCH
                Debug.Fail("The resource string \"" + resourceName + "\" was not found.");
#endif
                InternalError.Throw(e.Message);
            }
        }
    }
}
