// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
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
    private readonly ref partial struct PropertyExpander
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
        ///  Expands property references in an expression and converts the result to a string.
        /// </summary>
        /// <param name="expression">The expression containing property references of the form <c>$(Name)</c>.</param>
        /// <param name="properties">The provider used to resolve property values.</param>
        /// <param name="options">The options controlling property expansion and truncation.</param>
        /// <param name="elementLocation">The location used for diagnostics and property-read tracking.</param>
        /// <param name="propertiesUseTracker">The tracker notified when properties are read.</param>
        /// <param name="fileSystem">The file system used by property functions.</param>
        /// <returns>
        ///  The expanded expression as an escaped string. Undefined properties and property functions
        ///  returning <see langword="null"/> contribute an empty string.
        /// </returns>
        /// <remarks>
        ///  The returned value remains escaped and may require unescaping by the caller.
        /// </remarks>
        internal static string ExpandPropertiesLeaveEscaped(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            if ((options & ExpanderOptions.ExpandProperties) == 0 || expression.IsNullOrEmpty())
            {
                return expression;
            }

            Assumed.NotNull(properties, "Cannot expand properties without providing properties");

            // Find and classify the first property reference before constructing the expander.
            int markerIndex = ExpressionShredder.IndexOfPropertyMarker(expression);

            if (markerIndex == -1 ||
                !TryGetPropertyInfo(expression, markerIndex, out int bodyLength, out PropertyReferenceFlags flags))
            {
                return expression;
            }

            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);

            object result = markerIndex == 0 && (flags & PropertyReferenceFlags.EndsExpression) != 0
                ? expander.ExpandSingleProperty(expression, flags)
                : expander.ExpandPropertyValues(expression, markerIndex, bodyLength, flags);

            return PropertyValueConverter.ToString(result);
        }

        /// <summary>
        ///  Expands property references in a property-function argument while preserving the type of
        ///  a single whole-expression value.
        /// </summary>
        /// <param name="expression">The expression containing property references of the form <c>$(Name)</c>.</param>
        /// <param name="properties">The provider used to resolve property values.</param>
        /// <param name="options">
        ///  The options controlling property expansion and truncation. This value must include
        ///  <see cref="ExpanderOptions.ExpandProperties"/>.
        /// </param>
        /// <param name="elementLocation">The location used for diagnostics and property-read tracking.</param>
        /// <param name="propertiesUseTracker">The tracker notified when properties are read.</param>
        /// <param name="fileSystem">The file system used by property functions.</param>
        /// <returns>
        ///  The typed expanded value when <paramref name="expression"/> consists of one property reference;
        ///  otherwise, the concatenated escaped string. Undefined properties and property functions returning
        ///  <see langword="null"/> contribute an empty string.
        /// </returns>
        /// <remarks>
        ///  String results remain escaped and may require unescaping by the caller. Expressions that
        ///  do not contain a property marker are returned unchanged.
        /// </remarks>
        internal static object ExpandFunctionArgumentProperties(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            // Function.Execute guarantees that property expansion is enabled for its arguments.
            Debug.Assert((options & ExpanderOptions.ExpandProperties) != 0, $"{ExpanderOptions.ExpandProperties} must be set.");

            if (expression.IsNullOrEmpty())
            {
                return expression;
            }

            Assumed.NotNull(properties, "Cannot expand properties without providing properties");

            // Keep the common argument-without-properties path small and inlineable.
            int markerIndex = ExpressionShredder.IndexOfPropertyMarker(expression);

            return markerIndex >= 0
                ? ExpandFunctionArgumentPropertiesCore(expression, markerIndex, properties, options, elementLocation, propertiesUseTracker, fileSystem)
                : expression;

            // Keep property classification and expansion out of Function.Execute's generated code.
            [MethodImpl(MethodImplOptions.NoInlining)]
            static object ExpandFunctionArgumentPropertiesCore(
                string expression,
                int markerIndex,
                IPropertyProvider<P> properties,
                ExpanderOptions options,
                IElementLocation elementLocation,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem)
            {
                if (!TryGetPropertyInfo(expression, markerIndex, out int bodyLength, out PropertyReferenceFlags flags))
                {
                    return expression;
                }

                PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);

                return markerIndex == 0 && (flags & PropertyReferenceFlags.EndsExpression) != 0
                    ? expander.ExpandSingleProperty(expression, flags)
                    : expander.ExpandPropertyValues(expression, markerIndex, bodyLength, flags);
            }
        }

        /// <summary>
        ///  Finds the closing parenthesis for a property reference and classifies the reference.
        /// </summary>
        /// <param name="expression">The expression containing the property reference.</param>
        /// <param name="markerIndex">The zero-based index of the <c>$(</c> marker.</param>
        /// <param name="bodyLength">The length of the property body, excluding <c>$(</c> and <c>)</c>.</param>
        /// <param name="flags">The characteristics discovered while scanning the property reference.</param>
        /// <returns>
        ///  <see langword="true"/> if a matching closing parenthesis was found; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        private static bool TryGetPropertyInfo(string expression, int markerIndex, out int bodyLength, out PropertyReferenceFlags flags)
        {
            Debug.Assert(markerIndex >= 0, $"{nameof(markerIndex)} must be non-negative.");

            int index = markerIndex + 2;
            int nestLevel = 1;
            int length = expression.Length;

            flags = PropertyReferenceFlags.None;

            // Scan for the matching closing parenthesis while classifying characters that may
            // require specialized expansion.
            while (index < length && nestLevel > 0)
            {
                char character = expression[index];

                switch (character)
                {
                    case '\'' or '`' or '"':
                        int quoteIndex = expression.IndexOf(character, index + 1);

                        if (quoteIndex < 0)
                        {
                            bodyLength = 0;
                            flags = 0;
                            return false;
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
                        flags |= PropertyReferenceFlags.PotentialPropertyFunction;
                        break;

                    case ':':
                        flags |= PropertyReferenceFlags.PotentialRegistryFunction;
                        break;
                }

                index++;
            }

            if (nestLevel == 0)
            {
                if (index == length)
                {
                    flags |= PropertyReferenceFlags.EndsExpression;
                }

                // index is one past the closing parenthesis. Exclude the two-character marker and
                // closing parenthesis from the body length.
                bodyLength = index - markerIndex - 3;
                return bodyLength >= 0;
            }

            bodyLength = 0;
            return false;
        }

        /// <summary>
        ///  Expands a property reference that occupies the entire expression.
        /// </summary>
        /// <param name="expression">The complete property-reference expression.</param>
        /// <param name="flags">The characteristics discovered while scanning the property reference.</param>
        /// <returns>
        ///  The expanded value, preserving its type when it is not a string.
        /// </returns>
        private object ExpandSingleProperty(string expression, PropertyReferenceFlags flags)
        {
            Debug.Assert((flags & PropertyReferenceFlags.EndsExpression) != 0, $"{PropertyReferenceFlags.EndsExpression} must be set.");

            // Compat hack: WebProjects may have an import with a condition like:
            //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
            // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
            // Be compatible by returning an empty string here.
            // The property body starts at index 2 only when the "$(" marker starts at index 0.
            if ((flags & PropertyReferenceFlags.PotentialPropertyFunction) != 0 &&
                expression.Length == SolutionsVsVersionExpression.Length &&
                string.Equals(expression, SolutionsVsVersionExpression, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            object propertyValue = ExpandProperty(expression, startIndex: 2, length: expression.Length - 3, flags);

            if (propertyValue is null)
            {
                return string.Empty;
            }

            if (_isTruncationEnabled)
            {
                string value = propertyValue.ToString();
                if (value.Length > CharacterLimitPerExpansion)
                {
                    propertyValue = TruncateString(value);
                }
            }

            return propertyValue is string stringValue
                ? FileUtilities.MaybeAdjustFilePath(stringValue)
                : propertyValue;
        }

        /// <summary>
        ///  Expands one or more property references embedded in an expression.
        /// </summary>
        /// <param name="expression">The expression containing the property references.</param>
        /// <param name="markerIndex">The zero-based index of the first <c>$(</c> marker.</param>
        /// <param name="bodyLength">The length of the first property body.</param>
        /// <param name="flags">The characteristics discovered while scanning the first property reference.</param>
        /// <returns>
        ///  The concatenated expanded value.
        /// </returns>
        private object ExpandPropertyValues(string expression, int markerIndex, int bodyLength, PropertyReferenceFlags flags)
        {
            Debug.Assert(markerIndex > 0 || markerIndex + bodyLength + 3 < expression.Length);

            // We will build our set of results as object components
            // so that we can either maintain the object's type in the event
            // that we have a single component, or convert to a string
            // if concatenation is required.
            using SpanBasedConcatenator results = new();

            // The index is the zero-based index into the expression,
            // where we've essentially read up to and copied into the target string.
            int index = 0;

            // Expand each well-formed property reference. If a later marker is malformed, preserve
            // it and the rest of the expression verbatim.
            do
            {
                // Append the result with the portion of the expression up to
                // (but not including) the "$(", and advance the index pointer.
                if (markerIndex - index > 0)
                {
                    results.Add(expression.AsMemory(index, markerIndex - index));
                }

                int startIndex = markerIndex + 2;

                object propertyValue = ExpandProperty(expression, startIndex, bodyLength, flags);

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

                index = startIndex + bodyLength + 1;
                markerIndex = ExpressionShredder.IndexOfPropertyMarker(expression, index);
            }
            while (markerIndex >= 0 && TryGetPropertyInfo(expression, markerIndex, out bodyLength, out flags));

            // If we couldn't find any more property markers in the expression just copy the remainder into the result.
            if (expression.Length - index > 0)
            {
                results.Add(expression.AsMemory(index));
            }

            return results.GetResult();
        }

        /// <summary>
        ///  Expands a property body contained within a segment of an expression.
        /// </summary>
        /// <param name="expression">The expression containing the property body.</param>
        /// <param name="startIndex">The zero-based start index of the property body.</param>
        /// <param name="length">The length of the property body.</param>
        /// <param name="flags">The characteristics discovered while scanning the property reference.</param>
        /// <returns>
        ///  The expanded property value.
        /// </returns>
        private object ExpandProperty(string expression, int startIndex, int length, PropertyReferenceFlags flags)
        {
            if (length == 0)
            {
                return string.Empty;
            }

            if ((flags & PropertyReferenceFlags.PotentialSpecialSyntax) != 0)
            {
                // Compat hack: as a special case, $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory) should return string.Empty
                // Note that very few properties have this exact length, so this check should be fast.
                if (length == VstsDbDirectoryProperty.Length &&
                    string.Equals(expression, startIndex, VstsDbDirectoryProperty, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if ((flags & PropertyReferenceFlags.PotentialRegistryFunction) != 0 &&
                    TryExpandRegistryValue(expression, startIndex, length, out string registryValue))
                {
                    return registryValue;
                }

                if ((flags & PropertyReferenceFlags.PotentialPropertyFunction) != 0)
                {
                    // This is likely to be a function expression
                    return ExpandPropertyBody(expression, startIndex, length, propertyValue: null);
                }
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
            return expander.ExpandPropertyBody(propertyBody, startIndex: 0, propertyBody.Length, propertyValue);
        }

        /// <summary>
        ///  Expands a property or property-function body contained within a segment of a string.
        /// </summary>
        /// <param name="propertyBody">The string containing the property body.</param>
        /// <param name="startIndex">The zero-based start index of the property body.</param>
        /// <param name="length">The length of the property body.</param>
        /// <param name="propertyValue">
        ///  The receiver value when recursively expanding an indexer; otherwise,
        ///  <see langword="null"/>.
        /// </param>
        /// <returns>
        ///  The expanded property or property-function value.
        /// </returns>
        private object ExpandPropertyBody(string propertyBody, int startIndex, int length, object propertyValue)
        {
            int bodyStartIndex = startIndex;
            int bodyLength = length;

            // Trim only the parsing bounds. For compatibility, a whitespace-padded property name
            // such as $( Foo ) expands to blank rather than resolving the trimmed name.
            bool wasTrimmed = propertyBody.TrimBounds(ref bodyStartIndex, ref bodyLength);

            if (IsValidPropertyName(propertyBody, bodyStartIndex, bodyLength))
            {
                if (wasTrimmed)
                {
                    // Whitespace is not valid in property names, so compatibility requires this
                    // reference to expand to blank regardless of the property provider.
                    TrackPropertyRead(propertyBody, startIndex, length, isUninitialized: true);

                    return string.Empty;
                }

                // Undefined properties expand to an empty string.
                return LookupProperty(propertyBody, startIndex, length);
            }

            if (propertyBody.IndexOf('.', bodyStartIndex, bodyLength) >= 0 || propertyBody[bodyStartIndex] == '[')
            {
                string materializedPropertyBody = MaterializePropertyBody(propertyBody, bodyStartIndex, bodyLength);

                if (BuildParameters.DebugExpansion)
                {
                    Console.WriteLine($"Expanding: {materializedPropertyBody}");
                }

                Function function = Function.ExtractPropertyFunction(
                    materializedPropertyBody,
                    _elementLocation,
                    propertyValue,
                    _propertiesUseTracker,
                    _fileSystem,
                    _propertiesUseTracker.LoggingContext);

                if (function is null)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        _elementLocation,
                        "InvalidFunctionPropertyExpression",
                        materializedPropertyBody,
                        string.Empty);

                    return null;
                }

                // Instance functions record their receiver property; static functions have none.
                if (!function.Receiver.IsNullOrEmpty())
                {
                    propertyValue = LookupProperty(function.Receiver);
                }

                try
                {
                    // Static functions have no receiver, so propertyValue may be null.
                    return function.Execute(propertyValue, _properties, _options, _elementLocation);
                }
                catch (Exception) when (_options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    return materializedPropertyBody;
                }
            }

            int indexerStart = propertyBody.IndexOf('[', bodyStartIndex, bodyLength);

            // A single indexer first resolves its receiver property, then recursively applies the
            // indexer to that value.
            if (propertyValue is null && indexerStart >= 0)
            {
                int indexerEnd = propertyBody.IndexOf(']', bodyStartIndex, bodyLength);

                if (indexerEnd < 0)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        _elementLocation,
                        "InvalidFunctionPropertyExpression",
                        MaterializePropertyBody(propertyBody, bodyStartIndex, bodyLength),
                        AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                }

                propertyValue = LookupProperty(propertyBody, bodyStartIndex, indexerStart - bodyStartIndex);

                return ExpandPropertyBody(
                    propertyBody,
                    indexerStart,
                    bodyStartIndex + bodyLength - indexerStart,
                    propertyValue);
            }

            ProjectErrorUtilities.ThrowInvalidProject(
                _elementLocation,
                "InvalidFunctionPropertyExpression",
                MaterializePropertyBody(propertyBody, bodyStartIndex, bodyLength),
                string.Empty);

            return null;

            static string MaterializePropertyBody(string propertyBody, int startIndex, int length)
                => startIndex == 0 && length == propertyBody.Length
                    ? propertyBody
                    : propertyBody.Substring(startIndex, length);
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
            P property = _properties.GetProperty(propertyName, startIndex, length);

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
                TrackPropertyRead(propertyName, startIndex, length, isUninitialized: true);
                return string.Empty;
            }

            // Resolved properties are tracked as initialized reads.
            TrackPropertyRead(propertyName, startIndex, length, isUninitialized: false);

            if (property is ProjectPropertyInstance.EnvironmentDerivedProjectPropertyInstance environmentDerivedProperty)
            {
                // Environment-derived properties defer logging until their value is requested.
                environmentDerivedProperty.loggingContext = _propertiesUseTracker.LoggingContext;
            }

            return property.GetEvaluatedValueEscaped(_elementLocation);
        }

        private void TrackPropertyRead(string propertyName, int startIndex, int length, bool isUninitialized)
            => _propertiesUseTracker.TrackRead(propertyName, startIndex, length, _elementLocation, isUninitialized);

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

#if NET
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
                object value = Registry.GetValue(keyName, valueName, defaultValue: null);
                result = PropertyValueConverter.ToString(value);
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
