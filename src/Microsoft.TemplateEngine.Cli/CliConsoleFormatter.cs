// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Microsoft.TemplateEngine.Cli
{
    internal sealed class CliConsoleFormatter : ConsoleFormatter, IDisposable
    {
        private readonly IDisposable? _optionsReloadToken;
        private ConsoleFormatterOptions _formatterOptions;

        public CliConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options) : base(nameof(CliConsoleFormatter))
            =>
            (_optionsReloadToken, _formatterOptions) =
                (options.OnChange(ReloadLoggerOptions), options.CurrentValue);

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            string? message = null;
            if (logEntry.Formatter != null)
            {
                message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            }
            if (logEntry.Exception == null && message == null)
            {
                return;
            }

            LogLevel logLevel = logEntry.LogLevel;
            switch (logLevel)
            {
                case LogLevel.Information:
                    textWriter.WriteLine(message);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    textWriter.WriteLine(string.Format(LocalizableStrings.GenericError, message));
                    WriteExceptionDetails(textWriter, logEntry);
                    break;
                case LogLevel.Warning:
                    textWriter.WriteLine(string.Format(LocalizableStrings.GenericWarning, message));
                    WriteExceptionDetails(textWriter, logEntry);
                    break;
                case LogLevel.Debug:
                case LogLevel.Trace:
                    CreateDebugMessage(textWriter, logEntry, message!, scopeProvider);
                    break;
            }
        }

        public void Dispose() => _optionsReloadToken?.Dispose();

        private void ReloadLoggerOptions(ConsoleFormatterOptions options) => _formatterOptions = options;

        private string GetCurrentDateTime()
        {
            string format = !string.IsNullOrWhiteSpace(_formatterOptions.TimestampFormat)
                ? _formatterOptions.TimestampFormat
                : "yyyy-MM-dd HH:mm:ss.fff";
            return (_formatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now).ToString(format);
        }

        private void CreateDebugMessage<TState>(TextWriter textWriter, in LogEntry<TState> logEntry, string message, IExternalScopeProvider? scopeProvider)
        {
            //timestamp and log level
            textWriter.Write($"[{GetCurrentDateTime()}] [{logEntry.LogLevel}]");
            //category
            if (!string.IsNullOrWhiteSpace(logEntry.Category))
            {
                textWriter.Write($" [{logEntry.Category}]");
            }

            // scope information
            if (scopeProvider != null && _formatterOptions.IncludeScopes)
            {
                scopeProvider.ForEachScope(
                    (scope, state) =>
                    {
                        state.Write($" => [{scope}]");
                    },
                    textWriter);
                textWriter.Write(": ");
            }

            // message
            textWriter.WriteLine(message);

            //exception
            WriteExceptionDetails(textWriter, logEntry);
        }

        private void WriteExceptionDetails<TState>(TextWriter textWriter, in LogEntry<TState> logEntry)
        {
            if (logEntry.Exception != null)
            {
                if (logEntry.LogLevel == LogLevel.Debug || logEntry.LogLevel == LogLevel.Trace)
                {
                    textWriter.WriteLine($"Details: {logEntry.Exception}");
                }
                else
                {
                    textWriter.WriteLine($"Details: {logEntry.Exception.Message}");
                }
            }
        }
    }
}
