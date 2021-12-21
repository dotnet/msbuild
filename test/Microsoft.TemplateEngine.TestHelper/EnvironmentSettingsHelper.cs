// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class EnvironmentSettingsHelper : IDisposable
    {
        private readonly List<string> _foldersToCleanup = new List<string>();

        public IEngineEnvironmentSettings CreateEnvironment(
            string? locale = null,
            bool virtualize = false,
            [CallerMemberName] string hostIdentifier = "",
            bool loadDefaultGenerator = true,
            IEnvironment? environment = null)
        {
            if (string.IsNullOrEmpty(locale))
            {
                locale = "en-US";
            }
            var builtIns = new List<(Type, IIdentifiedComponent)>();
            builtIns.AddRange(Edge.Components.AllComponents);
            if (loadDefaultGenerator)
            {
                builtIns.AddRange(Orchestrator.RunnableProjects.Components.AllComponents);
            }

            ITemplateEngineHost host = new TestHost(hostIdentifier: string.IsNullOrWhiteSpace(hostIdentifier) ? "TestRunner" : hostIdentifier)
            {
                BuiltInComponents = builtIns,
                FileSystem = new MonitoredFileSystem(new PhysicalFileSystem()),
                FallbackHostTemplateConfigNames = new[] { "dotnetcli" }
            };
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
