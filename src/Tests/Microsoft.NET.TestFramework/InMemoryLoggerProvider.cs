// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// <see cref="ILoggerProvider"/> which captures the log messages to collection for test purposes.
    /// </summary>
    public class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel, string)> _messagesCollection;

        public InMemoryLoggerProvider(List<(LogLevel, string)> messagesCollection)
        {
            _messagesCollection = messagesCollection;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new InMemoryLogger(_messagesCollection);
        }

        public void Dispose() => throw new NotImplementedException();

        private class InMemoryLogger : ILogger
        {
            private readonly List<(LogLevel, string)> _messagesCollection;

            public InMemoryLogger(List<(LogLevel, string)> messagesCollection) => _messagesCollection = messagesCollection;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return new Scope();
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _messagesCollection.Add((logLevel, formatter(state, exception)));
            }

            private class Scope : IDisposable
            {
                public void Dispose() { }
            }
        }

    }
}
