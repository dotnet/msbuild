// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class TestLoggerFactory : ILoggerFactory
    {
        private readonly List<ILoggerProvider> _loggerProviders = new List<ILoggerProvider>();

        private readonly List<ILoggerFactory> _factories = new List<ILoggerFactory>();

        public TestLoggerFactory(IMessageSink? messageSink = null)
        {
            if (messageSink != null)
            {
                SharedTestOutputHelper testOutputHelper = new SharedTestOutputHelper(messageSink);
                _loggerProviders =
                    new List<ILoggerProvider>() { new XunitLoggerProvider(testOutputHelper) };
            }
        }

        public void Dispose() => _factories.ForEach(f => f.Dispose());

        public ILogger CreateLogger(string categoryName)
        {
            var loggerFactory =
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                {
                    builder
                        .SetMinimumLevel(LogLevel.Trace);

                    if (_loggerProviders?.Any() ?? false)
                    {
                        foreach (ILoggerProvider loggerProvider in _loggerProviders)
                        {
                            builder.AddProvider(loggerProvider);
                        }
                    }
                    builder.AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                        options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
                        options.IncludeScopes = true;
                    });
                });
            _factories.Add(loggerFactory);
            return loggerFactory.CreateLogger(categoryName);
        }

        public ILogger CreateLogger() => CreateLogger("Test Host");

        public void AddProvider(ILoggerProvider provider)
        {
            _loggerProviders.Add(provider);
        }
    }
}
