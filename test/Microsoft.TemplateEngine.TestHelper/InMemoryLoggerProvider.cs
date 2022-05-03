// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.TestHelper
{
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
            private List<(LogLevel, string)> _messagesCollection;

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
