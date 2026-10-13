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
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using Microsoft.Win32;

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
        private const string SolutionsVsVersionExpression = "$(Solutions.VSVersion)";
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
                int length = closingParenIndex - startIndex;

                object propertyValue;

                if (length == 0)
                {
                    // Compat: $() should return string.Empty
                    propertyValue = string.Empty;
                }
                else if (!isPotentialPropertyFunction && !isPotentialRegistryFunction)
                {
                    propertyValue = LookupProperty(expression, startIndex, length);
                }
                else
                {
                    propertyValue = ExpandProperty(
                        expression,
                        startIndex,
                        length,
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
            int length,
            bool tryExtractRegistryFunction,
            bool tryExtractPropertyFunction)
        {
            // Compat hack: as a special case, $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory) should return string.Empty
            // Note that very few properties have this exact length, so this check should be fast.
            if (length == VstsDbDirectoryProperty.Length &&
                string.Equals(expression, startIndex, VstsDbDirectoryProperty, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // Compat hack: WebProjects may have an import with a condition like:
            //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
            // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
            // Be compatible by returning an empty string here.
            // The property body starts at index 2 only when the "$(" marker starts at index 0.
            if (startIndex == 2 &&
                expression.Length == SolutionsVsVersionExpression.Length &&
                string.Equals(expression, SolutionsVsVersionExpression, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (tryExtractRegistryFunction &&
                TryExpandRegistryValue(expression, startIndex, length, out string registryValue))
            {
                return registryValue;
            }

            if (tryExtractPropertyFunction)
            {
                // This is likely to be a function expression
                return ExpandPropertyBody(
                    expression.Substring(startIndex, length),
                    propertyValue: null);
            }

            // This is a regular property
            return LookupProperty(expression, startIndex, length);
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
                        propertyValue = LookupProperty(propertyBody, startIndex: 0, length: indexerStart);
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
        ///  Looks up a simple property by name.
        /// </summary>
        /// <param name="propertyName">The property name to look up.</param>
        /// <returns>
        ///  The escaped property value, or an empty string if the property is not defined.
        /// </returns>
        private string LookupProperty(string propertyName)
            => LookupProperty(propertyName, 0, propertyName.Length);

        /// <summary>
        ///  Looks up a simple property whose name is contained within a segment of a string.
        /// </summary>
        /// <param name="propertyName">The string containing the property name.</param>
        /// <param name="startIndex">The zero-based start index of the property name.</param>
        /// <param name="length">The length of the property name.</param>
        /// <returns>
        ///  The escaped property value, or an empty string if the property is not defined.
        /// </returns>
        private string LookupProperty(string propertyName, int startIndex, int length)
        {
            P property = _properties.GetProperty(propertyName, startIndex, endIndex: startIndex + length - 1);

            if (property is null)
            {
                // MSBuildThisFile* properties are synthesized from the current file rather than
                // stored in the property collection. Other undefined MSBuild* names also remain
                // synthetic and must not be tracked as uninitialized properties.
                if (ReservedPropertyNames.StartsWithMSBuild(propertyName, startIndex, length))
                {
                    return ExpandMSBuildThisFileProperty(propertyName, startIndex, length, _elementLocation.File);
                }

                // An ordinary undefined property expands to an empty string and is tracked so a
                // later assignment can report that the property was read before initialization.
                _propertiesUseTracker.TrackRead(propertyName, startIndex, length, _elementLocation, isUninitialized: true);
                return string.Empty;
            }

            // Resolved properties are tracked as initialized reads.
            _propertiesUseTracker.TrackRead(propertyName, startIndex, length, _elementLocation, isUninitialized: false);

            if (property is ProjectPropertyInstance.EnvironmentDerivedProjectPropertyInstance environmentDerivedProperty)
            {
                // Environment-derived properties defer logging until their value is requested.
                environmentDerivedProperty.loggingContext = _propertiesUseTracker.LoggingContext;
            }

            return property.GetEvaluatedValueEscaped(_elementLocation);
        }

        /// <summary>
        ///  Expands an <c>MSBuildThisFile*</c> property whose name is contained within a segment
        ///  of a string.
        /// </summary>
        /// <param name="propertyName">The string containing the property name.</param>
        /// <param name="startIndex">The zero-based start index of the property name.</param>
        /// <param name="length">The length of the property name.</param>
        /// <param name="file">The path of the project or imported file being evaluated.</param>
        /// <returns>
        ///  The expanded property value, or an empty string if the segment does not identify an
        ///  <c>MSBuildThisFile*</c> property or <paramref name="file"/> is empty.
        /// </returns>
        private static string ExpandMSBuildThisFileProperty(string propertyName, int startIndex, int length, string file)
        {
            if (file.Length == 0 ||
                !ReservedPropertyNames.TryGetKind(propertyName, startIndex, length, out ReservedPropertyKind propertyKind))
            {
                return string.Empty;
            }

            switch (propertyKind)
            {
                case ReservedPropertyKind.ThisFile:
                    return Path.GetFileName(file);

                case ReservedPropertyKind.ThisFileName:
                    return Path.GetFileNameWithoutExtension(file);

                case ReservedPropertyKind.ThisFileFullPath:
                    return FileUtilities.NormalizePath(file);

                case ReservedPropertyKind.ThisFileExtension:
                    return Path.GetExtension(file);

                case ReservedPropertyKind.ThisFileDirectory:
                    return FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(file));

                case ReservedPropertyKind.ThisFileDirectoryNoRoot:
                    string directory = Path.GetDirectoryName(file);
                    int rootLength = Path.GetPathRoot(directory).Length;
                    return FileUtilities.EnsureTrailingNoLeadingSlash(directory, rootLength);

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        ///  Attempts to expand a registry expression contained within a segment of a string.
        /// </summary>
        /// <param name="expression">The string containing the registry expression.</param>
        /// <param name="startIndex">The zero-based start index of the registry expression.</param>
        /// <param name="length">The length of the registry expression.</param>
        /// <param name="result">The expanded registry value when the segment is a registry expression.</param>
        /// <returns>
        ///  <see langword="true"/> if the segment is a registry expression; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Registry expressions use syntax such as
        ///  <c>Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation</c>. The value name
        ///  and preceding <c>@</c> may be omitted to request the default value.
        /// </remarks>
        private bool TryExpandRegistryValue(string expression, int startIndex, int length, out string result)
        {
            // False means that the segment is not a registry expression. Once the prefix is
            // recognized, the method either returns true with a value or throws for invalid input.
            if (length < RegistryPrefix.Length ||
                !string.Equals(expression, startIndex, RegistryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result = null;
                return false;
            }

#if RUNTIME_TYPE_NETCORE
            // .NET Core MSBuild used to always return empty, so match that behavior
            // on non-Windows (no registry).
            if (!NativeMethodsShared.IsWindows)
            {
                result = string.Empty;
                return true;
            }
#endif

            // The text after "Registry:" contains a key and an optional value name separated by
            // '@'. Omitting '@', or placing it last, requests the key's default value.
            int keyNameStartIndex = startIndex + RegistryPrefix.Length;
            int registryLength = length - RegistryPrefix.Length;
            int firstAtSignIndex = expression.IndexOf('@', keyNameStartIndex, registryLength);

            string keyName;
            string valueName;

            if (firstAtSignIndex >= 0)
            {
                int keyNameLength = firstAtSignIndex - keyNameStartIndex;
                int valueNameLength = registryLength - keyNameLength - 1;

                // A literal second '@' is ambiguous. Registry keys and value names containing '@'
                // must use MSBuild escaping and are unescaped below.
                if (valueNameLength > 0 && expression.IndexOf('@', firstAtSignIndex + 1, valueNameLength) >= 0)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        _elementLocation,
                        "InvalidRegistryPropertyExpression",
                        $"$({expression.Substring(startIndex, length)})",
                        string.Empty);
                }

                // A trailing '@' has no value-name text and therefore selects the default value.
                keyName = expression.Substring(keyNameStartIndex, keyNameLength);
                valueName = valueNameLength > 0
                    ? expression.Substring(firstAtSignIndex + 1, valueNameLength)
                    : null;
            }
            else
            {
                // Without a delimiter, the entire remaining segment is the key name.
                keyName = expression.Substring(keyNameStartIndex, registryLength);
                valueName = null;
            }

            // We rely on the '@' character to delimit the key and its value, but the registry
            // allows this character to be used in the names of keys and the names of values.
            // Hence we use our standard escaping mechanism to allow users to access such keys
            // and values.
            keyName = EscapingUtilities.UnescapeAll(keyName);

            if (valueName != null)
            {
                valueName = EscapingUtilities.UnescapeAll(valueName);
            }

            try
            {
                // Full-framework MSBuild can run on non-Windows runtimes such as Mono. Avoid
                // registry access there except for supported user keys and the compatibility case.
                if (!NativeMethodsShared.IsWindows && !keyName.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                {
                    // Fake common requests to HKLM that we can resolve

                    // This is the base path of the framework
                    if (keyName.StartsWith(@"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework", StringComparison.OrdinalIgnoreCase) &&
                        valueName.Equals("InstallRoot", StringComparison.OrdinalIgnoreCase))
                    {
                        result = NativeMethodsShared.FrameworkBasePath + Path.DirectorySeparatorChar;
                        return true;
                    }

                    result = string.Empty;
                    return true;
                }

                object value = Registry.GetValue(keyName, valueName, defaultValue: null);
                result = ConvertToString(value);
                return true;
            }
            catch (Exception ex) when (!ExceptionHandling.NotExpectedRegistryException(ex))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    _elementLocation,
                    "InvalidRegistryPropertyExpression",
                    $"$({expression.Substring(startIndex, length)})",
                    ex.Message);
            }

            result = null;
            return false;
        }
    }
}
