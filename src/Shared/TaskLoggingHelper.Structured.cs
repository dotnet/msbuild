// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#if BUILD_ENGINE
using Microsoft.Build.Utilities;
#endif

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
        /// Caller expressions such as <c>project.FullPath</c> are useful default names.
        /// These expressions can change during refactoring.
        /// This wrapper preserves a stable logging schema without changing the standard interpolation format.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        public readonly struct NamedStructuredLogValue<T>
        {
            internal NamedStructuredLogValue(string name, T value)
            {
                ArgumentException.ThrowIfNullOrEmpty(name);
                Name = name;
                Value = value;
            }

            internal string Name { get; }
            internal T Value { get; }
        }

        /// <summary>
        /// Creates a value with an explicit stable name for use inside a structured interpolated message.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The stable structured name.</param>
        /// <param name="value">The value.</param>
        /// <returns>The named value.</returns>
        /// <remarks>
        /// Use this method when the caller expression is not a stable schema name.
        /// For other expressions, use automatic caller-expression capture.
        /// </remarks>
        public NamedStructuredLogValue<T> Named<T>(string name, T value) => new(name, value);

        /// <summary>
        /// Builds structured task log messages from C# interpolated strings.
        /// </summary>
        /// <remarks>
        /// The handler creates an invariant named template for structured analysis.
        /// It creates a positional template only when the Change Wave fallback emits an ordinary event.
        /// Both paths create the complete display text only when a consumer requests it.
        /// The <c>out bool</c> constructor also prevents evaluation of expressions for disabled messages.
        /// </remarks>
        [InterpolatedStringHandler]
        public ref struct StructuredLogInterpolatedStringHandler
        {
            private readonly bool _enabled;
            private readonly bool _emitStructured;
            private readonly StringBuilder? _messageFormat;
            private ValueStringBuilder _template;
            private readonly object[]? _messageArguments;
            private readonly KeyValuePair<string, string?>[]? _values;
            private int _fallbackName;
            private int _formattedIndex;

            /// <summary>
            /// Creates a handler that always captures warning or error text.
            /// </summary>
            /// <param name="literalLength">Compiler-provided literal length used only to size buffers.</param>
            /// <param name="formattedCount">Compiler-provided hole count used only to size collections.</param>
            /// <param name="shouldAppend">
            /// Receives <see langword="true"/> because warnings and errors must reach the engine.
            /// The engine applies suppression and warning-as-error policy.
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
            /// <param name="logger">The helper that checks whether a logger can receive the message.</param>
            /// <param name="shouldAppend">
            /// Receives <see langword="false"/> when no logger can receive a normal-importance message.
            /// The compiler then skips all interpolation expressions.
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
            /// <param name="logger">The helper that checks whether a logger can receive the message.</param>
            /// <param name="importance">The importance used for the engine visibility check.</param>
            /// <param name="shouldAppend">
            /// Receives <see langword="false"/> when no logger can receive this importance.
            /// The compiler then skips all interpolation expressions.
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
                _emitStructured = enabled && ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11);
                shouldAppend = enabled;
                _fallbackName = 0;
                _formattedIndex = 0;
                if (enabled)
                {
                    int capacity = literalLength + (formattedCount * 11);
                    _messageFormat = _emitStructured ? null : new StringBuilder(capacity);
                    _template = new ValueStringBuilder(capacity);
                    _messageArguments = _emitStructured
                        ? null
                        : formattedCount == 0 ? Array.Empty<object>() : new object[formattedCount];
                    _values = formattedCount == 0
                        ? Array.Empty<KeyValuePair<string, string?>>()
                        : new KeyValuePair<string, string?>[formattedCount];
                }
                else
                {
                    _messageFormat = null;
                    _template = default;
                    _messageArguments = null;
                    _values = null;
                }
            }

            /// <summary>
            /// Appends literal text.
            /// </summary>
            /// <param name="value">Literal source text supplied by the compiler.</param>
            /// <remarks>
            /// The handler escapes braces in each active template.
            /// Thus, a literal brace cannot become a positional or named hole.
            /// </remarks>
            public void AppendLiteral(string value)
            {
                if (!_enabled)
                {
                    return;
                }

                if (_messageFormat is not null)
                {
                    AppendEscapedLiteral(_messageFormat, value);
                }

                AppendEscapedLiteral(ref _template, value);
            }

            /// <summary>
            /// Appends a value using its caller expression as the structured name.
            /// </summary>
            /// <typeparam name="T">The value type.</typeparam>
            /// <param name="value">The value to capture.</param>
            /// <param name="expression">
            /// The compiler-provided source expression.
            /// It supplies a default name without requiring a duplicate string literal.
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
            /// <param name="format">The standard interpolation format. The handler does not use it as a name.</param>
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
            /// <param name="alignment">The standard interpolation alignment for the display text.</param>
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
            /// <param name="alignment">The standard interpolation alignment for the display text.</param>
            /// <param name="format">The standard interpolation format. The handler does not use it as a name.</param>
            /// <param name="expression">The compiler-provided source expression used as the default name.</param>
            public void AppendFormatted<T>(
                T value,
                int alignment,
                string? format,
                [CallerArgumentExpression(nameof(value))] string? expression = null)
                => AppendFormattedCore(value, alignment, format, expression);

            /// <summary>
            /// Appends a value carrying an explicit stable structured name.
            /// </summary>
            public void AppendFormatted<T>(NamedStructuredLogValue<T> value)
                => AppendFormattedCore(value.Value, alignment: 0, format: null, value.Name);

            /// <summary>
            /// Appends a formatted value carrying an explicit stable structured name.
            /// </summary>
            public void AppendFormatted<T>(NamedStructuredLogValue<T> value, string? format)
                => AppendFormattedCore(value.Value, alignment: 0, format, value.Name);

            /// <summary>
            /// Appends an aligned value carrying an explicit stable structured name.
            /// </summary>
            public void AppendFormatted<T>(NamedStructuredLogValue<T> value, int alignment)
                => AppendFormattedCore(value.Value, alignment, format: null, value.Name);

            /// <summary>
            /// Appends an aligned, formatted value carrying an explicit stable structured name.
            /// </summary>
            public void AppendFormatted<T>(NamedStructuredLogValue<T> value, int alignment, string? format)
                => AppendFormattedCore(value.Value, alignment, format, value.Name);

            private void AppendFormattedCore<T>(T value, int alignment, string? format, string? expression)
            {
                if (!_enabled)
                {
                    return;
                }

                object? actualValue = value;
                string name = GetUniqueName(SanitizeName(expression));
                int index = _formattedIndex++;
                if (_messageFormat is not null)
                {
                    _messageFormat.Append('{').Append(index);
                    if (alignment != 0)
                    {
                        _messageFormat.Append(',').Append(alignment.ToString(CultureInfo.InvariantCulture));
                    }

                    _messageFormat.Append('}');
                    _messageArguments![index] = FormatValue(actualValue, format, CultureInfo.CurrentCulture);
                }

                _template.Append('{');
                _template.Append(name);
                if (alignment != 0)
                {
                    _template.Append(',');
                    _template.Append(alignment.ToString(CultureInfo.InvariantCulture));
                }

                if (!string.IsNullOrEmpty(format))
                {
                    _template.Append(':');
                    _template.Append(format);
                }

                _template.Append('}');
                _values![index] = new KeyValuePair<string, string?>(
                    name,
                    actualValue == null ? null : FormatValue(actualValue, format, CultureInfo.CurrentCulture));
            }

            private string SanitizeName(string? name)
            {
                name = name?.Trim();
                if (name is null || !IsUsableInferredName(name))
                {
                    return $"Value{_fallbackName++}";
                }

                return name;
            }

            private static bool IsUsableInferredName(string name)
            {
                if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_'))
                {
                    return false;
                }

                for (int i = 1; i < name.Length; i++)
                {
                    char c = name[i];
                    if (!(char.IsLetterOrDigit(c) || c is '_' or '.'))
                    {
                        return false;
                    }
                }

                return true;
            }

            private string GetUniqueName(string name)
            {
                if (!ContainsName(name))
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
                while (ContainsName(candidate));

                return candidate;
            }

            private bool ContainsName(string name)
            {
                for (int i = 0; i < _formattedIndex; i++)
                {
                    if (string.Equals(_values![i].Key, name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Returns a positional composite format only when the Change Wave compatibility fallback is active.
            /// </summary>
            /// <remarks>
            /// The structured path renders the named template directly.
            /// It does not store a second template for older event types.
            /// </remarks>
            internal string? GetDisplayFormat() => _messageFormat?.ToString();

            /// <summary>
            /// Returns positional arguments only when needed to restore the pre-Change-Wave event shape.
            /// </summary>
            internal object[]? GetMessageArguments() => _messageArguments;

            /// <summary>
            /// Returns the invariant named template used both as the event display format and by structured consumers.
            /// </summary>
            /// <remarks>
            /// A compiler-generated call consumes the handler one time.
            /// Thus, this method can dispose the builder and return its pooled storage.
            /// The owned builder also makes nested logging safe.
            /// </remarks>
            internal string GetOriginalFormat() => _template.ToStringAndDispose();

            /// <summary>
            /// Returns the captured string values separately from the lazy display state.
            /// Reading <see cref="BuildEventArgs.Message"/> does not remove these values.
            /// </summary>
            internal IReadOnlyList<KeyValuePair<string, string?>> GetValues() => _values!;

            /// <summary>
            /// Indicates whether the compiler populated this handler.
            /// Disabled overloads check this value before they read the handler state.
            /// This check avoids placeholder allocations for non-null return values.
            /// </summary>
            internal readonly bool IsEnabled => _enabled;
        }

        /// <summary>
        /// Logs a structured normal-importance interpolated message.
        /// </summary>
        /// <param name="message">
        /// The compiler-built handler.
        /// A plain string cannot convert to this parameter.
        /// Thus, literal and preformatted strings use their existing overloads.
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
        /// The compiler-built handler.
        /// Its constructor receives <paramref name="importance"/>.
        /// The compiler does not evaluate interpolation expressions for a disabled message.
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
        /// <param name="file">
        /// Optional source file. The task location is used when the file, line, and column are not specified.
        /// </param>
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
        /// The compiler-built handler.
        /// Warning capture does not use importance filtering.
        /// The engine must apply suppression and warning-as-error policy.
        /// </param>
        public void LogWarning(ref StructuredLogInterpolatedStringHandler message)
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredWarningCore(
                null, null, null, null, null, 0, 0, 0, 0,
                null, message.GetOriginalFormat(), message.GetValues(),
                message.GetDisplayFormat(), message.GetMessageArguments());
        }

        /// <summary>
        /// Logs a structured interpolated warning with source-location metadata.
        /// </summary>
        /// <param name="subcategory">Optional warning category.</param>
        /// <param name="warningCode">Optional warning code for suppression and warning-as-error policy.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="file">
        /// Optional source file. The task location is used when the file, line, and column are not specified.
        /// </param>
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
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredWarningCore(
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
        }

        /// <summary>
        /// Logs a structured interpolated warning with source-location metadata and a help link.
        /// </summary>
        /// <param name="subcategory">Optional warning category.</param>
        /// <param name="warningCode">Optional warning code for suppression and warning-as-error policy.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="helpLink">Optional link to additional diagnostic information.</param>
        /// <param name="file">
        /// Optional source file. The task location is used when the file, line, and column are not specified.
        /// </param>
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
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredWarningCore(
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
        }

        /// <summary>
        /// Logs a structured interpolated error.
        /// </summary>
        /// <param name="message">
        /// The compiler-built handler. Errors are always captured because they determine build success.
        /// </param>
        public void LogError(ref StructuredLogInterpolatedStringHandler message)
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredErrorCore(
                null, null, null, null, null, 0, 0, 0, 0,
                null, message.GetOriginalFormat(), message.GetValues(),
                message.GetDisplayFormat(), message.GetMessageArguments());
        }

        /// <summary>
        /// Logs a structured interpolated error with source-location metadata.
        /// </summary>
        /// <param name="subcategory">Optional error category.</param>
        /// <param name="errorCode">Optional error code.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="file">
        /// Optional source file. The task location is used when the file, line, and column are not specified.
        /// </param>
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
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredErrorCore(
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
        }

        /// <summary>
        /// Logs a structured interpolated error with source-location metadata and a help link.
        /// </summary>
        /// <param name="subcategory">Optional error category.</param>
        /// <param name="errorCode">Optional error code.</param>
        /// <param name="helpKeyword">Optional IDE help keyword.</param>
        /// <param name="helpLink">Optional link to additional diagnostic information.</param>
        /// <param name="file">
        /// Optional source file. The task location is used when the file, line, and column are not specified.
        /// </param>
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
        {
            if (!message.IsEnabled)
            {
                return;
            }

            LogStructuredErrorCore(
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
        }

        /// <summary>
        /// Logs a structured normal-importance message from an invariant named template and ordered values.
        /// </summary>
        /// <param name="messageTemplate">
        /// The invariant named template.
        /// Its separate values let non-C# callers use the same binary-log string deduplication as handler callers.
        /// </param>
        /// <param name="values">
        /// Values in template occurrence order. A null element remains a structured null value.
        /// </param>
        public void LogStructuredMessage(string messageTemplate, params object?[]? values)
            => LogStructuredMessage(MessageImportance.Normal, messageTemplate, values);

        /// <summary>
        /// Logs a structured message from an invariant named template and ordered values.
        /// </summary>
        /// <param name="importance">The importance used for filtering and the emitted event.</param>
        /// <param name="messageTemplate">The invariant named template.</param>
        /// <param name="values">
        /// Values in template occurrence order. A null element remains a structured null value.
        /// </param>
        public void LogStructuredMessage(MessageImportance importance, string messageTemplate, params object?[]? values)
        {
            if (!LogsMessagesOfImportance(importance))
            {
                return;
            }

            StructuredMessageData data = ParseStructuredMessage(
                messageTemplate,
                values,
                buildDisplayFormat: !ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11));
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
        /// <param name="values">
        /// Values in template occurrence order. A null element remains a structured null value.
        /// </param>
        public void LogStructuredWarning(string messageTemplate, params object?[]? values)
        {
            StructuredMessageData data = ParseStructuredMessage(
                messageTemplate,
                values,
                buildDisplayFormat: !ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11));
            LogStructuredWarningCore(
                null, null, null, null, null, 0, 0, 0, 0,
                data.Message, data.OriginalFormat, data.Values, data.MessageFormat, data.MessageArguments);
        }

        /// <summary>
        /// Logs a structured error from an invariant named template and ordered values.
        /// </summary>
        /// <param name="messageTemplate">The invariant named template.</param>
        /// <param name="values">
        /// Values in template occurrence order. A null element remains a structured null value.
        /// </param>
        public void LogStructuredError(string messageTemplate, params object?[]? values)
        {
            StructuredMessageData data = ParseStructuredMessage(
                messageTemplate,
                values,
                buildDisplayFormat: !ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11));
            LogStructuredErrorCore(
                null, null, null, null, null, 0, 0, 0, 0,
                data.Message, data.OriginalFormat, data.Values, data.MessageFormat, data.MessageArguments);
        }

        /// <summary>
        /// Logs an already-localized display message with a separate invariant template and named values.
        /// </summary>
        /// <remarks>
        /// A resource-based message needs localized text for users and an invariant template for tools.
        /// Separate inputs keep localized text from dividing events into different template groups.
        /// </remarks>
        /// <param name="importance">The importance used for filtering and the emitted event.</param>
        /// <param name="originalFormat">The invariant named template persisted for structured consumers.</param>
        /// <param name="localizedMessage">The already-localized visible message.</param>
        /// <param name="values">
        /// Ordered named values.
        /// Each name must match its template hole.
        /// This rule prevents localized text from changing the structured schema.
        /// </param>
        public void LogStructuredMessage(
            MessageImportance importance,
            string originalFormat,
            string localizedMessage,
            IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            ArgumentNullException.ThrowIfNull(localizedMessage);
            if (!LogsMessagesOfImportance(importance))
            {
                return;
            }

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
            BuildMessageEventArgs e;
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11))
            {
                e = new StructuredBuildMessageEventArgs(
                    subcategory,
                    code,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message ?? originalFormat,
                    originalFormat,
                    values!,
                    helpKeyword,
                    TaskName,
                    importance,
                    DateTime.UtcNow);
            }
            else
            {
                e = new BuildMessageEventArgs(
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
            }

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
            BuildErrorEventArgs e;
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11))
            {
                e = new StructuredBuildErrorEventArgs(
                    subcategory,
                    errorCode,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message ?? originalFormat,
                    originalFormat,
                    values!,
                    helpKeyword,
                    TaskName,
                    helpLink,
                    DateTime.UtcNow);
            }
            else
            {
                e = new BuildErrorEventArgs(
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
            }

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

            BuildWarningEventArgs e;
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11))
            {
                e = new StructuredBuildWarningEventArgs(
                    subcategory,
                    warningCode,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message ?? originalFormat,
                    originalFormat,
                    values!,
                    helpKeyword,
                    TaskName,
                    helpLink,
                    DateTime.UtcNow);
            }
            else
            {
                e = new BuildWarningEventArgs(
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
            }

            BuildEngine.LogWarningEvent(e);
        }

        private static StructuredMessageData ParseStructuredMessage(
            string messageTemplate,
            object?[]? values,
            bool buildDisplayFormat = true)
        {
            ArgumentNullException.ThrowIfNull(messageTemplate);
            values ??= Array.Empty<object?>();

            var normalized = new StringBuilder(messageTemplate.Length);
            StringBuilder? composite = buildDisplayFormat ? new StringBuilder(messageTemplate.Length) : null;
            var holes = new (string Name, string? Format)[values.Length];
            int holeCount = 0;

            for (int i = 0; i < messageTemplate.Length;)
            {
                char c = messageTemplate[i];
                if (c == '{')
                {
                    if (i + 1 < messageTemplate.Length && messageTemplate[i + 1] == '{')
                    {
                        normalized.Append("{{");
                        composite?.Append("{{");
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

                    name = GetUniqueName(holes, holeCount, SanitizeName(name));
                    string suffix = separator < 0 ? string.Empty : hole.Substring(separator);
                    string? format = null;
                    int colon = suffix.IndexOf(':');
                    if (colon >= 0)
                    {
                        format = suffix.Substring(colon + 1);
                    }

                    normalized.Append('{').Append(name).Append(suffix).Append('}');
                    if (composite is not null)
                    {
                        composite.Append('{').Append(holeCount).Append(suffix).Append('}');
                    }

                    if (holeCount < holes.Length)
                    {
                        holes[holeCount] = (name, format);
                    }

                    holeCount++;
                    i = close + 1;
                    continue;
                }

                if (c == '}')
                {
                    if (i + 1 < messageTemplate.Length && messageTemplate[i + 1] == '}')
                    {
                        normalized.Append("}}");
                        composite?.Append("}}");
                        i += 2;
                        continue;
                    }

                    throw new FormatException("The structured message template contains an unmatched '}'.");
                }

                normalized.Append(c);
                composite?.Append(c);
                i++;
            }

            if (holeCount != values.Length)
            {
                throw new FormatException(
                    $"The structured message template contains {holeCount} holes but {values.Length} values were supplied.");
            }

            var structuredValues = new KeyValuePair<string, string?>[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                object? value = values[i];
                structuredValues[i] = new KeyValuePair<string, string?>(
                    holes[i].Name,
                    value == null ? null : FormatValue(value, holes[i].Format, CultureInfo.CurrentCulture));
            }

            return new StructuredMessageData(
                null,
                normalized.ToString(),
                structuredValues,
                composite?.ToString(),
                buildDisplayFormat ? (object[])(object)values : null);
        }

        private static StructuredMessageData ValidateNamedStructuredMessage(
            string originalFormat,
            string localizedMessage,
            IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            ArgumentNullException.ThrowIfNull(localizedMessage);
            ArgumentNullException.ThrowIfNull(values);
            object?[] rawValues = new object?[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                rawValues[i] = values[i].Value;
            }

            StructuredMessageData parsed = ParseStructuredMessage(originalFormat, rawValues, buildDisplayFormat: false);
            for (int i = 0; i < values.Count; i++)
            {
                if (!string.Equals(parsed.Values[i].Key, values[i].Key, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Structured value '{values[i].Key}' does not match template hole '{parsed.Values[i].Key}' at position {i}.",
                        nameof(values));
                }
            }

            return new StructuredMessageData(localizedMessage, parsed.OriginalFormat, parsed.Values, null, null);
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

        private static string GetUniqueName(
            (string Name, string? Format)[] holes,
            int holeCount,
            string name)
        {
            if (!ContainsName(holes, holeCount, name))
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
            while (ContainsName(holes, holeCount, candidate));

            return candidate;
        }

        private static bool ContainsName(
            (string Name, string? Format)[] holes,
            int holeCount,
            string name)
        {
            for (int i = 0; i < Math.Min(holeCount, holes.Length); i++)
            {
                if (string.Equals(holes[i].Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatValue(object? value, string? format, IFormatProvider provider)
            => value is IFormattable formattable
                ? formattable.ToString(format, provider)
                : value?.ToString() ?? string.Empty;

        private static void AppendEscapedLiteral(StringBuilder builder, string value)
        {
            if (value.IndexOf('{') < 0 && value.IndexOf('}') < 0)
            {
                builder.Append(value);
                return;
            }

            foreach (char c in value)
            {
                if (c is '{' or '}')
                {
                    // Composite formats escape literal braces by doubling them.
                    builder.Append(c);
                }

                builder.Append(c);
            }
        }

        private static void AppendEscapedLiteral(ref ValueStringBuilder builder, string value)
        {
            if (value.IndexOf('{') < 0 && value.IndexOf('}') < 0)
            {
                builder.Append(value);
                return;
            }

            foreach (char c in value)
            {
                if (c is '{' or '}')
                {
                    builder.Append(c);
                }

                builder.Append(c);
            }
        }

        private readonly record struct StructuredMessageData(
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
