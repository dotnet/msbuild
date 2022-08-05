// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class EnvironmentSettingsHelper : IDisposable
    {
        private readonly List<string> _foldersToCleanup = new List<string>();
        private SharedTestOutputHelper _testOutputHelper;

        public EnvironmentSettingsHelper(IMessageSink messageSink)
        {
            _testOutputHelper = new SharedTestOutputHelper(messageSink);
        }

        public IEngineEnvironmentSettings CreateEnvironment(
            string? locale = null,
            bool virtualize = false,
            [CallerMemberName] string hostIdentifier = "",
            bool loadDefaultGenerator = true,
            IEnvironment? environment = null,
            IReadOnlyList<(Type, IIdentifiedComponent)>? additionalComponents = null,
            IEnumerable<ILoggerProvider>? addLoggerProviders = null)
        {
            if (string.IsNullOrEmpty(locale))
            {
                locale = "en-US";
            }
            var builtIns = new List<(Type, IIdentifiedComponent)>();
            if (additionalComponents != null)
            {
                builtIns.AddRange(additionalComponents);
            }
            builtIns.AddRange(Edge.Components.AllComponents);
            if (loadDefaultGenerator)
            {
                builtIns.AddRange(Orchestrator.RunnableProjects.Components.AllComponents);
            }

            IEnumerable<ILoggerProvider> loggerProviders = new[] { new XunitLoggerProvider(_testOutputHelper) };
            if (addLoggerProviders != null)
            {
                loggerProviders = loggerProviders.Concat(addLoggerProviders);
            }
            ITemplateEngineHost host = new TestHost(
                hostIdentifier: hostIdentifier,
                additionalComponents: additionalComponents,
                fileSystem: new MonitoredFileSystem(new PhysicalFileSystem()),
                fallbackNames: new[] { "dotnetcli" },
                addLoggerProviders: loggerProviders);

            CultureInfo.CurrentUICulture = new CultureInfo(locale);
            EngineEnvironmentSettings engineEnvironmentSettings;
            if (virtualize)
            {
                engineEnvironmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: true, environment: environment);
            }
            else
            {
                var templateEngineRoot = Path.Combine(CreateTemporaryFolder(), ".templateengine");
                engineEnvironmentSettings = new EngineEnvironmentSettings(host, settingsLocation: templateEngineRoot, environment: environment);
            }
            return engineEnvironmentSettings;
        }

        public string CreateTemporaryFolder(string name = "")
        {
            string folder = TestUtils.CreateTemporaryFolder(name);
            _foldersToCleanup.Add(folder);
            return folder;
        }

        public void Dispose()
        {
            _foldersToCleanup.ForEach(f => Directory.Delete(f, true));
        }
    }
}
