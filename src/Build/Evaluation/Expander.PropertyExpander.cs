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
using Microsoft.Build.Evaluation.Context;
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
    /// This is a private nested type, exposed only through the Expander class.
    /// That allows it to hide its private methods even from Expander.
    /// </remarks>
    private readonly ref struct PropertyExpander
    {
        private const string RegistryPrefix = "Registry:";
        private const string SolutionsVsVersionProperty = "Solutions.VSVersion";
        private const string SolutionsVsVersionExpression = "$(" + SolutionsVsVersionProperty + ")";
        private const string VstsDbDirectoryProperty = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory";

        private readonly IPropertyProvider<P> _properties;
        private readonly ExpanderOptions _options;
        private readonly IElementLocation _elementLocation;
        private readonly PropertiesUseTracker _propertiesUseTracker;
        private readonly IFileSystem _fileSystem;
        private readonly bool _isTruncationEnabled;

        private PropertyExpander(
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            _properties = properties;
            _options = options;
            _elementLocation = elementLocation;
            _propertiesUseTracker = propertiesUseTracker;
            _fileSystem = fileSystem;
            _isTruncationEnabled = IsTruncationEnabled(options);
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

            // If there are no substitutions, then just return the string.
            int markerIndex = ExpressionShredder.IndexOfPropertyMarker(expression);
            if (markerIndex == -1)
            {
                return expression;
            }

            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.ExpandPropertiesLeaveTypedAndEscaped(expression, markerIndex);
        }

        private object ExpandPropertiesLeaveTypedAndEscaped(string expression, int markerIndex)
        {
            // We will build our set of results as object components
            // so that we can either maintain the object's type in the event
            // that we have a single component, or convert to a string
            // if concatenation is required.
            using SpanBasedConcatenator results = new();

            // The index is the zero-based index into the expression,
            // where we've essentially read up to and copied into the target string.
            int index = 0;

            // Search for "$(" in the expression.  Loop until we don't find it any more.
            while (markerIndex >= 0)
            {
                // Append the result with the portion of the expression up to
                // (but not including) the "$(", and advance the index pointer.
                if (markerIndex - index > 0)
                {
                    results.Add(expression.AsMemory(index, markerIndex - index));
                }

                int startIndex = markerIndex + 2;

                // Following the "$(" we need to locate the matching ')'
                // Scan for the matching closing bracket, skipping any nested ones
                // This is a very complete, fast validation of parenthesis matching including for nested
                // function calls.
                int closingParenIndex = FindClosingParenthesis(
                    expression,
                    startIndex,
                    out bool isPotentialPropertyFunction,
                    out bool isPotentialRegistryFunction);

                if (closingParenIndex < 0)
                {
                    // If we didn't find the closing parenthesis, that means this
                    // isn't really a well-formed property. Copy the remainder of the
                    // expression (starting with the "$(" that we found) into the result, and return.
                    results.Add(expression.AsMemory(markerIndex));
                    return results.GetResult();
                }

                // Expand the property body between the "$(" marker and its matching closing parenthesis.
                int endIndex = closingParenIndex - 1;
                int length = closingParenIndex - startIndex;

                object propertyValue;

                if (length == 0)
                {
                    // Compat: $() should return string.Empty
                    propertyValue = string.Empty;
                }
                else if (!isPotentialPropertyFunction && !isPotentialRegistryFunction)
                {
                    propertyValue = LookupProperty(expression, startIndex, endIndex);
                }
                else
                {
                    propertyValue = ExpandProperty(
                        expression,
                        startIndex,
                        endIndex,
                        isPotentialRegistryFunction,
                        isPotentialPropertyFunction);
                }

                if (propertyValue != null)
                {
                    if (_isTruncationEnabled)
                    {
                        string value = propertyValue.ToString();
                        if (value.Length > CharacterLimitPerExpansion)
                        {
                            propertyValue = TruncateString(value);
                        }
                    }

                    results.Add(propertyValue);
                }

                index = closingParenIndex + 1;
                markerIndex = ExpressionShredder.IndexOfPropertyMarker(expression, index);
            }

            // If we couldn't find any more property markers in the expression just copy the remainder into the result.
            if (expression.Length - index > 0)
            {
                results.Add(expression.AsMemory(index));
            }

            return results.GetResult();
        }

        /// <summary>
        ///  Finds the closing parenthesis that matches the opening parenthesis immediately
        ///  preceding <paramref name="index"/>.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="index">The index at which to begin scanning.</param>
        /// <param name="isPotentialPropertyFunction">
        ///  Whether the property body might contain a property function.
        /// </param>
        /// <param name="isPotentialRegistryFunction">
        ///  Whether the property body might contain a registry function.
        /// </param>
        /// <returns>
        ///  The index of the matching closing parenthesis, or <c>-1</c> if it was not found.
        /// </returns>
        private static int FindClosingParenthesis(
            string expression,
            int index,
            out bool isPotentialPropertyFunction,
            out bool isPotentialRegistryFunction)
        {
            int nestLevel = 1;
            int length = expression.Length;

            isPotentialPropertyFunction = false;
            isPotentialRegistryFunction = false;

            // Scan for our closing ')'
            while (index < length && nestLevel > 0)
            {
                char character = expression[index];

                switch (character)
                {
                    case '\'' or '`' or '"':
                        int quoteIndex = expression.IndexOf(character, index + 1);

                        if (quoteIndex < 0)
                        {
                            return -1;
                        }

                        index = quoteIndex;
                        break;

                    case '(':
                        nestLevel++;
                        break;

                    case ')':
                        nestLevel--;
                        break;

                    case '.' or '[' or '$':
                        isPotentialPropertyFunction = true;
                        break;

                    case ':':
                        isPotentialRegistryFunction = true;
                        break;
                }

                index++;
            }

            // We will have parsed past the ')', so step back one character.
            return nestLevel == 0 ? index - 1 : -1;
        }

        private object ExpandProperty(
            string expression,
            int startIndex,
            int endIndex,
            bool tryExtractRegistryFunction,
            bool tryExtractPropertyFunction)
        {
            // startIndex and endIndex inclusively delimit the property body.
            int length = endIndex - startIndex + 1;

            // Compat hack: as a special case, $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory) should return string.Empty
            // Note that very few properties have this exact length, so this check should be fast.
            if (length == VstsDbDirectoryProperty.Length &&
                string.Compare(expression, startIndex, VstsDbDirectoryProperty, 0, VstsDbDirectoryProperty.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return string.Empty;
            }

            // Compat hack: WebProjects may have an import with a condition like:
            //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
            // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
            // Be compatible by returning an empty string here.
            if (length == SolutionsVsVersionProperty.Length &&
                string.Equals(expression, SolutionsVsVersionExpression, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (tryExtractRegistryFunction &&
                length >= RegistryPrefix.Length &&
                string.Compare(expression, startIndex, RegistryPrefix, 0, RegistryPrefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                // If the property body starts with any of our special objects, then deal with them
                // This is a registry reference, like $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)
                // Note: ExpandRegistryValue returns an empty string if not on Windows.
                return ExpandRegistryValue(
                    expression.Substring(startIndex, length));
            }

            if (tryExtractPropertyFunction)
            {
                // This is likely to be a function expression
                return ExpandPropertyBody(
                    expression.Substring(startIndex, length),
                    propertyValue: null);
            }

            // This is a regular property
            return LookupProperty(expression, startIndex, endIndex);
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
            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.ExpandPropertyBody(propertyBody, propertyValue);
        }

        private object ExpandPropertyBody(string propertyBody, object propertyValue)
        {
            Function function = null;
            string propertyName = propertyBody;

            // Trim the body for compatibility reasons:
            // Spaces are not valid property name chars, but $( Foo ) is allowed, and should always expand to BLANK.
            // Do a very fast check for leading and trailing whitespace, and trim them from the property body if we have any.
            // But we will do a property name lookup on the propertyName that we held onto.
            if (char.IsWhiteSpace(propertyBody[0]) || char.IsWhiteSpace(propertyBody[^1]))
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
                        _elementLocation,
                        propertyValue,
                        _propertiesUseTracker,
                        _fileSystem,
                        _propertiesUseTracker.LoggingContext);

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
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                        return null;
                    }
                }
                else if (propertyValue == null && propertyBody.Contains('[')) // a single property indexer
                {
                    int indexerStart = propertyBody.IndexOf('[');
                    int indexerEnd = propertyBody.IndexOf(']');

                    if (indexerStart < 0 || indexerEnd < 0)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                    }
                    else
                    {
                        propertyValue = LookupProperty(propertyBody, 0, indexerStart - 1);
                        propertyBody = propertyBody.Substring(indexerStart);

                        // recurse so that the function representing the indexer can be executed on the property value
                        return ExpandPropertyBody(propertyBody, propertyValue);
                    }
                }
                else
                {
                    // In the event that we have been handed an unrecognized property body, throw
                    // an invalid function property exception.
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, string.Empty);
                    return null;
                }
            }

            // Find the property value in our property collection.  This
            // will automatically return "" (empty string) if the property
            // doesn't exist in the collection, and we're not executing a static function
            if (!string.IsNullOrEmpty(propertyName))
            {
                propertyValue = LookupProperty(propertyName);
            }

            if (function != null)
            {
                try
                {
                    // Because of the rich expansion capabilities of MSBuild, we need to keep things
                    // as strings, since property expansion & string embedding can happen anywhere
                    // propertyValue can be null here, when we're invoking a static function
                    propertyValue = function.Execute(propertyValue, _properties, _options, _elementLocation);
                }
                catch (Exception) when (_options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
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
                convertedString = Convert.ToString(valueToConvert, CultureInfo.InvariantCulture);
            }

            return convertedString;
        }

        /// <summary>
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
        /// </summary>
        private object LookupProperty(string propertyName)
        {
            return LookupProperty(propertyName, 0, propertyName.Length - 1);
        }

        /// <summary>
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
        /// </summary>
        private object LookupProperty(string propertyName, int startIndex, int endIndex)
        {
            P property = _properties.GetProperty(propertyName, startIndex, endIndex);

            object propertyValue;

            bool isArtificial = property == null && ((endIndex - startIndex) >= 7) &&
                               MSBuildNameIgnoreCaseComparer.Default.Equals("MSBuild", propertyName, startIndex, 7);

            _propertiesUseTracker.TrackRead(propertyName, startIndex, endIndex, _elementLocation, property == null, isArtificial);

            if (isArtificial)
            {
                // It could be one of the MSBuildThisFileXXXX properties,
                // whose values vary according to the file they are in.
                if (startIndex != 0 || endIndex != propertyName.Length)
                {
                    propertyValue = ExpandMSBuildThisFileProperty(propertyName.Substring(startIndex, endIndex - startIndex + 1));
                }
                else
                {
                    propertyValue = ExpandMSBuildThisFileProperty(propertyName);
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
                    environmentDerivedProperty.loggingContext = _propertiesUseTracker.LoggingContext;
                }

                propertyValue = property.GetEvaluatedValueEscaped(_elementLocation);
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
        private object ExpandMSBuildThisFileProperty(string propertyName)
        {
            if (!ReservedPropertyNames.IsReservedProperty(propertyName))
            {
                return String.Empty;
            }

            if (_elementLocation.File.Length == 0)
            {
                return String.Empty;
            }

            string value = String.Empty;

            // Because String.Equals checks the length first, and these strings are almost
            // all different lengths, this sequence is efficient.
            if (String.Equals(propertyName, ReservedPropertyNames.thisFile, StringComparison.OrdinalIgnoreCase))
            {
                value = Path.GetFileName(_elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileName, StringComparison.OrdinalIgnoreCase))
            {
                value = Path.GetFileNameWithoutExtension(_elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileFullPath, StringComparison.OrdinalIgnoreCase))
            {
                value = FileUtilities.NormalizePath(_elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                value = Path.GetExtension(_elementLocation.File);
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileDirectory, StringComparison.OrdinalIgnoreCase))
            {
                value = FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(_elementLocation.File));
            }
            else if (String.Equals(propertyName, ReservedPropertyNames.thisFileDirectoryNoRoot, StringComparison.OrdinalIgnoreCase))
            {
                string directory = Path.GetDirectoryName(_elementLocation.File);
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
        private string ExpandRegistryValue(string registryExpression)
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
            string registryLocation = registryExpression.Substring(RegistryPrefix.Length);

            // Split off the value name -- the part after the "@" sign. If there's no "@" sign, then it's the default value name
            // we want.
            int firstAtSignOffset = registryLocation.IndexOf('@');
            int lastAtSignOffset = registryLocation.LastIndexOf('@');

            ProjectErrorUtilities.VerifyThrowInvalidProject(firstAtSignOffset == lastAtSignOffset, _elementLocation, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", String.Empty);

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
                    EvaluationObservationSession.Current?.RecordExternalInput(
                        EvaluationExternalInputKind.Registry,
                        "RegistryPropertyExpression",
                        registryExpression,
                        string.Concat("<failed:", ex.GetType().FullName, ">"));
                    EvaluationObservationSession.Current?.RecordOperationFailure(
                        EvaluationObservationCategory.Registry,
                        "RegistryPropertyExpression",
                        path: null,
                        provider: null,
                        ex);
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidRegistryPropertyExpression", $"$({registryExpression})", ex.Message);
                }
            }

            EvaluationObservationSession.Current?.RecordExternalInput(
                EvaluationExternalInputKind.Registry,
                "RegistryPropertyExpression",
                registryExpression,
                result);
            return result;
        }
    }
}
