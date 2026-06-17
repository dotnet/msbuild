// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
#if !NET
using System.Linq;
#endif
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using Microsoft.Win32;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

#if FEATURE_MSIOREDIST
// File is intentionally NOT aliased — all typeof() comparisons use fully-qualified
// System.IO.File to match the types registered in AvailableStaticMethods.
using Path = Microsoft.IO.Path;
#endif

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// Expands property expressions, like $(Configuration) and $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation).
    /// </summary>
    /// <remarks>
    /// This is a private nested class, exposed only through the Expander class.
    /// That allows it to hide its private methods even from Expander.
    /// </remarks>
    private static class PropertyExpander
    {
        /// <summary>
        /// This method takes a string which may contain any number of
        /// "$(propertyname)" tags in it.  It replaces all those tags with
        /// the actual property values, and returns a new string.  For example,
        ///
        ///     string processedString =
        ///         propertyBag.ExpandProperties("Value of NoLogo is $(NoLogo).");
        ///
        /// This code might produce:
        ///
        ///     processedString = "Value of NoLogo is true."
        ///
        /// If the sourceString contains an embedded property which doesn't
        /// have a value, then we replace that tag with an empty string.
        ///
        /// This method leaves the result escaped.  Callers may need to unescape on their own as appropriate.
        /// </summary>
        internal static string ExpandPropertiesLeaveEscaped(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            return
                ConvertToString(
                    ExpandPropertiesLeaveTypedAndEscaped(
                        expression,
                        properties,
                        options,
                        elementLocation,
                        propertiesUseTracker,
                        fileSystem));
        }

        /// <summary>
        /// This method takes a string which may contain any number of
        /// "$(propertyname)" tags in it.  It replaces all those tags with
        /// the actual property values, and returns a new string.  For example,
        ///
        ///     string processedString =
        ///         propertyBag.ExpandProperties("Value of NoLogo is $(NoLogo).");
        ///
        /// This code might produce:
        ///
        ///     processedString = "Value of NoLogo is true."
        ///
        /// If the sourceString contains an embedded property which doesn't
        /// have a value, then we replace that tag with an empty string.
        ///
        /// This method leaves the result typed and escaped.  Callers may need to convert to string, and unescape on their own as appropriate.
        /// </summary>
        internal static object ExpandPropertiesLeaveTypedAndEscaped(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            if (((options & ExpanderOptions.ExpandProperties) == 0) || String.IsNullOrEmpty(expression))
            {
                return expression;
            }

            Assumed.NotNull(properties, "Cannot expand properties without providing properties");

            // These are also zero-based indices into the expression, but
            // these tell us where the current property tag begins and ends.
            int propertyStartIndex, propertyEndIndex;

            // If there are no substitutions, then just return the string.
            propertyStartIndex = s_invariantCompareInfo.IndexOf(expression, "$(", CompareOptions.Ordinal);
            if (propertyStartIndex == -1)
            {
                return expression;
            }

            // We will build our set of results as object components
            // so that we can either maintain the object's type in the event
            // that we have a single component, or convert to a string
            // if concatenation is required.
            using Expander<P, I>.SpanBasedConcatenator results = new Expander<P, I>.SpanBasedConcatenator();

            // The sourceIndex is the zero-based index into the expression,
            // where we've essentially read up to and copied into the target string.
            int sourceIndex = 0;

            // Search for "$(" in the expression.  Loop until we don't find it
            // any more.
            while (propertyStartIndex != -1)
            {
                // Append the result with the portion of the expression up to
                // (but not including) the "$(", and advance the sourceIndex pointer.
                if (propertyStartIndex - sourceIndex > 0)
                {
                    results.Add(expression.AsMemory(sourceIndex, propertyStartIndex - sourceIndex));
                }

                // Following the "$(" we need to locate the matching ')'
                // Scan for the matching closing bracket, skipping any nested ones
                // This is a very complete, fast validation of parenthesis matching including for nested
                // function calls.
                propertyEndIndex = ScanForClosingParenthesis(expression.AsSpan(), propertyStartIndex + 2, out bool tryExtractPropertyFunction, out bool tryExtractRegistryFunction);

                if (propertyEndIndex == -1)
                {
                    // If we didn't find the closing parenthesis, that means this
                    // isn't really a well-formed property tag.  Just literally
                    // copy the remainder of the expression (starting with the "$("
                    // that we found) into the result, and quit.
                    results.Add(expression.AsMemory(propertyStartIndex, expression.Length - propertyStartIndex));
                    sourceIndex = expression.Length;
                }
                else
                {
                    // Aha, we found the closing parenthesis.  All the stuff in
                    // between the "$(" and the ")" constitutes the property body.
                    // Note: Current propertyStartIndex points to the "$", and
                    // propertyEndIndex points to the ")".  That's why we have to
                    // add 2 for the start of the substring, and subtract 2 for
                    // the length.
                    string propertyBody;

                    // A property value of null will indicate that we're calling a static function on a type
                    object propertyValue;

                    // Compat: $() should return String.Empty
                    if (propertyStartIndex + 2 == propertyEndIndex)
                    {
                        propertyValue = String.Empty;
                    }
                    else if ((expression.Length - (propertyStartIndex + 2)) > 9 && tryExtractRegistryFunction && s_invariantCompareInfo.IndexOf(expression, "Registry:", propertyStartIndex + 2, 9, CompareOptions.OrdinalIgnoreCase) == propertyStartIndex + 2)
                    {
                        propertyBody = expression.Substring(propertyStartIndex + 2, propertyEndIndex - propertyStartIndex - 2);

                        // If the property body starts with any of our special objects, then deal with them
                        // This is a registry reference, like $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)
                        propertyValue = ExpandRegistryValue(propertyBody, elementLocation); // This func returns an empty string if not on Windows
                    }

                    // Compat hack: as a special case, $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory) should return String.Empty
                    // In this case, tryExtractRegistryFunction will be false. Note that very few properties are exactly 77 chars, so this check should be fast.
                    else if ((propertyEndIndex - (propertyStartIndex + 2)) == 77 && s_invariantCompareInfo.IndexOf(expression, @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory", propertyStartIndex + 2, 77, CompareOptions.OrdinalIgnoreCase) == propertyStartIndex + 2)
                    {
                        propertyValue = String.Empty;
                    }

                    // Compat hack: WebProjects may have an import with a condition like:
                    //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
                    // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
                    // Be compatible by returning an empty string here.
                    else if ((propertyEndIndex - (propertyStartIndex + 2)) == 19 && String.Equals(expression, "$(Solutions.VSVersion)", StringComparison.Ordinal))
                    {
                        propertyValue = String.Empty;
                    }
                    else if (tryExtractPropertyFunction)
                    {
                        propertyBody = expression.Substring(propertyStartIndex + 2, propertyEndIndex - propertyStartIndex - 2);

                        // This is likely to be a function expression
                        propertyValue = ExpandPropertyBody(
                            propertyBody,
                            null,
                            properties,
                            options,
                            elementLocation,
                            propertiesUseTracker,
                            fileSystem);
                    }
                    else // This is a regular property
                    {
                        propertyValue = LookupProperty(properties, expression, propertyStartIndex + 2, propertyEndIndex - 1, elementLocation, propertiesUseTracker);
                    }

                    if (propertyValue != null)
                    {
                        if (IsTruncationEnabled(options))
                        {
                            var value = propertyValue.ToString();
                            if (value.Length > CharacterLimitPerExpansion)
                            {
                                propertyValue = TruncateString(value);
                            }
                        }

                        // Record our result, and advance
                        // our sourceIndex pointer to the character just after the closing
                        // parenthesis.
                        results.Add(propertyValue);
                    }
                    sourceIndex = propertyEndIndex + 1;
                }

                propertyStartIndex = s_invariantCompareInfo.IndexOf(expression, "$(", sourceIndex, CompareOptions.Ordinal);
            }

            // If we couldn't find any more property tags in the expression just copy the remainder into the result.
            if (expression.Length - sourceIndex > 0)
            {
                results.Add(expression.AsMemory(sourceIndex, expression.Length - sourceIndex));
            }

            return results.GetResult();
        }

        /// <summary>
        /// Expand the body of the property, including any functions that it may contain.
        /// </summary>
        internal static object ExpandPropertyBody(
            string propertyBody,
            object propertyValue,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            Function function = null;
            string propertyName = propertyBody;

            // Trim the body for compatibility reasons:
            // Spaces are not valid property name chars, but $( Foo ) is allowed, and should always expand to BLANK.
            // Do a very fast check for leading and trailing whitespace, and trim them from the property body if we have any.
            // But we will do a property name lookup on the propertyName that we held onto.
            if (Char.IsWhiteSpace(propertyBody[0]) || Char.IsWhiteSpace(propertyBody[propertyBody.Length - 1]))
            {
                propertyBody = propertyBody.Trim();
            }

            // If we don't have a clean propertybody then we'll do deeper checks to see
            // if what we have is a function
            if (!IsValidPropertyName(propertyBody))
            {
                if (propertyBody.Contains('.') || propertyBody[0] == '[')
                {
                    if (BuildParameters.DebugExpansion)
                    {
                        Console.WriteLine("Expanding: {0}", propertyBody);
                    }

                    // This is a function
                    function = Function.ExtractPropertyFunction(
                        propertyBody,
                        elementLocation,
                        propertyValue,
                        propertiesUseTracker,
                        fileSystem,
                        propertiesUseTracker.LoggingContext);

                    // We may not have been able to parse out a function
                    if (function != null)
                    {
                        // We will have either extracted the actual property name
                        // or realized that there is none (static function), and have recorded a null
                        propertyName = function.Receiver;
                    }
                    else
                    {
                        // In the event that we have been handed an unrecognized property body, throw
                        // an invalid function property exception.
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                        return null;
                    }
                }
                else if (propertyValue == null && propertyBody.Contains('[')) // a single property indexer
                {
                    int indexerStart = propertyBody.IndexOf('[');
                    int indexerEnd = propertyBody.IndexOf(']');

                    if (indexerStart < 0 || indexerEnd < 0)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                    }
                    else
                    {
                        propertyValue = LookupProperty(properties, propertyBody, 0, indexerStart - 1, elementLocation, propertiesUseTracker);
                        propertyBody = propertyBody.Substring(indexerStart);

                        // recurse so that the function representing the indexer can be executed on the property value
                        return ExpandPropertyBody(
                            propertyBody,
                            propertyValue,
                            properties,
                            options,
                            elementLocation,
                            propertiesUseTracker,
                            fileSystem);
                    }
                }
                else
                {
                    // In the event that we have been handed an unrecognized property body, throw
                    // an invalid function property exception.
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                    return null;
                }
            }

            // Find the property value in our property collection.  This
            // will automatically return "" (empty string) if the property
            // doesn't exist in the collection, and we're not executing a static function
            if (!String.IsNullOrEmpty(propertyName))
            {
                propertyValue = LookupProperty(properties, propertyName, elementLocation, propertiesUseTracker);
            }

            if (function != null)
            {
                try
                {
                    // Because of the rich expansion capabilities of MSBuild, we need to keep things
                    // as strings, since property expansion & string embedding can happen anywhere
                    // propertyValue can be null here, when we're invoking a static function
                    propertyValue = function.Execute(propertyValue, properties, options, elementLocation);
                }
                catch (Exception) when (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    propertyValue = propertyBody;
                }
            }

            return propertyValue;
        }

        /// <summary>
        /// Convert the object into an MSBuild friendly string
        /// Arrays are supported.
        /// Will not return NULL.
        /// </summary>
        internal static string ConvertToString(object valueToConvert)
        {
            if (valueToConvert == null)
            {
                return String.Empty;
            }
            // If the value is a string, then there is nothing to do
            if (valueToConvert is string stringValue)
            {
                return stringValue;
            }

            string convertedString;
            if (valueToConvert is IDictionary dictionary)
            {
                // If the return type is an IDictionary, then we convert this to
                // a semi-colon delimited set of A=B pairs.
                // Key and Value are converted to string and escaped
                if (dictionary.Count > 0)
                {
                    using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(";");
                        }

                        // convert and escape each key and value in the dictionary entry
                        builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Key)));
                        builder.Append("=");
                        builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Value)));
                    }

                    convertedString = builder.ToString();
                }
                else
                {
                    convertedString = string.Empty;
                }
            }
            else if (valueToConvert is IEnumerable enumerable)
            {
                // If the return is enumerable, then we'll convert to semi-colon delimited elements
                // each of which must be converted, so we'll recurse for each element
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

                foreach (object element in enumerable)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(";");
                    }

                    // we need to convert and escape each element of the array
                    builder.Append(EscapingUtilities.Escape(ConvertToString(element)));
                }

                convertedString = builder.ToString();
            }
            else
            {
                // The fall back is always to just convert to a string directly.
                // Issue: https://github.com/dotnet/msbuild/issues/9757
                if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12))
                {
                    convertedString = Convert.ToString(valueToConvert, CultureInfo.InvariantCulture);
                }
                else
                {
                    convertedString = valueToConvert.ToString();
                }
            }

            return convertedString;
        }

        /// <summary>
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
        /// </summary>
        private static object LookupProperty(IPropertyProvider<P> properties, string propertyName, IElementLocation elementLocation, PropertiesUseTracker propertiesUseTracker)
        {
            return LookupProperty(properties, propertyName, 0, propertyName.Length - 1, elementLocation, propertiesUseTracker);
        }

        /// <summary>
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
        /// </summary>
        private static object LookupProperty(IPropertyProvider<P> properties, string propertyName, int startIndex, int endIndex, IElementLocation elementLocation, PropertiesUseTracker propertiesUseTracker)
        {
            P property = properties.GetProperty(propertyName, startIndex, endIndex);

            object propertyValue;

            bool isArtificial = property == null && ((endIndex - startIndex) >= 7) &&
                               MSBuildNameIgnoreCaseComparer.Default.Equals("MSBuild", propertyName, startIndex, 7);

            propertiesUseTracker.TrackRead(propertyName, startIndex, endIndex, elementLocation, property == null, isArtificial);

            if (isArtificial)
            {
                // It could be one of the MSBuildThisFileXXXX properties,
                // whose values vary according to the file they are in.
                if (startIndex != 0 || endIndex != propertyName.Length)
                {
                    propertyValue = ExpandMSBuildThisFileProperty(propertyName.Substring(startIndex, endIndex - startIndex + 1), elementLocation);
                }
                else
                {
                    propertyValue = ExpandMSBuildThisFileProperty(propertyName, elementLocation);
                }
            }
            else if (property == null)
            {
                propertyValue = String.Empty;
            }
            else
            {
                if (property is ProjectPropertyInstance.EnvironmentDerivedProjectPropertyInstance environmentDerivedProperty)
                {
                    environmentDerivedProperty.loggingContext = propertiesUseTracker.LoggingContext;
                }

                propertyValue = property.GetEvaluatedValueEscaped(elementLocation);
            }

            return propertyValue;
        }

        /// <summary>
        /// If the property name provided is one of the special
        /// per file properties named "MSBuildThisFileXXXX" then returns the value of that property.
        /// If the location provided does not have a path (eg., if it comes from a file that has
        /// never been saved) then returns empty string.
        /// If the property name is not one of those properties, returns empty string.
        /// </summary>
        private static object ExpandMSBuildThisFileProperty(string propertyName, IElementLocation elementLocation)
        {
            if (!ReservedPropertyNames.IsReservedProperty(propertyName))
            {
                return String.Empty;
            }

            if (elementLocation.File.Length == 0)
            {
                return String.Empty;
            }

            string value = String.Empty;

            // Because String.Equals checks the length first, and these strings are almost
            // all different lengths, this sequence is efficient.
            if (String.Equals(propertyName, ReservedPropertyNames.thisFile, StringComparison.OrdinalIgnoreCase))
            {
                value = Path.GetFileName(elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileName, StringComparison.OrdinalIgnoreCase))
            {
                value = Path.GetFileNameWithoutExtension(elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileFullPath, StringComparison.OrdinalIgnoreCase))
            {
                value = FileUtilities.NormalizePath(elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                value = Path.GetExtension(elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileDirectory, StringComparison.OrdinalIgnoreCase))
            {
                value = FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(elementLocation.File));
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileDirectoryNoRoot, StringComparison.OrdinalIgnoreCase))
            {
                string directory = Path.GetDirectoryName(elementLocation.File);
                int rootLength = Path.GetPathRoot(directory).Length;
                value = FileUtilities.EnsureTrailingNoLeadingSlash(directory, rootLength);
            }

            return value;
        }

        /// <summary>
        /// Given a string like "Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation", return the value at that location
        /// in the registry. If the value isn't found, returns String.Empty.
        /// Properties may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>
        private static string ExpandRegistryValue(string registryExpression, IElementLocation elementLocation)
        {
#if RUNTIME_TYPE_NETCORE
            // .NET Core MSBuild used to always return empty, so match that behavior
            // on non-Windows (no registry).
            if (!NativeMethodsShared.IsWindows)
            {
                return string.Empty;
            }
#endif

            // Remove "Registry:" prefix
            string registryLocation = registryExpression.Substring(9);

            // Split off the value name -- the part after the "@" sign. If there's no "@" sign, then it's the default value name
            // we want.
            int firstAtSignOffset = registryLocation.IndexOf('@');
            int lastAtSignOffset = registryLocation.LastIndexOf('@');

            ProjectErrorUtilities.VerifyThrowInvalidProject(firstAtSignOffset == lastAtSignOffset, elementLocation, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", String.Empty);

            string valueName = lastAtSignOffset == -1 || lastAtSignOffset == registryLocation.Length - 1
                ? null : registryLocation.Substring(lastAtSignOffset + 1);

            // If there's no '@', or '@' is first, then we'll use null or String.Empty for the location; otherwise
            // the location is the part before the '@'
            string registryKeyName = lastAtSignOffset != -1 ? registryLocation.Substring(0, lastAtSignOffset) : registryLocation;

            string result = String.Empty;
            if (registryKeyName != null)
            {
                // We rely on the '@' character to delimit the key and its value, but the registry
                // allows this character to be used in the names of keys and the names of values.
                // Hence we use our standard escaping mechanism to allow users to access such keys
                // and values.
                registryKeyName = EscapingUtilities.UnescapeAll(registryKeyName);

                if (valueName != null)
                {
                    valueName = EscapingUtilities.UnescapeAll(valueName);
                }

                try
                {
                    // Unless we are running under Windows, don't bother with anything but the user keys
                    if (!NativeMethodsShared.IsWindows && !registryKeyName.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fake common requests to HKLM that we can resolve

                        // This is the base path of the framework
                        if (registryKeyName.StartsWith(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework",
                            StringComparison.OrdinalIgnoreCase) &&
                            valueName.Equals("InstallRoot", StringComparison.OrdinalIgnoreCase))
                        {
                            return NativeMethodsShared.FrameworkBasePath + Path.DirectorySeparatorChar;
                        }

                        return string.Empty;
                    }

                    object valueFromRegistry = Registry.GetValue(registryKeyName, valueName, null /* default if key or value name is not found */);

                    if (valueFromRegistry != null)
                    {
                        // Convert the result to a string that is reasonable for MSBuild
                        result = ConvertToString(valueFromRegistry);
                    }
                    else
                    {
                        // This means either the key or value was not found in the registry.  In this case,
                        // we simply expand the property value to String.Empty to imitate the behavior of
                        // normal properties.
                        result = String.Empty;
                    }
                }
                catch (Exception ex) when (!ExceptionHandling.NotExpectedRegistryException(ex))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidRegistryPropertyExpression", $"$({registryExpression})", ex.Message);
                }
            }

            return result;
        }
    }
}
