// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using NuGet.Common;
using Xunit.Abstractions;
using DiagnosticMessage = Xunit.Sdk.DiagnosticMessage;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    internal class XunitNuGetLogger : ILogger
    {
        private readonly IMessageSink _messageSink;

        public XunitNuGetLogger(IMessageSink sink)
        {
            _messageSink = sink;
        }

        public void WriteLine(string message)
        {
            _messageSink.OnMessage(new DiagnosticMessage(message));
        }

        public void Log(LogLevel level, string data) => WriteLine($"[{level}]: {data}");

        public void Log(ILogMessage message) => WriteLine($"[{message.Level}]: {message.Message}");

        public Task LogAsync(LogLevel level, string data)
        {
            WriteLine($"[{level}]: {data}");
            return Task.FromResult(0);
        }

        public Task LogAsync(ILogMessage message)
        {
            WriteLine($"[{message.Level}]: {message.Message}");
            return Task.FromResult(0);
        }

        public void LogDebug(string data) => Log(LogLevel.Debug, data);

        public void LogError(string data) => Log(LogLevel.Error, data);

        public void LogInformation(string data) => Log(LogLevel.Information, data);

        public void LogInformationSummary(string data) => Log(LogLevel.Information, data);

        public void LogMinimal(string data) => Log(LogLevel.Minimal, data);

        public void LogVerbose(string data) => Log(LogLevel.Verbose, data);

        public void LogWarning(string data) => Log(LogLevel.Warning, data);
    }
}
