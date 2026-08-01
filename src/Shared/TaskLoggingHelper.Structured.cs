// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable enable

#if BUILD_ENGINE
namespace Microsoft.Build.BackEnd
#else
namespace Microsoft.Build.Utilities
#endif
{
#if BUILD_ENGINE
    internal
#else
    public
#endif
    partial class TaskLoggingHelper
    {
        /// <summary>
        /// Wraps a value with an explicit stable name for structured interpolation.
        /// </summary>
        /// <remarks>
        /// Caller expressions such as <c>project.FullPath</c> are useful defaults but may change
        /// during refactoring. This wrapper lets callers preserve a stable logging schema without
        /// reserving any part of the standard interpolation format string.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        public readonly struct NamedStructuredLogValue<T> : INamedStructuredLogValue
        {
            internal NamedStructuredLogValue(string name, T value)
            {
                ArgumentException.ThrowIfNullOrEmpty(name);
                Name = name;
                Value = value;
            }

            internal string Name { get; }
            internal T Value { get; }

            string INamedStructuredLogValue.Name => Name;
            object? INamedStructuredLogValue.Value => Value;
        }

        private interface INamedStructuredLogValue
        {
            string Name { get; }
            object? Value { get; }
        }

        /// <summary>
        /// Creates a value with an explicit stable name for use inside a structured interpolated message.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The stable structured name.</param>
        /// <param name="value">The value.</param>
        /// <returns>The named value.</returns>
        /// <remarks>
        /// Use this only when the caller expression is not an appropriate long-lived schema name.
        /// Ordinary expressions should rely on automatic caller-expression capture.
        /// </remarks>
        public NamedStructuredLogValue<T> Named<T>(string name, T value) => new(name, value);

        /// <summary>
        /// Builds structured task log messages from C# interpolated strings.
        /// </summary>
        /// <remarks>
        /// The handler creates two templates for different consumers: a named invariant template
        /// for structured analysis, and a positional composite template for lazy visible-message
        /// rendering. Keeping them separate avoids creating the full message unless requested while
        /// retaining meaningful names in binary logs. The <c>out bool</c> constructor pattern also
        /// prevents interpolation expressions from being evaluated for disabled messages.
        /// </remarks>
        [InterpolatedStringHandler]
        public ref struct StructuredLogInterpolatedStringHandler
        {
            private readonly bool _enabled;
            private readonly StringBuilder? _messageFormat;
            private readonly StringBuilder? _template;
            private readonly object[]? _messageArguments;
            private readonly KeyValuePair<string, string?>[]? _values;
            private readonly HashSet<string>? _names;
            private int _fallbackName;
            private int _formattedIndex;

            /// <summary>
            /// Creates a handler that always captures warning or error text.
            /// </summary>
            /// <param name="literalLength">Compiler-provided literal length used only to size buffers.</param>
            /// <param name="formattedCount">Compiler-provided hole count used only to size collections.</param>
            /// <param name="shouldAppend">
            /// Receives <see langword="true"/> because warnings and errors must reach the engine,
            /// where suppression and warning-as-error policy are applied.
            /// </param>
            public StructuredLogInterpolatedStringHandler(int literalLength, int formattedCount, out bool shouldAppend)
                : this(literalLength, formattedCount, enabled: true, out shouldAppend)
            {
            }

            /// <summary>
            /// Creates a handler for a normal-importance message.
            /// </summary>
            /// <param name="literalLength">Compiler-provided literal length used only to size buffers.</param>
            /// <param name="formattedCount">Compiler-provided hole count used only to size collections.</param>
            /// <param name="logger">The helper whose engine services determine whether the message can be observed.</param>
            /// <param name="shouldAppend">
            /// Receives <see langword="false"/> when no logger can observe a normal-importance
            /// message, allowing the compiler to skip every interpolation expression.
            /// </param>
            public StructuredLogInterpolatedStringHandler(
                int literalLength,
                int formattedCount,
                TaskLoggingHelper logger,
                out bool shouldAppend)
                : this(literalLength, formattedCount, logger.LogsMessagesOfImportance(MessageImportance.Normal), out shouldAppend)
            {
            }

            /// <summary>
            /// Creates a handler for a message with the specified importance.
            /// </summary>
            /// <param name="literalLength">Compiler-provided literal length used only to size buffers.</param>
            /// <param name="formattedCount">Compiler-provided hole count used only to size collections.</param>
            /// <param name="logger">The helper whose engine services determine whether the message can be observed.</param>
            /// <param name="importance">The importance used for the engine visibility check.</param>
            /// <param name="shouldAppend">
            /// Receives <see langword="false"/> when no logger can observe this importance,
            /// allowing the compiler to skip every interpolation expression.
            /// </param>
            public StructuredLogInterpolatedStringHandler(
                int literalLength,
                int formattedCount,
                TaskLoggingHelper logger,
                MessageImportance importance,
                out bool shouldAppend)
                : this(literalLength, formattedCount, logger.LogsMessagesOfImportance(importance), out shouldAppend)
            {
            }

            private StructuredLogInterpolatedStringHandler(
                int literalLength,
                int formattedCount,
                bool enabled,
                out bool shouldAppend)
            {
                _enabled = enabled;
                shouldAppend = enabled;
                _fallbackName = 0;
                _formattedIndex = 0;
                if (enabled)
                {
                    int capacity = literalLength + (formattedCount * 11);
                    _messageFormat = new StringBuilder(capacity);
                    _template = new StringBuilder(capacity);
                    _messageArguments = formattedCount == 0 ? Array.Empty<object>() : new object[formattedCount];
                    _values = formattedCount == 0
                        ? Array.Empty<KeyValuePair<string, string?>>()
                        : new KeyValuePair<string, string?>[formattedCount];
                    _names = new HashSet<string>(StringComparer.Ordinal);
                }
                else
                {
                    _messageFormat = null;
                    _template = null;
                    _messageArguments = null;
                    _values = null;
                    _names = null;
                }
            }

            /// <summary>
            /// Appends literal text.
            /// </summary>
            /// <param name="value">Literal source text supplied by the compiler.</param>
            /// <remarks>
            /// Braces are escaped in both templates so literal braces cannot later become
            /// positional or named holes.
            /// </remarks>
            public void AppendLiteral(string value)
            {
                if (!_enabled)
                {
                    return;
                }

                AppendEscapedLiteral(_messageFormat!, value);
                AppendEscapedLiteral(_template!, value);
            }

            /// <summary>
            /// Appends a value using its caller expression as the structured name.
            /// </summary>
            /// <typeparam name="T">The value type.</typeparam>
            /// <param name="value">The value to capture.</param>
            /// <param name="expression">
            /// The compiler-provided source expression. It supplies a useful default name without
            /// requiring callers to duplicate names in string literals.
            /// </param>
            public void AppendFormatted<T>(
                T value,
                [CallerArgumentExpression(nameof(value))] string? expression = null)
                => AppendFormattedCore(value, alignment: 0, format: null, expression);

            /// <summary>
            /// Appends a formatted value using its caller expression as the structured name.
            /// </summary>
            /// <typeparam name="T">The value type.</typeparam>
            /// <param name="value">The value to capture.</param>
            /// <param name="format">The standard interpolation format; it is never repurposed as a name.</param>
            /// <param name="expression">The compiler-provided source expression used as the default name.</param>
            public void AppendFormatted<T>(
                T value,
                string? format,
                [CallerArgumentExpression(nameof(value))] string? expression = null)
                => AppendFormattedCore(value, alignment: 0, format, expression);

            /// <summary>
            /// Appends an aligned value using its caller expression as the structured name.
            /// </summary>
            /// <typeparam name="T">The value type.</typeparam>
            /// <param name="value">The value to capture.</param>
            /// <param name="alignment">The standard interpolation alignment used only for visible rendering.</param>
            /// <param name="expression">The compiler-provided source expression used as the default name.</param>
            public void AppendFormatted<T>(
                T value,
                int alignment,
                [CallerArgumentExpression(nameof(value))] string? expression = null)
                => AppendFormattedCore(value, alignment, format: null, expression);

            /// <summary>
            /// Appends an aligned, formatted value using its caller expression as the structured name.
            /// </summary>
            /// <typeparam name="T">The value type.</typeparam>
            /// <param name="value">The value to capture.</param>
            /// <param name="alignment">The standard interpolation alignment used only for visible rendering.</param>
            /// <param name="format">The standard interpolation format; it is never repurposed as a name.</param>
            /// <param name="expression">The compiler-provided source expression used as the default name.</param>
            public void AppendFormatted<T>(
                T value,
                int alignment,
                string? format,
                [CallerArgumentExpression(nameof(value))] string? expression = null)
                => AppendFormattedCore(value, alignment, format, expression);

            private void AppendFormattedCore<T>(T value, int alignment, string? format, string? expression)
            {
                if (!_enabled)
                {
                    return;
                }

                string? name = expression;
                object? actualValue = value;
                if (actualValue is INamedStructuredLogValue namedValue)
                {
                    name = namedValue.Name;
                    actualValue = namedValue.Value;
                }

                name = GetUniqueName(SanitizeName(name));
                string displayValue = FormatValue(actualValue, format, CultureInfo.CurrentCulture);
                int index = _formattedIndex++;
                _messageFormat!.Append('{').Append(index);
                if (alignment != 0)
                {
                    _messageFormat.Append(',').Append(alignment.ToString(CultureInfo.InvariantCulture));
                }

                _messageFormat.Append('}');
                _messageArguments![index] = displayValue;

                _template!.Append('{').Append(name);
                if (alignment != 0)
                {
                    _template.Append(',').Append(alignment.ToString(CultureInfo.InvariantCulture));
                }

                if (!string.IsNullOrEmpty(format))
                {
                    _template.Append(':').Append(format);
                }

                _template.Append('}');
                _values![index] = new KeyValuePair<string, string?>(
                    name,
                    actualValue == null ? null : FormatValue(actualValue, format, CultureInfo.InvariantCulture));
            }

            private string SanitizeName(string? name)
            {
                name = name?.Trim();
                if (name is null || name.Length == 0)
                {
                    return $"Value{_fallbackName++}";
                }

                StringBuilder? sanitized = null;
                for (int i = 0; i < name.Length; i++)
                {
                    char c = name[i];
                    if (c is '{' or '}' or ',' or ':')
                    {
                        sanitized ??= new StringBuilder(name);
                        sanitized[i] = '_';
                    }
                }

                return sanitized?.ToString() ?? name;
            }

            private string GetUniqueName(string name)
            {
                if (_names!.Add(name))
                {
                    return name;
                }

                int suffix = 2;
                string candidate;
                do
                {
                    candidate = name + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }
                while (!_names.Add(candidate));

                return candidate;
            }

            /// <summary>
            /// Returns the positional composite format used by <see cref="LazyFormattedBuildEventArgs"/>.
            /// It is intentionally distinct from <see cref="GetOriginalFormat"/> so the visible message can remain
            /// lazy while structured consumers receive stable source-level names.
            /// </summary>
            internal string GetDisplayFormat() => _messageFormat!.ToString();

            /// <summary>
            /// Returns display arguments captured separately from the format so binary-log string tables can
            /// deduplicate components without first creating the complete visible message.
            /// </summary>
            internal object[] GetMessageArguments() => _messageArguments!;

            /// <summary>
            /// Returns the invariant named template intended for structured consumers. Unlike the positional
            /// display format, this template preserves meaningful names for querying and aggregation.
            /// </summary>
            internal string GetOriginalFormat() => _template!.ToString();

            /// <summary>
            /// Returns invariant string values independently from lazy display state so reading
            /// <see cref="BuildEventArgs.Message"/> cannot discard the structured payload.
            /// </summary>
            internal IReadOnlyList<KeyValuePair<string, string?>> GetValues() => _values!;

            /// <summary>
            /// Indicates whether the compiler populated this handler. Disabled message overloads check this before
            /// reading state so the non-null getter contract remains true without allocating placeholder objects.
            /// </summary>
            internal readonly bool IsEnabled => _enabled;
        }

        /// <summary>
        /// Logs a structured normal-importance interpolated message.
        /// </summary>
        /// <param name="message">
        /// The compiler-built handler. A plain string cannot convert to this parameter, which keeps
        /// literal and preformatted-string calls on their existing overloads.
        /// </param>
        public void LogMessage(
            [InterpolatedStringHandlerArgument("")] ref StructuredLogInterpolatedStringHandler message)
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredMessageCore(
                MessageImportance.Normal,
                null,
                message.GetOriginalFormat(),
                message.GetValues(),
                message.GetDisplayFormat(),
                message.GetMessageArguments());
        }

        /// <summary>
        /// Logs a structured interpolated message with the specified importance.
        /// </summary>
        /// <param name="importance">The importance used both for filtering and the emitted event.</param>
        /// <param name="message">
        /// The compiler-built handler. Its constructor receives <paramref name="importance"/> so
        /// disabled interpolation expressions are never evaluated.
        /// </param>
        public void LogMessage(
            MessageImportance importance,
            [InterpolatedStringHandlerArgument("", nameof(importance))] ref StructuredLogInterpolatedStringHandler message)
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredMessageCore(
                importance,
                null,
                message.GetOriginalFormat(),
                message.GetValues(),
                message.GetDisplayFormat(),
                message.GetMessageArguments());
        }

        /// <summary>
        /// Logs a structured interpolated message with source-location metadata.
        /// </summary>
        /// <param name="subcategory">Optional diagnostic category.</param>
        /// <param name="code">Optional diagnostic code.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="file">Optional source file; the task location is used when omitted with zero line and column.</param>
        /// <param name="lineNumber">Starting line, or zero when unavailable.</param>
        /// <param name="columnNumber">Starting column, or zero when unavailable.</param>
        /// <param name="endLineNumber">Ending line, or zero when unavailable.</param>
        /// <param name="endColumnNumber">Ending column, or zero when unavailable.</param>
        /// <param name="importance">The importance used both for filtering and the emitted event.</param>
        /// <param name="message">The compiler-built handler containing lazy display and structured state.</param>
        public void LogMessage(
            string? subcategory,
            string? code,
            string? helpKeyword,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            MessageImportance importance,
            [InterpolatedStringHandlerArgument("", nameof(importance))] ref StructuredLogInterpolatedStringHandler message)
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredMessageCore(
                subcategory,
                code,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                importance,
                null,
                message.GetOriginalFormat(),
                message.GetValues(),
                message.GetDisplayFormat(),
                message.GetMessageArguments());
        }

        /// <summary>
        /// Logs a structured interpolated warning.
        /// </summary>
        /// <param name="message">
        /// The compiler-built handler. Warning capture is not importance-filtered because the engine
        /// must apply suppression and warning-as-error policy.
        /// </param>
        public void LogWarning(ref StructuredLogInterpolatedStringHandler message)
            => LogStructuredWarningCore(
                null, null, null, null, null, 0, 0, 0, 0,
                null, message.GetOriginalFormat(), message.GetValues(),
                message.GetDisplayFormat(), message.GetMessageArguments());

        /// <summary>
        /// Logs a structured interpolated warning with source-location metadata.
        /// </summary>
        /// <param name="subcategory">Optional warning category.</param>
        /// <param name="warningCode">Optional warning code used by suppression and warning-as-error policy.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="file">Optional source file; the task location is used when omitted with zero line and column.</param>
        /// <param name="lineNumber">Starting line, or zero when unavailable.</param>
        /// <param name="columnNumber">Starting column, or zero when unavailable.</param>
        /// <param name="endLineNumber">Ending line, or zero when unavailable.</param>
        /// <param name="endColumnNumber">Ending column, or zero when unavailable.</param>
        /// <param name="message">The compiler-built handler containing lazy display and structured state.</param>
        public void LogWarning(
            string? subcategory,
            string? warningCode,
            string? helpKeyword,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            ref StructuredLogInterpolatedStringHandler message)
            => LogStructuredWarningCore(
                subcategory,
                warningCode,
                helpKeyword,
                null,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                null,
                message.GetOriginalFormat(),
                message.GetValues(),
                message.GetDisplayFormat(),
                message.GetMessageArguments());

        /// <summary>
        /// Logs a structured interpolated warning with source-location metadata and a help link.
        /// </summary>
        /// <param name="subcategory">Optional warning category.</param>
        /// <param name="warningCode">Optional warning code used by suppression and warning-as-error policy.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="helpLink">Optional link to additional diagnostic information.</param>
        /// <param name="file">Optional source file; the task location is used when omitted with zero line and column.</param>
        /// <param name="lineNumber">Starting line, or zero when unavailable.</param>
        /// <param name="columnNumber">Starting column, or zero when unavailable.</param>
        /// <param name="endLineNumber">Ending line, or zero when unavailable.</param>
        /// <param name="endColumnNumber">Ending column, or zero when unavailable.</param>
        /// <param name="message">The compiler-built handler containing lazy display and structured state.</param>
        public void LogWarning(
            string? subcategory,
            string? warningCode,
            string? helpKeyword,
            string? helpLink,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            ref StructuredLogInterpolatedStringHandler message)
            => LogStructuredWarningCore(
                subcategory,
                warningCode,
                helpKeyword,
                helpLink,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                null,
                message.GetOriginalFormat(),
                message.GetValues(),
                message.GetDisplayFormat(),
                message.GetMessageArguments());

        /// <summary>
        /// Logs a structured interpolated error.
        /// </summary>
        /// <param name="message">
        /// The compiler-built handler. Errors are always captured because they determine build success.
        /// </param>
        public void LogError(ref StructuredLogInterpolatedStringHandler message)
            => LogStructuredErrorCore(
                null, null, null, null, null, 0, 0, 0, 0,
                null, message.GetOriginalFormat(), message.GetValues(),
                message.GetDisplayFormat(), message.GetMessageArguments());

        /// <summary>
        /// Logs a structured interpolated error with source-location metadata.
        /// </summary>
        /// <param name="subcategory">Optional error category.</param>
        /// <param name="errorCode">Optional error code.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="file">Optional source file; the task location is used when omitted with zero line and column.</param>
        /// <param name="lineNumber">Starting line, or zero when unavailable.</param>
        /// <param name="columnNumber">Starting column, or zero when unavailable.</param>
        /// <param name="endLineNumber">Ending line, or zero when unavailable.</param>
        /// <param name="endColumnNumber">Ending column, or zero when unavailable.</param>
        /// <param name="message">The compiler-built handler containing lazy display and structured state.</param>
        public void LogError(
            string? subcategory,
            string? errorCode,
            string? helpKeyword,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            ref StructuredLogInterpolatedStringHandler message)
            => LogStructuredErrorCore(
                subcategory,
                errorCode,
                helpKeyword,
                null,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                null,
                message.GetOriginalFormat(),
                message.GetValues(),
                message.GetDisplayFormat(),
                message.GetMessageArguments());

        /// <summary>
        /// Logs a structured interpolated error with source-location metadata and a help link.
        /// </summary>
        /// <param name="subcategory">Optional error category.</param>
        /// <param name="errorCode">Optional error code.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="helpLink">Optional link to additional diagnostic information.</param>
        /// <param name="file">Optional source file; the task location is used when omitted with zero line and column.</param>
        /// <param name="lineNumber">Starting line, or zero when unavailable.</param>
        /// <param name="columnNumber">Starting column, or zero when unavailable.</param>
        /// <param name="endLineNumber">Ending line, or zero when unavailable.</param>
        /// <param name="endColumnNumber">Ending column, or zero when unavailable.</param>
        /// <param name="message">The compiler-built handler containing lazy display and structured state.</param>
        public void LogError(
            string? subcategory,
            string? errorCode,
            string? helpKeyword,
            string? helpLink,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            ref StructuredLogInterpolatedStringHandler message)
            => LogStructuredErrorCore(
                subcategory,
                errorCode,
                helpKeyword,
                helpLink,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                null,
                message.GetOriginalFormat(),
                message.GetValues(),
                message.GetDisplayFormat(),
                message.GetMessageArguments());

        /// <summary>
        /// Logs a structured normal-importance message from an invariant named template and ordered values.
        /// </summary>
        /// <param name="messageTemplate">
        /// The invariant named template. It is separate from the values so non-C# callers receive
        /// the same binary-log deduplication as handler callers.
        /// </param>
        /// <param name="values">Values in template occurrence order; null elements remain structured nulls.</param>
        public void LogStructuredMessage(string messageTemplate, params object?[]? values)
            => LogStructuredMessage(MessageImportance.Normal, messageTemplate, values);

        /// <summary>
        /// Logs a structured message from an invariant named template and ordered values.
        /// </summary>
        /// <param name="importance">The importance used for filtering and the emitted event.</param>
        /// <param name="messageTemplate">The invariant named template.</param>
        /// <param name="values">Values in template occurrence order; null elements remain structured nulls.</param>
        public void LogStructuredMessage(MessageImportance importance, string messageTemplate, params object?[]? values)
        {
            StructuredMessageData data = ParseStructuredMessage(messageTemplate, values);
            LogStructuredMessageCore(
                importance,
                data.Message,
                data.OriginalFormat,
                data.Values,
                data.MessageFormat,
                data.MessageArguments);
        }

        /// <summary>
        /// Logs a structured warning from an invariant named template and ordered values.
        /// </summary>
        /// <param name="messageTemplate">The invariant named template.</param>
        /// <param name="values">Values in template occurrence order; null elements remain structured nulls.</param>
        public void LogStructuredWarning(string messageTemplate, params object?[]? values)
        {
            StructuredMessageData data = ParseStructuredMessage(messageTemplate, values);
            LogStructuredWarningCore(
                null, null, null, null, null, 0, 0, 0, 0,
                data.Message, data.OriginalFormat, data.Values, data.MessageFormat, data.MessageArguments);
        }

        /// <summary>
        /// Logs a structured error from an invariant named template and ordered values.
        /// </summary>
        /// <param name="messageTemplate">The invariant named template.</param>
        /// <param name="values">Values in template occurrence order; null elements remain structured nulls.</param>
        public void LogStructuredError(string messageTemplate, params object?[]? values)
        {
            StructuredMessageData data = ParseStructuredMessage(messageTemplate, values);
            LogStructuredErrorCore(
                null, null, null, null, null, 0, 0, 0, 0,
                data.Message, data.OriginalFormat, data.Values, data.MessageFormat, data.MessageArguments);
        }

        /// <summary>
        /// Logs an already-localized display message with a separate invariant template and named values.
        /// </summary>
        /// <remarks>
        /// Resource-based messages need two representations: localized text for people and an
        /// invariant schema for machines. Accepting both prevents localization from fragmenting
        /// template-based aggregation.
        /// </remarks>
        /// <param name="importance">The importance used for filtering and the emitted event.</param>
        /// <param name="originalFormat">The invariant named template persisted for structured consumers.</param>
        /// <param name="localizedMessage">The already-localized visible message.</param>
        /// <param name="values">
        /// Ordered named values. Names must match the template so the localized text cannot silently
        /// change the structured schema.
        /// </param>
        public void LogStructuredMessage(
            MessageImportance importance,
            string originalFormat,
            string localizedMessage,
            IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            StructuredMessageData data = ValidateNamedStructuredMessage(originalFormat, localizedMessage, values);
            LogStructuredMessageCore(importance, data.Message, data.OriginalFormat, data.Values, null, null);
        }

        /// <summary>
        /// Logs an already-localized warning with a separate invariant template and named values.
        /// </summary>
        /// <param name="originalFormat">The invariant named template persisted for structured consumers.</param>
        /// <param name="localizedMessage">The already-localized visible warning.</param>
        /// <param name="values">Ordered named values whose names must match the template.</param>
        public void LogStructuredWarning(
            string originalFormat,
            string localizedMessage,
            IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            StructuredMessageData data = ValidateNamedStructuredMessage(originalFormat, localizedMessage, values);
            LogStructuredWarningCore(
                null, null, null, null, null, 0, 0, 0, 0,
                data.Message, data.OriginalFormat, data.Values, null, null);
        }

        /// <summary>
        /// Logs an already-localized error with a separate invariant template and named values.
        /// </summary>
        /// <param name="originalFormat">The invariant named template persisted for structured consumers.</param>
        /// <param name="localizedMessage">The already-localized visible error.</param>
        /// <param name="values">Ordered named values whose names must match the template.</param>
        public void LogStructuredError(
            string originalFormat,
            string localizedMessage,
            IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            StructuredMessageData data = ValidateNamedStructuredMessage(originalFormat, localizedMessage, values);
            LogStructuredErrorCore(
                null, null, null, null, null, 0, 0, 0, 0,
                data.Message, data.OriginalFormat, data.Values, null, null);
        }

        private void LogStructuredMessageCore(
            MessageImportance importance,
            string? message,
            string originalFormat,
            IReadOnlyList<KeyValuePair<string, string?>>? values,
            string? messageFormat,
            object[]? messageArguments)
            => LogStructuredMessageCore(
                null, null, null, null, 0, 0, 0, 0, importance,
                message, originalFormat, values, messageFormat, messageArguments);

        private void LogStructuredMessageCore(
            string? subcategory,
            string? code,
            string? helpKeyword,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            MessageImportance importance,
            string? message,
            string originalFormat,
            IReadOnlyList<KeyValuePair<string, string?>>? values,
            string? messageFormat,
            object[]? messageArguments)
        {
            if (!LogsMessagesOfImportance(importance))
            {
                return;
            }

            ErrorUtilities.VerifyThrowInvalidOperation(
                BuildEngine != null,
                "LoggingBeforeTaskInitialization",
                message ?? messageFormat ?? string.Empty);
            bool fillInLocation = string.IsNullOrEmpty(file) && lineNumber == 0 && columnNumber == 0;
            var e = new ExtendedBuildMessageEventArgs(
                StructuredBuildEventArgsData.EventType,
                subcategory,
                code,
                fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                endLineNumber,
                endColumnNumber,
                messageFormat ?? message,
                helpKeyword,
                TaskName,
                importance,
                DateTime.UtcNow,
                messageArguments);
            StructuredBuildEventArgsData.Set(e, originalFormat, values!);

            BuildEngine.LogMessageEvent(e);
        }

        private void LogStructuredErrorCore(
            string? subcategory,
            string? errorCode,
            string? helpKeyword,
            string? helpLink,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string? message,
            string originalFormat,
            IReadOnlyList<KeyValuePair<string, string?>>? values,
            string? messageFormat,
            object[]? messageArguments)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                BuildEngine != null,
                "LoggingBeforeTaskInitialization",
                message ?? messageFormat ?? string.Empty);
            bool fillInLocation = string.IsNullOrEmpty(file) && lineNumber == 0 && columnNumber == 0;
            var e = new ExtendedBuildErrorEventArgs(
                StructuredBuildEventArgsData.EventType,
                subcategory,
                errorCode,
                fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                endLineNumber,
                endColumnNumber,
                messageFormat ?? message,
                helpKeyword,
                TaskName,
                helpLink,
                DateTime.UtcNow,
                messageArguments);
            StructuredBuildEventArgsData.Set(e, originalFormat, values!);

            BuildEngine.LogErrorEvent(e);
            HasLoggedErrors = true;
        }

        private void LogStructuredWarningCore(
            string? subcategory,
            string? warningCode,
            string? helpKeyword,
            string? helpLink,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string? message,
            string originalFormat,
            IReadOnlyList<KeyValuePair<string, string?>>? values,
            string? messageFormat,
            object[]? messageArguments)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                BuildEngine != null,
                "LoggingBeforeTaskInitialization",
                message ?? messageFormat ?? string.Empty);
            bool fillInLocation = string.IsNullOrEmpty(file) && lineNumber == 0 && columnNumber == 0;
            if (BuildEngine is IBuildEngine8 buildEngine8 && buildEngine8.ShouldTreatWarningAsError(warningCode))
            {
                LogStructuredErrorCore(
                    subcategory,
                    warningCode,
                    helpKeyword,
                    helpLink,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message,
                    originalFormat,
                    values,
                    messageFormat,
                    messageArguments);
                return;
            }

            var e = new ExtendedBuildWarningEventArgs(
                StructuredBuildEventArgsData.EventType,
                subcategory,
                warningCode,
                fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                endLineNumber,
                endColumnNumber,
                messageFormat ?? message,
                helpKeyword,
                TaskName,
                helpLink,
                DateTime.UtcNow,
                messageArguments);
            StructuredBuildEventArgsData.Set(e, originalFormat, values!);

            BuildEngine.LogWarningEvent(e);
        }

        private static StructuredMessageData ParseStructuredMessage(string messageTemplate, object?[]? values)
        {
            ArgumentNullException.ThrowIfNull(messageTemplate);
            values ??= Array.Empty<object?>();

            var normalized = new StringBuilder(messageTemplate.Length);
            var composite = new StringBuilder(messageTemplate.Length);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var holes = new List<(string Name, string? Format)>();

            for (int i = 0; i < messageTemplate.Length;)
            {
                char c = messageTemplate[i];
                if (c == '{')
                {
                    if (i + 1 < messageTemplate.Length && messageTemplate[i + 1] == '{')
                    {
                        normalized.Append("{{");
                        composite.Append("{{");
                        i += 2;
                        continue;
                    }

                    int close = messageTemplate.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        throw new FormatException("The structured message template contains an unmatched '{'.");
                    }

                    string hole = messageTemplate.Substring(i + 1, close - i - 1);
                    int separator = IndexOfSeparator(hole);
                    string name = (separator < 0 ? hole : hole.Substring(0, separator)).Trim();
                    if (name.Length == 0)
                    {
                        throw new FormatException("Structured message holes must have names.");
                    }

                    name = GetUniqueName(names, SanitizeName(name));
                    string suffix = separator < 0 ? string.Empty : hole.Substring(separator);
                    string? format = null;
                    int colon = suffix.IndexOf(':');
                    if (colon >= 0)
                    {
                        format = suffix.Substring(colon + 1);
                    }

                    normalized.Append('{').Append(name).Append(suffix).Append('}');
                    composite.Append('{').Append(holes.Count).Append(suffix).Append('}');
                    holes.Add((name, format));
                    i = close + 1;
                    continue;
                }

                if (c == '}')
                {
                    if (i + 1 < messageTemplate.Length && messageTemplate[i + 1] == '}')
                    {
                        normalized.Append("}}");
                        composite.Append("}}");
                        i += 2;
                        continue;
                    }

                    throw new FormatException("The structured message template contains an unmatched '}'.");
                }

                normalized.Append(c);
                composite.Append(c);
                i++;
            }

            if (holes.Count != values.Length)
            {
                throw new FormatException(
                    $"The structured message template contains {holes.Count} holes but {values.Length} values were supplied.");
            }

            var structuredValues = new List<KeyValuePair<string, string?>>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                object? value = values[i];
                structuredValues.Add(new KeyValuePair<string, string?>(
                    holes[i].Name,
                    value == null ? null : FormatValue(value, holes[i].Format, CultureInfo.InvariantCulture)));
            }

            return new StructuredMessageData(
                null,
                normalized.ToString(),
                structuredValues,
                composite.ToString(),
                ToDisplayArguments(values));
        }

        private static StructuredMessageData ValidateNamedStructuredMessage(
            string originalFormat,
            string localizedMessage,
            IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            object?[] rawValues = new object?[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                rawValues[i] = values[i].Value;
            }

            StructuredMessageData parsed = ParseStructuredMessage(originalFormat, rawValues);
            for (int i = 0; i < values.Count; i++)
            {
                if (!string.Equals(parsed.Values[i].Key, values[i].Key, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Structured value '{values[i].Key}' does not match template hole '{parsed.Values[i].Key}' at position {i}.",
                        nameof(values));
                }
            }

            ArgumentNullException.ThrowIfNull(localizedMessage);
            return new StructuredMessageData(localizedMessage, parsed.OriginalFormat, parsed.Values, null, null);
        }

        private static object[] ToDisplayArguments(object?[] values)
        {
            var result = new object[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = values[i]!;
            }

            return result;
        }

        private static int IndexOfSeparator(string hole)
        {
            int comma = hole.IndexOf(',');
            int colon = hole.IndexOf(':');
            if (comma < 0)
            {
                return colon;
            }

            return colon < 0 ? comma : Math.Min(comma, colon);
        }

        private static string SanitizeName(string name)
            => name.Replace('{', '_').Replace('}', '_').Replace(',', '_').Replace(':', '_');

        private static string GetUniqueName(HashSet<string> names, string name)
        {
            if (names.Add(name))
            {
                return name;
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = name + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            while (!names.Add(candidate));

            return candidate;
        }

        private static string FormatValue(object? value, string? format, IFormatProvider provider)
            => value is IFormattable formattable
                ? formattable.ToString(format, provider)
                : value?.ToString() ?? string.Empty;

        private static void AppendEscapedLiteral(StringBuilder builder, string value)
        {
            foreach (char c in value)
            {
                if (c is '{' or '}')
                {
                    builder.Append(c);
                }

                builder.Append(c);
            }
        }

        private sealed class StructuredMessageData(
            string? message,
            string originalFormat,
            IReadOnlyList<KeyValuePair<string, string?>> values,
            string? messageFormat,
            object[]? messageArguments)
        {
            public string? Message { get; } = message;
            public string OriginalFormat { get; } = originalFormat;
            public IReadOnlyList<KeyValuePair<string, string?>> Values { get; } = values;
            public string? MessageFormat { get; } = messageFormat;
            public object[]? MessageArguments { get; } = messageArguments;
        }
    }
}
